// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.IO.Compression;
using System.Text.Json;

using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

using Excalibur.EventSourcing.Abstractions;

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

		// Read existing events if blob exists, then merge
		var existingEvents = new List<StoredEvent>();
		if (await BlobExistsAsync(blobClient, cancellationToken).ConfigureAwait(false))
		{
			existingEvents.AddRange(await ReadEventsFromBlobAsync(blobClient, cancellationToken).ConfigureAwait(false));
		}

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

		// Write merged events as compressed JSON
		await WriteEventsToBlobAsync(blobClient, existingEvents, cancellationToken).ConfigureAwait(false);

		_logger.LogDebug(
			"Archived {NewCount} events for {AggregateId} to blob (total {TotalCount})",
			newEvents.Count, aggregateId, existingEvents.Count);
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

		var events = await JsonSerializer.DeserializeAsync<List<StoredEvent>>(
			gzipStream, _jsonOptions, cancellationToken).ConfigureAwait(false);

		return events ?? [];
	}

	private async Task WriteEventsToBlobAsync(
		BlobClient blobClient,
		List<StoredEvent> events,
		CancellationToken cancellationToken)
	{
		using var memoryStream = new MemoryStream();
		{
			await using var gzipStream = new GZipStream(memoryStream, CompressionLevel.Optimal, leaveOpen: true);
			await JsonSerializer.SerializeAsync(gzipStream, events, _jsonOptions, cancellationToken)
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
			},
			cancellationToken).ConfigureAwait(false);
	}
}
