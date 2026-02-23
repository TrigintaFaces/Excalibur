// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using System.Diagnostics;

using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.AuditLogging.Aws;

/// <summary>
/// Exports audit events to AWS CloudWatch Logs via the PutLogEvents API.
/// </summary>
/// <remarks>
/// <para>
/// This exporter sends audit events as structured JSON log entries to a
/// CloudWatch Logs log group and stream. Events are searchable via
/// CloudWatch Logs Insights.
/// </para>
/// <para>
/// Authentication uses the standard AWS credential chain.
/// </para>
/// </remarks>
public sealed partial class AwsCloudWatchAuditExporter : IAuditLogExporter
{
	private readonly HttpClient _httpClient;
	private readonly AwsAuditOptions _options;
	private readonly ILogger<AwsCloudWatchAuditExporter> _logger;
	private readonly string _streamName;

	/// <summary>
	/// Initializes a new instance of the <see cref="AwsCloudWatchAuditExporter"/> class.
	/// </summary>
	/// <param name="httpClient">The HTTP client for making requests.</param>
	/// <param name="options">The AWS audit options.</param>
	/// <param name="logger">The logger.</param>
	public AwsCloudWatchAuditExporter(
		HttpClient httpClient,
		IOptions<AwsAuditOptions> options,
		ILogger<AwsCloudWatchAuditExporter> logger)
	{
		ArgumentNullException.ThrowIfNull(httpClient);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_httpClient = httpClient;
		_options = options.Value;
		_logger = logger;
		_streamName = _options.StreamName ?? $"dispatch-audit-{Environment.MachineName}";
	}

	/// <inheritdoc />
	public string Name => "AwsCloudWatch";

	/// <inheritdoc />
	public async Task<AuditExportResult> ExportAsync(
		AuditEvent auditEvent,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(auditEvent);

		try
		{
			var payload = CreatePayload(auditEvent);
			var json = JsonSerializer.Serialize(payload, AwsAuditJsonContext.Default.CloudWatchAuditPayload);
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
			.GroupBy(x => x.Index / _options.BatchSize)
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
			// Attempt a DescribeLogGroups-style check using the configured endpoint
			var serviceUrl = _options.ServiceUrl
							 ?? $"https://logs.{_options.Region}.amazonaws.com";
			using var request = new HttpRequestMessage(HttpMethod.Get, serviceUrl);
			var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

			// Any response from the endpoint indicates reachability
			var isHealthy = response.StatusCode is not HttpStatusCode.ServiceUnavailable
							and not HttpStatusCode.GatewayTimeout;
			var elapsedMs = (long)Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;

			return new AuditExporterHealthResult
			{
				IsHealthy = isHealthy,
				ExporterName = Name,
				Endpoint = serviceUrl,
				LatencyMs = elapsedMs,
				CheckedAt = DateTimeOffset.UtcNow,
				ErrorMessage = isHealthy ? null : $"Unexpected status code: {response.StatusCode}",
				Diagnostics = new Dictionary<string, string>
				{
					["StatusCode"] = ((int)response.StatusCode).ToString(),
					["Region"] = _options.Region,
					["LogGroupName"] = _options.LogGroupName,
					["StreamName"] = _streamName
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
				Endpoint = $"https://logs.{_options.Region}.amazonaws.com",
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

	private static CloudWatchAuditPayload CreatePayload(AuditEvent auditEvent)
	{
		return new CloudWatchAuditPayload
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
		// Send each event as a separate log line in a single batch
		var sb = new StringBuilder();
		foreach (var evt in events)
		{
			var payload = CreatePayload(evt);
			_ = sb.AppendLine(JsonSerializer.Serialize(payload, AwsAuditJsonContext.Default.CloudWatchAuditPayload));
		}

		var response = await SendWithRetryAsync(sb.ToString(), cancellationToken).ConfigureAwait(false);

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
		var serviceUrl = _options.ServiceUrl
						 ?? $"https://logs.{_options.Region}.amazonaws.com";

		while (attempts <= _options.MaxRetryAttempts)
		{
			attempts++;

			try
			{
				using var request = new HttpRequestMessage(HttpMethod.Post, serviceUrl)
				{
					Content = new StringContent(json, Encoding.UTF8, "application/x-amz-json-1.1")
				};

				request.Headers.Add("X-Amz-Target", "Logs_20140328.PutLogEvents");

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

	[LoggerMessage(AwsAuditLoggingEventId.EventForwarded, LogLevel.Debug,
		"Exported audit event {EventId} to AWS CloudWatch")]
	private partial void LogAuditEventExported(string eventId);

	[LoggerMessage(AwsAuditLoggingEventId.ForwardFailedStatus, LogLevel.Warning,
		"Failed to export audit event {EventId} to AWS CloudWatch. Status: {StatusCode}, Response: {Response}")]
	private partial void LogAuditExportFailed(string eventId, HttpStatusCode statusCode, string response);

	[LoggerMessage(AwsAuditLoggingEventId.ForwardFailedHttpError, LogLevel.Error,
		"HTTP error exporting audit event {EventId} to AWS CloudWatch")]
	private partial void LogAuditExportHttpError(Exception exception, string eventId);

	[LoggerMessage(AwsAuditLoggingEventId.ForwardFailedTimeout, LogLevel.Error,
		"Timeout exporting audit event {EventId} to AWS CloudWatch")]
	private partial void LogAuditExportTimeout(Exception exception, string eventId);

	[LoggerMessage(AwsAuditLoggingEventId.ForwardFailedBatchChunk, LogLevel.Error,
		"Error exporting batch chunk to AWS CloudWatch")]
	private partial void LogAuditExportBatchChunkError(Exception exception);

	[LoggerMessage(AwsAuditLoggingEventId.BatchForwarded, LogLevel.Information,
		"Exported batch of {TotalCount} audit events to AWS CloudWatch. Success: {SuccessCount}, Failed: {FailedCount}")]
	private partial void LogAuditExportBatchSummary(int totalCount, int successCount, int failedCount);

	[LoggerMessage(AwsAuditLoggingEventId.HealthCheckFailed, LogLevel.Warning,
		"AWS CloudWatch health check failed")]
	private partial void LogHealthCheckFailed(Exception exception);

	[LoggerMessage(AwsAuditLoggingEventId.ForwardRetried, LogLevel.Debug,
		"Retrying AWS CloudWatch export (attempt {Attempt}) after {Delay}ms due to {StatusCode}")]
	private partial void LogAuditExportRetry(int attempt, double delay, HttpStatusCode statusCode);

	/// <summary>
	/// CloudWatch audit event payload.
	/// </summary>
	internal sealed class CloudWatchAuditPayload
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
		[JsonPropertyName("resource_classification")] public string? ResourceClassification { get; init; }
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
