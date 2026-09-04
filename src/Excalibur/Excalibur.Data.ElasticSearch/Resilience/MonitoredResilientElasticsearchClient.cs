// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Elastic.Clients.Elasticsearch;
using Elastic.Transport;

using Excalibur.Data.ElasticSearch.Exceptions;
using Excalibur.Data.ElasticSearch.Monitoring;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Polly.CircuitBreaker;

namespace Excalibur.Data.ElasticSearch.Resilience;

#pragma warning disable IL2026, IL3050, IL2075 // This class inherently uses reflection for Elasticsearch request inspection and monitoring

/// <summary>
/// Provides resilient Elasticsearch operations with comprehensive monitoring, metrics collection, and distributed tracing. This client
/// integrates resilience patterns with observability features for production-ready Elasticsearch operations.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="MonitoredResilientElasticsearchClient" /> class. </remarks>
/// <param name="client"> The underlying Elasticsearch client. </param>
/// <param name="pipeline"> The retry and circuit-breaker pipeline every call runs through. </param>
/// <param name="circuitBreaker"> The circuit breaker for preventing cascading failures. </param>
/// <param name="monitoringService"> The monitoring service for observability. </param>
/// <param name="options"> The Elasticsearch configuration options. </param>
/// <param name="logger"> The logger for diagnostic information. </param>
/// <param name="timeProvider">
/// The time provider used to schedule operation timeouts. Defaults to <see cref="TimeProvider.System" />. Supplying a
/// controllable provider allows timeout behavior to be exercised deterministically instead of racing the wall clock.
/// </param>
/// <exception cref="ArgumentNullException"> Thrown when any required parameter is null. </exception>
internal sealed class MonitoredResilientElasticsearchClient(
	ElasticsearchClient client,
	ElasticsearchResiliencePipeline pipeline,
	IElasticsearchCircuitBreaker circuitBreaker,
	ElasticsearchMonitoringService monitoringService,
	IOptions<ElasticsearchConfigurationOptions> options,
	ILogger<MonitoredResilientElasticsearchClient> logger,
	TimeProvider? timeProvider = null) : IResilientElasticsearchClient, IDisposable
{
	private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

	private readonly ElasticsearchClient _client = client ?? throw new ArgumentNullException(nameof(client));
	private readonly ElasticsearchResiliencePipeline _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));

	private readonly IElasticsearchCircuitBreaker _circuitBreaker =
		circuitBreaker ?? throw new ArgumentNullException(nameof(circuitBreaker));

	private readonly ElasticsearchMonitoringService _monitoringService =
		monitoringService ?? throw new ArgumentNullException(nameof(monitoringService));

	private readonly ElasticsearchResilienceOptions _resilienceSettings =
		options?.Value?.Resilience ?? throw new ArgumentNullException(nameof(options));

	private readonly ILogger<MonitoredResilientElasticsearchClient> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
	private volatile bool _disposed;

	/// <inheritdoc />
	public bool IsCircuitBreakerOpen => _circuitBreaker.IsOpen;

	/// <inheritdoc />
	public async Task<SearchResponse<TDocument>> SearchAsync<TDocument>(
		SearchRequest request,
		CancellationToken cancellationToken)
	{
		ThrowIfDisposed();

		var indexName = ExtractIndexName(request);
		using var monitoringContext = _monitoringService.StartOperation("search", request, indexName);

		if (!_resilienceSettings.Enabled)
		{
			var response = await _client.SearchAsync<TDocument>(request, cancellationToken).ConfigureAwait(false);
			monitoringContext.Complete(response);
			return response;
		}

		using var timeout = new CancellationTokenSource(_resilienceSettings.Timeouts.SearchTimeout, _timeProvider);
		using var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

		var result = await ExecuteWithResilienceAsync(
			operation: () => _client.SearchAsync<TDocument>(request, combinedToken.Token),
			operationType: "search",
			indexName: indexName,
			monitoringContext: monitoringContext,
			createException: (ex, attempts) => new ElasticsearchSearchException(
				indexName ?? "unknown",
				typeof(TDocument),
				$"Search operation failed after {attempts} attempts: {ex.Message}",
				ex),
			cancellationToken: combinedToken.Token,
			callerToken: cancellationToken).ConfigureAwait(false);

		var documentCount = result.IsValidResponse
			? result.HitsMetadata?.Total?.Match(
				totalHits => totalHits != null ? totalHits.Value : null,
				longValue => (long?)longValue)
			: null;
		monitoringContext.Complete(result, documentCount);
		return result;
	}

	/// <inheritdoc />
	public async Task<IndexResponse> IndexAsync<TDocument>(
		IndexRequest<TDocument> request,
		CancellationToken cancellationToken)
	{
		ThrowIfDisposed();

		var indexName = ExtractIndexName(request);
		var documentId = ExtractDocumentId(request);
		using var monitoringContext = _monitoringService.StartOperation("index", request, indexName, documentId);

		if (!_resilienceSettings.Enabled)
		{
			var response = await _client.IndexAsync(request, cancellationToken).ConfigureAwait(false);
			monitoringContext.Complete(response, 1);
			return response;
		}

		using var timeout = new CancellationTokenSource(_resilienceSettings.Timeouts.IndexTimeout, _timeProvider);
		using var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

		var result = await ExecuteWithResilienceAsync(
			operation: () => _client.IndexAsync(request, combinedToken.Token),
			operationType: "index",
			indexName: indexName,
			monitoringContext: monitoringContext,
			createException: (ex, attempts) => new ElasticsearchIndexingException(
				"unknown", typeof(TDocument), $"Index operation failed after {attempts} attempts: {ex.Message}", ex),
			cancellationToken: combinedToken.Token,
			callerToken: cancellationToken).ConfigureAwait(false);

		monitoringContext.Complete(result, result.IsValidResponse ? 1 : null);
		return result;
	}

	/// <inheritdoc />
	public async Task<UpdateResponse<TDocument>> UpdateAsync<TDocument>(
		UpdateRequest<TDocument, object> request,
		CancellationToken cancellationToken)
	{
		ThrowIfDisposed();

		var indexName = ExtractIndexName(request);
		var documentId = ExtractDocumentId(request);
		using var monitoringContext = _monitoringService.StartOperation("update", request, indexName, documentId);

		if (!_resilienceSettings.Enabled)
		{
			var response = await _client.UpdateAsync(request, cancellationToken).ConfigureAwait(false);
			monitoringContext.Complete(response, 1);
			return response;
		}

		using var timeout = new CancellationTokenSource(_resilienceSettings.Timeouts.IndexTimeout, _timeProvider);
		using var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

		var result = await ExecuteWithResilienceAsync(
			operation: () => _client.UpdateAsync(request, combinedToken.Token),
			operationType: "update",
			indexName: indexName,
			monitoringContext: monitoringContext,
			createException: (ex, attempts) => new ElasticsearchUpdateException(
				indexName ?? "unknown",
				typeof(TDocument),
				$"Update operation failed after {attempts} attempts: {ex.Message}",
				ex),
			cancellationToken: combinedToken.Token,
			callerToken: cancellationToken).ConfigureAwait(false);

		monitoringContext.Complete(result, result.IsValidResponse ? 1 : null);
		return result;
	}

	/// <inheritdoc />
	public async Task<DeleteResponse> DeleteAsync(
		DeleteRequest request,
		CancellationToken cancellationToken)
	{
		ThrowIfDisposed();

		var indexName = ExtractIndexName(request);
		var documentId = ExtractDocumentId(request);
		using var monitoringContext = _monitoringService.StartOperation("delete", request, indexName, documentId);

		if (!_resilienceSettings.Enabled)
		{
			var response = await _client.DeleteAsync(request, cancellationToken).ConfigureAwait(false);
			monitoringContext.Complete(response, 1);
			return response;
		}

		using var timeout = new CancellationTokenSource(_resilienceSettings.Timeouts.DeleteTimeout, _timeProvider);
		using var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

		var result = await ExecuteWithResilienceAsync(
			operation: () => _client.DeleteAsync(request, combinedToken.Token),
			operationType: "delete",
			indexName: indexName,
			monitoringContext: monitoringContext,
			createException: (ex, attempts) => new ElasticsearchDeleteException(
				documentId ?? "unknown",
				typeof(object),
				$"Delete operation failed after {attempts} attempts: {ex.Message}",
				ex),
			cancellationToken: combinedToken.Token,
			callerToken: cancellationToken).ConfigureAwait(false);

		monitoringContext.Complete(result, result.IsValidResponse ? 1 : null);
		return result;
	}

	/// <inheritdoc />
	public async Task<BulkResponse> BulkAsync(
		BulkRequest request,
		CancellationToken cancellationToken)
	{
		ThrowIfDisposed();

		var indexName = ExtractIndexName(request);
		using var monitoringContext = _monitoringService.StartOperation("bulk", request, indexName);

		if (!_resilienceSettings.Enabled)
		{
			var response = await _client.BulkAsync(request, cancellationToken).ConfigureAwait(false);
			var documentCount = response.IsValidResponse ? response.Items.Count : (int?)null;
			monitoringContext.Complete(response, documentCount);
			return response;
		}

		using var timeout = new CancellationTokenSource(_resilienceSettings.Timeouts.BulkTimeout, _timeProvider);
		using var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

		var result = await ExecuteWithResilienceAsync(
			operation: () => _client.BulkAsync(request, combinedToken.Token),
			operationType: "bulk",
			indexName: indexName,
			monitoringContext: monitoringContext,
			createException: (ex, attempts) => new ElasticsearchIndexingException(
				indexName ?? "unknown",
				typeof(object),
				$"Bulk operation failed after {attempts} attempts: {ex.Message}",
				ex),
			cancellationToken: combinedToken.Token,
			callerToken: cancellationToken).ConfigureAwait(false);

		var bulkDocumentCount = result.IsValidResponse ? result.Items.Count : (int?)null;
		monitoringContext.Complete(result, bulkDocumentCount);
		return result;
	}

	/// <inheritdoc />
	public async Task<GetResponse<TDocument>> GetAsync<TDocument>(
		GetRequest request,
		CancellationToken cancellationToken)
	{
		ThrowIfDisposed();

		var indexName = ExtractIndexName(request);
		var documentId = ExtractDocumentId(request);
		using var monitoringContext = _monitoringService.StartOperation("get", request, indexName, documentId);

		if (!_resilienceSettings.Enabled)
		{
			var response = await _client.GetAsync<TDocument>(request, cancellationToken).ConfigureAwait(false);
			monitoringContext.Complete(response, response is { IsValidResponse: true, Found: true } ? 1 : 0);
			return response;
		}

		using var timeout = new CancellationTokenSource(_resilienceSettings.Timeouts.SearchTimeout, _timeProvider);
		using var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

		var result = await ExecuteWithResilienceAsync(
			operation: () => _client.GetAsync<TDocument>(request, combinedToken.Token),
			operationType: "get",
			indexName: indexName,
			monitoringContext: monitoringContext,
			createException: (ex, attempts) => new ElasticsearchGetByIdException(
				documentId ?? "unknown",
				typeof(TDocument),
				$"Get operation failed after {attempts} attempts: {ex.Message}",
				ex),
			cancellationToken: combinedToken.Token,
			callerToken: cancellationToken).ConfigureAwait(false);

		var getDocumentCount = result is { IsValidResponse: true, Found: true } ? 1 : 0;
		monitoringContext.Complete(result, getDocumentCount);
		return result;
	}

	/// <inheritdoc />
	public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken)
	{
		ThrowIfDisposed();

		using var monitoringContext = _monitoringService.StartOperation("health_check", new { });

		try
		{
			var healthResponse = await _client.Cluster.HealthAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
			var isHealthy = healthResponse.IsValidResponse &&
							healthResponse.Status != HealthStatus.Red;

			monitoringContext.Complete(healthResponse);
			return isHealthy;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Health check operation failed");

			// Cannot pass error response to Complete, just return false
			return false;
		}
	}

	/// <inheritdoc />
	public void Dispose()
	{
		if (!_disposed)
		{
			_circuitBreaker?.Dispose();
			_disposed = true;
		}
	}

	/// <summary>
	/// Extracts the index name from an Elasticsearch request.
	/// </summary>
	/// <param name="request"> The Elasticsearch request. </param>
	/// <returns> The index name if available, otherwise null. </returns>
	private static string? ExtractIndexName(object request)
	{
		// Use reflection to extract index information since IRequest interface is no longer available
		var requestType = request.GetType();
		var routeValuesProperty = requestType.GetProperty("RouteValues");

		if (routeValuesProperty?.GetValue(request) is IDictionary<string, object> routeValues &&
			routeValues.TryGetValue("index", out var value))
		{
			return value?.ToString();
		}

		return null;
	}

	/// <summary>
	/// Extracts the document ID from an Elasticsearch request.
	/// </summary>
	/// <param name="request"> The Elasticsearch request. </param>
	/// <returns> The document ID if available, otherwise null. </returns>
	private static string? ExtractDocumentId(object request)
	{
		// Use reflection to extract document ID information since IRequest interface is no longer available
		var requestType = request.GetType();
		var routeValuesProperty = requestType.GetProperty("RouteValues");

		if (routeValuesProperty?.GetValue(request) is IDictionary<string, object> routeValues &&
			routeValues.TryGetValue("id", out var value))
		{
			return value?.ToString();
		}

		return null;
	}


	/// <summary>
	/// Builds the exception reported when the operation's own timeout budget elapses, as opposed to the caller
	/// cancelling. The <see cref="TimeoutException" /> is carried as the inner exception so callers can
	/// distinguish "it timed out" from "it failed", mirroring how <c>HttpClient</c> reports its own timeout.
	/// </summary>
	private static Exception CreateTimeoutException(
		Func<Exception, int, Exception> createException,
		string operationType,
		int attempts,
		Exception? inner = null)
	{
		var timeoutException = inner is null
			? new TimeoutException($"{operationType} operation timed out after {attempts} attempt(s).")
			: new TimeoutException($"{operationType} operation timed out after {attempts} attempt(s).", inner);

		return createException(timeoutException, attempts);
	}

	/// <summary>
	/// Executes an operation with full resilience patterns including retry and circuit breaker, with comprehensive monitoring and observability.
	/// </summary>
	/// <typeparam name="TResponse"> The type of response expected from the operation. </typeparam>
	/// <param name="operation"> The operation to execute. </param>
	/// <param name="operationType"> The type of operation for logging and monitoring. </param>
	/// <param name="indexName"> The name of the index being operated on. </param>
	/// <param name="monitoringContext"> The monitoring context for the operation. </param>
	/// <param name="createException"> Function to create an appropriate exception when all retries are exhausted. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <param name="callerToken">
	/// The caller-supplied token, used to distinguish caller cancellation (propagated unchanged) from the operation
	/// timeout elapsing (surfaced as a <see cref="TimeoutException" /> wrapped in the operation's domain exception).
	/// </param>
	/// <returns> The response from the successful operation. </returns>
	/// <exception cref="Exception"> Thrown when the operation fails after all resilience mechanisms are exhausted. </exception>
	/// <exception cref="InvalidOperationException"></exception>
	private async Task<TResponse> ExecuteWithResilienceAsync<TResponse>(
		Func<Task<TResponse>> operation,
		string operationType,
		string? indexName,
		ElasticsearchMonitoringService.ElasticsearchMonitoringContext monitoringContext,
		Func<Exception, int, Exception> createException,
		CancellationToken cancellationToken,
		CancellationToken callerToken)
		where TResponse : TransportResponse
	{
		var attempts = 0;

		try
		{
			return await _pipeline.ExecuteAsync(
				async _ =>
				{
					attempts++;

					// Polly owns the retry loop now, so an attempt beyond the first IS a retry. Recording
					// it here keeps per-attempt telemetry that would otherwise only appear once, after the
					// ladder had already been walked.
					if (attempts > 1)
					{
						_monitoringService.RecordRetryAttempt(
							operationType,
							attempts - 1,
							_resilienceSettings.Retry.MaxAttempts,
							TimeSpan.Zero,
							exception: null,
							indexName,
							monitoringContext.Activity);
					}

					// The Elasticsearch client does not reliably surface a cancelled token as an
					// OperationCanceledException; it can return an invalid response instead. The tokens
					// are inspected directly so a cancellation is not mistaken for a retryable failure.
					// Throwing (rather than pre-checking and building the final exception here) lets this
					// join the SAME catch clause below as a mid-flight cancellation, so it is wrapped
					// exactly once instead of twice.
					callerToken.ThrowIfCancellationRequested();
					cancellationToken.ThrowIfCancellationRequested();

					var result = await operation().ConfigureAwait(false);

					var isValid = result is Elastic.Transport.Products.Elasticsearch.ElasticsearchResponse esResponse
						? esResponse.IsValidResponse
						: result.ApiCallDetails?.HasSuccessfulStatusCode == true;

					if (!isValid)
					{
						var invalidResponseException =
							new ElasticsearchInvalidResponseException(operationType, result.ApiCallDetails?.HttpStatusCode);
						ReportFailure(operationType, invalidResponseException, indexName);

						// An unsuccessful response is a failure the pipeline must see. Returning it
						// would hide the outcome from both the retry and the breaker, so the call
						// would neither be retried nor counted.
						throw invalidResponseException;
					}

					_logger.LogDebug(
						"{OperationType} operation succeeded on attempt {Attempt}",
						operationType, attempts);

					return result;
				},
				callerToken).ConfigureAwait(false);
		}
		catch (BrokenCircuitException)
		{
			_logger.LogWarning("Circuit breaker is open for {OperationType} operations", operationType);
			throw new InvalidOperationException($"Circuit breaker is open for {operationType} operations");
		}
		catch (OperationCanceledException) when (!callerToken.IsCancellationRequested)
		{
			throw CreateTimeoutException(createException, operationType, attempts);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			ReportFailure(operationType, ex, indexName);
			_monitoringService.RecordRetryAttempt(
				operationType, attempts, _resilienceSettings.Retry.MaxAttempts,
				TimeSpan.Zero, ex, indexName, monitoringContext.Activity);
			_logger.LogWarning(ex, "{OperationType} operation failed after {Attempts} attempt(s)",
				operationType, attempts);
			throw createException(ex, attempts);
		}
	}

	/// <summary>
	/// Reports a failed operation to the monitoring system.
	/// </summary>
	/// <param name="operationType"> The type of operation that failed. </param>
	/// <param name="exception"> The exception that occurred. </param>
	/// <param name="indexName"> The name of the index being operated on. </param>
	private void ReportFailure(string operationType, Exception exception, string? indexName)
	{
		// The pipeline records the outcome and opens the circuit itself, so this reports the
		// resulting state rather than driving it. Comparing before and against after here would
		// compare two reads of a transition that already happened inside the call.
		if (_resilienceSettings.CircuitBreaker.Enabled && _circuitBreaker.IsOpen)
		{
			_monitoringService.RecordCircuitBreakerStateChange("closed", "open", operationType);
		}
	}

	/// <summary>
	/// Throws an <see cref="ObjectDisposedException" /> if the client has been disposed.
	/// </summary>
	/// <exception cref="ObjectDisposedException"> Thrown when the client has been disposed. </exception>
	private void ThrowIfDisposed()
	{
		if (_disposed)
		{
			throw new ObjectDisposedException(nameof(MonitoredResilientElasticsearchClient));
		}
	}

	/// <summary>
	/// Represents a simplified health check response for monitoring purposes.
	/// </summary>
	private sealed class HealthCheckResponse(bool isValid, ApiCallDetails? apiCallDetails, Exception? serverError)
	{
		public HealthCheckResponse()
			: this(isValid: false, apiCallDetails: null, serverError: null)
		{
		}

		public bool IsValidResponse { get; init; } = isValid;

		public ApiCallDetails? ApiCallDetails { get; init; } = apiCallDetails;

		public Exception? ElasticsearchServerError { get; init; } = serverError;

		public Exception? Exception { get; init; }

		public HealthStatus? Status { get; init; }
	}
}
