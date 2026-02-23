// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using System.Diagnostics;

using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.AuditLogging.Elasticsearch;

/// <summary>
/// Exports audit events to Elasticsearch via the Bulk API.
/// </summary>
/// <remarks>
/// <para>
/// This exporter uses the Elasticsearch Bulk API to efficiently index audit events.
/// Documents are indexed with time-based index names for easy lifecycle management.
/// </para>
/// <para>
/// Supports optional API key authentication and configurable refresh policies.
/// </para>
/// </remarks>
public sealed partial class ElasticsearchAuditExporter : IAuditLogExporter
{
	private readonly HttpClient _httpClient;
	private readonly ElasticsearchExporterOptions _options;
	private readonly ILogger<ElasticsearchAuditExporter> _logger;
	private readonly Uri _bulkApiUri;

	/// <summary>
	/// Initializes a new instance of the <see cref="ElasticsearchAuditExporter"/> class.
	/// </summary>
	/// <param name="httpClient">The HTTP client for making requests.</param>
	/// <param name="options">The Elasticsearch exporter options.</param>
	/// <param name="logger">The logger.</param>
	public ElasticsearchAuditExporter(
		HttpClient httpClient,
		IOptions<ElasticsearchExporterOptions> options,
		ILogger<ElasticsearchAuditExporter> logger)
	{
		ArgumentNullException.ThrowIfNull(httpClient);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_httpClient = httpClient;
		_options = options.Value;
		_logger = logger;

		var baseUrl = _options.ElasticsearchUrl.TrimEnd('/');
		_bulkApiUri = new Uri($"{baseUrl}/_bulk?refresh={_options.RefreshPolicy}");
	}

	/// <inheritdoc />
	public string Name => "Elasticsearch";

	/// <inheritdoc />
	public async Task<AuditExportResult> ExportAsync(
		AuditEvent auditEvent,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(auditEvent);

		try
		{
			var ndjson = BuildBulkPayload([auditEvent]);
			var response = await SendWithRetryAsync(ndjson, cancellationToken).ConfigureAwait(false);

			if (response.IsSuccessStatusCode)
			{
				LogAuditEventExported(auditEvent.EventId);

				return new AuditExportResult { Success = true, EventId = auditEvent.EventId, ExportedAt = DateTimeOffset.UtcNow };
			}

			var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
			var isTransient = IsTransientStatusCode(response.StatusCode);

			LogAuditExportFailed(auditEvent.EventId, response.StatusCode, errorBody);

			return new AuditExportResult
			{
				Success = false,
				EventId = auditEvent.EventId,
				ExportedAt = DateTimeOffset.UtcNow,
				ErrorMessage = $"HTTP {(int)response.StatusCode}: {errorBody}",
				IsTransientError = isTransient
			};
		}
		catch (HttpRequestException ex)
		{
			LogAuditExportHttpError(ex, auditEvent.EventId);

			return new AuditExportResult
			{
				Success = false,
				EventId = auditEvent.EventId,
				ExportedAt = DateTimeOffset.UtcNow,
				ErrorMessage = ex.Message,
				IsTransientError = true
			};
		}
		catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
		{
			LogAuditExportTimeout(ex, auditEvent.EventId);

			return new AuditExportResult
			{
				Success = false,
				EventId = auditEvent.EventId,
				ExportedAt = DateTimeOffset.UtcNow,
				ErrorMessage = "Request timed out",
				IsTransientError = true
			};
		}
	}

