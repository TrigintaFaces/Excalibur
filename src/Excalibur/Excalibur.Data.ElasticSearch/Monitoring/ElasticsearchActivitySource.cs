// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Elastic.Clients.Elasticsearch;
using Elastic.Transport.Products.Elasticsearch;

namespace Excalibur.Data.ElasticSearch.Monitoring;

/// <summary>
/// Provides distributed tracing capabilities for Elasticsearch operations following OpenTelemetry semantic conventions.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ElasticsearchActivitySource" /> class.
/// </remarks>
/// <param name="name"> The name of the activity source. </param>
/// <param name="version"> The version of the activity source. </param>
public sealed class ElasticsearchActivitySource(string name = "Excalibur.Data.ElasticSearch", string? version = null) : IDisposable
{
	private volatile bool _disposed;

	/// <summary>
	/// Gets the underlying ActivitySource instance.
	/// </summary>
	/// <value>
	/// The underlying ActivitySource instance.
	/// </value>
	public ActivitySource Source { get; } = new ActivitySource(name, version);

	/// <summary>
	/// Enriches an activity with request details.
	/// </summary>
	/// <param name="activity"> The activity to enrich. </param>
	/// <param name="request"> The Elasticsearch request. </param>
	/// <param name="recordRequestBody"> Whether to record the request body. </param>
	[RequiresUnreferencedCode("Reflection and JSON serialization may require unreferenced types for dynamic type inspection and serialization")]
	[RequiresDynamicCode("Reflection and JSON serialization use dynamic code to inspect types and serialize objects")]
	public static void EnrichWithRequestDetails(Activity? activity, object request, bool recordRequestBody = false)
	{
		if (activity == null)
		{
			return;
		}

		// Record request type
		_ = activity.SetTag("elasticsearch.request.type", request.GetType().Name);

		// Record request routing information if available using reflection
		var requestType = request.GetType();
		var routeValuesProperty = requestType.GetProperty("RouteValues");

		if (routeValuesProperty?.GetValue(request) is IDictionary<string, object> routeValues)
		{
			if (routeValues.TryGetValue("index", out var value))
			{
				_ = activity.SetTag("elasticsearch.request.index", value);
			}

			if (routeValues.TryGetValue("id", out var routeValue))
			{
				_ = activity.SetTag("elasticsearch.request.document_id", routeValue);
			}
		}

		// Record request body if requested (be careful with sensitive data)
		if (recordRequestBody && request is not null)
		{
			try
			{
				var requestBody = JsonSerializer.Serialize(request);
				if (requestBody.Length <= 1024) // Limit body size
				{
					_ = activity.SetTag("elasticsearch.request.body", requestBody);
				}
				else
				{
					_ = activity.SetTag("elasticsearch.request.body_size", requestBody.Length);
				}
			}
			catch
			{
				// Ignore serialization errors
			}
		}
	}

	/// <summary>
	/// Enriches an activity with response details.
	/// </summary>
	/// <param name="activity"> The activity to enrich. </param>
	/// <param name="response"> The Elasticsearch response. </param>
	/// <param name="recordResponseBody"> Whether to record the response body. </param>
	[RequiresUnreferencedCode("Reflection and JSON serialization may require unreferenced types for dynamic type inspection and serialization")]
	[RequiresDynamicCode("Reflection and JSON serialization use dynamic code to inspect types and serialize objects")]
	public static void EnrichWithResponseDetails(Activity? activity, ElasticsearchResponse response, bool recordResponseBody = false)
	{
		if (activity == null)
		{
			return;
		}

		// Record response status
		_ = activity.SetTag("elasticsearch.response.success", response.IsValidResponse);

		if (response.ApiCallDetails != null)
		{
			_ = activity.SetTag("http.status_code", response.ApiCallDetails.HttpStatusCode ?? 0);
			_ = activity.SetTag("http.method", response.ApiCallDetails.HttpMethod.ToString());
			_ = activity.SetTag("url.full", response.ApiCallDetails.Uri?.ToString());
		}

		// Record response timing if available
		var exception = response.ApiCallDetails?.OriginalException;
		if (!response.IsValidResponse && exception != null)
		{
			_ = activity.SetTag("elasticsearch.error.type", exception.GetType().Name);
			_ = activity.SetTag("elasticsearch.error.reason", exception.Message);
			_ = activity.SetStatus(ActivityStatusCode.Error, exception.Message);
		}

		// Record specific response metrics based on response type
		switch (response)
		{
			case SearchResponse<object> { IsValidResponse: true } searchResponse:
				var totalHits = searchResponse.HitsMetadata?.Total?.Match(
					static totalHits => totalHits?.Value ?? 0,
					static longValue => longValue) ?? 0;
				_ = activity.SetTag("elasticsearch.hits.total", totalHits);
				_ = activity.SetTag("elasticsearch.took", searchResponse.Took);
				break;

			case BulkResponse { IsValidResponse: true } bulkResponse:
				_ = activity.SetTag("elasticsearch.bulk.items", bulkResponse.Items.Count);
				_ = activity.SetTag("elasticsearch.bulk.errors", bulkResponse.Errors);
				_ = activity.SetTag("elasticsearch.took", bulkResponse.Took);
				break;

			case IndexResponse { IsValidResponse: true } indexResponse:
				_ = activity.SetTag("elasticsearch.document.result", indexResponse.Result.ToString());
				_ = activity.SetTag("elasticsearch.document.version", indexResponse.Version);
				break;
			default:
				break;
		}

		// Record response body if requested and response is small enough
		if (recordResponseBody && response.IsValidResponse)
		{
			try
			{
				var responseBody = JsonSerializer.Serialize(response);
				if (responseBody.Length <= 1024) // Limit body size
				{
					_ = activity.SetTag("elasticsearch.response.body", responseBody);
				}
				else
				{
					_ = activity.SetTag("elasticsearch.response.body_size", responseBody.Length);
				}
			}
			catch
			{
				// Ignore serialization errors
			}
		}
	}

