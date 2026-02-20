// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.AuditLogging.Splunk;

/// <summary>
/// Exports audit events to Splunk via HTTP Event Collector (HEC).
/// </summary>
/// <remarks>
/// <para>
/// This exporter supports both real-time single event export and batch export modes.
/// It includes retry logic with exponential backoff for transient failures.
/// </para>
/// <para>
/// For optimal performance in high-volume scenarios, use batch export mode
/// and enable compression.
/// </para>
/// </remarks>
public sealed partial class SplunkAuditExporter : IAuditLogExporter
{
	private readonly HttpClient _httpClient;
	private readonly SplunkExporterOptions _options;
	private readonly ILogger<SplunkAuditExporter> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="SplunkAuditExporter"/> class.
	/// </summary>
	/// <param name="httpClient">The HTTP client for making requests.</param>
	/// <param name="options">The Splunk exporter options.</param>
	/// <param name="logger">The logger.</param>
	public SplunkAuditExporter(
		HttpClient httpClient,
		IOptions<SplunkExporterOptions> options,
		ILogger<SplunkAuditExporter> logger)
	{
		ArgumentNullException.ThrowIfNull(httpClient);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_httpClient = httpClient;
		_options = options.Value;
		_logger = logger;
	}

	/// <inheritdoc />
	public string Name => "Splunk";

