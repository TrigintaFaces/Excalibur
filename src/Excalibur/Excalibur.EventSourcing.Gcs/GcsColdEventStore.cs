// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Polly;
using Polly.Retry;
using System.IO.Compression;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using Google.Cloud.Storage.V1;

using Microsoft.Extensions.Logging;

using Excalibur.Dispatch;
using Excalibur.EventSourcing.TieredStorage;

namespace Excalibur.EventSourcing.Gcs;

/// <summary>
/// Google Cloud Storage implementation of <see cref="IColdEventStore"/>.
/// </summary>
/// <remarks>
/// Events are stored as gzip-compressed JSON objects, one object per aggregate.
/// Object naming: <c>{prefix}/{tenantSegment}/{aggregateSegment}/events.json.gz</c>, and
/// <c>{tenantSegment}/{aggregateSegment}/events.json.gz</c> when no prefix is configured. Both segments are
/// Base64Url-encoded, so neither appears verbatim: write lifecycle rules and IAM prefix conditions against
/// the encoded form, never against a raw tenant or aggregate identifier.
/// TypedV2 separates aggregate types and requires explicit activation and migration under an external
/// legacy-operation fence. Layout and client binding remain fixed. Legacy marker checks use metadata
/// without changing supplied clients; typed operations require a client configured to preserve raw bytes.
/// Missing-object classification requires bucket metadata permission and rejects unavailable buckets.
/// </remarks>
internal sealed class GcsColdEventStore : IColdEventStore, IColdEventStoreMigration, IDisposable
{
	private const int MaxConcurrencyRetries = 5;

	/// <summary>
	/// Retries the <see cref="WriteAsync" /> read-modify-write body when GCS rejects the conditional
	/// upload because another writer committed first.
	/// </summary>
	/// <remarks>
	/// Built on Polly rather than a hand-rolled loop. The delegate re-reads the object on every attempt,
	/// so re-invoking it IS the compare-and-swap retry -- each attempt operates on the now-current
	/// generation rather than replaying a stale one. No backoff: the loop this replaced retried
	/// immediately, and a delay here only widens the window for another writer to win again.
	/// <para>
	/// Only a precondition failure is retried. A conflict status is deliberately NOT handled here, unlike
	/// the S3 store, because GCS signals a lost compare-and-swap with 412 alone.
	/// </para>
	/// </remarks>
	private readonly ResiliencePipeline _writeRetryPipeline;

	private ResiliencePipeline BuildWriteRetryPipeline() =>
		new ResiliencePipelineBuilder()
			.AddRetry(new RetryStrategyOptions
			{
				ShouldHandle = new PredicateBuilder().Handle<Google.GoogleApiException>(
					static ex => ex.HttpStatusCode == System.Net.HttpStatusCode.PreconditionFailed),
				MaxRetryAttempts = MaxConcurrencyRetries,
				Delay = TimeSpan.Zero,
				BackoffType = DelayBackoffType.Constant,
				OnRetry = args =>
				{
					_logger.LogDebug(
						"Concurrent archive detected (status {Status}); retrying (attempt {Attempt})",
						(args.Outcome.Exception as Google.GoogleApiException)?.HttpStatusCode,
						args.AttemptNumber + 1);
					return default;
				},
			})
			.Build();

	private readonly StorageClient _storageClient;
	private readonly string _bucketName;
	private readonly string _objectPrefix;
	private readonly ILogger<GcsColdEventStore> _logger;
	private readonly ColdArchiveLayout _layout;
	private readonly Lazy<GcsColdArchiveMigrationStorage> _migrationStorage;
	private readonly Lazy<ColdArchiveMigrationCoordinator> _migration;
	private readonly bool _ownsClient;
	private int _disposed;

	/// <summary>
	/// The archive JSON contract: the single canonical event serializer options, with type metadata supplied
	/// by the source-generated context so the archive path needs no runtime reflection.
	/// </summary>
	/// <remarks>
	/// The contract is SOURCED from <see cref="EventSerializationDefaults"/> rather than restated on the
	/// context, so the archive cannot drift from the format the rest of the framework reads and writes. The
	/// context supplies type metadata only.
	/// </remarks>
	internal static readonly JsonTypeInfo<List<StoredEvent>> ArchiveTypeInfo = CreateArchiveTypeInfo();

	private static JsonTypeInfo<List<StoredEvent>> CreateArchiveTypeInfo()
	{
		var options = EventSerializationDefaults.CreateCanonicalOptions();
		_ = EventSerializationDefaults.TryApplyTypeInfoResolver(options, GcsColdStoreJsonContext.Default);
		return (JsonTypeInfo<List<StoredEvent>>)options.GetTypeInfo(typeof(List<StoredEvent>));
	}