	/// <summary>
	/// Enriches an activity with retry attempt details.
	/// </summary>
	/// <param name="activity"> The activity to enrich. </param>
	/// <param name="attemptNumber"> The current attempt number. </param>
	/// <param name="maxAttempts"> The maximum number of attempts. </param>
	/// <param name="delay"> The delay before this attempt. </param>
	/// <param name="exception"> The exception that triggered the retry. </param>
	public static void EnrichWithRetryDetails(
		Activity? activity,
		int attemptNumber,
		int maxAttempts,
		TimeSpan? delay = null,
		Exception? exception = null)
	{
		if (activity == null)
		{
			return;
		}

		_ = activity.SetTag("elasticsearch.retry.attempt", attemptNumber);
		_ = activity.SetTag("elasticsearch.retry.max_attempts", maxAttempts);

		if (delay.HasValue)
		{
			_ = activity.SetTag("elasticsearch.retry.delay_ms", delay.Value.TotalMilliseconds);
		}

		if (exception != null)
		{
			_ = activity.SetTag("elasticsearch.retry.exception.type", exception.GetType().Name);
			_ = activity.SetTag("elasticsearch.retry.exception.message", exception.Message);
		}
	}

	/// <summary>
	/// Enriches an activity with circuit breaker details.
	/// </summary>
	/// <param name="activity"> The activity to enrich. </param>
	/// <param name="state"> The current circuit breaker state. </param>
	/// <param name="failureCount"> The current failure count. </param>
	/// <param name="successCount"> The current success count. </param>
	public static void EnrichWithCircuitBreakerDetails(
		Activity? activity,
		string state,
		int? failureCount = null,
		int? successCount = null)
	{
		if (activity == null)
		{
			return;
		}

		_ = activity.SetTag("elasticsearch.circuit_breaker.state", state);

		if (failureCount.HasValue)
		{
			_ = activity.SetTag("elasticsearch.circuit_breaker.failure_count", failureCount.Value);
		}

		if (successCount.HasValue)
		{
			_ = activity.SetTag("elasticsearch.circuit_breaker.success_count", successCount.Value);
		}
	}

	/// <summary>
	/// Starts a new activity for an Elasticsearch operation.
	/// </summary>
	/// <param name="operationType"> The type of operation (e.g., "search", "index", "delete"). </param>
	/// <param name="indexName"> The name of the index being operated on. </param>
	/// <param name="documentId"> The document ID for single-document operations. </param>
	/// <returns> A new activity if tracing is enabled, otherwise null. </returns>
	public Activity? StartOperation(string operationType, string? indexName = null, string? documentId = null)
	{
		ThrowIfDisposed();

		var operationName = $"elasticsearch.{operationType.ToLowerInvariant()}";
		var activity = Source.StartActivity(operationName);

		if (activity != null)
		{
			// Set OpenTelemetry semantic convention attributes
			_ = activity.SetTag("db.system", "elasticsearch");
			_ = activity.SetTag("db.operation", operationType);

			if (!string.IsNullOrWhiteSpace(indexName))
			{
				_ = activity.SetTag("db.name", indexName);
				_ = activity.SetTag("elasticsearch.index.name", indexName);
			}

			if (!string.IsNullOrWhiteSpace(documentId))
			{
				_ = activity.SetTag("elasticsearch.document.id", documentId);
			}

			// Set additional Elasticsearch-specific attributes
			_ = activity.SetTag("elasticsearch.operation.type", operationType);
		}

		return activity;
	}

	/// <summary>
	/// Starts a new activity for a resilience operation (retry, circuit breaker).
	/// </summary>
	/// <param name="resilienceType"> The type of resilience operation (e.g., "retry", "circuit_breaker"). </param>
	/// <param name="parentOperationType"> The parent operation type being protected. </param>
	/// <param name="additionalAttributes"> Additional attributes to add to the activity. </param>
	/// <returns> A new activity if tracing is enabled, otherwise null. </returns>
	public Activity? StartResilienceOperation(
		string resilienceType,
		string? parentOperationType = null,
		Dictionary<string, object>? additionalAttributes = null)
	{
		ThrowIfDisposed();

		var operationName = $"elasticsearch.resilience.{resilienceType.ToLowerInvariant()}";
		var activity = Source.StartActivity(operationName);

		if (activity != null)
		{
			_ = activity.SetTag("elasticsearch.resilience.type", resilienceType);

			if (!string.IsNullOrWhiteSpace(parentOperationType))
			{
				_ = activity.SetTag("elasticsearch.parent_operation.type", parentOperationType);
			}

			if (additionalAttributes != null)
			{
				foreach (var kvp in additionalAttributes)
				{
					_ = activity.SetTag(kvp.Key, kvp.Value);
				}
			}
		}

		return activity;
	}

	/// <inheritdoc />
	public void Dispose()
	{
		if (!_disposed)
		{
			Source.Dispose();
			_disposed = true;
		}
	}

	/// <summary>
	/// Throws an <see cref="ObjectDisposedException" /> if the activity source has been disposed.
	/// </summary>
	/// <exception cref="ObjectDisposedException"> Thrown when the instance has been disposed. </exception>
	private void ThrowIfDisposed()
	{
		if (_disposed)
		{
			throw new ObjectDisposedException(nameof(ElasticsearchActivitySource));
		}
	}
}