	/// <inheritdoc />
	public async Task<AuditExportResult> ExportAsync(
		AuditEvent auditEvent,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(auditEvent);

		try
		{
			var hecEvent = CreateHecEvent(auditEvent);
			var json = JsonSerializer.Serialize(
				hecEvent,
				SplunkAuditJsonContext.Default.SplunkHecEvent);

			using var content = new StringContent(json, Encoding.UTF8, "application/json");
			using var request = CreateRequest(content);

			var response = await SendWithRetryAsync(request, cancellationToken).ConfigureAwait(false);

			if (response.IsSuccessStatusCode)
			{
				LogAuditEventExported(auditEvent.EventId);

				return new AuditExportResult { Success = true, EventId = auditEvent.EventId, ExportedAt = DateTimeOffset.UtcNow };
			}

			var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
			var isTransient = IsTransientStatusCode(response.StatusCode);

			LogAuditExportFailed(
				auditEvent.EventId,
				response.StatusCode,
				errorBody);

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
				ErrorMessage = Resources.SplunkAuditExporter_RequestTimedOut,
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
			.GroupBy(x => x.Index / _options.MaxBatchSize)
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
					// Mark all events in chunk as failed
					foreach (var evt in chunk)
					{
						failedEventIds.Add(evt.EventId);
						errors[evt.EventId] = result.errorMessage
											  ?? Resources.SplunkAuditExporter_UnknownError;
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

		LogAuditExportBatchSummary(
			auditEvents.Count,
			successCount,
			failedEventIds.Count);

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
		var sw = Stopwatch.StartNew();

		try
		{
			// Send a health check request to the HEC endpoint
			using var request = new HttpRequestMessage(HttpMethod.Get, _options.HecEndpoint);
			request.Headers.Authorization = new AuthenticationHeaderValue("Splunk", _options.HecToken);

			var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
			sw.Stop();

			// HEC returns 400 for GET requests but that indicates it's reachable
			var isHealthy = response.StatusCode is HttpStatusCode.OK
				or HttpStatusCode.BadRequest
				or HttpStatusCode.MethodNotAllowed;

			return new AuditExporterHealthResult
			{
				IsHealthy = isHealthy,
				ExporterName = Name,
				Endpoint = _options.HecEndpoint.ToString(),
				LatencyMs = sw.ElapsedMilliseconds,
				CheckedAt = DateTimeOffset.UtcNow,
				ErrorMessage = isHealthy ? null : $"Unexpected status code: {response.StatusCode}",
				Diagnostics = new Dictionary<string, string>
				{
					["StatusCode"] = ((int)response.StatusCode).ToString(),
					["Index"] = _options.Index ?? "(default)",
					["SourceType"] = _options.SourceType
				}
			};
		}
		catch (Exception ex)
		{
			sw.Stop();

			LogHealthCheckFailed(ex);

			return new AuditExporterHealthResult
			{
				IsHealthy = false,
				ExporterName = Name,
				Endpoint = _options.HecEndpoint.ToString(),
				LatencyMs = sw.ElapsedMilliseconds,
				CheckedAt = DateTimeOffset.UtcNow,
				ErrorMessage = ex.Message
			};
		}
	}

	private static async Task<HttpRequestMessage> CloneRequestAsync(
		HttpRequestMessage request,
		CancellationToken cancellationToken)
	{
		var clone = new HttpRequestMessage(request.Method, request.RequestUri);

		foreach (var header in request.Headers)
		{
			_ = clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
		}

		if (request.Content != null)
		{
			var content = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
			clone.Content = new StringContent(content, Encoding.UTF8, "application/json");
		}

		return clone;
	}

	private static bool IsTransientStatusCode(HttpStatusCode statusCode) =>
		statusCode is HttpStatusCode.RequestTimeout
			or HttpStatusCode.TooManyRequests
			or HttpStatusCode.InternalServerError
			or HttpStatusCode.BadGateway
			or HttpStatusCode.ServiceUnavailable
			or HttpStatusCode.GatewayTimeout;

	private async Task<(bool success, string? errorMessage)> ExportChunkAsync(
		List<AuditEvent> events,
		CancellationToken cancellationToken)
	{
		// Build newline-delimited JSON for batch
		var sb = new StringBuilder();
		foreach (var evt in events)
		{
			var hecEvent = CreateHecEvent(evt);
			_ = sb.Append(JsonSerializer.Serialize(
				hecEvent,
				SplunkAuditJsonContext.Default.SplunkHecEvent));
			_ = sb.Append('\n');
		}

		using var content = new StringContent(sb.ToString(), Encoding.UTF8, "application/json");
		using var request = CreateRequest(content);

		var response = await SendWithRetryAsync(request, cancellationToken).ConfigureAwait(false);

		if (response.IsSuccessStatusCode)
		{
			return (true, null);
		}

		var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
		return (false, $"HTTP {(int)response.StatusCode}: {errorBody}");
	}

	private HttpRequestMessage CreateRequest(HttpContent content)
	{
		var request = new HttpRequestMessage(HttpMethod.Post, _options.HecEndpoint) { Content = content };

		request.Headers.Authorization = new AuthenticationHeaderValue("Splunk", _options.HecToken);

		if (_options.UseAck && !string.IsNullOrEmpty(_options.Channel))
		{
			request.Headers.Add("X-Splunk-Request-Channel", _options.Channel);
		}

		return request;
	}

	private async Task<HttpResponseMessage> SendWithRetryAsync(
		HttpRequestMessage request,
		CancellationToken cancellationToken)
	{
		var attempts = 0;
		HttpResponseMessage? lastResponse = null;

		while (attempts <= _options.MaxRetryAttempts)
		{
			attempts++;

			try
			{
				// Clone the request for retry (content is consumed on first send)
				using var clonedRequest = await CloneRequestAsync(request, cancellationToken).ConfigureAwait(false);
				lastResponse = await _httpClient.SendAsync(clonedRequest, cancellationToken).ConfigureAwait(false);

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

		return lastResponse ?? throw new HttpRequestException(
			Resources.SplunkAuditExporter_FailedAfterRetries);
	}

	private SplunkHecEvent CreateHecEvent(AuditEvent auditEvent)
	{
		return new SplunkHecEvent
		{
			Time = auditEvent.Timestamp.ToUnixTimeSeconds(),
			Host = _options.Host ?? Environment.MachineName,
			Source = _options.Source ?? "dispatch",
			SourceType = _options.SourceType,
			Index = _options.Index,
			Event = new SplunkAuditEventPayload
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
			}
		};
	}

	[LoggerMessage(SplunkAuditLoggingEventId.EventForwarded, LogLevel.Debug,
		"Exported audit event {EventId} to Splunk")]
	private partial void LogAuditEventExported(string eventId);

	[LoggerMessage(SplunkAuditLoggingEventId.ForwardFailedStatus, LogLevel.Warning,
		"Failed to export audit event {EventId} to Splunk. Status: {StatusCode}, Response: {Response}")]
	private partial void LogAuditExportFailed(string eventId, HttpStatusCode statusCode, string response);

	[LoggerMessage(SplunkAuditLoggingEventId.ForwardFailedHttpError, LogLevel.Error,
		"HTTP error exporting audit event {EventId} to Splunk")]
	private partial void LogAuditExportHttpError(Exception exception, string eventId);

	[LoggerMessage(SplunkAuditLoggingEventId.ForwardFailedTimeout, LogLevel.Error,
		"Timeout exporting audit event {EventId} to Splunk")]
	private partial void LogAuditExportTimeout(Exception exception, string eventId);

	[LoggerMessage(SplunkAuditLoggingEventId.ForwardFailedBatchChunk, LogLevel.Error,
		"Error exporting batch chunk to Splunk")]
	private partial void LogAuditExportBatchChunkError(Exception exception);

	[LoggerMessage(SplunkAuditLoggingEventId.BatchForwarded, LogLevel.Information,
		"Exported batch of {TotalCount} audit events to Splunk. Success: {SuccessCount}, Failed: {FailedCount}")]
	private partial void LogAuditExportBatchSummary(int totalCount, int successCount, int failedCount);

	[LoggerMessage(SplunkAuditLoggingEventId.HealthCheckFailed, LogLevel.Warning,
		"Splunk HEC health check failed")]
	private partial void LogHealthCheckFailed(Exception exception);

	[LoggerMessage(SplunkAuditLoggingEventId.ForwardRetried, LogLevel.Debug,
		"Retrying Splunk export (attempt {Attempt}) after {Delay}ms due to {StatusCode}")]
	private partial void LogAuditExportRetry(int attempt, double delay, HttpStatusCode statusCode);

	/// <summary>
	/// Splunk HEC event wrapper.
	/// </summary>
	internal sealed class SplunkHecEvent
	{
		[JsonPropertyName("time")] public long Time { get; init; }

		[JsonPropertyName("host")] public string? Host { get; init; }

		[JsonPropertyName("source")] public string? Source { get; init; }

		[JsonPropertyName("sourcetype")] public string? SourceType { get; init; }

		[JsonPropertyName("index")] public string? Index { get; init; }

		[JsonPropertyName("event")] public SplunkAuditEventPayload? Event { get; init; }
	}

	/// <summary>
	/// Audit event payload for Splunk.
	/// </summary>
	internal sealed class SplunkAuditEventPayload
	{
		[JsonPropertyName("event_id")] public string? EventId { get; init; }

		[JsonPropertyName("event_type")] public string? EventType { get; init; }

		[JsonPropertyName("action")] public string? Action { get; init; }

		[JsonPropertyName("outcome")] public string? Outcome { get; init; }

		[JsonPropertyName("timestamp")] public DateTimeOffset Timestamp { get; init; }

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
}
