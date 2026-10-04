// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.IO.Compression;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using Amazon.S3;
using Amazon.S3.Model;

using Microsoft.Extensions.Logging;

using Polly;
using Polly.Retry;

using Excalibur.Dispatch;
using Excalibur.EventSourcing.TieredStorage;

namespace Excalibur.EventSourcing.AwsS3;

/// <summary>
/// AWS S3 implementation of <see cref="IColdEventStore"/>.
/// </summary>
/// <remarks>
/// Events are stored as gzip-compressed JSON objects, one object per aggregate.
/// Key pattern: <c>{keyPrefix}/{tenantSegment}/{aggregateSegment}/events.json.gz</c>, and
/// <c>{tenantSegment}/{aggregateSegment}/events.json.gz</c> when no key prefix is configured. Both segments
/// are Base64Url-encoded, so neither appears verbatim: write lifecycle rules and IAM prefix conditions
/// against the encoded form, never against a raw tenant or aggregate identifier.
/// TypedV2 uses a type-qualified key and requires explicit activation and migration. Client routing,
/// bucket and prefix must identify one fixed, strongly consistent namespace for the instance lifetime.
/// Legacy guards do not require endpoint discovery; typed operations and migration do. Activation never
/// changes the captured layout, and does not supply the required external legacy-operation fence.
/// </remarks>
internal sealed class AwsS3ColdEventStore : IColdEventStore, IColdEventStoreMigration
{
	private const int MaxConcurrencyRetries = 5;

	private readonly IAmazonS3 _s3Client;
	private readonly string _bucketName;
	private readonly string _keyPrefix;
	private readonly ILogger<AwsS3ColdEventStore> _logger;
	private readonly ColdArchiveLayout _layout;
	private readonly AwsS3ColdArchiveMigrationStorage _migrationStorage;
	private readonly Lazy<ColdArchiveMigrationCoordinator> _migration;

	/// <summary>
	/// Retries the WriteAsync read-modify-write body on an S3 precondition/conflict response (another
	/// writer raced the compare-and-swap). Built on Polly's <see cref="ResiliencePipeline"/> rather than a
	/// hand-rolled loop: the delegate re-reads the object each attempt, so retrying it is
	/// exactly the CAS-retry semantics the loop needs, and Polly still owns attempt counting/telemetry.
	/// No backoff between attempts -- the original loop retried immediately, and this preserves that.
	/// </summary>
	private readonly ResiliencePipeline _writeRetryPipeline;

