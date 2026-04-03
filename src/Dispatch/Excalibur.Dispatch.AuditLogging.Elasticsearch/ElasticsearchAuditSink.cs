// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.AuditLogging.Elasticsearch;

/// <summary>
/// Writes audit events to Elasticsearch via the Bulk API in real-time.
/// </summary>
/// <remarks>
/// <para>
/// This is a lightweight, fire-and-forget audit event writer that indexes
/// individual audit events using the Elasticsearch Bulk API. Elasticsearch
/// serves as a search and analytics sink -- for compliance-grade tamper-evident
/// audit storage, use a SQL-backed <c>IAuditStore</c> instead (see ADR-290).
/// </para>
/// </remarks>
internal sealed partial class ElasticsearchAuditSink
{
	private readonly HttpClient _httpClient;
	private readonly ElasticsearchAuditSinkOptions _options;
	private readonly ILogger<ElasticsearchAuditSink> _logger;
	private readonly Uri[] _bulkApiUris;
	private int _nodeIndex;

	public ElasticsearchAuditSink(
		HttpClient httpClient,
		IOptions<ElasticsearchAuditSinkOptions> options,
		ILogger<ElasticsearchAuditSink> logger)
	{
		ArgumentNullException.ThrowIfNull(httpClient);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_httpClient = httpClient;
		_options = options.Value;
		_logger = logger;

		var nodeUrls = _options.GetResolvedNodeUrls();
		_bulkApiUris = new Uri[nodeUrls.Count];
		for (var i = 0; i < nodeUrls.Count; i++)
		{
			var baseUrl = nodeUrls[i].TrimEnd('/');
			_bulkApiUris[i] = new Uri($"{baseUrl}/_bulk?refresh={_options.RefreshPolicy}");
		}
	}

	private Uri GetNextBulkUri()
	{
		if (_bulkApiUris.Length == 1)
		{
			return _bulkApiUris[0];
		}

		var index = Interlocked.Increment(ref _nodeIndex);
		return _bulkApiUris[((index % _bulkApiUris.Length) + _bulkApiUris.Length) % _bulkApiUris.Length];
	}

	/// <summary>
	/// Writes a single audit event to Elasticsearch.
	/// </summary>
	/// <param name="auditEvent">The audit event to write.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	public async ValueTask WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(auditEvent);

		var ndjson = BuildBulkPayload(auditEvent);

		try
		{
			var response = await SendWithRetryAsync(ndjson, cancellationToken).ConfigureAwait(false);

			if (response.IsSuccessStatusCode)
			{
				LogAuditEventWritten(auditEvent.Action);
				return;
			}

			var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
			LogAuditSinkWriteFailed(auditEvent.Action, response.StatusCode, errorBody);

			throw new InvalidOperationException(
				$"Failed to write audit event to Elasticsearch. HTTP {(int)response.StatusCode}: {errorBody}");
		}
		catch (HttpRequestException ex)
		{
			LogAuditSinkHttpError(ex, auditEvent.Action);
			throw;
		}
		catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
		{
			LogAuditSinkTimeout(ex, auditEvent.Action);
			throw new TimeoutException("Elasticsearch audit sink request timed out.", ex);
		}
	}

	private string BuildBulkPayload(AuditEvent auditEvent)
	{
		var indexName = $"{_options.IndexPrefix}-{auditEvent.Timestamp:yyyy.MM.dd}";
		var sb = new StringBuilder(512);

		var action = new ElasticsearchAuditExporter.BulkIndexAction
		{
			Index = new ElasticsearchAuditExporter.BulkActionMeta { IndexName = indexName, Id = auditEvent.EventId }
		};
		_ = sb.Append(JsonSerializer.Serialize(action, ElasticsearchAuditJsonContext.Default.BulkIndexAction));
		_ = sb.Append('\n');

		var payload = ElasticsearchAuditExporter.CreatePayload(auditEvent, _options.ApplicationName);
		_ = sb.Append(JsonSerializer.Serialize(payload, ElasticsearchAuditJsonContext.Default.ElasticsearchAuditPayload));
		_ = sb.Append('\n');

		return sb.ToString();
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
					LogAuditSinkRetry(attempts, delay.TotalMilliseconds, lastResponse.StatusCode);

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
		var request = new HttpRequestMessage(HttpMethod.Post, GetNextBulkUri())
		{
			Content = new StringContent(ndjson, Encoding.UTF8, "application/x-ndjson")
		};

		if (!string.IsNullOrEmpty(_options.ApiKey))
		{
			request.Headers.Authorization = new AuthenticationHeaderValue("ApiKey", _options.ApiKey);
		}

		return request;
	}

	private static bool IsTransientStatusCode(HttpStatusCode statusCode) =>
		statusCode is HttpStatusCode.RequestTimeout
			or HttpStatusCode.TooManyRequests
			or HttpStatusCode.InternalServerError
			or HttpStatusCode.BadGateway
			or HttpStatusCode.ServiceUnavailable
			or HttpStatusCode.GatewayTimeout;

	[LoggerMessage(ElasticsearchAuditLoggingEventId.SinkEventWritten, LogLevel.Debug,
		"Wrote audit event to Elasticsearch sink: {Action}")]
	private partial void LogAuditEventWritten(string action);

	[LoggerMessage(ElasticsearchAuditLoggingEventId.SinkWriteFailed, LogLevel.Warning,
		"Failed to write audit event to Elasticsearch sink: {Action}. Status: {StatusCode}, Response: {Response}")]
	private partial void LogAuditSinkWriteFailed(string action, HttpStatusCode statusCode, string response);

	[LoggerMessage(ElasticsearchAuditLoggingEventId.SinkHttpError, LogLevel.Error,
		"HTTP error writing audit event to Elasticsearch sink: {Action}")]
	private partial void LogAuditSinkHttpError(Exception exception, string action);

	[LoggerMessage(ElasticsearchAuditLoggingEventId.SinkTimeout, LogLevel.Error,
		"Timeout writing audit event to Elasticsearch sink: {Action}")]
	private partial void LogAuditSinkTimeout(Exception exception, string action);

	[LoggerMessage(ElasticsearchAuditLoggingEventId.SinkRetried, LogLevel.Debug,
		"Retrying Elasticsearch sink write (attempt {Attempt}) after {Delay}ms due to {StatusCode}")]
	private partial void LogAuditSinkRetry(int attempt, double delay, HttpStatusCode statusCode);
}