	internal GcsColdEventStore(
		StorageClient storageClient,
		string bucketName,
		string objectPrefix,
		ILogger<GcsColdEventStore> logger,
		ColdArchiveLayout layout = ColdArchiveLayout.Legacy,
		bool ownsClient = false)
	{
		ArgumentNullException.ThrowIfNull(storageClient);
		ArgumentNullException.ThrowIfNull(bucketName);
		ArgumentNullException.ThrowIfNull(logger);
		if (!Enum.IsDefined(layout))
		{
			throw new ArgumentOutOfRangeException(nameof(layout));
		}

		_storageClient = storageClient;
		_bucketName = bucketName;
		_objectPrefix = objectPrefix ?? "";
		_logger = logger;
		_layout = layout;
		_ownsClient = ownsClient;
		_migrationStorage = new Lazy<GcsColdArchiveMigrationStorage>(() => new GcsColdArchiveMigrationStorage(storageClient, bucketName, _objectPrefix));
		_migration = new Lazy<ColdArchiveMigrationCoordinator>(() => new ColdArchiveMigrationCoordinator(_migrationStorage.Value));
		_writeRetryPipeline = BuildWriteRetryPipeline();
	}

	/// <inheritdoc />
	public void Dispose()
	{
		if (Interlocked.Exchange(ref _disposed, 1) == 0 && _ownsClient)
		{
			_storageClient.Dispose();
		}
	}

	/// <inheritdoc />
	public Task ActivateTypedLayoutAsync(CancellationToken cancellationToken) =>
		_migration.Value.ActivateTypedLayoutAsync(cancellationToken);

	/// <inheritdoc />
	public Task MigrateAsync(KeyedTenantPartition tenant, string aggregateId, string aggregateType,
		CancellationToken cancellationToken) =>
		_migration.Value.MigrateAsync(tenant, aggregateId, aggregateType, cancellationToken);

