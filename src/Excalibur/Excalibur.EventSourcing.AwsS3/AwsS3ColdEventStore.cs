// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

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
/// </remarks>
internal sealed class AwsS3ColdEventStore : IColdEventStore
{
	private const int MaxConcurrencyRetries = 5;

	private readonly IAmazonS3 _s3Client;
	private readonly string _bucketName;
	private readonly string _keyPrefix;
	private readonly ILogger<AwsS3ColdEventStore> _logger;

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
		ILogger<AwsS3ColdEventStore> logger)
	{
		ArgumentNullException.ThrowIfNull(s3Client);
		ArgumentNullException.ThrowIfNull(bucketName);
		ArgumentNullException.ThrowIfNull(logger);

		_s3Client = s3Client;
		_bucketName = bucketName;
		_keyPrefix = keyPrefix ?? "";
		_logger = logger;
		_writeRetryPipeline = BuildWriteRetryPipeline();
	}

	/// <inheritdoc />
	public async Task<long> WriteAsync(
		KeyedTenantPartition tenant,
		string aggregateId,
		IReadOnlyList<StoredEvent> events,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(tenant);
		ArgumentNullException.ThrowIfNull(aggregateId);
		ArgumentNullException.ThrowIfNull(events);

		if (events.Count == 0)
		{
			return -1;
		}

		var key = GetObjectKey(tenant, aggregateId);

		// Optimistic-concurrency read-modify-write: a concurrent archive must not silently overwrite (lost
		// update). We capture the source object's ETag on read and write conditionally (IfMatch for an
		// update, IfNoneMatch=* for a create); a precondition failure means another writer raced us, so
		// Polly re-invokes the whole delegate, which re-reads and retries against the now-current object.
		return await _writeRetryPipeline.ExecuteAsync(
			async ct =>
			{
				var (existingEvents, etag) = await TryDownloadForUpdateAsync(key, ct).ConfigureAwait(false);

				// Membership, not maximum. Selecting by "version greater than the existing max" silently
				// DROPS a submitted version that falls into a gap below it — cold holding {0,1,5} would
				// discard a submitted {2,3,4} as already-present. Presence is a set question, ask it as one.
				var existingVersions = existingEvents.Select(e => e.Version).ToHashSet();
				var newEvents = events.Where(e => !existingVersions.Contains(e.Version)).ToList();

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
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(tenant);
		ArgumentNullException.ThrowIfNull(aggregateId);

		var key = GetObjectKey(tenant, aggregateId);
		if (!await ObjectExistsAsync(key, cancellationToken).ConfigureAwait(false))
		{
			return Array.Empty<StoredEvent>();
		}

		return await ReadEventsFromS3Async(key, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<StoredEvent>> ReadAsync(
		KeyedTenantPartition tenant,
		string aggregateId,
		long fromVersion,
		CancellationToken cancellationToken)
	{
		var allEvents = await ReadAsync(tenant, aggregateId, cancellationToken).ConfigureAwait(false);
		return allEvents.Where(e => e.Version > fromVersion).ToList();
	}

	/// <inheritdoc />
	public async Task<bool> HasArchivedEventsAsync(
		KeyedTenantPartition tenant,
		string aggregateId,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(tenant);
		ArgumentNullException.ThrowIfNull(aggregateId);
		return await ObjectExistsAsync(GetObjectKey(tenant, aggregateId), cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Returns the highest version <c>V</c> such that every version from the aggregate's lowest archived
	/// version through <c>V</c> is present in <paramref name="ascendingEvents"/>, or <c>-1</c> when nothing
	/// is archived.
	/// </summary>
	/// <remarks>
	/// The interface promises a <strong>contiguous</strong> durable prefix, and a maximum is not a prefix.
	/// Reporting the maximum over a set containing a gap authorizes the caller to delete hot events across
	/// that gap — destroying the only surviving copy of versions cold never stored. Scanning for the first
	/// discontinuity is what makes the returned watermark mean what the contract says it means.
	/// </remarks>
	private static long ContiguousDurablePrefix(IReadOnlyList<StoredEvent> ascendingEvents)
	{
		if (ascendingEvents.Count == 0)
		{
			return -1;
		}

		var watermark = ascendingEvents[0].Version;
		for (var i = 1; i < ascendingEvents.Count; i++)
		{
			var version = ascendingEvents[i].Version;
			if (version == watermark)
			{
				// A duplicate version neither extends nor breaks the run.
				continue;
			}

			if (version != watermark + 1)
			{
				break;
			}

			watermark = version;
		}

		return watermark;
	}

	private string GetObjectKey(KeyedTenantPartition tenant, string aggregateId)
	{
		// BOTH components are Base64Url-encoded (injective, alphabet excludes '/' and '\'), so the key is a
		// function of the whole (tenant, aggregate) pair and distinct pairs cannot share an object. Encoding
		// the aggregate term is load-bearing, not belt-and-braces: the Replace-based sanitation this
		// supersedes was many-to-one, so 'a\b' and 'a_b' addressed the SAME object within one tenant.
		var tenantSegment = ColdStorageKey.TenantSegment(tenant);
		var aggregateSegment = ColdStorageKey.AggregateSegment(aggregateId);
		return string.IsNullOrEmpty(_keyPrefix)
			? $"{tenantSegment}/{aggregateSegment}/events.json.gz"
			: $"{_keyPrefix}/{tenantSegment}/{aggregateSegment}/events.json.gz";
	}

	private async Task<bool> ObjectExistsAsync(string key, CancellationToken cancellationToken)
	{
		try
		{
			await _s3Client.GetObjectMetadataAsync(_bucketName, key, cancellationToken).ConfigureAwait(false);
			return true;
		}
		catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
		{
			return false;
		}
	}

	private async Task<List<StoredEvent>> ReadEventsFromS3Async(string key, CancellationToken cancellationToken)
	{
		var response = await _s3Client.GetObjectAsync(_bucketName, key, cancellationToken).ConfigureAwait(false);

		await using var responseStream = response.ResponseStream;
		await using var gzipStream = new GZipStream(responseStream, CompressionMode.Decompress);

		var events = await JsonSerializer.DeserializeAsync(
			gzipStream, ArchiveTypeInfo, cancellationToken).ConfigureAwait(false);

		return events ?? [];
	}

	/// <summary>
	/// Downloads the current archive object (if any) and its ETag in a single request. Returns an empty
	/// list and a <see langword="null"/> ETag when the object does not yet exist (create path).
	/// </summary>
	private async Task<(List<StoredEvent> Events, string? ETag)> TryDownloadForUpdateAsync(
		string key,
		CancellationToken cancellationToken)
	{
		try
		{
			var response = await _s3Client.GetObjectAsync(_bucketName, key, cancellationToken).ConfigureAwait(false);

			await using var responseStream = response.ResponseStream;
			await using var gzipStream = new GZipStream(responseStream, CompressionMode.Decompress);

			var events = await JsonSerializer.DeserializeAsync(
				gzipStream, ArchiveTypeInfo, cancellationToken).ConfigureAwait(false);

			return (events ?? [], response.ETag);
		}
		catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
		{
			return ([], null);
		}
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

		await _s3Client.PutObjectAsync(request, cancellationToken).ConfigureAwait(false);
	}
}
