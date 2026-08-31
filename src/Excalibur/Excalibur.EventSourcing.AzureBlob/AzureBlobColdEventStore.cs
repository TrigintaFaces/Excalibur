// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

using Microsoft.Extensions.Logging;

using Excalibur.Dispatch;

namespace Excalibur.EventSourcing.AzureBlob;

/// <summary>
/// Azure Blob Storage implementation of <see cref="IColdEventStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// Events are stored as gzip-compressed JSON blobs, one blob per aggregate.
/// Blob naming convention: <c>{tenantSegment}/{aggregateSegment}.json.gz</c>, relative to the configured
/// container. Both segments are Base64Url-encoded, so neither appears verbatim: write lifecycle rules and
/// access policies against the encoded form, never against a raw tenant or aggregate identifier. No
/// container prefix is prepended to the blob name — the container itself is the only scoping above the
/// tenant segment.
/// </para>
/// <para>
/// Subsequent writes for the same aggregate append events by reading the existing
/// blob, merging, and overwriting. For write-heavy archival scenarios, consider
/// using version-range blobs (future enhancement).
/// </para>
/// </remarks>
internal sealed class AzureBlobColdEventStore : IColdEventStore
{
	private const int MaxConcurrencyRetries = 5;

	private readonly BlobContainerClient _containerClient;
	private readonly ILogger<AzureBlobColdEventStore> _logger;

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
		_ = EventSerializationDefaults.TryApplyTypeInfoResolver(options, AzureBlobColdStoreJsonContext.Default);
		return (JsonTypeInfo<List<StoredEvent>>)options.GetTypeInfo(typeof(List<StoredEvent>));
	}

	internal AzureBlobColdEventStore(
		BlobContainerClient containerClient,
		ILogger<AzureBlobColdEventStore> logger)
	{
		ArgumentNullException.ThrowIfNull(containerClient);
		ArgumentNullException.ThrowIfNull(logger);

		_containerClient = containerClient;
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

		var blobClient = GetBlobClient(tenant, aggregateId);

		// Optimistic-concurrency read-modify-write: a concurrent archive must not silently overwrite
		// (lost update). We capture the source blob's ETag on read and write conditionally (IfMatch for an
		// update, IfNoneMatch=* for a create); a precondition failure means another writer raced us, so we
		// re-read and retry against the now-current blob.
		for (var attempt = 0; ; attempt++)
		{
			var (existingEvents, etag) = await TryDownloadForUpdateAsync(blobClient, cancellationToken)
				.ConfigureAwait(false);

			// Merge by version MEMBERSHIP, not by maximum. Selecting by "version greater than the existing
			// max" silently DROPS a submitted version that falls into a gap below it — cold holding {0,1,5}
			// would discard a submitted {2,3,4} as already-present. Presence is a set question.
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

			// IfMatch=etag when updating an existing blob; IfNoneMatch=* (create-only) when none existed.
			var conditions = etag is { } e
				? new BlobRequestConditions { IfMatch = e }
				: new BlobRequestConditions { IfNoneMatch = ETag.All };

			try
			{
				await WriteEventsToBlobAsync(blobClient, existingEvents, conditions, cancellationToken)
					.ConfigureAwait(false);

				_logger.LogDebug(
					"Archived {NewCount} events for {AggregateId} to blob (total {TotalCount})",
					newEvents.Count, aggregateId, existingEvents.Count);
				// The conditional upload has been awaited and acknowledged, so the merged set is durable —
				// but durability is not contiguity. Report the prefix actually present, so a caller holding
				// the only other copy of a gap never deletes across it.
				return ContiguousDurablePrefix(existingEvents);
			}
			catch (RequestFailedException ex) when (
				(ex.Status == 412 || ex.Status == 409) && attempt < MaxConcurrencyRetries)
			{
				// Another writer committed between our read and write — re-read and retry.
				_logger.LogDebug(
					"Concurrent archive detected for {AggregateId} (status {Status}); retrying (attempt {Attempt})",
					aggregateId, ex.Status, attempt + 1);
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

		var blobClient = GetBlobClient(tenant, aggregateId);

		if (!await BlobExistsAsync(blobClient, cancellationToken).ConfigureAwait(false))
		{
			return Array.Empty<StoredEvent>();
		}

		return await ReadEventsFromBlobAsync(blobClient, cancellationToken).ConfigureAwait(false);
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

		var blobClient = GetBlobClient(tenant, aggregateId);
		return await BlobExistsAsync(blobClient, cancellationToken).ConfigureAwait(false);
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

	private BlobClient GetBlobClient(KeyedTenantPartition tenant, string aggregateId)
	{
		// BOTH components are Base64Url-encoded (injective, alphabet excludes '/' and '\'), so the blob name
		// is a function of the whole (tenant, aggregate) pair and distinct pairs cannot share a blob.
		// Encoding the aggregate term is load-bearing, not belt-and-braces: the Replace-based sanitation this
		// supersedes was many-to-one, and collapsed 'a/b', 'a\b' and 'a_b' onto ONE blob within one tenant.
		var tenantSegment = ColdStorageKey.TenantSegment(tenant);
		var aggregateSegment = ColdStorageKey.AggregateSegment(aggregateId);
		return _containerClient.GetBlobClient($"{tenantSegment}/{aggregateSegment}.json.gz");
	}

	private static async Task<bool> BlobExistsAsync(
		BlobClient blobClient,
		CancellationToken cancellationToken)
	{
		var response = await blobClient.ExistsAsync(cancellationToken).ConfigureAwait(false);
		return response.Value;
	}

	private async Task<List<StoredEvent>> ReadEventsFromBlobAsync(
		BlobClient blobClient,
		CancellationToken cancellationToken)
	{
		var downloadResponse = await blobClient.DownloadContentAsync(cancellationToken).ConfigureAwait(false);

		using var compressedStream = downloadResponse.Value.Content.ToStream();
		await using var gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress);

		var events = await JsonSerializer.DeserializeAsync(
			gzipStream, ArchiveTypeInfo, cancellationToken).ConfigureAwait(false);

		return events ?? [];
	}

	/// <summary>
	/// Downloads the current archive blob (if any) and its ETag in a single request. Returns an empty
	/// list and a <see langword="null"/> ETag when the blob does not yet exist (create path).
	/// </summary>
	private async Task<(List<StoredEvent> Events, ETag? ETag)> TryDownloadForUpdateAsync(
		BlobClient blobClient,
		CancellationToken cancellationToken)
	{
		try
		{
			var downloadResponse = await blobClient.DownloadContentAsync(cancellationToken).ConfigureAwait(false);

			using var compressedStream = downloadResponse.Value.Content.ToStream();
			await using var gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress);

			var events = await JsonSerializer.DeserializeAsync(
				gzipStream, ArchiveTypeInfo, cancellationToken).ConfigureAwait(false);

			return (events ?? [], downloadResponse.Value.Details.ETag);
		}
		catch (RequestFailedException ex) when (ex.Status == 404)
		{
			return ([], null);
		}
	}

	private async Task WriteEventsToBlobAsync(
		BlobClient blobClient,
		List<StoredEvent> events,
		BlobRequestConditions conditions,
		CancellationToken cancellationToken)
	{
		using var memoryStream = new MemoryStream();
		{
			await using var gzipStream = new GZipStream(memoryStream, CompressionLevel.Optimal, leaveOpen: true);
			await JsonSerializer.SerializeAsync(gzipStream, events, ArchiveTypeInfo, cancellationToken)
				.ConfigureAwait(false);
		}

		memoryStream.Position = 0;

		await blobClient.UploadAsync(
			memoryStream,
			new BlobUploadOptions
			{
				HttpHeaders = new BlobHttpHeaders
				{
					ContentType = "application/json",
					ContentEncoding = "gzip",
				},
				Conditions = conditions,
			},
			cancellationToken).ConfigureAwait(false);
	}
}