	/// <inheritdoc />
	public async Task<AuditExportBatchResult> ExportBatchAsync(
		IReadOnlyList<AuditEvent> auditEvents,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(auditEvents);

		if (auditEvents.Count == 0)
		{
			return new AuditExportBatchResult { TotalCount = 0, SuccessCount = 0, FailedCount = 0, ExportedAt = DateTimeOffset.UtcNow };
		}

		var failedEventIds = new List<string>();
		var errors = new Dictionary<string, string>();
		var successCount = 0;

		// Process in chunks if batch is larger than max batch size
		var chunks = auditEvents
			.Select((e, i) => new { Event = e, Index = i })
			.GroupBy(x => x.Index / _options.BulkBatchSize)
			.Select(g => g.Select(x => x.Event).ToList())
			.ToList();

		foreach (var chunk in chunks)
		{
			try
			{
				var result = await ExportChunkAsync(chunk, cancellationToken).ConfigureAwait(false);

				if (result.success)
				{
					successCount += chunk.Count;
				}
				else
				{
					foreach (var evt in chunk)
					{
						failedEventIds.Add(evt.EventId);
						errors[evt.EventId] = result.errorMessage ?? "Unknown error";
					}
				}
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				LogAuditExportBatchChunkError(ex);

				foreach (var evt in chunk)
				{
					failedEventIds.Add(evt.EventId);
					errors[evt.EventId] = ex.Message;
				}
			}
		}

		LogAuditExportBatchSummary(auditEvents.Count, successCount, failedEventIds.Count);

		return new AuditExportBatchResult
		{
			TotalCount = auditEvents.Count,
			SuccessCount = successCount,
			FailedCount = failedEventIds.Count,
			ExportedAt = DateTimeOffset.UtcNow,
			FailedEventIds = failedEventIds.Count > 0 ? failedEventIds : null,
			Errors = errors.Count > 0 ? errors : null
		};
	}

	/// <inheritdoc />
	public async Task<AuditExporterHealthResult> CheckHealthAsync(CancellationToken cancellationToken)
	{
		var startTimestamp = Stopwatch.GetTimestamp();

		try
		{
			var healthUri = new Uri($"{_options.ElasticsearchUrl.TrimEnd('/')}/_cluster/health");
			using var request = new HttpRequestMessage(HttpMethod.Get, healthUri);
			ApplyAuthentication(request);

			var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

			var isHealthy = response.IsSuccessStatusCode;
			var elapsedMs = (long)Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;

			return new AuditExporterHealthResult
			{
				IsHealthy = isHealthy,
				ExporterName = Name,
				Endpoint = _options.ElasticsearchUrl,
				LatencyMs = elapsedMs,
				CheckedAt = DateTimeOffset.UtcNow,
				ErrorMessage = isHealthy ? null : $"Unexpected status code: {response.StatusCode}",
				Diagnostics = new Dictionary<string, string>
				{
					["StatusCode"] = ((int)response.StatusCode).ToString(),
					["IndexPrefix"] = _options.IndexPrefix,
					["RefreshPolicy"] = _options.RefreshPolicy
				}
			};
		}
		catch (Exception ex)
		{
			LogHealthCheckFailed(ex);
			var elapsedMs = (long)Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;

			return new AuditExporterHealthResult
			{
				IsHealthy = false,
				ExporterName = Name,
				Endpoint = _options.ElasticsearchUrl,
				LatencyMs = elapsedMs,
				CheckedAt = DateTimeOffset.UtcNow,
				ErrorMessage = ex.Message
			};
		}
	}

	private static bool IsTransientStatusCode(HttpStatusCode statusCode) =>
		statusCode is HttpStatusCode.RequestTimeout
			or HttpStatusCode.TooManyRequests
			or HttpStatusCode.InternalServerError
			or HttpStatusCode.BadGateway
			or HttpStatusCode.ServiceUnavailable
			or HttpStatusCode.GatewayTimeout;

	private string BuildBulkPayload(IReadOnlyList<AuditEvent> events)
	{
		var sb = new StringBuilder();

		foreach (var auditEvent in events)
		{
			var indexName = $"{_options.IndexPrefix}-{auditEvent.Timestamp:yyyy.MM.dd}";
			var action = new BulkIndexAction
			{
				Index = new BulkActionMeta { IndexName = indexName, Id = auditEvent.EventId }
			};
			_ = sb.Append(JsonSerializer.Serialize(action, ElasticsearchAuditJsonContext.Default.BulkIndexAction));
			_ = sb.Append('\n');

			var payload = CreatePayload(auditEvent);
			_ = sb.Append(JsonSerializer.Serialize(payload, ElasticsearchAuditJsonContext.Default.ElasticsearchAuditPayload));
			_ = sb.Append('\n');
		}

		return sb.ToString();
	}