	private ResiliencePipeline BuildWriteRetryPipeline() =>
		new ResiliencePipelineBuilder()
			.AddRetry(new RetryStrategyOptions
			{
				ShouldHandle = new PredicateBuilder().Handle<AmazonS3Exception>(
					ex => ex.StatusCode is HttpStatusCode.PreconditionFailed or HttpStatusCode.Conflict),
				MaxRetryAttempts = MaxConcurrencyRetries,
				Delay = TimeSpan.Zero,
				BackoffType = DelayBackoffType.Constant,
				OnRetry = args =>
				{
					_logger.LogDebug(
						"Concurrent archive detected (status {Status}); retrying (attempt {Attempt})",
						(args.Outcome.Exception as AmazonS3Exception)?.StatusCode, args.AttemptNumber + 1);
					return ValueTask.CompletedTask;
				},
			})
			.Build();

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
		_ = EventSerializationDefaults.TryApplyTypeInfoResolver(options, AwsS3ColdStoreJsonContext.Default);
		return (JsonTypeInfo<List<StoredEvent>>)options.GetTypeInfo(typeof(List<StoredEvent>));
	}

	internal AwsS3ColdEventStore(
		IAmazonS3 s3Client,
		string bucketName,
		string keyPrefix,
		ILogger<AwsS3ColdEventStore> logger,
		ColdArchiveLayout layout = ColdArchiveLayout.Legacy)
	{
		ArgumentNullException.ThrowIfNull(s3Client);
		ArgumentNullException.ThrowIfNull(bucketName);
		ArgumentNullException.ThrowIfNull(logger);
		if (!Enum.IsDefined(layout))
		{
			throw new ArgumentOutOfRangeException(nameof(layout));
		}

		_s3Client = s3Client;
		_bucketName = bucketName;
		_keyPrefix = keyPrefix ?? "";
		_logger = logger;
		_layout = layout;
		_migrationStorage = new AwsS3ColdArchiveMigrationStorage(s3Client, bucketName, _keyPrefix);
		_migration = new Lazy<ColdArchiveMigrationCoordinator>(() => new ColdArchiveMigrationCoordinator(_migrationStorage));
		_writeRetryPipeline = BuildWriteRetryPipeline();
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

		var key = _layout == ColdArchiveLayout.TypedV2
			? _migrationStorage.GetTypedKey(tenant, aggregateId, aggregateType)
			: _migrationStorage.GetLegacyKey(tenant, aggregateId);

		// Optimistic-concurrency read-modify-write: a concurrent archive must not silently overwrite (lost
		// update). We capture the source object's ETag on read and write conditionally (IfMatch for an
		// update, IfNoneMatch=* for a create); a precondition failure means another writer raced us, so
		// Polly re-invokes the whole delegate, which re-reads and retries against the now-current object.
		return await _writeRetryPipeline.ExecuteAsync(
			async ct =>
			{
				var (existingEvents, etag) = await ReadArchiveAsync(tenant, aggregateId, aggregateType, ct).ConfigureAwait(false);

				// Membership, not maximum. Selecting by "version greater than the existing max" silently
				// DROPS a submitted version that falls into a gap below it — cold holding {0,1,5} would
				// discard a submitted {2,3,4} as already-present. Presence is a set question, ask it as one.
				var newEvents = ColdArchiveBatch.GetAdditions(tenant, aggregateId, existingEvents, events, aggregateType);

				if (newEvents.Count == 0)
				{
					_logger.LogDebug(
						"No new events to archive for {AggregateId}; all versions already in cold storage",
						aggregateId);
					// Every submitted version is already present, but "present" is not "safe to delete up
					// to": the caller may delete only across a CONTIGUOUS durable prefix, so report what is
					// actually contiguous in cold rather than the submitted maximum.
					return ContiguousDurablePrefix(existingEvents);
				}

				existingEvents.AddRange(newEvents);

				// A gap-filling batch appends out of order ({1,2} + {5,6} then {3,4}), and every downstream
				// reader — including the watermark below — assumes ascending order.
				existingEvents.Sort(static (left, right) => left.Version.CompareTo(right.Version));

				await WriteEventsToS3Async(key, existingEvents, etag, ct).ConfigureAwait(false);

				_logger.LogDebug(
					"Archived {NewCount} events for {AggregateId} to S3 (total {TotalCount})",
					newEvents.Count, aggregateId, existingEvents.Count);
				// The conditional upload has been awaited and acknowledged by S3, so the merged set is
				// durable — but durability is not contiguity. Report the prefix actually present, so a
				// caller holding the only other copy of a gap never deletes across it.
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

	private async Task<(List<StoredEvent> Events, string? ETag)> ReadArchiveAsync(
		KeyedTenantPartition tenant, string aggregateId, string aggregateType, CancellationToken cancellationToken)
	{
		ColdArchiveMigrationObject? archive;
		if (_layout == ColdArchiveLayout.TypedV2)
		{
			archive = await _migration.Value.ReadTypedAsync(tenant, aggregateId, aggregateType, cancellationToken).ConfigureAwait(false);
		}
		else
		{
			// Presence alone disables Legacy. Do not resolve namespace identity for this compatibility path.
			if (await _migrationStorage.ReadAsync(_migrationStorage.GetLayoutKey(), cancellationToken).ConfigureAwait(false) is not null
				|| await _migrationStorage.ReadAsync(_migrationStorage.GetReceiptKey(tenant, aggregateId), cancellationToken).ConfigureAwait(false) is not null)
			{
				throw new InvalidOperationException("A layout marker or migration receipt exists; legacy archive operations are disabled.");
			}

			archive = await _migrationStorage.ReadAsync(_migrationStorage.GetLegacyKey(tenant, aggregateId), cancellationToken).ConfigureAwait(false);
		}

		if (archive is null)
		{
			return ([], null);
		}

		var events = (await _migrationStorage.DecodeAsync(archive, cancellationToken).ConfigureAwait(false)).ToList();
		ColdArchiveBatch.ValidateStream(events, tenant, aggregateId, aggregateType);
		events.Sort(static (left, right) => left.Version.CompareTo(right.Version));
		return (events, archive.Revision);
	}
	private async Task WriteEventsToS3Async(
		string key,
		List<StoredEvent> events,
		string? etag,
		CancellationToken cancellationToken)
	{
		using var memoryStream = new MemoryStream();
		{
			await using var gzipStream = new GZipStream(memoryStream, CompressionLevel.Optimal, leaveOpen: true);
			await JsonSerializer.SerializeAsync(gzipStream, events, ArchiveTypeInfo, cancellationToken)
				.ConfigureAwait(false);
		}

		memoryStream.Position = 0;

		var request = new PutObjectRequest
		{
			BucketName = _bucketName,
			Key = key,
			InputStream = memoryStream,
			ContentType = "application/json",
		};
		request.Headers.ContentEncoding = "gzip";

		// Conditional write: IfMatch=etag updates only if unchanged; IfNoneMatch=* creates only if absent.
		// Either way a concurrent writer's commit triggers a precondition failure, never a silent overwrite.
		if (etag is { } e)
		{
			request.IfMatch = e;
		}
		else
		{
			request.IfNoneMatch = "*";
		}

		var response = await _s3Client.PutObjectAsync(request, cancellationToken).ConfigureAwait(false);
		if (response.HttpStatusCode != HttpStatusCode.OK)
		{
			throw new InvalidOperationException("The archive upload did not return a successful durable acknowledgement.");
		}
	}
}
