// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.AuditLogging.GoogleCloud;

/// <summary>
/// Exports audit events to Google Cloud Logging via the Cloud Logging API v2.
/// </summary>
/// <remarks>
/// <para>
/// This exporter writes structured log entries to Cloud Logging.
/// Entries are searchable via the Logs Explorer and can trigger alerts via
/// Cloud Monitoring.
/// </para>
/// <para>
/// Authentication uses Application Default Credentials (ADC).
/// </para>
/// </remarks>
public sealed partial class GoogleCloudLoggingAuditExporter : IAuditLogExporter
{
	private readonly HttpClient _httpClient;
	private readonly GoogleCloudAuditOptions _options;
	private readonly ILogger<GoogleCloudLoggingAuditExporter> _logger;
	private readonly string _logName;

	/// <summary>
	/// Initializes a new instance of the <see cref="GoogleCloudLoggingAuditExporter"/> class.
	/// </summary>
	/// <param name="httpClient">The HTTP client for making requests.</param>
	/// <param name="options">The Google Cloud audit options.</param>
	/// <param name="logger">The logger.</param>
	public GoogleCloudLoggingAuditExporter(
		HttpClient httpClient,
		IOptions<GoogleCloudAuditOptions> options,
		ILogger<GoogleCloudLoggingAuditExporter> logger)
	{
		ArgumentNullException.ThrowIfNull(httpClient);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_httpClient = httpClient;
		_options = options.Value;
		_logger = logger;
		_logName = $"projects/{_options.ProjectId}/logs/{_options.LogName}";
	}

	/// <inheritdoc />
	public string Name => "GoogleCloudLogging";