	private static ElasticsearchAuditPayload CreatePayload(AuditEvent auditEvent)
	{
		return new ElasticsearchAuditPayload
		{
			EventId = auditEvent.EventId,
			EventType = auditEvent.EventType.ToString(),
			Action = auditEvent.Action,
			Outcome = auditEvent.Outcome.ToString(),
			Timestamp = auditEvent.Timestamp,
			ActorId = auditEvent.ActorId,
			ActorType = auditEvent.ActorType,
			ResourceId = auditEvent.ResourceId,
			ResourceType = auditEvent.ResourceType,
			ResourceClassification = auditEvent.ResourceClassification?.ToString(),
			TenantId = auditEvent.TenantId,
			CorrelationId = auditEvent.CorrelationId,
			SessionId = auditEvent.SessionId,
			IpAddress = auditEvent.IpAddress,
			UserAgent = auditEvent.UserAgent,
			Reason = auditEvent.Reason,
			Metadata = auditEvent.Metadata,
			EventHash = auditEvent.EventHash
		};
	}

	private async Task<(bool success, string? errorMessage)> ExportChunkAsync(
		List<AuditEvent> events,
		CancellationToken cancellationToken)
	{
		var ndjson = BuildBulkPayload(events);
		var response = await SendWithRetryAsync(ndjson, cancellationToken).ConfigureAwait(false);

		if (response.IsSuccessStatusCode)
		{
			return (true, null);
		}

		var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
		return (false, $"HTTP {(int)response.StatusCode}: {errorBody}");
	}

	private async Task<HttpResponseMessage> SendWithRetryAsync(
		string ndjson,
		CancellationToken cancellationToken)
	{
		var attempts = 0;
		HttpResponseMessage? lastResponse = null;

		while (attempts <= _options.MaxRetryAttempts)
		{
			attempts++;

			try
			{
				using var request = CreateRequest(ndjson);
				lastResponse = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

				if (lastResponse.IsSuccessStatusCode || !IsTransientStatusCode(lastResponse.StatusCode))
				{
					return lastResponse;
				}

				if (attempts <= _options.MaxRetryAttempts)
				{
					var delay = _options.RetryBaseDelay * Math.Pow(2, attempts - 1);
					LogAuditExportRetry(
						attempts,
						delay.TotalMilliseconds,
						lastResponse.StatusCode);

					await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
				}
			}
			catch (HttpRequestException) when (attempts <= _options.MaxRetryAttempts)
			{
				var delay = _options.RetryBaseDelay * Math.Pow(2, attempts - 1);
				await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
			}
		}

		return lastResponse ?? throw new HttpRequestException("Failed after all retry attempts");
	}

	private HttpRequestMessage CreateRequest(string ndjson)
	{
		var request = new HttpRequestMessage(HttpMethod.Post, _bulkApiUri)
		{
			Content = new StringContent(ndjson, Encoding.UTF8, "application/x-ndjson")
		};

		ApplyAuthentication(request);

		return request;
	}

	private void ApplyAuthentication(HttpRequestMessage request)
	{
		if (!string.IsNullOrEmpty(_options.ApiKey))
		{
			request.Headers.Authorization = new AuthenticationHeaderValue("ApiKey", _options.ApiKey);
		}
	}

	[LoggerMessage(ElasticsearchAuditLoggingEventId.EventForwarded, LogLevel.Debug,
		"Exported audit event {EventId} to Elasticsearch")]
	private partial void LogAuditEventExported(string eventId);

	[LoggerMessage(ElasticsearchAuditLoggingEventId.ForwardFailedStatus, LogLevel.Warning,
		"Failed to export audit event {EventId} to Elasticsearch. Status: {StatusCode}, Response: {Response}")]
	private partial void LogAuditExportFailed(string eventId, HttpStatusCode statusCode, string response);

