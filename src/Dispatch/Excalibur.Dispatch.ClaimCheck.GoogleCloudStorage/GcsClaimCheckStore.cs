// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;

using Excalibur.Dispatch.ClaimCheck.GoogleCloudStorage.Internal;
using Excalibur.Dispatch.Patterns.ClaimCheck;

using Google.Cloud.Storage.V1;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.ClaimCheck.GoogleCloudStorage;

/// <summary>
/// Google Cloud Storage implementation of the <see cref="IClaimCheckProvider"/> for storing
/// large message payloads in GCS buckets.
/// </summary>
public sealed partial class GcsClaimCheckStore : IClaimCheckProvider
{
	private readonly IStorageClientSeam _storageClient;
	private readonly GcsClaimCheckOptions _options;
	private readonly ClaimCheckOptions _claimCheckOptions;
	private readonly ILogger<GcsClaimCheckStore> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="GcsClaimCheckStore"/> class.
	/// Creates a default <see cref="StorageClient"/> using application default credentials.
	/// </summary>
	/// <param name="options">The GCS-specific options.</param>
	/// <param name="claimCheckOptions">The core claim check options.</param>
	/// <param name="logger">The logger instance.</param>
	public GcsClaimCheckStore(
		IOptions<GcsClaimCheckOptions> options,
		IOptions<ClaimCheckOptions> claimCheckOptions,
		ILogger<GcsClaimCheckStore> logger)
	{
		// Validate parameters before creating the SDK client to ensure
		// ArgumentNullException is thrown instead of credential-related exceptions.
		_options = (options ?? throw new ArgumentNullException(nameof(options))).Value;
		_claimCheckOptions = (claimCheckOptions ?? throw new ArgumentNullException(nameof(claimCheckOptions))).Value;
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_storageClient = new StorageClientAdapter(StorageClient.Create());
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="GcsClaimCheckStore"/> class
	/// with an existing <see cref="StorageClient"/>.
	/// </summary>
	/// <param name="storageClient">The GCS storage client.</param>
	/// <param name="options">The GCS-specific options.</param>
	/// <param name="claimCheckOptions">The core claim check options.</param>
	/// <param name="logger">The logger instance.</param>
	public GcsClaimCheckStore(
		StorageClient storageClient,
		IOptions<GcsClaimCheckOptions> options,
		IOptions<ClaimCheckOptions> claimCheckOptions,
		ILogger<GcsClaimCheckStore> logger)
		: this(CreateAdapter(storageClient), options, claimCheckOptions, logger)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="GcsClaimCheckStore"/>
	/// class using a pre-built adapter. Used by tests to substitute the SDK via
	/// the <see cref="IStorageClientSeam"/> seam.
	/// </summary>
	/// <param name="storageClient">The storage client adapter.</param>
	/// <param name="options">The GCS-specific options.</param>
	/// <param name="claimCheckOptions">The core claim check options.</param>
	/// <param name="logger">The logger instance.</param>
	internal GcsClaimCheckStore(
		IStorageClientSeam storageClient,
		IOptions<GcsClaimCheckOptions> options,
		IOptions<ClaimCheckOptions> claimCheckOptions,
		ILogger<GcsClaimCheckStore> logger)
	{
		_storageClient = storageClient ?? throw new ArgumentNullException(nameof(storageClient));
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_claimCheckOptions = claimCheckOptions?.Value ?? throw new ArgumentNullException(nameof(claimCheckOptions));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	private static IStorageClientSeam CreateAdapter(StorageClient storageClient)
	{
		ArgumentNullException.ThrowIfNull(storageClient);
		return new StorageClientAdapter(storageClient);
	}

	/// <inheritdoc />
	public async Task<ClaimCheckReference> StoreAsync(
		byte[] payload,
		CancellationToken cancellationToken,
		ClaimCheckMetadata? metadata = null)
	{
		ArgumentNullException.ThrowIfNull(payload);

		var id = $"{_claimCheckOptions.IdPrefix}{Guid.NewGuid():N}";
		var objectName = GetObjectName(id);

		metadata ??= new ClaimCheckMetadata();

		var gcsObject = new Google.Apis.Storage.v1.Data.Object
		{
			Bucket = _options.BucketName,
			Name = objectName,
			ContentType = metadata.ContentType ?? "application/octet-stream",
			Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
			{
				["claim-check-id"] = id,
				["original-size"] = payload.Length.ToString(CultureInfo.InvariantCulture),
				["stored-at"] = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)
			}
		};

		if (!string.IsNullOrEmpty(metadata.ContentType))
		{
			gcsObject.Metadata["content-type"] = metadata.ContentType;
		}

		using var stream = new MemoryStream(payload);
		await _storageClient.UploadObjectAsync(
			gcsObject,
			stream,
			cancellationToken).ConfigureAwait(false);

		LogStoredPayload(id, payload.Length);

		return new ClaimCheckReference
		{
			Id = id,
			BlobName = objectName,
			Location = $"gs://{_options.BucketName}/{objectName}",
			Size = payload.Length,
			StoredAt = DateTimeOffset.UtcNow,
			ExpiresAt = DateTimeOffset.UtcNow.Add(_claimCheckOptions.RetentionPeriod),
			Metadata = metadata
		};
	}

	/// <inheritdoc />
	public async Task<byte[]> RetrieveAsync(
		ClaimCheckReference reference,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(reference);

		var objectName = GetObjectName(reference.Id);

		try
		{
			using var ms = new MemoryStream();
			await _storageClient.DownloadObjectAsync(
				_options.BucketName,
				objectName,
				ms,
				cancellationToken).ConfigureAwait(false);

			var data = ms.ToArray();
			LogRetrievedPayload(reference.Id, data.Length);
			return data;
		}
		catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
		{
			throw new KeyNotFoundException($"Claim check '{reference.Id}' not found in GCS.", ex);
		}
	}

	/// <inheritdoc />
	public async Task<bool> DeleteAsync(
		ClaimCheckReference reference,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(reference);

		var objectName = GetObjectName(reference.Id);

		try
		{
			await _storageClient.DeleteObjectAsync(
				_options.BucketName,
				objectName,
				cancellationToken).ConfigureAwait(false);

			LogDeletedClaimCheck(reference.Id);
			return true;
		}
		catch (Google.GoogleApiException)
		{
			return false;
		}
	}

	/// <inheritdoc />
	public bool ShouldUseClaimCheck(byte[] payload)
	{
		ArgumentNullException.ThrowIfNull(payload);
		return payload.Length >= _claimCheckOptions.PayloadThreshold;
	}

	private string GetObjectName(string claimCheckId)
	{
		var date = DateTimeOffset.UtcNow;
		return $"{_options.Prefix}{date:yyyy/MM/dd}/{claimCheckId}";
	}

	[LoggerMessage(3310, LogLevel.Debug, "Stored claim check '{ClaimCheckId}' in GCS ({Size} bytes)")]
	private partial void LogStoredPayload(string claimCheckId, int size);

	[LoggerMessage(3311, LogLevel.Debug, "Retrieved claim check '{ClaimCheckId}' from GCS ({Size} bytes)")]
	private partial void LogRetrievedPayload(string claimCheckId, int size);

	[LoggerMessage(3312, LogLevel.Debug, "Deleted claim check '{ClaimCheckId}' from GCS")]
	private partial void LogDeletedClaimCheck(string claimCheckId);
}
