// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Buffers;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Search;
using Elastic.Clients.Elasticsearch.QueryDsl;

using Microsoft.Extensions.Logging;

using Excalibur.AuditLogging;

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
	private readonly IAuditIntegrityStrategy _integrityStrategy;
	private readonly ILogger _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="SecurityAuditMaintenanceService"/> class.
	/// </summary>
	internal SecurityAuditMaintenanceService(
		ElasticsearchClient elasticsearchClient,
		AuditOptions configuration,
		IAuditIntegrityStrategy integrityStrategy,
		ILogger logger)
	{
		_elasticsearchClient = elasticsearchClient;
		_configuration = configuration;
		_integrityStrategy = integrityStrategy;
		_logger = logger;
	}

	/// <summary>
	/// Raised when audit archiving is completed.
	/// </summary>
	internal event EventHandler<AuditArchiveCompletedEventArgs>? AuditArchiveCompleted;

	/// <summary>
	/// Computes the keyed-MAC integrity tag for an audit event via the shared
	/// <see cref="IAuditIntegrityStrategy"/>, returning a self-describing <c>v1:{keyId}:{base64(tag)}</c>
	/// token. The Elasticsearch audit stream is a concurrent, best-effort sink (overlapping writers,
	/// re-queue-on-failure), so each record is tagged as a <b>genesis-null per-event keyed MAC</b>
	/// (<c>priorTag: null</c>) — no write-time hash-chain, which would false-fail on the substrate's
	/// legitimate reorder/re-queue (SA ruling 17568; ordering-detection carved to <c>nkz47q</c>).
	/// </summary>
	/// <remarks>
	/// Fails closed: the strategy throws if the signing key cannot be obtained, rather than emitting an
	/// unprotected tag. There is no unkeyed code path.
	/// </remarks>
	internal ValueTask<string> ComputeIntegrityHashAsync(
		SecurityAuditEvent auditEvent,
		CancellationToken cancellationToken) =>
		_integrityStrategy.ComputeTagAsync(Canonicalize(auditEvent), priorTag: null, cancellationToken);

	/// <summary>
	/// Produces the deterministic, injective canonical byte representation of an audit event's
	/// integrity-covered fields, for use as the input to <see cref="IAuditIntegrityStrategy"/>. The
	/// <see cref="SecurityAuditEvent.IntegrityHash"/> field is excluded (it is the output, not an input), so
	/// the write and verify paths canonicalize identical bytes.
	/// </summary>
	/// <remarks>
	/// Fields are emitted in a fixed, stable order and each rendered culture-invariantly; the
	/// <see cref="SecurityAuditEvent.Details"/> entries are ordered by key (ordinal) and preceded by their
	/// count so a distinct event can never collide to the same bytes. The shared
	/// <see cref="AuditRecordCanonicalizer"/> length-prefixes and version-stamps the result.
	/// </remarks>
	/// <param name="auditEvent">The event whose integrity-covered fields are canonicalized.</param>
	/// <returns>The canonical bytes (version-prefixed) for keyed-MAC computation/verification.</returns>
	internal static ReadOnlyMemory<byte> Canonicalize(SecurityAuditEvent auditEvent)
	{
		ArgumentNullException.ThrowIfNull(auditEvent);

		// Canonicalize over the DURABLE projection — what Elasticsearch actually stores and returns — so the
		// write-time bytes equal the reload-time bytes BY CONSTRUCTION (enforce-invariants-structurally), not
		// by luck (SA ruling 17652). Two fields are otherwise round-trip-unstable across the ES JSON boundary:
		//   • Timestamp: signed as Unix MILLISECONDS — ES `date` stores ms precision, so signing the full-tick
		//     in-memory value mismatches every reloaded record. Unix-ms is the absolute instant, so it is also
		//     immune to offset normalization on reload.
		//   • Details: each value normalized to a stable JSON normal form identical whether it is a native CLR
		//     value (write) or a JsonElement (reload) — never Convert.ToString on a runtime-typed box.
		var fields = new List<string?>(9 + (auditEvent.Details.Count * 2))
		{
			auditEvent.EventId,
			auditEvent.Timestamp.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture),
			((int)auditEvent.EventType).ToString(CultureInfo.InvariantCulture),
			((int)auditEvent.Severity).ToString(CultureInfo.InvariantCulture),
			auditEvent.Source,
			auditEvent.UserId,
			auditEvent.SourceIpAddress,
			auditEvent.UserAgent,
			auditEvent.Details.Count.ToString(CultureInfo.InvariantCulture),
		};

		foreach (var entry in auditEvent.Details.OrderBy(static e => e.Key, StringComparer.Ordinal))
		{
			fields.Add(entry.Key);
			fields.Add(NormalizeDetailValue(entry.Value));
		}

		return AuditRecordCanonicalizer.Canonicalize([.. fields]);
	}

	/// <summary>
	/// Renders a <see cref="SecurityAuditEvent.Details"/> value to a stable JSON normal form that is identical
	/// whether the value is a native CLR type (at write/sign time) or a <see cref="JsonElement"/> (after the
	/// Elasticsearch JSON round-trip at verify time), so the canonical bytes match by construction — including
	/// <b>nested objects/arrays</b> (e.g. an auth event's <c>Context</c> dictionary). Object keys are emitted in
	/// ordinal-sorted order on <em>both</em> sides (never <see cref="JsonElement.GetRawText"/>, which preserves
	/// the stored key order and would diverge from the sorted CLR emission). AOT-safe: explicit
	/// <see cref="Utf8JsonWriter"/> writes, never reflection-based serialization.
	/// </summary>
	private static string NormalizeDetailValue(object? value)
	{
		var buffer = new ArrayBufferWriter<byte>();
		using (var writer = new Utf8JsonWriter(buffer))
		{
			WriteCanonicalValue(writer, value);
		}

		return Encoding.UTF8.GetString(buffer.WrittenSpan);
	}

	// Recursively emits a native CLR Details value as canonical JSON (ordinal-sorted object keys). The
	// reload-path twin is WriteCanonicalElement; together they guarantee write-bytes == reload-bytes.
	private static void WriteCanonicalValue(Utf8JsonWriter writer, object? value)
	{
		switch (value)
		{
			case null: writer.WriteNullValue(); break;
			case JsonElement element: WriteCanonicalElement(writer, element); break;
			case string s: writer.WriteStringValue(s); break;
			case bool b: writer.WriteBooleanValue(b); break;
			case int i: writer.WriteNumberValue(i); break;
			case long l: writer.WriteNumberValue(l); break;
			case double d: writer.WriteNumberValue(d); break;
			case decimal m: writer.WriteNumberValue(m); break;
			case float f: writer.WriteNumberValue(f); break;
			case DateTimeOffset dto: writer.WriteStringValue(dto); break;
			case DateTime dt: writer.WriteStringValue(dt); break;
			case Guid g: writer.WriteStringValue(g); break;
			case IDictionary<string, object> dict:
				writer.WriteStartObject();
				foreach (var entry in dict.OrderBy(static e => e.Key, StringComparer.Ordinal))
				{
					writer.WritePropertyName(entry.Key);
					WriteCanonicalValue(writer, entry.Value);
				}

				writer.WriteEndObject();
				break;
			case System.Collections.IEnumerable sequence:
				writer.WriteStartArray();
				foreach (var item in sequence)
				{
					WriteCanonicalValue(writer, item);
				}

				writer.WriteEndArray();
				break;
			default: writer.WriteStringValue(value.ToString()); break;
		}
	}

	// Recursively emits a reloaded JsonElement as canonical JSON (ordinal-sorted object keys), mirroring
	// WriteCanonicalValue so a value canonicalizes identically as a CLR type (write) or JsonElement (read).
	private static void WriteCanonicalElement(Utf8JsonWriter writer, JsonElement element)
	{
		switch (element.ValueKind)
		{
			case JsonValueKind.Object:
				writer.WriteStartObject();
				foreach (var property in element.EnumerateObject().OrderBy(static p => p.Name, StringComparer.Ordinal))
				{
					writer.WritePropertyName(property.Name);
					WriteCanonicalElement(writer, property.Value);
				}

				writer.WriteEndObject();
				break;
			case JsonValueKind.Array:
				writer.WriteStartArray();
				foreach (var item in element.EnumerateArray())
				{
					WriteCanonicalElement(writer, item);
				}

				writer.WriteEndArray();
				break;
			default:
				element.WriteTo(writer); // string / number / true / false / null — canonical scalar
				break;
		}
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
	/// Validates the keyed-MAC integrity of an audit event without mutating the original, via the shared
	/// <see cref="IAuditIntegrityStrategy"/> (genesis-null per-event MAC, matching the write path). Fails
	/// closed: a missing/malformed tag, an unknown/unavailable key, or a MAC mismatch all report the record
	/// as a violation (<see langword="false"/>) rather than as valid.
	/// </summary>
	private ValueTask<bool> ValidateEventIntegrityAsync(
		SecurityAuditEvent auditEvent,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(auditEvent.IntegrityHash))
		{
			return ValueTask.FromResult(false);
		}

		return _integrityStrategy.VerifyAsync(
			Canonicalize(auditEvent), priorTag: null, auditEvent.IntegrityHash, cancellationToken);
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
