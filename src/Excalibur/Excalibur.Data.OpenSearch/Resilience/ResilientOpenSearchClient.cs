// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using OpenSearch.Client;
using OpenSearch.Net;

namespace Excalibur.Data.OpenSearch.Resilience;

/// <summary>
/// Provides resilient OpenSearch operations with retry policies and circuit breaker.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ResilientOpenSearchClient" /> class.
/// </remarks>
/// <param name="client"> The underlying OpenSearch client. </param>
/// <param name="retryPolicy"> The retry policy for handling transient failures. </param>
/// <param name="circuitBreaker"> The circuit breaker for preventing cascading failures. </param>
/// <param name="options"> The resilience configuration options. </param>
/// <param name="logger"> The logger for diagnostic information. </param>
/// <exception cref="ArgumentNullException"> Thrown when any required parameter is null. </exception>
internal sealed class ResilientOpenSearchClient(
	OpenSearchClient client,
	IOpenSearchRetryPolicy retryPolicy,
	IOpenSearchCircuitBreaker circuitBreaker,
	IOptions<OpenSearchConfigurationOptions> options,
	ILogger<ResilientOpenSearchClient> logger) : IResilientOpenSearchClient, IResilientOpenSearchClientDiagnostics, IDisposable
{
	private readonly OpenSearchClient _client = client ?? throw new ArgumentNullException(nameof(client));
	private readonly IOpenSearchRetryPolicy _retryPolicy = retryPolicy ?? throw new ArgumentNullException(nameof(retryPolicy));

	private readonly IOpenSearchCircuitBreaker _circuitBreaker =
		circuitBreaker ?? throw new ArgumentNullException(nameof(circuitBreaker));

	private readonly OpenSearchResilienceOptions _settings =
		options?.Value?.Resilience ?? throw new ArgumentNullException(nameof(options));

	private readonly ILogger<ResilientOpenSearchClient> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
	private volatile bool _disposed;

	/// <inheritdoc />
	public bool IsCircuitBreakerOpen => _circuitBreaker.IsOpen;

	/// <inheritdoc />
	public async Task<ISearchResponse<TDocument>> SearchAsync<TDocument>(
		Func<SearchDescriptor<TDocument>, ISearchRequest> selector,
		CancellationToken cancellationToken)
		where TDocument : class
	{
		ThrowIfDisposed();

		if (!_settings.Enabled)
		{
			return await _client.SearchAsync(selector, cancellationToken).ConfigureAwait(false);
		}

		using var timeout = new CancellationTokenSource(_settings.Timeouts.SearchTimeout);
		using var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

		return await ExecuteWithResilienceAsync(
			operation: () => _client.SearchAsync(selector, combinedToken.Token),
			operationType: "Search",
			createException: (ex, attempts) => new InvalidOperationException(
				$"Search operation failed after {attempts} attempts: {ex.Message}", ex),
			cancellationToken: combinedToken.Token).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<IndexResponse> IndexAsync<TDocument>(
		TDocument document,
		Func<IndexDescriptor<TDocument>, IIndexRequest<TDocument>> selector,
		CancellationToken cancellationToken)
		where TDocument : class
	{
		ThrowIfDisposed();

		if (!_settings.Enabled)
		{
			return await _client.IndexAsync(document, selector, cancellationToken).ConfigureAwait(false);
		}

		using var timeout = new CancellationTokenSource(_settings.Timeouts.IndexTimeout);
		using var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

		return await ExecuteWithResilienceAsync(
			operation: () => _client.IndexAsync(document, selector, combinedToken.Token),
			operationType: "Index",
			createException: (ex, attempts) => new InvalidOperationException(
				$"Index operation failed after {attempts} attempts: {ex.Message}", ex),
			cancellationToken: combinedToken.Token).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<DeleteResponse> DeleteAsync<TDocument>(
		DocumentPath<TDocument> id,
		Func<DeleteDescriptor<TDocument>, IDeleteRequest>? selector,
		CancellationToken cancellationToken)
		where TDocument : class
	{
		ThrowIfDisposed();

		if (!_settings.Enabled)
		{
			return await _client.DeleteAsync(id, selector, cancellationToken).ConfigureAwait(false);
		}

		using var timeout = new CancellationTokenSource(_settings.Timeouts.DeleteTimeout);
		using var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

		return await ExecuteWithResilienceAsync(
			operation: () => _client.DeleteAsync(id, selector, combinedToken.Token),
			operationType: "Delete",
			createException: (ex, attempts) => new InvalidOperationException(
				$"Delete operation failed after {attempts} attempts: {ex.Message}", ex),
			cancellationToken: combinedToken.Token).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<BulkResponse> BulkAsync(
		Func<BulkDescriptor, IBulkRequest> selector,
		CancellationToken cancellationToken)
	{
		ThrowIfDisposed();

		if (!_settings.Enabled)
		{
			return await _client.BulkAsync(selector, cancellationToken).ConfigureAwait(false);
		}

		using var timeout = new CancellationTokenSource(_settings.Timeouts.BulkTimeout);
		using var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

		return await ExecuteWithResilienceAsync(
			operation: () => _client.BulkAsync(selector, combinedToken.Token),
			operationType: "Bulk",
			createException: (ex, attempts) => new InvalidOperationException(
				$"Bulk operation failed after {attempts} attempts: {ex.Message}", ex),
			cancellationToken: combinedToken.Token).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<GetResponse<TDocument>> GetAsync<TDocument>(
		DocumentPath<TDocument> id,
		Func<GetDescriptor<TDocument>, IGetRequest>? selector,
		CancellationToken cancellationToken)
		where TDocument : class
	{
		ThrowIfDisposed();

		if (!_settings.Enabled)
		{
			return await _client.GetAsync(id, selector, cancellationToken).ConfigureAwait(false);
		}

		using var timeout = new CancellationTokenSource(_settings.Timeouts.SearchTimeout);
		using var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

		return await ExecuteWithResilienceAsync(
			operation: () => _client.GetAsync(id, selector, combinedToken.Token),
			operationType: "Get",
			createException: (ex, attempts) => new InvalidOperationException(
				$"Get operation failed after {attempts} attempts: {ex.Message}", ex),
			cancellationToken: combinedToken.Token).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken)
	{
		ThrowIfDisposed();

		try
		{
			var healthResponse = await _client.Cluster.HealthAsync(
				new ClusterHealthRequest(), cancellationToken).ConfigureAwait(false);

			if (!healthResponse.IsValid)
			{
				return false;
			}

			var statusStr = healthResponse.Status.ToString();
			return statusStr is not ("red" or "Red");
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

			// OpenSearch client exceptions may be transient
			OpenSearchClientException ose => ose.Response?.HttpStatusCode switch
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
	/// <exception cref="InvalidOperationException">Thrown when the circuit breaker is open.</exception>
	private async Task<TResponse> ExecuteWithResilienceAsync<TResponse>(
		Func<Task<TResponse>> operation,
		string operationType,
		Func<Exception, int, Exception> createException,
		CancellationToken cancellationToken)
		where TResponse : IResponse
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

				// Check if response indicates a successful operation
				if (result.IsValid)
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
			catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
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
			throw new ObjectDisposedException(nameof(ResilientOpenSearchClient));
		}
	}
}
