// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Search;
using Elastic.Clients.Elasticsearch.QueryDsl;

using Microsoft.Extensions.Logging;

namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Handles audit log integrity validation, event archival, and deletion of archived events.
/// </summary>
/// <remarks>
/// <para>
/// Extracted from <see cref="SecurityAuditor"/> following SRP. Contains all
/// maintenance operations: integrity hash validation, archive-to-gzip, scroll-based
/// batch processing, and deletion of archived events.
/// </para>
/// </remarks>
internal sealed class SecurityAuditMaintenanceService
{
	// Self-describing keyed-MAC integrity token: "v1:{keyId}:{base64(HMAC-SHA256)}". The keyId travels
	// with the tag so records remain verifiable across key rotation; the "v1" prefix versions the scheme.
	private const string IntegrityTagVersion = "v1";

	private readonly ElasticsearchClient _elasticsearchClient;
	private readonly AuditOptions _configuration;
	private readonly IAuditSigningKeyProvider _signingKeyProvider;
	private readonly ILogger _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="SecurityAuditMaintenanceService"/> class.
	/// </summary>
	internal SecurityAuditMaintenanceService(
		ElasticsearchClient elasticsearchClient,
		AuditOptions configuration,
		IAuditSigningKeyProvider signingKeyProvider,
		ILogger logger)
	{
		_elasticsearchClient = elasticsearchClient;
		_configuration = configuration;
		_signingKeyProvider = signingKeyProvider;
		_logger = logger;
	}

	/// <summary>
	/// Raised when audit archiving is completed.
	/// </summary>
	internal event EventHandler<AuditArchiveCompletedEventArgs>? AuditArchiveCompleted;

	/// <summary>
	/// Computes a keyed-MAC (HMAC-SHA256) integrity tag for an audit event, returning a self-describing
	/// <c>v1:{keyId}:{base64(tag)}</c> token. Because the key is secret and held outside the audit index,
	/// an actor with write access to the records cannot forge a matching tag (unlike an unkeyed hash).
	/// </summary>
	/// <remarks>
	/// Fails closed: if the signing key cannot be obtained the operation throws rather than emitting an
	/// unprotected tag.
	/// </remarks>
	internal async ValueTask<string> ComputeIntegrityHashAsync(
		SecurityAuditEvent auditEvent,
		CancellationToken cancellationToken)
	{
		var (keyId, key) = await _signingKeyProvider.GetCurrentSigningKeyAsync(cancellationToken).ConfigureAwait(false);
		var tag = ComputeMac(key, auditEvent);
		return $"{IntegrityTagVersion}:{keyId}:{Convert.ToBase64String(tag)}";
	}

