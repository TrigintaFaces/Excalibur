// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.IO.Compression;
using System.Text.Json;

using Amazon.S3;
using Amazon.S3.Model;

using Excalibur.EventSourcing.Abstractions;

using Microsoft.Extensions.Logging;

namespace Excalibur.EventSourcing.AwsS3;

/// <summary>
/// AWS S3 implementation of <see cref="IColdEventStore"/>.
/// </summary>
/// <remarks>
/// Events are stored as gzip-compressed JSON objects, one object per aggregate.
/// Key pattern: <c>{keyPrefix}/{aggregateId}/events.json.gz</c>.
/// </remarks>
internal sealed class AwsS3ColdEventStore : IColdEventStore
{
	private readonly IAmazonS3 _s3Client;
	private readonly string _bucketName;
	private readonly string _keyPrefix;
	private readonly ILogger<AwsS3ColdEventStore> _logger;
	private readonly JsonSerializerOptions _jsonOptions;

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

		var key = GetObjectKey(aggregateId);

		// Read existing events if object exists, then merge
		var existingEvents = new List<StoredEvent>();
		if (await ObjectExistsAsync(key, cancellationToken).ConfigureAwait(false))
		{
			existingEvents.AddRange(await ReadEventsFromS3Async(key, cancellationToken).ConfigureAwait(false));
		}

		var maxExistingVersion = existingEvents.Count > 0 ? existingEvents[^1].Version : -1;
		var newEvents = events.Where(e => e.Version > maxExistingVersion).ToList();

		if (newEvents.Count == 0)
		{
			_logger.LogDebug("No new events to archive for {AggregateId}; all versions already in cold storage", aggregateId);
			return;
		}

		existingEvents.AddRange(newEvents);
		await WriteEventsToS3Async(key, existingEvents, cancellationToken).ConfigureAwait(false);

		_logger.LogDebug(
			"Archived {NewCount} events for {AggregateId} to S3 (total {TotalCount})",
			newEvents.Count, aggregateId, existingEvents.Count);
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<StoredEvent>> ReadAsync(
		string aggregateId,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(aggregateId);

		var key = GetObjectKey(aggregateId);
		if (!await ObjectExistsAsync(key, cancellationToken).ConfigureAwait(false))
		{
			return Array.Empty<StoredEvent>();
		}

		return await ReadEventsFromS3Async(key, cancellationToken).ConfigureAwait(false);
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
		return await ObjectExistsAsync(GetObjectKey(aggregateId), cancellationToken).ConfigureAwait(false);
	}

	private string GetObjectKey(string aggregateId)
	{
		var safeName = aggregateId.Replace('\\', '_');
		return string.IsNullOrEmpty(_keyPrefix)
			? $"{safeName}/events.json.gz"
			: $"{_keyPrefix}/{safeName}/events.json.gz";
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

		var events = await JsonSerializer.DeserializeAsync<List<StoredEvent>>(
			gzipStream, _jsonOptions, cancellationToken).ConfigureAwait(false);

		return events ?? [];
	}

	private async Task WriteEventsToS3Async(
		string key,
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

		var request = new PutObjectRequest
		{
			BucketName = _bucketName,
			Key = key,
			InputStream = memoryStream,
			ContentType = "application/json",
		};
		request.Headers.ContentEncoding = "gzip";

		await _s3Client.PutObjectAsync(request, cancellationToken).ConfigureAwait(false);
	}
}
