// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.AuditLogging.Sentinel;

/// <summary>
/// Exports audit events to Azure Sentinel via the Log Analytics Data Collector API.
/// </summary>
/// <remarks>
/// <para>
/// This exporter uses the HTTP Data Collector API to send custom log data to
/// Azure Monitor Log Analytics workspace. The data becomes available for
/// querying in Azure Sentinel.
/// </para>
/// <para>
/// Authentication uses the workspace ID and shared key to generate an
/// HMAC-SHA256 signature for each request.
/// </para>
/// </remarks>
public sealed partial class SentinelAuditExporter : IAuditLogExporter
{
	private readonly HttpClient _httpClient;
	private readonly SentinelExporterOptions _options;
	private readonly ILogger<SentinelAuditExporter> _logger;
	private readonly Uri _dataCollectorUri;

	/// <summary>
	/// Initializes a new instance of the <see cref="SentinelAuditExporter"/> class.
	/// </summary>
	/// <param name="httpClient">The HTTP client for making requests.</param>
	/// <param name="options">The Sentinel exporter options.</param>
	/// <param name="logger">The logger.</param>
	public SentinelAuditExporter(
		HttpClient httpClient,
		IOptions<SentinelExporterOptions> options,
		ILogger<SentinelAuditExporter> logger)
	{
		ArgumentNullException.ThrowIfNull(httpClient);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_httpClient = httpClient;
		_options = options.Value;
		_logger = logger;

		_dataCollectorUri = new Uri(
			$"https://{_options.WorkspaceId}.ods.opinsights.azure.com/api/logs?api-version=2016-04-01");
	}

	/// <inheritdoc />
	public string Name => "AzureSentinel";