	/// <inheritdoc />
	public async Task<AuditExportResult> ExportAsync(
		AuditEvent auditEvent,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(auditEvent);

		try
		{
			var payload = BuildWriteRequest([auditEvent]);
			var json = JsonSerializer.Serialize(payload, GoogleCloudAuditJsonContext.Default.CloudLoggingPayload);
			var response = await SendWithRetryAsync(json, cancellationToken).ConfigureAwait(false);

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
		var sw = Stopwatch.StartNew();

		try
		{
			var healthUri = new Uri($"https://logging.googleapis.com/v2/entries:list");
			using var request = new HttpRequestMessage(HttpMethod.Post, healthUri)
			{
				Content = new StringContent(
					$"{{\"resourceNames\":[\"projects/{_options.ProjectId}\"],\"pageSize\":1}}",
					Encoding.UTF8, "application/json")
			};

			var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
			sw.Stop();

			var isHealthy = response.IsSuccessStatusCode;

			return new AuditExporterHealthResult
			{
				IsHealthy = isHealthy,
				ExporterName = Name,
				Endpoint = $"logging.googleapis.com",
				LatencyMs = sw.ElapsedMilliseconds,
				CheckedAt = DateTimeOffset.UtcNow,
				ErrorMessage = isHealthy ? null : $"Unexpected status code: {response.StatusCode}",
				Diagnostics = new Dictionary<string, string>
				{
					["StatusCode"] = ((int)response.StatusCode).ToString(),
					["ProjectId"] = _options.ProjectId,
					["LogName"] = _options.LogName,
					["ResourceType"] = _options.ResourceType
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
				Endpoint = "logging.googleapis.com",
				LatencyMs = sw.ElapsedMilliseconds,
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

	private CloudLoggingPayload BuildWriteRequest(IReadOnlyList<AuditEvent> events)
	{
		var entries = events.Select(e => new CloudLoggingAuditPayload
		{
			LogName = _logName,
			Severity = "INFO",
			Timestamp = e.Timestamp.ToString("O"),
			JsonPayload = new Dictionary<string, string?>
			{
				["event_id"] = e.EventId,
				["event_type"] = e.EventType.ToString(),
				["action"] = e.Action,
				["outcome"] = e.Outcome.ToString(),
				["actor_id"] = e.ActorId,
				["actor_type"] = e.ActorType,
				["resource_id"] = e.ResourceId,
				["resource_type"] = e.ResourceType,
				["resource_classification"] = e.ResourceClassification?.ToString(),
				["tenant_id"] = e.TenantId,
				["correlation_id"] = e.CorrelationId,
				["session_id"] = e.SessionId,
				["ip_address"] = e.IpAddress,
				["user_agent"] = e.UserAgent,
				["reason"] = e.Reason,
				["event_hash"] = e.EventHash
			},
			Labels = _options.Labels
		}).ToList();

		return new CloudLoggingPayload
		{
			LogName = _logName,
			Resource = new Dictionary<string, string> { ["type"] = _options.ResourceType },
			Entries = entries
		};
	}

	private async Task<(bool success, string? errorMessage)> ExportChunkAsync(
		List<AuditEvent> events,
		CancellationToken cancellationToken)
	{
		var payload = BuildWriteRequest(events);
		var json = JsonSerializer.Serialize(payload, GoogleCloudAuditJsonContext.Default.CloudLoggingPayload);
		var response = await SendWithRetryAsync(json, cancellationToken).ConfigureAwait(false);

		if (response.IsSuccessStatusCode)
		{
			return (true, null);
		}

		var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
		return (false, $"HTTP {(int)response.StatusCode}: {errorBody}");
	}

	private async Task<HttpResponseMessage> SendWithRetryAsync(
		string json,
		CancellationToken cancellationToken)
	{
		var attempts = 0;
		HttpResponseMessage? lastResponse = null;
		var writeUri = new Uri("https://logging.googleapis.com/v2/entries:write");

		while (attempts <= _options.MaxRetryAttempts)
		{
			attempts++;

			try
			{
				using var request = new HttpRequestMessage(HttpMethod.Post, writeUri)
				{
					Content = new StringContent(json, Encoding.UTF8, "application/json")
				};

				lastResponse = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

				if (lastResponse.IsSuccessStatusCode || !IsTransientStatusCode(lastResponse.StatusCode))
				{
					return lastResponse;
				}

				if (attempts <= _options.MaxRetryAttempts)
				{
					var delay = _options.RetryBaseDelay * Math.Pow(2, attempts - 1);
					LogAuditExportRetry(attempts, delay.TotalMilliseconds, lastResponse.StatusCode);
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

	[LoggerMessage(GoogleCloudAuditLoggingEventId.EventForwarded, LogLevel.Debug,
		"Exported audit event {EventId} to Google Cloud Logging")]
	private partial void LogAuditEventExported(string eventId);

	[LoggerMessage(GoogleCloudAuditLoggingEventId.ForwardFailedStatus, LogLevel.Warning,
		"Failed to export audit event {EventId} to Google Cloud Logging. Status: {StatusCode}, Response: {Response}")]
	private partial void LogAuditExportFailed(string eventId, HttpStatusCode statusCode, string response);

	[LoggerMessage(GoogleCloudAuditLoggingEventId.ForwardFailedHttpError, LogLevel.Error,
		"HTTP error exporting audit event {EventId} to Google Cloud Logging")]
	private partial void LogAuditExportHttpError(Exception exception, string eventId);

	[LoggerMessage(GoogleCloudAuditLoggingEventId.ForwardFailedTimeout, LogLevel.Error,
		"Timeout exporting audit event {EventId} to Google Cloud Logging")]
	private partial void LogAuditExportTimeout(Exception exception, string eventId);

	[LoggerMessage(GoogleCloudAuditLoggingEventId.ForwardFailedBatchChunk, LogLevel.Error,
		"Error exporting batch chunk to Google Cloud Logging")]
	private partial void LogAuditExportBatchChunkError(Exception exception);

	[LoggerMessage(GoogleCloudAuditLoggingEventId.BatchForwarded, LogLevel.Information,
		"Exported batch of {TotalCount} audit events to Google Cloud Logging. Success: {SuccessCount}, Failed: {FailedCount}")]
	private partial void LogAuditExportBatchSummary(int totalCount, int successCount, int failedCount);

	[LoggerMessage(GoogleCloudAuditLoggingEventId.HealthCheckFailed, LogLevel.Warning,
		"Google Cloud Logging health check failed")]
	private partial void LogHealthCheckFailed(Exception exception);

	[LoggerMessage(GoogleCloudAuditLoggingEventId.ForwardRetried, LogLevel.Debug,
		"Retrying Google Cloud Logging export (attempt {Attempt}) after {Delay}ms due to {StatusCode}")]
	private partial void LogAuditExportRetry(int attempt, double delay, HttpStatusCode statusCode);

	/// <summary>
	/// Cloud Logging write entries request payload.
	/// </summary>
	internal sealed class CloudLoggingPayload
	{
		[JsonPropertyName("logName")] public string? LogName { get; init; }
		[JsonPropertyName("resource")] public Dictionary<string, string>? Resource { get; init; }
		[JsonPropertyName("entries")] public List<CloudLoggingAuditPayload>? Entries { get; init; }
	}

	/// <summary>
	/// Cloud Logging log entry payload.
	/// </summary>
	internal sealed class CloudLoggingAuditPayload
	{
		[JsonPropertyName("logName")] public string? LogName { get; init; }
		[JsonPropertyName("severity")] public string? Severity { get; init; }
		[JsonPropertyName("timestamp")] public string? Timestamp { get; init; }
		[JsonPropertyName("jsonPayload")] public Dictionary<string, string?>? JsonPayload { get; init; }
		[JsonPropertyName("labels")] public Dictionary<string, string>? Labels { get; init; }
	}
}
