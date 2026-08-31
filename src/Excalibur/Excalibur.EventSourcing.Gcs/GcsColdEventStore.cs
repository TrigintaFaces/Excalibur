// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using Google.Cloud.Storage.V1;

using Microsoft.Extensions.Logging;

using Excalibur.Dispatch;

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
/// </remarks>
internal sealed class GcsColdEventStore : IColdEventStore
{
	private const int MaxConcurrencyRetries = 5;

	private readonly StorageClient _storageClient;
	private readonly string _bucketName;
	private readonly string _objectPrefix;
	private readonly ILogger<GcsColdEventStore> _logger;

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
		ILogger<GcsColdEventStore> logger)
	{
		ArgumentNullException.ThrowIfNull(storageClient);
		ArgumentNullException.ThrowIfNull(bucketName);
		ArgumentNullException.ThrowIfNull(logger);

		_storageClient = storageClient;
		_bucketName = bucketName;
		_objectPrefix = objectPrefix ?? "";
		_logger = logger;
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

		var objectName = GetObjectName(tenant, aggregateId);

		// Optimistic-concurrency read-modify-write: a concurrent archive must not silently overwrite (lost
		// update). We capture the source object's generation on read and write conditionally
		// (IfGenerationMatch=generation for an update, IfGenerationMatch=0 for a create); a precondition
		// failure means another writer raced us, so we re-read and retry against the now-current object.
		for (var attempt = 0; ; attempt++)
		{
			var (existingEvents, generation) = await TryDownloadForUpdateAsync(objectName, cancellationToken)
				.ConfigureAwait(false);

			// Membership, not maximum. Selecting by "version greater than the existing max" silently DROPS a
			// submitted version that falls into a gap below it — cold holding {0,1,5} would discard a
			// submitted {2,3,4} as already-present. Presence is a set question, so ask it as one.
			var existingVersions = existingEvents.Select(e => e.Version).ToHashSet();
			var newEvents = events.Where(e => !existingVersions.Contains(e.Version)).ToList();

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

			try
			{
				await WriteEventsToGcsAsync(objectName, existingEvents, generation, cancellationToken).ConfigureAwait(false);

				_logger.LogDebug(
					"Archived {NewCount} events for {AggregateId} to GCS (total {TotalCount})",
					newEvents.Count, aggregateId, existingEvents.Count);
				// The conditional upload has been awaited and acknowledged by GCS, so the merged set is
				// durable — but durability is not contiguity. Report the prefix actually present, so a caller
				// holding the only other copy of a gap never deletes across it.
				return ContiguousDurablePrefix(existingEvents);
			}
			catch (Google.GoogleApiException ex) when (
				ex.HttpStatusCode == System.Net.HttpStatusCode.PreconditionFailed && attempt < MaxConcurrencyRetries)
			{
				// Another writer committed between our read and write — re-read and retry.
				_logger.LogDebug(
					"Concurrent archive detected for {AggregateId} (status {Status}); retrying (attempt {Attempt})",
					aggregateId, ex.HttpStatusCode, attempt + 1);
			}
		}
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<StoredEvent>> ReadAsync(
		KeyedTenantPartition tenant,
		string aggregateId,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(tenant);
		ArgumentNullException.ThrowIfNull(aggregateId);

		var objectName = GetObjectName(tenant, aggregateId);
		if (!await ObjectExistsAsync(objectName, cancellationToken).ConfigureAwait(false))
		{
			return Array.Empty<StoredEvent>();
		}

		return await ReadEventsFromGcsAsync(objectName, cancellationToken).ConfigureAwait(false);
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
		return await ObjectExistsAsync(GetObjectName(tenant, aggregateId), cancellationToken).ConfigureAwait(false);
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
			return false;
		}
	}

	private async Task<List<StoredEvent>> ReadEventsFromGcsAsync(
		string objectName,
		CancellationToken cancellationToken)
	{
		using var memoryStream = new MemoryStream();
		await _storageClient.DownloadObjectAsync(
			_bucketName, objectName, memoryStream, cancellationToken: cancellationToken).ConfigureAwait(false);

		memoryStream.Position = 0;
		await using var gzipStream = new GZipStream(memoryStream, CompressionMode.Decompress);

		var events = await JsonSerializer.DeserializeAsync(
			gzipStream, ArchiveTypeInfo, cancellationToken).ConfigureAwait(false);

		return events ?? [];
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

			memoryStream.Position = 0;
			await using var gzipStream = new GZipStream(memoryStream, CompressionMode.Decompress);

			var events = await JsonSerializer.DeserializeAsync(
				gzipStream, ArchiveTypeInfo, cancellationToken).ConfigureAwait(false);

			return (events ?? [], downloaded.Generation);
		}
		catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
		{
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

		await _storageClient.UploadObjectAsync(
			_bucketName,
			objectName,
			"application/json",
			memoryStream,
			options,
			cancellationToken: cancellationToken).ConfigureAwait(false);
	}
}
