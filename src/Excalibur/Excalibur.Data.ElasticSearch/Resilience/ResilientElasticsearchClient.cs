// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Elastic.Clients.Elasticsearch;
using Elastic.Transport;

using Excalibur.Data.ElasticSearch.Exceptions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.ElasticSearch.Resilience;

/// <summary>
/// Provides resilient Elasticsearch operations with retry policies and circuit breaker.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ResilientElasticsearchClient" /> class.
/// </remarks>
/// <param name="client"> The underlying Elasticsearch client. </param>
/// <param name="retryPolicy"> The retry policy for handling transient failures. </param>
/// <param name="circuitBreaker"> The circuit breaker for preventing cascading failures. </param>
/// <param name="options"> The resilience configuration options. </param>
/// <param name="logger"> The logger for diagnostic information. </param>
/// <exception cref="ArgumentNullException"> Thrown when any required parameter is null. </exception>
public sealed class ResilientElasticsearchClient(
	ElasticsearchClient client,
	IElasticsearchRetryPolicy retryPolicy,
	IElasticsearchCircuitBreaker circuitBreaker,
	IOptions<ElasticsearchConfigurationOptions> options,
	ILogger<ResilientElasticsearchClient> logger) : IResilientElasticsearchClient, IDisposable
{
	private readonly ElasticsearchClient _client = client ?? throw new ArgumentNullException(nameof(client));
	private readonly IElasticsearchRetryPolicy _retryPolicy = retryPolicy ?? throw new ArgumentNullException(nameof(retryPolicy));

	private readonly IElasticsearchCircuitBreaker _circuitBreaker =
		circuitBreaker ?? throw new ArgumentNullException(nameof(circuitBreaker));

	private readonly ElasticsearchResilienceOptions _settings =
		options?.Value?.Resilience ?? throw new ArgumentNullException(nameof(options));

	private readonly ILogger<ResilientElasticsearchClient> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
	private volatile bool _disposed;

	/// <inheritdoc />
	public bool IsCircuitBreakerOpen => _circuitBreaker.IsOpen;

	/// <inheritdoc />
	public async Task<SearchResponse<TDocument>> SearchAsync<TDocument>(
		SearchRequest request,
		CancellationToken cancellationToken)
	{
		ThrowIfDisposed();

		if (!_settings.Enabled)
		{
			return await _client.SearchAsync<TDocument>(request, cancellationToken).ConfigureAwait(false);
		}

		using var timeout = new CancellationTokenSource(_settings.Timeouts.SearchTimeout);
		using var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

		return await ExecuteWithResilienceAsync(
			operation: () => _client.SearchAsync<TDocument>(request, combinedToken.Token),
			operationType: "Search",
			createException: (ex, attempts) => new ElasticsearchSearchException(
				"unknown", typeof(TDocument), $"Search operation failed after {attempts} attempts: {ex.Message}", ex),
			cancellationToken: combinedToken.Token).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<IndexResponse> IndexAsync<TDocument>(
		IndexRequest<TDocument> request,
		CancellationToken cancellationToken)
	{
		ThrowIfDisposed();

		if (!_settings.Enabled)
		{
			return await _client.IndexAsync(request, cancellationToken).ConfigureAwait(false);
		}

		using var timeout = new CancellationTokenSource(_settings.Timeouts.IndexTimeout);
		using var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

		return await ExecuteWithResilienceAsync(
			operation: () => _client.IndexAsync(request, combinedToken.Token),
			operationType: "Index",
			createException: (ex, attempts) => new ElasticsearchIndexingException(
				"unknown", typeof(TDocument), $"Index operation failed after {attempts} attempts: {ex.Message}", ex),
			cancellationToken: combinedToken.Token).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<UpdateResponse<TDocument>> UpdateAsync<TDocument>(
		UpdateRequest<TDocument, object> request,
		CancellationToken cancellationToken)
	{
		ThrowIfDisposed();

		if (!_settings.Enabled)
		{
			return await _client.UpdateAsync(request, cancellationToken).ConfigureAwait(false);
		}

		using var timeout = new CancellationTokenSource(_settings.Timeouts.IndexTimeout);
		using var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

		return await ExecuteWithResilienceAsync(
			operation: () => _client.UpdateAsync(request, combinedToken.Token),
			operationType: "Update",
			createException: (ex, attempts) => new ElasticsearchUpdateException(
				"unknown",
				typeof(TDocument),
				$"Update operation failed after {attempts} attempts: {ex.Message}",
				ex),
			cancellationToken: combinedToken.Token).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<DeleteResponse> DeleteAsync(
		DeleteRequest request,
		CancellationToken cancellationToken)
	{
		ThrowIfDisposed();

		if (!_settings.Enabled)
		{
			return await _client.DeleteAsync(request, cancellationToken).ConfigureAwait(false);
		}

		using var timeout = new CancellationTokenSource(_settings.Timeouts.DeleteTimeout);
		using var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

		return await ExecuteWithResilienceAsync(
			operation: () => _client.DeleteAsync(request, combinedToken.Token),
			operationType: "Delete",
			createException: (ex, attempts) => new ElasticsearchDeleteException(
				"unknown",
				documentType: null,
				$"Delete operation failed after {attempts} attempts: {ex.Message}",
				ex),
			cancellationToken: combinedToken.Token).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<BulkResponse> BulkAsync(
		BulkRequest request,
		CancellationToken cancellationToken)
	{
		ThrowIfDisposed();

		if (!_settings.Enabled)
		{
			return await _client.BulkAsync(request, cancellationToken).ConfigureAwait(false);
		}

		using var timeout = new CancellationTokenSource(_settings.Timeouts.BulkTimeout);
		using var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

		return await ExecuteWithResilienceAsync(
			operation: () => _client.BulkAsync(request, combinedToken.Token),
			operationType: "Bulk",
			createException: (ex, attempts) => new ElasticsearchIndexingException(
				"unknown", typeof(object), $"Bulk operation failed after {attempts} attempts: {ex.Message}", ex),
			cancellationToken: combinedToken.Token).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<GetResponse<TDocument>> GetAsync<TDocument>(
		GetRequest request,
		CancellationToken cancellationToken)
	{
		ThrowIfDisposed();

		if (!_settings.Enabled)
		{
			return await _client.GetAsync<TDocument>(request, cancellationToken).ConfigureAwait(false);
		}

		using var timeout = new CancellationTokenSource(_settings.Timeouts.SearchTimeout);
		using var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

		return await ExecuteWithResilienceAsync(
			operation: () => _client.GetAsync<TDocument>(request, combinedToken.Token),
			operationType: "Get",
			createException: (ex, attempts) => new ElasticsearchGetByIdException(
				"unknown",
				typeof(TDocument),
				$"Get operation failed after {attempts} attempts: {ex.Message}",
				ex),
			cancellationToken: combinedToken.Token).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken)
	{
		ThrowIfDisposed();

		try
		{
			var healthResponse = await _client.Cluster.HealthAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
			return healthResponse.IsValidResponse &&
				   healthResponse.Status != HealthStatus.Red;
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Health check failed");
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
	/// Executes an operation with full resilience patterns including retry and circuit breaker.
	/// </summary>
	/// <typeparam name="TResponse"> The type of response expected from the operation. </typeparam>
	/// <param name="operation"> The operation to execute. </param>
	/// <param name="operationType"> The type of operation for logging and monitoring. </param>
	/// <param name="createException"> Function to create an appropriate exception when all retries are exhausted. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The response from the successful operation. </returns>
	/// <exception cref="Exception"> Thrown when the operation fails after all resilience mechanisms are exhausted. </exception>
	/// <exception cref="InvalidOperationException"></exception>
	private async Task<TResponse> ExecuteWithResilienceAsync<TResponse>(
		Func<Task<TResponse>> operation,
		string operationType,
		Func<Exception, int, Exception> createException,
		CancellationToken cancellationToken)
		where TResponse : TransportResponse
	{
		// Check circuit breaker state first
		if (_settings.CircuitBreaker.Enabled && _circuitBreaker.IsOpen)
		{
			_logger.LogWarning("Circuit breaker is open for {OperationType} operations", operationType);
			throw new InvalidOperationException($"Circuit breaker is open for {operationType} operations");
		}

		var attempts = 0;
		Exception? lastException = null;

		while (attempts < _settings.Retry.MaxAttempts + 1) // +1 for initial attempt
		{
			attempts++;

			try
			{
				var result = await operation().ConfigureAwait(false);

				// Record successful operation with circuit breaker
				if (_settings.CircuitBreaker.Enabled)
				{
					await _circuitBreaker.RecordSuccessAsync().ConfigureAwait(false);
				}

				// Check if response indicates a successful operation In the new client, we need to check the response differently
				var isValid = result switch
				{
					TransportResponse tr => tr.ApiCallDetails?.HttpStatusCode is >= 200 and < 300,
					_ => false,
				};
				if (isValid)
				{
					_logger.LogDebug(
						"{OperationType} operation succeeded on attempt {Attempt}",
						operationType, attempts);
					return result;
				}

				// Response is not valid - treat as failure for retry logic
				var responseException = new InvalidOperationException($"{operationType} operation returned invalid response");
				lastException = responseException;

				// Record failure with circuit breaker
				if (_settings.CircuitBreaker.Enabled)
				{
					await _circuitBreaker.RecordFailureAsync().ConfigureAwait(false);
				}

				// Check if we should retry
				if (!ShouldRetry(responseException, attempts))
				{
					break;
				}
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				lastException = ex;

				// Record failure with circuit breaker
				if (_settings.CircuitBreaker.Enabled)
				{
					await _circuitBreaker.RecordFailureAsync().ConfigureAwait(false);
				}

				_logger.LogWarning(ex, "{OperationType} operation failed on attempt {Attempt}",
					operationType, attempts);

				// Check if we should retry
				if (!ShouldRetry(ex, attempts))
				{
					break;
				}

				// Apply retry delay with exponential backoff and jitter
				if (attempts <= _settings.Retry.MaxAttempts)
				{
					var delay = _retryPolicy.GetRetryDelay(attempts - 1);
					_logger.LogDebug(
						"Retrying {OperationType} operation in {Delay}ms",
						operationType, delay.TotalMilliseconds);

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
				"{OperationType} operation permanently failed after {Attempts} attempts",
				operationType, attempts);
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
	/// Determines whether an operation should be retried based on the exception and attempt count.
	/// </summary>
	/// <param name="exception"> The exception that occurred. </param>
	/// <param name="attemptNumber"> The current attempt number (1-based). </param>
	/// <returns> True if the operation should be retried, false otherwise. </returns>
	private bool ShouldRetry(Exception exception, int attemptNumber)
	{
		// Don't retry if retries are disabled
		if (!_settings.Retry.Enabled)
		{
			return false;
		}

		// Don't retry if we've reached max attempts
		if (attemptNumber >= _settings.Retry.MaxAttempts + 1)
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
			throw new ObjectDisposedException(nameof(ResilientElasticsearchClient));
		}
	}
}
