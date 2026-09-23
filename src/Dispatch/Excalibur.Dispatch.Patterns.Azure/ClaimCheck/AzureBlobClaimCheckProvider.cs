// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;

using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Excalibur.Dispatch.Patterns.ClaimCheck;

namespace Excalibur.Dispatch.Patterns.ClaimCheck;

/// <summary>
/// Azure Blob Storage implementation of the Claim Check pattern.
/// </summary>
public partial class AzureBlobClaimCheckProvider : IClaimCheckProvider
{
	private readonly BlobContainerClient _containerClient;
	private readonly ClaimCheckOptions _options;
	private readonly ILogger<AzureBlobClaimCheckProvider> _logger;
	private volatile bool _containerEnsured;

	/// <summary>
	/// Initializes a new instance of the <see cref="AzureBlobClaimCheckProvider" /> class.
	/// </summary>
	/// <param name="options"> The claim check options. </param>
	/// <param name="logger"> The logger. </param>
	public AzureBlobClaimCheckProvider(
		IOptions<ClaimCheckOptions> options,
		ILogger<AzureBlobClaimCheckProvider> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_logger = logger;

		var blobServiceClient = new BlobServiceClient(_options.ConnectionString);
		_containerClient = blobServiceClient.GetBlobContainerClient(_options.ContainerName);
	}

	/// <summary>
	/// Ensures the blob container exists, creating it if necessary.
	/// Uses lazy initialization to avoid synchronous I/O in the constructor.
	/// </summary>
	private async Task EnsureContainerExistsAsync(CancellationToken cancellationToken)
	{
		if (_containerEnsured)
		{
			return;
		}

		_ = await _containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
		_containerEnsured = true;
	}

	/// <inheritdoc />
	public async Task<ClaimCheckReference> StoreAsync(
		byte[] payload,
		CancellationToken cancellationToken,
		ClaimCheckMetadata? metadata = null)
	{
		ArgumentNullException.ThrowIfNull(payload);

		await EnsureContainerExistsAsync(cancellationToken).ConfigureAwait(false);

		// The identifier carries its own date partition, so the key is derivable from the identifier
		// alone and never from the clock at lookup time.
		var id = ClaimCheckId.Create(_options.IdPrefix, DateTimeOffset.UtcNow);
		var blobName = RequireStorageKey(id);
		var blobClient = _containerClient.GetBlobClient(blobName);

		metadata ??= new ClaimCheckMetadata();
		var payloadToStore = payload;

		// Apply compression if enabled and payload meets threshold
		if (_options.EnableCompression && payload.Length >= _options.CompressionThreshold)
		{
			payloadToStore = await CompressAsync(payload, cancellationToken).ConfigureAwait(false);
			metadata.IsCompressed = true;
			metadata.OriginalSize = payload.Length;
		}

		// Calculate checksum if validation is enabled
		string? checksum = null;
		if (_options.ValidateChecksum)
		{
			checksum = CalculateChecksum(payloadToStore);
		}

		// Prepare blob metadata
		var blobMetadata = new Dictionary<string, string>
			(StringComparer.Ordinal)
		{
			["claimCheckId"] = id,
			["originalSize"] = payload.Length.ToString(CultureInfo.InvariantCulture),
			["compressed"] = metadata.IsCompressed.ToString(CultureInfo.InvariantCulture),
			["storedAt"] = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
		};

		if (!string.IsNullOrEmpty(metadata.ContentType))
		{
			blobMetadata["contentType"] = metadata.ContentType;
		}

		if (!string.IsNullOrEmpty(checksum))
		{
			blobMetadata["checksum"] = checksum;
		}

		// Add custom properties
		foreach (var prop in metadata.Properties)
		{
			blobMetadata[$"custom_{prop.Key}"] = prop.Value;
		}

		// Upload blob with metadata
		var uploadOptions = new BlobUploadOptions
		{
			Metadata = blobMetadata,
			HttpHeaders = new BlobHttpHeaders
			{
				ContentType = metadata.ContentType ?? "application/octet-stream",
				ContentEncoding = metadata.IsCompressed ? "gzip" : null,
			},
		};

		_ = await blobClient.UploadAsync(
			new BinaryData(payloadToStore),
			uploadOptions,
			cancellationToken).ConfigureAwait(false);

		var storedAt = DateTimeOffset.UtcNow;

		var reference = new ClaimCheckReference
		{
			Id = id,
			// The reference exists to be self-describing: a caller persists it and resolves the payload
			// later. Leaving BlobName empty forces every later lookup to recompute the name, which is why
			// the name was dropped here and why nothing noticed.
			BlobName = blobName,
			Location = blobClient.Uri.ToString(),
			Size = payloadToStore.Length,
			StoredAt = storedAt,
			ExpiresAt = _options.ResolveExpiresAt(storedAt),
			Metadata = metadata,
		};

		LogStoredPayload(id, payloadToStore.Length, payload.Length);

		return reference;
	}