	[LoggerMessage(ElasticsearchAuditLoggingEventId.ForwardFailedHttpError, LogLevel.Error,
		"HTTP error exporting audit event {EventId} to Elasticsearch")]
	private partial void LogAuditExportHttpError(Exception exception, string eventId);

	[LoggerMessage(ElasticsearchAuditLoggingEventId.ForwardFailedTimeout, LogLevel.Error,
		"Timeout exporting audit event {EventId} to Elasticsearch")]
	private partial void LogAuditExportTimeout(Exception exception, string eventId);

	[LoggerMessage(ElasticsearchAuditLoggingEventId.ForwardFailedBatchChunk, LogLevel.Error,
		"Error exporting batch chunk to Elasticsearch")]
	private partial void LogAuditExportBatchChunkError(Exception exception);

	[LoggerMessage(ElasticsearchAuditLoggingEventId.BatchForwarded, LogLevel.Information,
		"Exported batch of {TotalCount} audit events to Elasticsearch. Success: {SuccessCount}, Failed: {FailedCount}")]
	private partial void LogAuditExportBatchSummary(int totalCount, int successCount, int failedCount);

	[LoggerMessage(ElasticsearchAuditLoggingEventId.HealthCheckFailed, LogLevel.Warning,
		"Elasticsearch health check failed")]
	private partial void LogHealthCheckFailed(Exception exception);

	[LoggerMessage(ElasticsearchAuditLoggingEventId.ForwardRetried, LogLevel.Debug,
		"Retrying Elasticsearch export (attempt {Attempt}) after {Delay}ms due to {StatusCode}")]
	private partial void LogAuditExportRetry(int attempt, double delay, HttpStatusCode statusCode);

	/// <summary>
	/// Elasticsearch audit event payload.
	/// </summary>
	internal sealed class ElasticsearchAuditPayload
	{
		[JsonPropertyName("event_id")] public string? EventId { get; init; }

		[JsonPropertyName("event_type")] public string? EventType { get; init; }

		[JsonPropertyName("action")] public string? Action { get; init; }

		[JsonPropertyName("outcome")] public string? Outcome { get; init; }

		[JsonPropertyName("@timestamp")] public DateTimeOffset Timestamp { get; init; }

		[JsonPropertyName("actor_id")] public string? ActorId { get; init; }

		[JsonPropertyName("actor_type")] public string? ActorType { get; init; }

		[JsonPropertyName("resource_id")] public string? ResourceId { get; init; }

		[JsonPropertyName("resource_type")] public string? ResourceType { get; init; }

		[JsonPropertyName("resource_classification")]
		public string? ResourceClassification { get; init; }

		[JsonPropertyName("tenant_id")] public string? TenantId { get; init; }

		[JsonPropertyName("correlation_id")] public string? CorrelationId { get; init; }

		[JsonPropertyName("session_id")] public string? SessionId { get; init; }

		[JsonPropertyName("ip_address")] public string? IpAddress { get; init; }

		[JsonPropertyName("user_agent")] public string? UserAgent { get; init; }

		[JsonPropertyName("reason")] public string? Reason { get; init; }

		[JsonPropertyName("metadata")] public IReadOnlyDictionary<string, string>? Metadata { get; init; }

		[JsonPropertyName("event_hash")] public string? EventHash { get; init; }
	}

	/// <summary>
	/// Bulk API index action wrapper.
	/// </summary>
	internal sealed class BulkIndexAction
	{
		[JsonPropertyName("index")] public BulkActionMeta? Index { get; init; }
	}

	/// <summary>
	/// Bulk API action metadata.
	/// </summary>
	internal sealed class BulkActionMeta
	{
		[JsonPropertyName("_index")] public string? IndexName { get; init; }

		[JsonPropertyName("_id")] public string? Id { get; init; }
	}
}
