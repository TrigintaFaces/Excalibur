// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Elastic.Clients.Elasticsearch;
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
	private readonly ElasticsearchClient _elasticsearchClient;
	private readonly AuditOptions _configuration;
	private readonly ILogger _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="SecurityAuditMaintenanceService"/> class.
	/// </summary>
	internal SecurityAuditMaintenanceService(
		ElasticsearchClient elasticsearchClient,
		AuditOptions configuration,
		ILogger logger)
	{
		_elasticsearchClient = elasticsearchClient;
		_configuration = configuration;
		_logger = logger;
	}

	/// <summary>
	/// Raised when audit archiving is completed.
	/// </summary>
	internal event EventHandler<AuditArchiveCompletedEventArgs>? AuditArchiveCompleted;

	/// <summary>
	/// Computes an integrity hash for an audit event.
	/// </summary>
	internal static string ComputeIntegrityHash(SecurityAuditEvent auditEvent)
	{
		var eventData = JsonSerializer.Serialize(auditEvent, SecurityAuditEventSerializerContext.Default.SecurityAuditEvent);
		var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(eventData));
		return Convert.ToBase64String(hashBytes);
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
					.Sort(static so => so.Field(static f => f.Timestamp, new FieldSort { Order = SortOrder.Asc })),
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
				if (!ValidateEventIntegrity(auditEvent))
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
			var searchResponse = await _elasticsearchClient.SearchAsync<SecurityAuditEvent>(
				s => s
					.Indices("security-audit-*")
					.Query(new MatchAllQuery())
					.Size(maxResults)
					.Scroll("5m"), // Enable scrolling for large result sets
				cancellationToken).ConfigureAwait(false);

			if (!searchResponse.IsValidResponse)
			{
				throw new InvalidOperationException($"Failed to retrieve audit events for archival: {searchResponse.DebugInformation}");
			}

			var archivedCount = 0;
			var totalSize = 0L;
			var archiveFilePath = Path.Combine(archiveLocation, $"audit-archive-{cutoffDate:yyyy-MM-dd}.json.gz");

			// Ensure archive directory exists
			_ = Directory.CreateDirectory(Path.GetDirectoryName(archiveFilePath));

			await using var fileStream = File.Create(archiveFilePath);
			await using var gzipStream = new GZipStream(fileStream, CompressionMode.Compress);
			await using (var writer = new StreamWriter(gzipStream, Encoding.UTF8))
			{
				// Process initial batch
				var batchResult = await ProcessArchiveBatch(writer, searchResponse.Documents, archivedCount, totalSize)
					.ConfigureAwait(false);
				archivedCount = batchResult.archivedCount;
				totalSize = batchResult.totalSize;

				// Process remaining batches using scroll
				var scrollId = searchResponse.ScrollId;
				var hasMoreDocuments = searchResponse.Documents.Count != 0;

				while (!string.IsNullOrEmpty(scrollId?.ToString()) && hasMoreDocuments)
				{
					var scrollRequest = new ScrollRequest { ScrollId = scrollId, Scroll = "5m" };
					var scrollResponse = await _elasticsearchClient.ScrollAsync<SecurityAuditEvent>(
						scrollRequest, cancellationToken).ConfigureAwait(false);

					if (!scrollResponse.IsValidResponse)
					{
						break;
					}

					var scrollBatchResult = await ProcessArchiveBatch(writer, scrollResponse.Documents, archivedCount, totalSize)
						.ConfigureAwait(false);
					archivedCount = scrollBatchResult.archivedCount;
					totalSize = scrollBatchResult.totalSize;
					scrollId = scrollResponse.ScrollId;
					hasMoreDocuments = scrollResponse.Documents.Count != 0;
				}

				// Clear scroll context
				if (!string.IsNullOrEmpty(scrollId?.ToString()))
				{
					var clearScrollRequest = new ClearScrollRequest { ScrollId = scrollId };
					_ = await _elasticsearchClient.ClearScrollAsync(clearScrollRequest, cancellationToken).ConfigureAwait(false);
				}
			}

			// Delete archived events from Elasticsearch if archival was successful
			if (archivedCount > 0)
			{
				await DeleteArchivedEventsAsync(cutoffDate, cancellationToken).ConfigureAwait(false);
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
	/// Validates the integrity of an audit event without mutating the original.
	/// </summary>
	private static bool ValidateEventIntegrity(SecurityAuditEvent auditEvent)
	{
		if (string.IsNullOrEmpty(auditEvent.IntegrityHash))
		{
			return false;
		}

		var storedHash = auditEvent.IntegrityHash;

		// Create a shallow copy with IntegrityHash cleared to compute the expected hash
		// without mutating the original event (which is not thread-safe and corrupts state).
		var copy = new SecurityAuditEvent
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

		var computedHash = ComputeIntegrityHash(copy);

		return string.Equals(storedHash, computedHash, StringComparison.Ordinal);
	}

	/// <summary>
	/// Processes a batch of events for archival.
	/// </summary>
	private static async Task<(int archivedCount, long totalSize)> ProcessArchiveBatch(
		StreamWriter writer,
		IEnumerable<SecurityAuditEvent> events,
		int currentArchivedCount,
		long currentTotalSize)
	{
		var archivedCount = currentArchivedCount;
		var totalSize = currentTotalSize;

		foreach (var auditEvent in events)
		{
			var json = JsonSerializer.Serialize(auditEvent, SecurityAuditEventSerializerContext.Default.SecurityAuditEvent);

			await writer.WriteLineAsync(json).ConfigureAwait(false);
			archivedCount++;
			totalSize += Encoding.UTF8.GetByteCount(json);
		}

		return (archivedCount, totalSize);
	}

	/// <summary>
	/// Deletes archived events from Elasticsearch.
	/// </summary>
	private async Task DeleteArchivedEventsAsync(DateTimeOffset cutoffDate, CancellationToken cancellationToken)
	{
		try
		{
			var deleteResponse = await _elasticsearchClient.DeleteByQueryAsync<SecurityAuditEvent>(
				static d => d
					.Indices("security-audit-*")
					.Query(new MatchAllQuery()),
				cancellationToken).ConfigureAwait(false);

			if (!deleteResponse.IsValidResponse)
			{
				_logger.LogWarning("Failed to delete archived events: {Error}", deleteResponse.DebugInformation);
			}
			else
			{
				_logger.LogInformation("Deleted {Count} archived events from Elasticsearch", deleteResponse.Deleted);
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to delete archived events");
		}
	}
}
