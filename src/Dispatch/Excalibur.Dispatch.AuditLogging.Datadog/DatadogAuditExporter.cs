// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.AuditLogging.Datadog;

/// <summary>
/// Exports audit events to Datadog via the Logs API.
/// </summary>
/// <remarks>
/// <para>
/// This exporter uses the Datadog Logs API v2 to send custom audit log data.
/// Logs are searchable and analyzable in Datadog Log Management.
/// </para>
/// <para>
/// Authentication uses an API key header (DD-API-KEY).
/// Supports gzip compression for improved performance with large batches.
/// </para>
/// </remarks>
public sealed partial class DatadogAuditExporter : IAuditLogExporter
{
	private readonly HttpClient _httpClient;
	private readonly DatadogExporterOptions _options;
	private readonly ILogger<DatadogAuditExporter> _logger;
	private readonly Uri _logsApiUri;

	/// <summary>
	/// Initializes a new instance of the <see cref="DatadogAuditExporter"/> class.
	/// </summary>
	/// <param name="httpClient">The HTTP client for making requests.</param>
	/// <param name="options">The Datadog exporter options.</param>
	/// <param name="logger">The logger.</param>
	public DatadogAuditExporter(
		HttpClient httpClient,
		IOptions<DatadogExporterOptions> options,
		ILogger<DatadogAuditExporter> logger)
	{
		ArgumentNullException.ThrowIfNull(httpClient);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_httpClient = httpClient;
		_options = options.Value;
		_logger = logger;

		_logsApiUri = new Uri($"https://http-intake.logs.{_options.Site}/api/v2/logs");
	}

	/// <inheritdoc />
	public string Name => "Datadog";