	/// <inheritdoc />
	public async Task<long> WriteAsync(
		KeyedTenantPartition tenant,
		string aggregateId,
		string aggregateType,
		IReadOnlyList<StoredEvent> events,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(tenant);
		ArgumentNullException.ThrowIfNull(aggregateId);
		ArgumentException.ThrowIfNullOrEmpty(aggregateType);
		ArgumentNullException.ThrowIfNull(events);

		events = ColdArchiveBatch.Snapshot(events);

		if (events.Count == 0)
		{
			_ = await ReadArchiveAsync(tenant, aggregateId, aggregateType, cancellationToken).ConfigureAwait(false);
			return -1;
		}

		var objectName = _layout == ColdArchiveLayout.TypedV2
			? PrefixKey(ColdStorageKey.StreamPath(tenant, aggregateType, aggregateId) + "/events.json.gz")
			: GetObjectName(tenant, aggregateId);

		// Optimistic-concurrency read-modify-write: a concurrent archive must not silently overwrite (lost
		// update). We capture the source object's generation on read and write conditionally
		// (IfGenerationMatch=generation for an update, IfGenerationMatch=0 for a create); a precondition
		// failure means another writer raced us, so we re-read and retry against the now-current object.
		return await _writeRetryPipeline.ExecuteAsync(
			async ct =>
			{
				var (existingEvents, generation) = await ReadArchiveAsync(tenant, aggregateId, aggregateType, ct)
					.ConfigureAwait(false);

			// Membership, not maximum. Selecting by "version greater than the existing max" silently DROPS a
			// submitted version that falls into a gap below it — cold holding {0,1,5} would discard a
			// submitted {2,3,4} as already-present. Presence is a set question, so ask it as one.
			var newEvents = ColdArchiveBatch.GetAdditions(tenant, aggregateId, existingEvents, events, aggregateType);

			if (newEvents.Count == 0)
			{
				_logger.LogDebug("No new events to archive for {AggregateId}; all versions already in cold storage", aggregateId);
				// Every submitted version is already present, but "present" is not "safe to delete up to":
				// the caller may delete only across a CONTIGUOUS durable prefix, so report what is actually
				// contiguous in cold rather than the submitted maximum.
				return ContiguousDurablePrefix(existingEvents);
			}

			existingEvents.AddRange(newEvents);

			// A gap-filling batch appends out of order ({1,2} + {5,6} then {3,4}), and every downstream
			// reader — including the watermark below — assumes ascending order.
			existingEvents.Sort(static (left, right) => left.Version.CompareTo(right.Version));

				// A precondition failure here means another writer committed between our read and our
				// write. It propagates, and the pipeline re-invokes this whole delegate so the next
				// attempt re-reads the now-current generation.
				await WriteEventsToGcsAsync(objectName, existingEvents, generation, ct).ConfigureAwait(false);

				_logger.LogDebug(
					"Archived {NewCount} events for {AggregateId} to GCS (total {TotalCount})",
					newEvents.Count, aggregateId, existingEvents.Count);

				// The conditional upload has been awaited and acknowledged by GCS, so the merged set is
				// durable — but durability is not contiguity. Report the prefix actually present, so a caller
				// holding the only other copy of a gap never deletes across it.
				return ContiguousDurablePrefix(existingEvents);
			},
			cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<StoredEvent>> ReadAsync(
		KeyedTenantPartition tenant,
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(tenant);
		ArgumentNullException.ThrowIfNull(aggregateId);
		ArgumentException.ThrowIfNullOrEmpty(aggregateType);

		var (events, _) = await ReadArchiveAsync(tenant, aggregateId, aggregateType, cancellationToken).ConfigureAwait(false);
		ColdArchiveBatch.ValidateStream(events, tenant, aggregateId, aggregateType);
		events.Sort(static (left, right) => left.Version.CompareTo(right.Version));
		return events;
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<StoredEvent>> ReadAsync(
		KeyedTenantPartition tenant,
		string aggregateId,
		string aggregateType,
		long fromVersion,
		CancellationToken cancellationToken)
	{
		var allEvents = await ReadAsync(tenant, aggregateId, aggregateType, cancellationToken).ConfigureAwait(false);
		return allEvents.Where(e => e.Version > fromVersion).ToList();
	}

	/// <inheritdoc />
	public async Task<bool> HasArchivedEventsAsync(
		KeyedTenantPartition tenant,
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		return (await ReadAsync(tenant, aggregateId, aggregateType, cancellationToken).ConfigureAwait(false)).Count > 0;
	}

	/// <summary>
	/// Returns the highest version <c>V</c> such that every version from zero through <c>V</c> is
	/// present in <paramref name="ascendingEvents"/>, or <c>-1</c> when no such prefix is proven.
	/// </summary>
	/// <remarks>
	/// The interface promises a <strong>contiguous</strong> durable prefix, and a maximum is not a prefix.
	/// Reporting the maximum over a set containing a gap authorizes the caller to delete hot events across
	/// that gap — destroying the only surviving copy of versions cold never stored. Scanning for the first
	/// discontinuity is what makes the returned watermark mean what the contract says it means.
	/// </remarks>
	private static long ContiguousDurablePrefix(IReadOnlyList<StoredEvent> ascendingEvents) =>
		ColdArchiveBatch.ContiguousDurablePrefix(ascendingEvents);

	private string PrefixKey(string key) => string.IsNullOrEmpty(_objectPrefix) ? key : _objectPrefix + "/" + key;

	private async Task<(List<StoredEvent> Events, long? Generation)> ReadArchiveAsync(
		KeyedTenantPartition tenant, string aggregateId, string aggregateType, CancellationToken cancellationToken)
	{
		List<StoredEvent> events;
		long? generation;
		if (_layout == ColdArchiveLayout.TypedV2)
		{
			var archive = await _migration.Value.ReadTypedAsync(tenant, aggregateId, aggregateType, cancellationToken).ConfigureAwait(false);
			if (archive is null)
			{
				return ([], null);
			}

			events = (await _migrationStorage.Value.DecodeAsync(archive, cancellationToken).ConfigureAwait(false)).ToList();
			generation = long.Parse(archive.Revision, CultureInfo.InvariantCulture);
		}
		else
		{
			// Metadata presence alone disables Legacy; never initialize the raw adapter on this path.
			if (await ObjectExistsAsync(PrefixKey("layout-v1.json"), cancellationToken).ConfigureAwait(false)
				|| await ObjectExistsAsync(PrefixKey($"migration-v1/{ColdStorageKey.TenantSegment(tenant)}/{ColdStorageKey.AggregateSegment(aggregateId)}.json"), cancellationToken).ConfigureAwait(false))
			{
				throw new InvalidOperationException("A layout marker or migration receipt exists; legacy archive operations are disabled.");
			}

			(events, generation) = await TryDownloadForUpdateAsync(GetObjectName(tenant, aggregateId), cancellationToken).ConfigureAwait(false);
		}

		ColdArchiveBatch.ValidateStream(events, tenant, aggregateId, aggregateType);
		events.Sort(static (left, right) => left.Version.CompareTo(right.Version));
		return (events, generation);
	}

	private async Task RequireBucketAsync(CancellationToken cancellationToken)
	{
		var bucket = await _storageClient.GetBucketAsync(_bucketName, cancellationToken: cancellationToken).ConfigureAwait(false);
		if (bucket.Name != _bucketName)
		{
			throw new InvalidDataException("The storage response does not identify the requested bucket.");
		}
	}

	private string GetObjectName(KeyedTenantPartition tenant, string aggregateId)
	{
		// BOTH components are Base64Url-encoded (injective, alphabet excludes '/' and '\'), so the key is a
		// function of the whole (tenant, aggregate) pair and distinct pairs cannot share an object. Encoding
		// the aggregate term is load-bearing, not belt-and-braces: the Replace-based sanitation this
		// supersedes was many-to-one, so 'a\b' and 'a_b' addressed the SAME object within one tenant.
		var tenantSegment = ColdStorageKey.TenantSegment(tenant);
		var aggregateSegment = ColdStorageKey.AggregateSegment(aggregateId);
		return string.IsNullOrEmpty(_objectPrefix)
			? $"{tenantSegment}/{aggregateSegment}/events.json.gz"
			: $"{_objectPrefix}/{tenantSegment}/{aggregateSegment}/events.json.gz";
	}

	private async Task<bool> ObjectExistsAsync(string objectName, CancellationToken cancellationToken)
	{
		try
		{
			await _storageClient.GetObjectAsync(_bucketName, objectName, cancellationToken: cancellationToken)
				.ConfigureAwait(false);
			return true;
		}
		catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
		{
			await RequireBucketAsync(cancellationToken).ConfigureAwait(false);
			return false;
		}
	}

	/// <summary>
	/// Downloads the current archive object (if any) and its generation in a single request. Returns an
	/// empty list and a <see langword="null"/> generation when the object does not yet exist (create path).
	/// </summary>
	private async Task<(List<StoredEvent> Events, long? Generation)> TryDownloadForUpdateAsync(
		string objectName,
		CancellationToken cancellationToken)
	{
		using var memoryStream = new MemoryStream();
		try
		{
			var downloaded = await _storageClient.DownloadObjectAsync(
				_bucketName, objectName, memoryStream, cancellationToken: cancellationToken).ConfigureAwait(false);
			if (downloaded.Bucket != _bucketName || downloaded.Name != objectName || downloaded.Generation is not > 0)
			{
				throw new InvalidDataException("The downloaded archive does not identify the requested object and generation.");
			}

			memoryStream.Position = 0;
			await using var gzipStream = new GZipStream(memoryStream, CompressionMode.Decompress);

			var events = await JsonSerializer.DeserializeAsync(
				gzipStream, ArchiveTypeInfo, cancellationToken).ConfigureAwait(false);

			return (events ?? throw new JsonException("The archive must contain an event array, not null."), downloaded.Generation);
		}
		catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
		{
			await RequireBucketAsync(cancellationToken).ConfigureAwait(false);
			return ([], null);
		}
	}

	private async Task WriteEventsToGcsAsync(
		string objectName,
		List<StoredEvent> events,
		long? generation,
		CancellationToken cancellationToken)
	{
		using var memoryStream = new MemoryStream();
		{
			await using var gzipStream = new GZipStream(memoryStream, CompressionLevel.Optimal, leaveOpen: true);
			await JsonSerializer.SerializeAsync(gzipStream, events, ArchiveTypeInfo, cancellationToken)
				.ConfigureAwait(false);
		}

		memoryStream.Position = 0;

		// Conditional write: IfGenerationMatch=generation updates only if unchanged; IfGenerationMatch=0
		// creates only if absent. Either way a concurrent writer's commit triggers a 412, never a silent
		// overwrite.
		var options = new UploadObjectOptions { IfGenerationMatch = generation ?? 0 };

		var uploaded = await _storageClient.UploadObjectAsync(
			_bucketName,
			objectName,
			"application/json",
			memoryStream,
			options,
			cancellationToken: cancellationToken).ConfigureAwait(false);
		if (uploaded.Bucket != _bucketName || uploaded.Name != objectName || uploaded.Generation is not > 0)
		{
			throw new InvalidDataException("The archive upload acknowledgement does not identify the requested object and generation.");
		}

		if (_layout == ColdArchiveLayout.TypedV2)
		{
			GcsArchiveIntegrity.Validate(memoryStream.ToArray(), uploaded.Size, uploaded.Crc32c, cancellationToken);
		}
	}
}
