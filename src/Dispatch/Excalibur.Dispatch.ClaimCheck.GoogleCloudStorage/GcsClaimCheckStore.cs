// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

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

		// The identifier carries its own date partition, so the key is derivable from the identifier
		// alone and never from the clock at lookup time.
		var id = ClaimCheckId.Create(_claimCheckOptions.IdPrefix, DateTimeOffset.UtcNow);
		var objectName = RequireStorageKey(id);

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

		var storedAt = DateTimeOffset.UtcNow;

		return new ClaimCheckReference
		{
			Id = id,
			BlobName = objectName,
			Location = $"gs://{_options.BucketName}/{objectName}",
			Size = payload.Length,
			StoredAt = storedAt,
			ExpiresAt = _claimCheckOptions.ResolveExpiresAt(storedAt),
			Metadata = metadata
		};
	}

	/// <inheritdoc />
	public async Task<byte[]> RetrieveAsync(
		ClaimCheckReference reference,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(reference);

		// The key is derived from the identifier and from nothing else the wire supplied. The previous
		// form honoured the recorded name verbatim, which let whoever could publish to the consumed queue
		// name ANY object in this container and have it fetched with our credentials. Falling back to
		// recomputing from the identifier would not have fixed it: the identifier is wire-supplied too, so
		// that merely moves which field names the object.
		//
		// The midnight problem the recorded-name branch existed to solve is solved differently now -- the
		// identifier carries its own date partition, so the key no longer depends on the clock at lookup.
		var objectName = RequireStorageKey(reference.Id);

		// An expired payload is a form of missing payload, so it surfaces as the same exception a deleted
		// or never-stored one does. Cloud Storage has no per-object time-to-live -- its lifecycle rules
		// are bucket-wide and delete on a daily schedule rather than at a per-payload instant -- so expiry
		// is enforced here, before the object is downloaded, rather than delegated to the store.
		if (reference.IsExpired(DateTimeOffset.UtcNow))
		{
			throw new KeyNotFoundException($"Claim check '{reference.Id}' has expired.");
		}

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
		if (!ClaimCheckId.TryGetStorageKey(_claimCheckOptions.IdPrefix, _options.Prefix, reference.Id, out var objectName))
		{
			return false;
		}

		try
		{
			// Cloud Storage's delete is idempotent and silent -- it succeeds identically for an object
			// that was never there -- so the delete itself cannot answer whether one existed. The contract
			// defines the return value as exactly that observation, so it has to be taken before deleting.
			// The catch below cannot serve: a missing object raises nothing to catch.
			//
			// This is an observation, not a claim about exclusivity: two callers deleting the same object
			// concurrently can both observe it present and both report true. The contract asks what this
			// caller saw, not who won; the sibling S3 store has the same shape for the same reason.
			var existed = await _storageClient.ObjectExistsAsync(
				_options.BucketName,
				objectName,
				cancellationToken).ConfigureAwait(false);

			await _storageClient.DeleteObjectAsync(
				_options.BucketName,
				objectName,
				cancellationToken).ConfigureAwait(false);

			LogDeletedClaimCheck(reference.Id);

			return existed;
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
		if (!ClaimCheckId.TryGetStorageKey(_claimCheckOptions.IdPrefix, _options.Prefix, claimCheckId, out var key))
		{
			// Reported as not-found rather than as a validation fault, deliberately: a forged identifier
			// and an identifier for a payload that never existed are the same observation from the
			// caller's side, and distinguishing them would confirm to a prober which of their guesses had
			// the right shape.
			throw new KeyNotFoundException("Claim check identifier is not one this store issued.");
		}

		return key;
	}

	[LoggerMessage(3310, LogLevel.Debug, "Stored claim check '{ClaimCheckId}' in GCS ({Size} bytes)")]
	private partial void LogStoredPayload(string claimCheckId, int size);

	[LoggerMessage(3311, LogLevel.Debug, "Retrieved claim check '{ClaimCheckId}' from GCS ({Size} bytes)")]
	private partial void LogRetrievedPayload(string claimCheckId, int size);

	[LoggerMessage(3312, LogLevel.Debug, "Deleted claim check '{ClaimCheckId}' from GCS")]
	private partial void LogDeletedClaimCheck(string claimCheckId);
}
