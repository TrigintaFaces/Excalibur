// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.IO.Compression;
using System.Text.Json;

using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

using Microsoft.Extensions.Logging;

namespace Excalibur.EventSourcing.AzureBlob;

/// <summary>
/// Azure Blob Storage implementation of <see cref="IColdEventStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// Events are stored as gzip-compressed JSON blobs, one blob per aggregate.
/// Blob naming convention: <c>{ContainerPrefix}/{aggregateId}.json.gz</c>.
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
	private readonly JsonSerializerOptions _jsonOptions;

	internal AzureBlobColdEventStore(
		BlobContainerClient containerClient,
		ILogger<AzureBlobColdEventStore> logger)
	{
		ArgumentNullException.ThrowIfNull(containerClient);
		ArgumentNullException.ThrowIfNull(logger);

		_containerClient = containerClient;
		_logger = logger;
		_jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
	}

	/// <inheritdoc />
	public async Task WriteAsync(
		string aggregateId,
		IReadOnlyList<StoredEvent> events,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(aggregateId);
		ArgumentNullException.ThrowIfNull(events);

		if (events.Count == 0)
		{
			return;
		}

		var blobClient = GetBlobClient(aggregateId);

		// Optimistic-concurrency read-modify-write: a concurrent archive must not silently overwrite
		// (lost update). We capture the source blob's ETag on read and write conditionally (IfMatch for an
		// update, IfNoneMatch=* for a create); a precondition failure means another writer raced us, so we
		// re-read and retry against the now-current blob.
		for (var attempt = 0; ; attempt++)
		{
			var (existingEvents, etag) = await TryDownloadForUpdateAsync(blobClient, cancellationToken)
				.ConfigureAwait(false);

			// Merge: deduplicate by version, keep latest
			var maxExistingVersion = existingEvents.Count > 0
				? existingEvents[^1].Version
				: -1;

			var newEvents = events.Where(e => e.Version > maxExistingVersion).ToList();
			if (newEvents.Count == 0)
			{
				_logger.LogDebug("No new events to archive for {AggregateId}; all versions already in cold storage", aggregateId);
				return;
			}

			existingEvents.AddRange(newEvents);

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
				return;
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
		string aggregateId,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(aggregateId);

		var blobClient = GetBlobClient(aggregateId);

		if (!await BlobExistsAsync(blobClient, cancellationToken).ConfigureAwait(false))
		{
			return Array.Empty<StoredEvent>();
		}

		return await ReadEventsFromBlobAsync(blobClient, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<StoredEvent>> ReadAsync(
		string aggregateId,
		long fromVersion,
		CancellationToken cancellationToken)
	{
		var allEvents = await ReadAsync(aggregateId, cancellationToken).ConfigureAwait(false);
		return allEvents.Where(e => e.Version > fromVersion).ToList();
	}

	/// <inheritdoc />
	public async Task<bool> HasArchivedEventsAsync(
		string aggregateId,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(aggregateId);

		var blobClient = GetBlobClient(aggregateId);
		return await BlobExistsAsync(blobClient, cancellationToken).ConfigureAwait(false);
	}

	private BlobClient GetBlobClient(string aggregateId)
	{
		// Sanitize aggregate ID for blob naming (replace unsafe chars)
		var safeName = aggregateId.Replace('/', '_').Replace('\\', '_');
		return _containerClient.GetBlobClient($"{safeName}.json.gz");
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

#pragma warning disable IL2026, IL3050 // Serialization inherently uses reflection
		var events = await JsonSerializer.DeserializeAsync<List<StoredEvent>>(
			gzipStream, _jsonOptions, cancellationToken).ConfigureAwait(false);
#pragma warning restore IL2026, IL3050

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

#pragma warning disable IL2026, IL3050 // Serialization inherently uses reflection
			var events = await JsonSerializer.DeserializeAsync<List<StoredEvent>>(
				gzipStream, _jsonOptions, cancellationToken).ConfigureAwait(false);
#pragma warning restore IL2026, IL3050

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
#pragma warning disable IL2026, IL3050 // Serialization inherently uses reflection
			await JsonSerializer.SerializeAsync(gzipStream, events, _jsonOptions, cancellationToken)
				.ConfigureAwait(false);
#pragma warning restore IL2026, IL3050
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