	/// <inheritdoc />
	public async Task<AuditExportResult> ExportAsync(
		AuditEvent auditEvent,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(auditEvent);

		try
		{
			var payload = new[] { CreateSentinelPayload(auditEvent) };
			var json = JsonSerializer.Serialize(
				payload,
				SentinelAuditJsonContext.Default.SentinelAuditPayloadArray);

			var response = await SendWithRetryAsync(json, cancellationToken).ConfigureAwait(false);

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
				ErrorMessage = Resources.SentinelAuditExporter_RequestTimedOut,
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
						errors[evt.EventId] = result.errorMessage ??
											  Resources.SentinelAuditExporter_UnknownError;
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
			// Send a minimal test payload to verify connectivity and authentication
			var testPayload = new[] { new SentinelHealthPayload { HealthCheck = true, Timestamp = DateTimeOffset.UtcNow } };
			var json = JsonSerializer.Serialize(
				testPayload,
				SentinelAuditJsonContext.Default.SentinelHealthPayloadArray);

			using var request = CreateRequest(json);
			var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
			sw.Stop();

			var isHealthy = response.IsSuccessStatusCode;

			return new AuditExporterHealthResult
			{
				IsHealthy = isHealthy,
				ExporterName = Name,
				Endpoint = _dataCollectorUri.Host,
				LatencyMs = sw.ElapsedMilliseconds,
				CheckedAt = DateTimeOffset.UtcNow,
				ErrorMessage = isHealthy ? null : $"Unexpected status code: {response.StatusCode}",
				Diagnostics = new Dictionary<string, string>
				{
					["StatusCode"] = ((int)response.StatusCode).ToString(),
					["WorkspaceId"] = _options.WorkspaceId[..Math.Min(8, _options.WorkspaceId.Length)] + "...",
					["LogType"] = _options.LogType
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
				Endpoint = _dataCollectorUri.Host,
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

	private static SentinelAuditPayload CreateSentinelPayload(AuditEvent auditEvent)
	{
		return new SentinelAuditPayload
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
		var payloads = events.Select(CreateSentinelPayload).ToArray();
		var json = JsonSerializer.Serialize(
			payloads,
			SentinelAuditJsonContext.Default.SentinelAuditPayloadArray);

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
			Resources.SentinelAuditExporter_FailedAfterRetries);
	}

	private HttpRequestMessage CreateRequest(string json)
	{
		var dateString = DateTimeOffset.UtcNow.ToString("r");
		var contentLength = Encoding.UTF8.GetByteCount(json);
		var signature = BuildSignature(dateString, contentLength);

		var request = new HttpRequestMessage(HttpMethod.Post, _dataCollectorUri)
		{
			Content = new StringContent(json, Encoding.UTF8, "application/json")
		};

		request.Headers.Add("Authorization", signature);
		request.Headers.Add("Log-Type", _options.LogType);
		request.Headers.Add("x-ms-date", dateString);

		if (!string.IsNullOrEmpty(_options.TimeGeneratedField))
		{
			request.Headers.Add("time-generated-field", _options.TimeGeneratedField);
		}

		if (!string.IsNullOrEmpty(_options.AzureResourceId))
		{
			request.Headers.Add("x-ms-AzureResourceId", _options.AzureResourceId);
		}

		return request;
	}

	private string BuildSignature(string dateString, int contentLength)
	{
		var stringToSign = $"POST\n{contentLength}\napplication/json\nx-ms-date:{dateString}\n/api/logs";
		var keyBytes = Convert.FromBase64String(_options.SharedKey);
		var encoding = new UTF8Encoding();
		var messageBytes = encoding.GetBytes(stringToSign);

		using var hmac = new HMACSHA256(keyBytes);
		var hash = hmac.ComputeHash(messageBytes);
		var signature = Convert.ToBase64String(hash);

		return $"SharedKey {_options.WorkspaceId}:{signature}";
	}

	[LoggerMessage(SentinelAuditLoggingEventId.EventForwarded, LogLevel.Debug,
		"Exported audit event {EventId} to Azure Sentinel")]
	private partial void LogAuditEventExported(string eventId);

	[LoggerMessage(SentinelAuditLoggingEventId.ForwardFailedStatus, LogLevel.Warning,
		"Failed to export audit event {EventId} to Azure Sentinel. Status: {StatusCode}, Response: {Response}")]
	private partial void LogAuditExportFailed(string eventId, HttpStatusCode statusCode, string response);

	[LoggerMessage(SentinelAuditLoggingEventId.ForwardFailedHttpError, LogLevel.Error,
		"HTTP error exporting audit event {EventId} to Azure Sentinel")]
	private partial void LogAuditExportHttpError(Exception exception, string eventId);

	[LoggerMessage(SentinelAuditLoggingEventId.ForwardFailedTimeout, LogLevel.Error,
		"Timeout exporting audit event {EventId} to Azure Sentinel")]
	private partial void LogAuditExportTimeout(Exception exception, string eventId);

	[LoggerMessage(SentinelAuditLoggingEventId.ForwardFailedBatchChunk, LogLevel.Error,
		"Error exporting batch chunk to Azure Sentinel")]
	private partial void LogAuditExportBatchChunkError(Exception exception);

	[LoggerMessage(SentinelAuditLoggingEventId.BatchForwarded, LogLevel.Information,
		"Exported batch of {TotalCount} audit events to Azure Sentinel. Success: {SuccessCount}, Failed: {FailedCount}")]
	private partial void LogAuditExportBatchSummary(int totalCount, int successCount, int failedCount);

	[LoggerMessage(SentinelAuditLoggingEventId.HealthCheckFailed, LogLevel.Warning,
		"Azure Sentinel health check failed")]
	private partial void LogHealthCheckFailed(Exception exception);

	[LoggerMessage(SentinelAuditLoggingEventId.ForwardRetried, LogLevel.Debug,
		"Retrying Azure Sentinel export (attempt {Attempt}) after {Delay}ms due to {StatusCode}")]
	private partial void LogAuditExportRetry(int attempt, double delay, HttpStatusCode statusCode);

	/// <summary>
	/// Health check payload for Azure Sentinel.
	/// </summary>
	internal sealed class SentinelHealthPayload
	{
		[JsonPropertyName("health_check")] public bool HealthCheck { get; init; }

		[JsonPropertyName("timestamp")] public DateTimeOffset Timestamp { get; init; }
	}

	/// <summary>
	/// Audit event payload for Azure Sentinel.
	/// </summary>
	internal sealed class SentinelAuditPayload
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