	/// <summary>
	/// Computes the raw HMAC-SHA256 tag over the canonical (integrity-tag-cleared) serialization of the
	/// event, so the write and verify paths hash identical bytes regardless of the incoming tag value.
	/// </summary>
	private static byte[] ComputeMac(byte[] key, SecurityAuditEvent auditEvent)
	{
		// Serialize a copy with the integrity tag cleared so write/verify are symmetric.
		var canonical = new SecurityAuditEvent
		{
			EventId = auditEvent.EventId,
			Timestamp = auditEvent.Timestamp,
			EventType = auditEvent.EventType,
			Severity = auditEvent.Severity,
			Source = auditEvent.Source,
			UserId = auditEvent.UserId,
			SourceIpAddress = auditEvent.SourceIpAddress,
			UserAgent = auditEvent.UserAgent,
			Details = auditEvent.Details,
			IntegrityHash = string.Empty,
		};

		var eventData = JsonSerializer.Serialize(canonical, SecurityAuditEventSerializerContext.Default.SecurityAuditEvent);
		return HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(eventData));
	}

	/// <summary>
	/// Validates the integrity of audit logs to detect tampering or corruption.
	/// </summary>
	internal async Task<AuditLogIntegrityResult> ValidateAuditIntegrityAsync(
		DateTimeOffset startTime,
		DateTimeOffset endTime,
		CancellationToken cancellationToken)
	{
		if (!_configuration.EnsureLogIntegrity)
		{
			return new AuditLogIntegrityResult(isValid: true, 0, 0, "Audit log integrity checking is disabled");
		}

		try
		{
			_logger.LogInformation("Validating audit log integrity from {StartTime} to {EndTime}", startTime, endTime);

			var maxResults = _configuration.MaxQueryResultSize;
			var searchResponse = await _elasticsearchClient.SearchAsync<SecurityAuditEvent>(
				s => s
					.Indices("security-audit-*")
					.Query(new MatchAllQuery())
					.Size(maxResults)
					.Sort(static so => so.Field(static f => f.Field("timestamp").Order(SortOrder.Asc))),
				cancellationToken).ConfigureAwait(false);

			if (!searchResponse.IsValidResponse)
			{
				throw new InvalidOperationException($"Failed to retrieve audit logs: {searchResponse.DebugInformation}");
			}

			var events = searchResponse.Documents.ToList();
			var totalEvents = events.Count;
			var corruptedEvents = 0;

			// Validate each event's integrity
			foreach (var auditEvent in events)
			{
				if (!await ValidateEventIntegrityAsync(auditEvent, cancellationToken).ConfigureAwait(false))
				{
					corruptedEvents++;
					_logger.LogWarning("Audit log integrity violation detected for event {EventId}", auditEvent.EventId);
				}
			}

			var isValid = corruptedEvents == 0;
			var message = isValid
				? "All audit logs passed integrity validation"
				: $"{corruptedEvents} corrupted events detected out of {totalEvents} total events";

			_logger.LogInformation("Audit log integrity validation completed: {IsValid} ({Message})", isValid, message);

			return new AuditLogIntegrityResult(isValid, totalEvents, corruptedEvents, message);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to validate audit integrity");
			throw;
		}
	}

	/// <summary>
	/// Validates the integrity of audit logs from a request object.
	/// </summary>
	internal async Task<AuditIntegrityResult> ValidateAuditIntegrityAsync(
		AuditIntegrityRequest validationRequest,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(validationRequest);

		try
		{
			var validationId = Guid.NewGuid();
			var startTime = DateTimeOffset.UtcNow;

			var result = await ValidateAuditIntegrityAsync(
				validationRequest.StartTime,
				validationRequest.EndTime,
				cancellationToken).ConfigureAwait(false);

			var executionTime = (long)(DateTimeOffset.UtcNow - startTime).TotalMilliseconds;

			return new AuditIntegrityResult(
				validationId,
				result.IsValid,
				result.TotalEvents,
				result.CorruptedEvents)
			{ ExecutionTimeMs = executionTime, Message = result.Message };
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to validate audit integrity");
			throw;
		}
	}

	/// <summary>
	/// Archives old audit events according to retention policies and compliance requirements.
	/// </summary>
	internal async Task<AuditArchiveResult> ArchiveAuditEventsAsync(
		DateTimeOffset cutoffDate,
		string archiveLocation,
		CancellationToken cancellationToken)
	{
		try
		{
			_logger.LogInformation(
				"Starting audit event archival for events before {CutoffDate} to {ArchiveLocation}",
				cutoffDate, archiveLocation);

			var maxResults = _configuration.MaxQueryResultSize;

			// Date-bound the selection: only events at or before the cutoff are candidates for
			// archival. Previously this used MatchAllQuery, which scrolled the ENTIRE index into
			// the archive regardless of age.
			var cutoff = DateMath.Anchored(cutoffDate.UtcDateTime);
			var searchResponse = await _elasticsearchClient.SearchAsync<SecurityAuditEvent>(
				s => s
					.Indices("security-audit-*")
					.Query(new DateRangeQuery("timestamp") { Lte = cutoff })
					.Size(maxResults)
					.Scroll("5m"), // Enable scrolling for large result sets
				cancellationToken).ConfigureAwait(false);

			if (!searchResponse.IsValidResponse)
			{
				throw new InvalidOperationException($"Failed to retrieve audit events for archival: {searchResponse.DebugInformation}");
			}

			var archivedCount = 0;
			var totalSize = 0L;

			// Collect the Elasticsearch document IDs actually written to the archive so deletion
			// targets ONLY those documents (never an unbounded delete), and only after the gzip
			// stream has been fully flushed and closed.
			var archivedIds = new List<string>();
			var archiveFilePath = Path.Combine(archiveLocation, $"audit-archive-{cutoffDate:yyyy-MM-dd}.json.gz");

			// Ensure archive directory exists
			_ = Directory.CreateDirectory(Path.GetDirectoryName(archiveFilePath)!);

			// Scope the file/gzip/writer streams to this block so they are flushed and closed
			// in order (writer -> gzip footer -> file -> disk) BEFORE any deletion runs. If a
			// write throws, the catch below rethrows and no deletion is attempted (AC-5).
			await using (var fileStream = File.Create(archiveFilePath))
			await using (var gzipStream = new GZipStream(fileStream, CompressionMode.Compress))
			await using (var writer = new StreamWriter(gzipStream, Encoding.UTF8))
			{
				// Process initial batch
				var batchResult = await ProcessArchiveBatch(writer, searchResponse.Hits, archivedIds, archivedCount, totalSize)
					.ConfigureAwait(false);
				archivedCount = batchResult.archivedCount;
				totalSize = batchResult.totalSize;

				// Process remaining batches using scroll
				var scrollId = searchResponse.ScrollId;
				var hasMoreDocuments = searchResponse.Hits.Count != 0;

				while (!string.IsNullOrEmpty(scrollId?.ToString()) && hasMoreDocuments)
				{
					var scrollRequest = new ScrollRequest { ScrollId = scrollId, Scroll = "5m" };
					var scrollResponse = await _elasticsearchClient.ScrollAsync<SecurityAuditEvent>(
						scrollRequest, cancellationToken).ConfigureAwait(false);

					if (!scrollResponse.IsValidResponse)
					{
						break;
					}

					var scrollBatchResult = await ProcessArchiveBatch(writer, scrollResponse.Hits, archivedIds, archivedCount, totalSize)
						.ConfigureAwait(false);
					archivedCount = scrollBatchResult.archivedCount;
					totalSize = scrollBatchResult.totalSize;
					scrollId = scrollResponse.ScrollId;
					hasMoreDocuments = scrollResponse.Hits.Count != 0;
				}

				// Clear scroll context
				if (!string.IsNullOrEmpty(scrollId?.ToString()))
				{
					var clearScrollRequest = new ClearScrollRequest { ScrollId = scrollId };
					_ = await _elasticsearchClient.ClearScrollAsync(clearScrollRequest, cancellationToken).ConfigureAwait(false);
				}

				// Flush buffered data through the writer before the streams dispose at the end
				// of this block.
				await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
			}

			// Delete archived events from Elasticsearch only after the archive has been fully
			// flushed and closed, and only the specific IDs that were archived (AC-4/AC-5).
			if (archivedIds.Count > 0)
			{
				await DeleteArchivedEventsByIdsAsync(archivedIds, cancellationToken).ConfigureAwait(false);
			}

			var result = new AuditArchiveResult
			{
				ArchiveId = Guid.NewGuid(),
				ArchivedAt = DateTimeOffset.UtcNow,
				CutoffDate = cutoffDate,
				EventsArchived = archivedCount,
				ArchiveSize = totalSize,
				ArchiveLocation = archiveFilePath,
				Success = true,
			};

			// Raise archive completed event
			AuditArchiveCompleted?.Invoke(this, new AuditArchiveCompletedEventArgs(
				archivedCount, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

			_logger.LogInformation(
				"Audit event archival completed: {EventCount} events archived to {FilePath}",
				archivedCount, archiveFilePath);

			return result;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to archive audit events");
			throw;
		}
	}

	/// <summary>
	/// Archives audit events from a request object.
	/// </summary>
	internal async Task<AuditArchiveResult> ArchiveAuditEventsAsync(
		AuditArchiveRequest archiveRequest,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(archiveRequest);

		try
		{
			return await ArchiveAuditEventsAsync(
				archiveRequest.CutoffDate,
				archiveRequest.ArchiveLocation,
				cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to archive audit events");
			throw;
		}
	}

	/// <summary>
	/// Validates the keyed-MAC integrity of an audit event without mutating the original. Fails closed:
	/// a missing/malformed tag, an unknown/unavailable key, or a MAC mismatch all report the record as a
	/// violation rather than as valid.
	/// </summary>
	private async ValueTask<bool> ValidateEventIntegrityAsync(
		SecurityAuditEvent auditEvent,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(auditEvent.IntegrityHash))
		{
			return false;
		}

		// Parse the self-describing token "v1:{keyId}:{base64(tag)}" (keyId is colon-free by contract).
		var parts = auditEvent.IntegrityHash.Split(':', 3);
		if (parts.Length != 3 || !string.Equals(parts[0], IntegrityTagVersion, StringComparison.Ordinal))
		{
			return false;
		}

		var keyId = parts[1];

		byte[] storedTag;
		try
		{
			storedTag = Convert.FromBase64String(parts[2]);
		}
		catch (FormatException)
		{
			return false;
		}

		// Fail closed: an unknown/unavailable key means the record cannot be verified -> NOT valid.
		var key = await _signingKeyProvider.GetSigningKeyAsync(keyId, cancellationToken).ConfigureAwait(false);
		if (key is null)
		{
			_logger.LogWarning(
				"Audit integrity key '{KeyId}' is unavailable; treating event {EventId} as unverifiable",
				keyId, auditEvent.EventId);
			return false;
		}

		var computedTag = ComputeMac(key, auditEvent);

		// Constant-time comparison to avoid a timing oracle on the MAC.
		return CryptographicOperations.FixedTimeEquals(computedTag, storedTag);
	}

	/// <summary>
	/// Processes a batch of events for archival.
	/// </summary>
	private static async Task<(int archivedCount, long totalSize)> ProcessArchiveBatch(
		StreamWriter writer,
		IReadOnlyCollection<Hit<SecurityAuditEvent>> hits,
		List<string> archivedIds,
		int currentArchivedCount,
		long currentTotalSize)
	{
		var archivedCount = currentArchivedCount;
		var totalSize = currentTotalSize;

		foreach (var hit in hits)
		{
			if (hit.Source is null)
			{
				continue;
			}

			var json = JsonSerializer.Serialize(hit.Source, SecurityAuditEventSerializerContext.Default.SecurityAuditEvent);

			await writer.WriteLineAsync(json).ConfigureAwait(false);

			// Record the document _id so only archived documents are deleted afterwards.
			if (!string.IsNullOrEmpty(hit.Id))
			{
				archivedIds.Add(hit.Id);
			}

			archivedCount++;
			totalSize += Encoding.UTF8.GetByteCount(json);
		}

		return (archivedCount, totalSize);
	}

	/// <summary>
	/// The fallback page size for the paginated delete when the configured query size is non-positive.
	/// </summary>
	private const int DefaultDeleteBatchSize = 1000;

	/// <summary>
	/// Deletes the specific archived events from Elasticsearch by their document IDs, in bounded
	/// pages.
	/// </summary>
	/// <remarks>
	/// Two independent safety bounds apply. First, only the IDs confirmed written to the (flushed and
	/// closed) archive are deleted — this replaces the previous unbounded <c>DeleteByQuery(MatchAll)</c>,
	/// which destroyed the entire audit index. Second, the archived IDs are paginated into chunks of the
	/// configured batch size and each <c>DeleteByQuery</c> is capped with <c>MaxDocs = batchSize</c>, so a
	/// large archival run never issues a single mass-delete (resource-safety / no one unbounded delete).
	/// </remarks>
	private async Task DeleteArchivedEventsByIdsAsync(
		IReadOnlyCollection<string> archivedIds,
		CancellationToken cancellationToken)
	{
		var batchSize = _configuration.MaxQueryResultSize > 0
			? _configuration.MaxQueryResultSize
			: DefaultDeleteBatchSize;

		var totalDeleted = 0L;

		foreach (var chunk in archivedIds.Chunk(batchSize))
		{
			try
			{
				var ids = new Ids(chunk.Select(static id => (Id)id).ToList());
				var deleteResponse = await _elasticsearchClient.DeleteByQueryAsync<SecurityAuditEvent>(
					d => d
						.Indices("security-audit-*")
						.Query(q => q.Ids(i => i.Values(ids)))
						.MaxDocs(chunk.Length),
					cancellationToken).ConfigureAwait(false);

				if (!deleteResponse.IsValidResponse)
				{
					_logger.LogWarning("Failed to delete archived events page: {Error}", deleteResponse.DebugInformation);
				}
				else
				{
					totalDeleted += deleteResponse.Deleted ?? 0;
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to delete archived events page of {Count}", chunk.Length);
			}
		}

		_logger.LogInformation(
			"Deleted {Count} archived events from Elasticsearch in pages of {BatchSize}", totalDeleted, batchSize);
	}
}
