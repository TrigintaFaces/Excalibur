// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using Excalibur.Compliance;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.AuditLogging.Elasticsearch;

/// <summary>
/// Writes audit events to Elasticsearch via the Bulk API in real-time.
/// </summary>
/// <remarks>
/// <para>
/// This is a lightweight, fire-and-forget audit event writer that indexes
/// individual audit events using the Elasticsearch Bulk API. Elasticsearch
/// serves as a search and analytics sink -- for compliance-grade tamper-evident
/// audit storage, use a SQL-backed <c>IAuditStore</c> instead.
/// </para>
/// </remarks>
internal sealed partial class ElasticsearchAuditSink
{
	private readonly HttpClient _httpClient;
	private readonly ElasticsearchAuditSinkOptions _options;
	private readonly ILogger<ElasticsearchAuditSink> _logger;
	private readonly Uri _bulkApiUri;

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

		// The request is built against the first node as a template; per-attempt node failover
		// (round-robin across the cluster) is applied by NodeFailoverHandler in the HttpClient pipeline.
		var nodeUrls = _options.GetResolvedNodeUrls();
		var baseUrl = nodeUrls[0].TrimEnd('/');
		_bulkApiUri = new Uri($"{baseUrl}/_bulk?refresh={_options.RefreshPolicy}");
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
			var response = await SendBulkAsync(ndjson, cancellationToken).ConfigureAwait(false);

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
#pragma warning disable CA1308 // Search index names must be lowercase
		var indexName = $"{_options.IndexPrefix.ToLowerInvariant()}-{auditEvent.Timestamp:yyyy.MM.dd}";
#pragma warning restore CA1308
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

	// Sends the bulk payload once. Transient-fault retry and per-attempt node failover are owned by
	// the HttpClient pipeline (standard resilience handler + NodeFailoverHandler), configured in DI.
	private async Task<HttpResponseMessage> SendBulkAsync(
		string ndjson,
		CancellationToken cancellationToken)
	{
		using var request = CreateRequest(ndjson);
		return await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
	}

	private HttpRequestMessage CreateRequest(string ndjson)
	{
		var request = new HttpRequestMessage(HttpMethod.Post, _bulkApiUri)
		{
			Content = new StringContent(ndjson, Encoding.UTF8, "application/x-ndjson")
		};

		if (!string.IsNullOrEmpty(_options.ApiKey))
		{
			request.Headers.Authorization = new AuthenticationHeaderValue("ApiKey", _options.ApiKey);
		}

		return request;
	}

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
}
