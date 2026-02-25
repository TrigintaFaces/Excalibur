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

		var reference = new ClaimCheckReference
		{
			Id = id,
			Location = blobClient.Uri.ToString(),
			Size = payloadToStore.Length,
			StoredAt = DateTimeOffset.UtcNow,
			ExpiresAt = DateTimeOffset.UtcNow.Add(_options.RetentionPeriod),
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

		var blobName = GetBlobName(reference.Id);
		var blobClient = _containerClient.GetBlobClient(blobName);

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
			throw new InvalidOperationException($"Claim check {reference.Id} not found", ex);
		}
	}

	/// <inheritdoc />
	public async Task<bool> DeleteAsync(
		ClaimCheckReference reference,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(reference);

		await EnsureContainerExistsAsync(cancellationToken).ConfigureAwait(false);

		var blobName = GetBlobName(reference.Id);
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
