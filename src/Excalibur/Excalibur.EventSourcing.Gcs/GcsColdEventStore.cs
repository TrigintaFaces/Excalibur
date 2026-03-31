// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.IO.Compression;
using System.Text.Json;

using Excalibur.EventSourcing.Abstractions;

using Google.Cloud.Storage.V1;

using Microsoft.Extensions.Logging;

namespace Excalibur.EventSourcing.Gcs;

/// <summary>
/// Google Cloud Storage implementation of <see cref="IColdEventStore"/>.
/// </summary>
/// <remarks>
/// Events are stored as gzip-compressed JSON objects, one object per aggregate.
/// Object naming: <c>{prefix}/{aggregateId}/events.json.gz</c>.
/// </remarks>
internal sealed class GcsColdEventStore : IColdEventStore
{
	private readonly StorageClient _storageClient;
	private readonly string _bucketName;
	private readonly string _objectPrefix;
	private readonly ILogger<GcsColdEventStore> _logger;
	private readonly JsonSerializerOptions _jsonOptions;

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

		var objectName = GetObjectName(aggregateId);

		var existingEvents = new List<StoredEvent>();
		if (await ObjectExistsAsync(objectName, cancellationToken).ConfigureAwait(false))
		{
			existingEvents.AddRange(await ReadEventsFromGcsAsync(objectName, cancellationToken).ConfigureAwait(false));
		}

		var maxExistingVersion = existingEvents.Count > 0 ? existingEvents[^1].Version : -1;
		var newEvents = events.Where(e => e.Version > maxExistingVersion).ToList();

		if (newEvents.Count == 0)
		{
			_logger.LogDebug("No new events to archive for {AggregateId}; all versions already in cold storage", aggregateId);
			return;
		}

		existingEvents.AddRange(newEvents);
		await WriteEventsToGcsAsync(objectName, existingEvents, cancellationToken).ConfigureAwait(false);

		_logger.LogDebug(
			"Archived {NewCount} events for {AggregateId} to GCS (total {TotalCount})",
			newEvents.Count, aggregateId, existingEvents.Count);
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<StoredEvent>> ReadAsync(
		string aggregateId,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(aggregateId);

		var objectName = GetObjectName(aggregateId);
		if (!await ObjectExistsAsync(objectName, cancellationToken).ConfigureAwait(false))
		{
			return Array.Empty<StoredEvent>();
		}

		return await ReadEventsFromGcsAsync(objectName, cancellationToken).ConfigureAwait(false);
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
		return await ObjectExistsAsync(GetObjectName(aggregateId), cancellationToken).ConfigureAwait(false);
	}

	private string GetObjectName(string aggregateId)
	{
		var safeName = aggregateId.Replace('\\', '_');
		return string.IsNullOrEmpty(_objectPrefix)
			? $"{safeName}/events.json.gz"
			: $"{_objectPrefix}/{safeName}/events.json.gz";
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

		var events = await JsonSerializer.DeserializeAsync<List<StoredEvent>>(
			gzipStream, _jsonOptions, cancellationToken).ConfigureAwait(false);

		return events ?? [];
	}

	private async Task WriteEventsToGcsAsync(
		string objectName,
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

		await _storageClient.UploadObjectAsync(
			_bucketName,
			objectName,
			"application/json",
			memoryStream,
			cancellationToken: cancellationToken).ConfigureAwait(false);
	}
}