	/// <inheritdoc />
	public async Task<AuditExportResult> ExportAsync(
		AuditEvent auditEvent,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(auditEvent);

		try
		{
			var payload = new[] { CreateDatadogLog(auditEvent) };
			var json = JsonSerializer.Serialize(
				payload,
				DatadogAuditJsonContext.Default.DatadogLogEntryArray);

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
				ErrorMessage = Resources.DatadogAuditExporter_RequestTimedOut,
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
											  ?? Resources.DatadogAuditExporter_UnknownError;
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
			// Send a minimal test log to verify connectivity and authentication
			var testPayload = new[]
			{
				new DatadogLogEntry
				{
					Message = Resources.DatadogAuditExporter_HealthCheckMessage,
					Service = _options.Service,
					Source = _options.Source,
					Hostname = _options.Hostname ?? Environment.MachineName,
					Ddsource = _options.Source,
					Ddtags = Resources.DatadogAuditExporter_HealthCheckTags
				}
			};
			var json = JsonSerializer.Serialize(
				testPayload,
				DatadogAuditJsonContext.Default.DatadogLogEntryArray);

			using var request = CreateRequest(json);
			var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
			sw.Stop();

			var isHealthy = response.IsSuccessStatusCode;

			return new AuditExporterHealthResult
			{
				IsHealthy = isHealthy,
				ExporterName = Name,
				Endpoint = _logsApiUri.Host,
				LatencyMs = sw.ElapsedMilliseconds,
				CheckedAt = DateTimeOffset.UtcNow,
				ErrorMessage = isHealthy ? null : $"Unexpected status code: {response.StatusCode}",
				Diagnostics = new Dictionary<string, string>
				{
					["StatusCode"] = ((int)response.StatusCode).ToString(),
					["Site"] = _options.Site,
					["Service"] = _options.Service,
					["Source"] = _options.Source
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
				Endpoint = _logsApiUri.Host,
				LatencyMs = sw.ElapsedMilliseconds,
				CheckedAt = DateTimeOffset.UtcNow,
				ErrorMessage = ex.Message
			};
		}
	}

	private static byte[] CompressGzip(byte[] data)
	{
		using var output = new MemoryStream();
		using (var gzip = new GZipStream(output, System.IO.Compression.CompressionLevel.Fastest))
		{
			gzip.Write(data, 0, data.Length);
		}

		return output.ToArray();
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
		var payloads = events.Select(CreateDatadogLog).ToArray();
		var json = JsonSerializer.Serialize(
			payloads,
			DatadogAuditJsonContext.Default.DatadogLogEntryArray);

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

		while (attempts <= _options.MaxRetryAttempts)
		{
			attempts++;

			try
			{
				using var request = CreateRequest(json);
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

		return lastResponse ?? throw new HttpRequestException(
			Resources.DatadogAuditExporter_FailedAfterRetries);
	}

	private HttpRequestMessage CreateRequest(string json)
	{
		var request = new HttpRequestMessage(HttpMethod.Post, _logsApiUri);
		request.Headers.Add("DD-API-KEY", _options.ApiKey);

		if (_options.UseCompression)
		{
			var bytes = Encoding.UTF8.GetBytes(json);
			var compressedBytes = CompressGzip(bytes);
			request.Content = new ByteArrayContent(compressedBytes);
			request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
			request.Content.Headers.ContentEncoding.Add("gzip");
		}
		else
		{
			request.Content = new StringContent(json, Encoding.UTF8, "application/json");
		}

		return request;
	}

	private DatadogLogEntry CreateDatadogLog(AuditEvent auditEvent)
	{
		var tags = new List<string>
		{
			$"event_type:{auditEvent.EventType}", $"outcome:{auditEvent.Outcome}", $"action:{auditEvent.Action}"
		};

		if (!string.IsNullOrEmpty(auditEvent.TenantId))
		{
			tags.Add($"tenant:{auditEvent.TenantId}");
		}

		if (auditEvent.ResourceClassification != null)
		{
			tags.Add($"classification:{auditEvent.ResourceClassification}");
		}

		if (!string.IsNullOrEmpty(_options.Tags))
		{
			tags.Add(_options.Tags);
		}

		return new DatadogLogEntry
		{
			Message = $"[{auditEvent.EventType}] {auditEvent.Action} - {auditEvent.Outcome}",
			Service = _options.Service,
			Source = _options.Source,
			Hostname = _options.Hostname ?? Environment.MachineName,
			Ddsource = _options.Source,
			Ddtags = string.Join(",", tags),
			Timestamp = auditEvent.Timestamp.ToUnixTimeMilliseconds(),
			Attributes = new DatadogAuditAttributes
			{
				EventId = auditEvent.EventId,
				EventType = auditEvent.EventType.ToString(),
				Action = auditEvent.Action,
				Outcome = auditEvent.Outcome.ToString(),
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

	[LoggerMessage(DatadogAuditLoggingEventId.EventForwarded, LogLevel.Debug,
		"Exported audit event {EventId} to Datadog")]
	private partial void LogAuditEventExported(string eventId);

	[LoggerMessage(DatadogAuditLoggingEventId.ForwardFailedStatus, LogLevel.Warning,
		"Failed to export audit event {EventId} to Datadog. Status: {StatusCode}, Response: {Response}")]
	private partial void LogAuditExportFailed(string eventId, HttpStatusCode statusCode, string response);

	[LoggerMessage(DatadogAuditLoggingEventId.ForwardFailedHttpError, LogLevel.Error,
		"HTTP error exporting audit event {EventId} to Datadog")]
	private partial void LogAuditExportHttpError(Exception exception, string eventId);

	[LoggerMessage(DatadogAuditLoggingEventId.ForwardFailedTimeout, LogLevel.Error,
		"Timeout exporting audit event {EventId} to Datadog")]
	private partial void LogAuditExportTimeout(Exception exception, string eventId);

	[LoggerMessage(DatadogAuditLoggingEventId.ForwardFailedBatchChunk, LogLevel.Error,
		"Error exporting batch chunk to Datadog")]
	private partial void LogAuditExportBatchChunkError(Exception exception);

	[LoggerMessage(DatadogAuditLoggingEventId.BatchForwarded, LogLevel.Information,
		"Exported batch of {TotalCount} audit events to Datadog. Success: {SuccessCount}, Failed: {FailedCount}")]
	private partial void LogAuditExportBatchSummary(int totalCount, int successCount, int failedCount);

	[LoggerMessage(DatadogAuditLoggingEventId.HealthCheckFailed, LogLevel.Warning,
		"Datadog health check failed")]
	private partial void LogHealthCheckFailed(Exception exception);

	[LoggerMessage(DatadogAuditLoggingEventId.ForwardRetried, LogLevel.Debug,
		"Retrying Datadog export (attempt {Attempt}) after {Delay}ms due to {StatusCode}")]
	private partial void LogAuditExportRetry(int attempt, double delay, HttpStatusCode statusCode);

	/// <summary>
	/// Datadog log entry structure.
	/// </summary>
	internal sealed class DatadogLogEntry
	{
		[JsonPropertyName("message")] public string? Message { get; init; }

		[JsonPropertyName("service")] public string? Service { get; init; }

		[JsonPropertyName("source")] public string? Source { get; init; }

		[JsonPropertyName("hostname")] public string? Hostname { get; init; }

		[JsonPropertyName("ddsource")] public string? Ddsource { get; init; }

		[JsonPropertyName("ddtags")] public string? Ddtags { get; init; }

		[JsonPropertyName("timestamp")] public long? Timestamp { get; init; }

		[JsonPropertyName("attributes")] public DatadogAuditAttributes? Attributes { get; init; }
	}

	/// <summary>
	/// Audit event attributes for Datadog.
	/// </summary>
	internal sealed class DatadogAuditAttributes
	{
		[JsonPropertyName("event_id")] public string? EventId { get; init; }

		[JsonPropertyName("event_type")] public string? EventType { get; init; }

		[JsonPropertyName("action")] public string? Action { get; init; }

		[JsonPropertyName("outcome")] public string? Outcome { get; init; }

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
