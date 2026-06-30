// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.IO.Compression;
using System.Net;
using System.Text.Json;

using Amazon.S3;
using Amazon.S3.Model;

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
	private const int MaxConcurrencyRetries = 5;

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

		// Optimistic-concurrency read-modify-write: a concurrent archive must not silently overwrite (lost
		// update). We capture the source object's ETag on read and write conditionally (IfMatch for an
		// update, IfNoneMatch=* for a create); a precondition failure means another writer raced us, so we
		// re-read and retry against the now-current object.
		for (var attempt = 0; ; attempt++)
		{
			var (existingEvents, etag) = await TryDownloadForUpdateAsync(key, cancellationToken)
				.ConfigureAwait(false);

			var maxExistingVersion = existingEvents.Count > 0 ? existingEvents[^1].Version : -1;
			var newEvents = events.Where(e => e.Version > maxExistingVersion).ToList();

			if (newEvents.Count == 0)
			{
				_logger.LogDebug("No new events to archive for {AggregateId}; all versions already in cold storage", aggregateId);
				return;
			}

			existingEvents.AddRange(newEvents);

			try
			{
				await WriteEventsToS3Async(key, existingEvents, etag, cancellationToken).ConfigureAwait(false);

				_logger.LogDebug(
					"Archived {NewCount} events for {AggregateId} to S3 (total {TotalCount})",
					newEvents.Count, aggregateId, existingEvents.Count);
				return;
			}
			catch (AmazonS3Exception ex) when (
				(ex.StatusCode == HttpStatusCode.PreconditionFailed || ex.StatusCode == HttpStatusCode.Conflict)
				&& attempt < MaxConcurrencyRetries)
			{
				// Another writer committed between our read and write — re-read and retry.
				_logger.LogDebug(
					"Concurrent archive detected for {AggregateId} (status {Status}); retrying (attempt {Attempt})",
					aggregateId, ex.StatusCode, attempt + 1);
			}
		}
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

#pragma warning disable IL2026, IL3050 // Serialization inherently uses reflection
		var events = await JsonSerializer.DeserializeAsync<List<StoredEvent>>(
			gzipStream, _jsonOptions, cancellationToken).ConfigureAwait(false);
#pragma warning restore IL2026, IL3050

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

#pragma warning disable IL2026, IL3050 // Serialization inherently uses reflection
			var events = await JsonSerializer.DeserializeAsync<List<StoredEvent>>(
				gzipStream, _jsonOptions, cancellationToken).ConfigureAwait(false);
#pragma warning restore IL2026, IL3050

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
#pragma warning disable IL2026, IL3050 // Serialization inherently uses reflection
			await JsonSerializer.SerializeAsync(gzipStream, events, _jsonOptions, cancellationToken)
				.ConfigureAwait(false);
#pragma warning restore IL2026, IL3050
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
