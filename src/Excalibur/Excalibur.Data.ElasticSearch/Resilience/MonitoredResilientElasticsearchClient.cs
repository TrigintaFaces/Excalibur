// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Elastic.Clients.Elasticsearch;
using Elastic.Transport;

using Excalibur.Data.ElasticSearch.Exceptions;
using Excalibur.Data.ElasticSearch.Monitoring;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.ElasticSearch.Resilience;

/// <summary>
/// Provides resilient Elasticsearch operations with comprehensive monitoring, metrics collection, and distributed tracing. This client
/// integrates resilience patterns with observability features for production-ready Elasticsearch operations.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="MonitoredResilientElasticsearchClient" /> class. </remarks>
/// <param name="client"> The underlying Elasticsearch client. </param>
/// <param name="retryPolicy"> The retry policy for handling transient failures. </param>
/// <param name="circuitBreaker"> The circuit breaker for preventing cascading failures. </param>
/// <param name="monitoringService"> The monitoring service for observability. </param>
/// <param name="options"> The Elasticsearch configuration options. </param>
/// <param name="logger"> The logger for diagnostic information. </param>
/// <exception cref="ArgumentNullException"> Thrown when any required parameter is null. </exception>
public sealed class MonitoredResilientElasticsearchClient(
	ElasticsearchClient client,
	IElasticsearchRetryPolicy retryPolicy,
	IElasticsearchCircuitBreaker circuitBreaker,
	ElasticsearchMonitoringService monitoringService,
	IOptions<ElasticsearchConfigurationOptions> options,
	ILogger<MonitoredResilientElasticsearchClient> logger) : IResilientElasticsearchClient, IDisposable
{
	private readonly ElasticsearchClient _client = client ?? throw new ArgumentNullException(nameof(client));
	private readonly IElasticsearchRetryPolicy _retryPolicy = retryPolicy ?? throw new ArgumentNullException(nameof(retryPolicy));

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

		using var timeout = new CancellationTokenSource(_resilienceSettings.Timeouts.SearchTimeout);
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
			cancellationToken: combinedToken.Token).ConfigureAwait(false);

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

		using var timeout = new CancellationTokenSource(_resilienceSettings.Timeouts.IndexTimeout);
		using var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

		var result = await ExecuteWithResilienceAsync(
			operation: () => _client.IndexAsync(request, combinedToken.Token),
			operationType: "index",
			indexName: indexName,
			monitoringContext: monitoringContext,
			createException: (ex, attempts) => new ElasticsearchIndexingException(
				"unknown", typeof(TDocument), $"Index operation failed after {attempts} attempts: {ex.Message}", ex),
			cancellationToken: combinedToken.Token).ConfigureAwait(false);

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

		using var timeout = new CancellationTokenSource(_resilienceSettings.Timeouts.IndexTimeout);
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
			cancellationToken: combinedToken.Token).ConfigureAwait(false);

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

		using var timeout = new CancellationTokenSource(_resilienceSettings.Timeouts.DeleteTimeout);
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
			cancellationToken: combinedToken.Token).ConfigureAwait(false);

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

		using var timeout = new CancellationTokenSource(_resilienceSettings.Timeouts.BulkTimeout);
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
			cancellationToken: combinedToken.Token).ConfigureAwait(false);

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

		using var timeout = new CancellationTokenSource(_resilienceSettings.Timeouts.SearchTimeout);
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
			cancellationToken: combinedToken.Token).ConfigureAwait(false);

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
	/// Determines whether an exception represents a transient failure that may succeed on retry.
	/// </summary>
	/// <param name="exception"> The exception to evaluate. </param>
	/// <returns> True if the exception is considered transient, false otherwise. </returns>
	private static bool IsTransientException(Exception exception) =>
		exception switch
		{
			// Network-related exceptions are generally transient
			HttpRequestException => true,
			TaskCanceledException => true,
			TimeoutException => true,

			// Elasticsearch transport exceptions may be transient
			TransportException te => te.ApiCallDetails?.HttpStatusCode switch
			{
				429 => true, // Too Many Requests
				502 => true, // Bad Gateway
				503 => true, // Service Unavailable
				504 => true, // Gateway Timeout
				_ => false,
			},

			// Default case - don't retry unless we know it's transient
			_ => false,
		};

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
	/// Executes an operation with full resilience patterns including retry and circuit breaker, with comprehensive monitoring and observability.
	/// </summary>
	/// <typeparam name="TResponse"> The type of response expected from the operation. </typeparam>
	/// <param name="operation"> The operation to execute. </param>
	/// <param name="operationType"> The type of operation for logging and monitoring. </param>
	/// <param name="indexName"> The name of the index being operated on. </param>
	/// <param name="monitoringContext"> The monitoring context for the operation. </param>
	/// <param name="createException"> Function to create an appropriate exception when all retries are exhausted. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The response from the successful operation. </returns>
	/// <exception cref="Exception"> Thrown when the operation fails after all resilience mechanisms are exhausted. </exception>
	/// <exception cref="InvalidOperationException"></exception>
	private async Task<TResponse> ExecuteWithResilienceAsync<TResponse>(
		Func<Task<TResponse>> operation,
		string operationType,
		string? indexName,
		ElasticsearchMonitoringService.ElasticsearchMonitoringContext monitoringContext,
		Func<Exception, int, Exception> createException,
		CancellationToken cancellationToken)
		where TResponse : TransportResponse
	{
		// Check circuit breaker state first
		if (_resilienceSettings.CircuitBreaker.Enabled && _circuitBreaker.IsOpen)
		{
			var circuitOpenMessage = $"Circuit breaker is open for {operationType} operations";
			_logger.LogWarning(circuitOpenMessage);
			throw new InvalidOperationException(circuitOpenMessage);
		}

		var attempts = 0;
		Exception? lastException = null;

		while (attempts < _resilienceSettings.Retry.MaxAttempts + 1) // +1 for initial attempt
		{
			attempts++;

			try
			{
				var result = await operation().ConfigureAwait(false);

				// Record successful operation with circuit breaker
				if (_resilienceSettings.CircuitBreaker.Enabled)
				{
					await _circuitBreaker.RecordSuccessAsync().ConfigureAwait(false);
				}

				// Check if response indicates a successful operation
				if (result is TransportResponse { ApiCallDetails.HttpStatusCode: >= 200 and < 300 } transportResponse)
				{
					_logger.LogDebug(
						"{OperationType} operation succeeded on attempt {Attempt}",
						operationType, attempts);
					return result;
				}

				// Response is not valid - treat as failure for retry logic
				var responseException = new InvalidOperationException($"{operationType} operation returned invalid response");
				lastException = responseException;

				// Record failure with circuit breaker and monitoring
				await RecordFailureAsync(operationType, responseException, indexName).ConfigureAwait(false);

				// Check if we should retry
				if (!ShouldRetry(responseException, attempts))
				{
					break;
				}
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				lastException = ex;

				// Record failure with circuit breaker and monitoring
				await RecordFailureAsync(operationType, ex, indexName).ConfigureAwait(false);

				_logger.LogWarning(ex, "{OperationType} operation failed on attempt {Attempt}",
					operationType, attempts);

				// Check if we should retry
				if (!ShouldRetry(ex, attempts))
				{
					break;
				}

				// Apply retry delay with exponential backoff and jitter
				if (attempts <= _resilienceSettings.Retry.MaxAttempts)
				{
					var delay = _retryPolicy.GetRetryDelay(attempts - 1);
					_logger.LogDebug(
						"Retrying {OperationType} operation in {Delay}ms",
						operationType, delay.TotalMilliseconds);

					// Record retry attempt in monitoring
					_monitoringService.RecordRetryAttempt(
						operationType, attempts, _resilienceSettings.Retry.MaxAttempts, delay, ex, indexName, monitoringContext.Activity);

					await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
				}
			}
			catch (OperationCanceledException)
			{
				_logger.LogInformation("{OperationType} operation was cancelled", operationType);
				throw;
			}
		}

		// All retries exhausted - log the failure
		if (lastException is not null)
		{
			_logger.LogError(
				lastException,
				"{OperationType} operation permanently failed after {Attempts} attempts on index {IndexName}",
				operationType, attempts, indexName ?? "unknown");

			// Record permanent failure in monitoring
			_monitoringService.RecordPermanentFailure(operationType, lastException, indexName);
		}

		// Create and throw appropriate exception
		var finalException = lastException is not null
			? createException(lastException, attempts)
			: new InvalidOperationException($"{operationType} operation failed without exception details");

		_logger.LogError(finalException, "{OperationType} operation failed after {Attempts} attempts",
			operationType, attempts);

		throw finalException;
	}

	/// <summary>
	/// Records a failure with both circuit breaker and monitoring systems.
	/// </summary>
	/// <param name="operationType"> The type of operation that failed. </param>
	/// <param name="exception"> The exception that occurred. </param>
	/// <param name="indexName"> The name of the index being operated on. </param>
	/// <returns> A task representing the failure recording operation. </returns>
	private async Task RecordFailureAsync(string operationType, Exception exception, string? indexName)
	{
		// Record failure with circuit breaker
		if (_resilienceSettings.CircuitBreaker.Enabled)
		{
			var wasOpen = _circuitBreaker.IsOpen;
			await _circuitBreaker.RecordFailureAsync().ConfigureAwait(false);
			var isNowOpen = _circuitBreaker.IsOpen;

			// Record circuit breaker state change if it occurred
			if (!wasOpen && isNowOpen)
			{
				_monitoringService.RecordCircuitBreakerStateChange("closed", "open", operationType);
			}
		}
	}

	/// <summary>
	/// Determines whether an operation should be retried based on the exception and attempt count.
	/// </summary>
	/// <param name="exception"> The exception that occurred. </param>
	/// <param name="attemptNumber"> The current attempt number (1-based). </param>
	/// <returns> True if the operation should be retried, false otherwise. </returns>
	private bool ShouldRetry(Exception exception, int attemptNumber)
	{
		// Don't retry if retries are disabled
		if (!_resilienceSettings.Retry.Enabled)
		{
			return false;
		}

		// Don't retry if we've reached max attempts
		if (attemptNumber >= _resilienceSettings.Retry.MaxAttempts + 1)
		{
			return false;
		}

		// Don't retry certain non-transient errors
		return IsTransientException(exception);
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
