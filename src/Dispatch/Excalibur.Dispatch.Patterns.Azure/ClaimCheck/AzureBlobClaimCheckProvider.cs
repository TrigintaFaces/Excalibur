// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;

using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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

		var id = GenerateClaimCheckId();
		var blobName = GetBlobName(id);
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

		// Resolve from the name the reference RECORDS, not from one recomputed now. GetBlobName derives a
		// date-partitioned path from the CURRENT UTC date, so a payload stored at 23:59:59 was looked for
		// under the next day's prefix a second later and reported not-found -- while still sitting in the
		// container. Claim check exists for payloads that outlive the message, so crossing midnight is
		// ordinary rather than exotic. The recorded name is only recomputed for references written before
		// this field was populated.
		var blobName = string.IsNullOrEmpty(reference.BlobName) ? GetBlobName(reference.Id) : reference.BlobName;
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

		// Resolve from the name the reference RECORDS, not from one recomputed now. GetBlobName derives a
		// date-partitioned path from the CURRENT UTC date, so a payload stored at 23:59:59 was looked for
		// under the next day's prefix a second later and reported not-found -- while still sitting in the
		// container. Claim check exists for payloads that outlive the message, so crossing midnight is
		// ordinary rather than exotic. The recorded name is only recomputed for references written before
		// this field was populated.
		var blobName = string.IsNullOrEmpty(reference.BlobName) ? GetBlobName(reference.Id) : reference.BlobName;
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

	private static string GetBlobName(string claimCheckId)
	{
		// Use hierarchical naming for better organization
		var date = DateTimeOffset.UtcNow;
		return $"{date:yyyy/MM/dd}/{claimCheckId}";
	}

	private string GenerateClaimCheckId() => $"{_options.IdPrefix}{Guid.NewGuid():N}";
}