	/// <inheritdoc />
	public async Task<byte[]> RetrieveAsync(
		ClaimCheckReference reference,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(reference);

		await EnsureContainerExistsAsync(cancellationToken).ConfigureAwait(false);

		// The key is derived from the identifier and from nothing else the wire supplied. The previous
		// form honoured the recorded name verbatim, which let whoever could publish to the consumed queue
		// name ANY object in this container and have it fetched with our credentials. Falling back to
		// recomputing from the identifier would not have fixed it: the identifier is wire-supplied too, so
		// that merely moves which field names the object.
		//
		// The midnight problem the recorded-name branch existed to solve is solved differently now -- the
		// identifier carries its own date partition, so the key no longer depends on the clock at lookup.
		var blobName = RequireStorageKey(reference.Id);
		var blobClient = _containerClient.GetBlobClient(blobName);

		// An expired payload is a form of missing payload, so it surfaces as the same exception a deleted
		// or never-stored one does. Blob storage has no per-blob time-to-live -- its lifecycle management
		// policies are account-wide and run on a daily schedule rather than at a per-payload instant -- so
		// expiry is enforced here, before the blob is downloaded, rather than delegated to the store.
		if (reference.IsExpired(DateTimeOffset.UtcNow))
		{
			throw new KeyNotFoundException($"Claim check {reference.Id} has expired.");
		}

		try
		{
			var response = await blobClient.DownloadContentAsync(cancellationToken).ConfigureAwait(false);
			var data = response.Value.Content.ToArray();

			// Validate checksum if enabled
			if (_options.ValidateChecksum && response.Value.Details.Metadata.TryGetValue("checksum", out var storedChecksum))
			{
				var calculatedChecksum = CalculateChecksum(data);
				if (!string.Equals(calculatedChecksum, storedChecksum, StringComparison.Ordinal))
				{
					throw new InvalidOperationException($"Checksum validation failed for claim check {reference.Id}");
				}
			}

			// Decompress if needed
			if (response.Value.Details.Metadata.TryGetValue("compressed", out var compressed) &&
				bool.Parse(compressed))
			{
				data = await DecompressAsync(data, cancellationToken).ConfigureAwait(false);
			}

			LogRetrievedPayload(reference.Id, data.Length);

			return data;
		}
		catch (RequestFailedException ex) when (ex.Status == 404)
		{
			LogClaimCheckNotFound(reference.Id);

			// KeyNotFoundException, matching the in-memory, S3 and GCS providers and the error handling our
			// own documentation hands consumers. This provider was the sole outlier, so a consumer who wrote
			// the documented catch got an unhandled exception on exactly the recovery path they wrote it for.
			throw new KeyNotFoundException($"Claim check {reference.Id} not found", ex);
		}
	}

	/// <inheritdoc />
	public async Task<bool> DeleteAsync(
		ClaimCheckReference reference,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(reference);

		await EnsureContainerExistsAsync(cancellationToken).ConfigureAwait(false);

		// The key is derived from the identifier and from nothing else the wire supplied. The previous
		// form honoured the recorded name verbatim, which let whoever could publish to the consumed queue
		// name ANY object in this container and have it fetched with our credentials. Falling back to
		// recomputing from the identifier would not have fixed it: the identifier is wire-supplied too, so
		// that merely moves which field names the object.
		//
		// The midnight problem the recorded-name branch existed to solve is solved differently now -- the
		// identifier carries its own date partition, so the key no longer depends on the clock at lookup.
		// A refused identifier is reported as "there was nothing to delete" rather than as a throw. The
		// contract of this method is the observation "did an object exist", and an identifier this store
		// never issued names no object, so false is the accurate answer. It is also the non-committal
		// one: throwing here would tell a prober which of their guesses had the right shape, and the
		// conformance kit already requires a non-existent reference to return false rather than raise.
		if (!ClaimCheckId.TryGetStorageKey(_options.IdPrefix, string.Empty, reference.Id, out var blobName))
		{
			return false;
		}
		var blobClient = _containerClient.GetBlobClient(blobName);

		try
		{
			var response = await blobClient.DeleteIfExistsAsync(
				DeleteSnapshotsOption.IncludeSnapshots,
				cancellationToken: cancellationToken).ConfigureAwait(false);

			if (response.Value)
			{
				LogDeletedClaimCheck(reference.Id);
			}

			return response.Value;
		}
		catch (RequestFailedException ex)
		{
			LogDeleteClaimCheckError(reference.Id, ex);
			return false;
		}
	}

	/// <inheritdoc />
	public bool ShouldUseClaimCheck(byte[] payload)
	{
		ArgumentNullException.ThrowIfNull(payload);
		return payload.Length >= _options.PayloadThreshold;
	}

	private static async Task<byte[]> CompressAsync(byte[] data, CancellationToken cancellationToken)
	{
		await using var output = new MemoryStream();
		var gzip = new GZipStream(output, CompressionLevel.Optimal);
		await using (gzip.ConfigureAwait(false))
		{
			await gzip.WriteAsync(data.AsMemory(), cancellationToken).ConfigureAwait(false);
		}

		return output.ToArray();
	}

	private static async Task<byte[]> DecompressAsync(byte[] data, CancellationToken cancellationToken)
	{
		await using var input = new MemoryStream(data);
		var gzip = new GZipStream(input, CompressionMode.Decompress);
		await using (gzip.ConfigureAwait(false))
		{
			await using var output = new MemoryStream();
			await gzip.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
			return output.ToArray();
		}
	}

	private static string CalculateChecksum(byte[] data)
	{
		var hash = SHA256.HashData(data);
		return Convert.ToBase64String(hash);
	}

	/// <summary>
	/// Resolves an identifier to its storage key, refusing any identifier this store did not mint.
	/// </summary>
	/// <param name="claimCheckId">The identifier, which may have arrived on the wire.</param>
	/// <returns>The container-relative storage key.</returns>
	/// <exception cref="KeyNotFoundException">
	/// Thrown when the identifier does not have the shape this store mints.
	/// </exception>
	private string RequireStorageKey(string claimCheckId)
	{
		if (!ClaimCheckId.TryGetStorageKey(_options.IdPrefix, string.Empty, claimCheckId, out var key))
		{
			// Reported as not-found rather than as a validation fault, deliberately: a forged identifier
			// and an identifier for a payload that never existed are the same observation from the
			// caller's side, and distinguishing them would confirm to a prober which of their guesses had
			// the right shape.
			throw new KeyNotFoundException("Claim check identifier is not one this store issued.");
		}

		return key;
	}
}
