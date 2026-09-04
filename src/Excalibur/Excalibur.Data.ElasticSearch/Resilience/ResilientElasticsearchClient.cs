// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Elastic.Clients.Elasticsearch;
using Elastic.Transport;

using Excalibur.Data.ElasticSearch.Exceptions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Polly.CircuitBreaker;

namespace Excalibur.Data.ElasticSearch.Resilience;

/// <summary>
/// Provides resilient Elasticsearch operations with retry policies and circuit breaker.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ResilientElasticsearchClient" /> class.
/// </remarks>
/// <param name="client"> The underlying Elasticsearch client. </param>
/// <param name="pipeline"> The retry and circuit-breaker pipeline every call runs through. </param>
/// <param name="circuitBreaker"> The circuit breaker for preventing cascading failures. </param>
/// <param name="options"> The resilience configuration options. </param>
/// <param name="logger"> The logger for diagnostic information. </param>
/// <param name="timeProvider">
/// The time provider used to schedule operation timeouts. Defaults to <see cref="TimeProvider.System" />. Supplying a
/// controllable provider allows timeout behavior to be exercised deterministically instead of racing the wall clock.
/// </param>
/// <exception cref="ArgumentNullException"> Thrown when any required parameter is null. </exception>
internal sealed class ResilientElasticsearchClient(
	ElasticsearchClient client,
	ElasticsearchResiliencePipeline pipeline,
	IElasticsearchCircuitBreaker circuitBreaker,
	IOptions<ElasticsearchConfigurationOptions> options,
	ILogger<ResilientElasticsearchClient> logger,
	TimeProvider? timeProvider = null) : IResilientElasticsearchClient, IDisposable
{
	private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

	private readonly ElasticsearchClient _client = client ?? throw new ArgumentNullException(nameof(client));
	private readonly ElasticsearchResiliencePipeline _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));

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

		using var timeout = new CancellationTokenSource(_settings.Timeouts.SearchTimeout, _timeProvider);
		using var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

		return await ExecuteWithResilienceAsync(
			operation: () => _client.SearchAsync<TDocument>(request, combinedToken.Token),
			operationType: "Search",
			createException: (ex, attempts) => new ElasticsearchSearchException(
				"unknown", typeof(TDocument), $"Search operation failed after {attempts} attempts: {ex.Message}", ex),
			cancellationToken: combinedToken.Token,
			callerToken: cancellationToken).ConfigureAwait(false);
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

		using var timeout = new CancellationTokenSource(_settings.Timeouts.IndexTimeout, _timeProvider);
		using var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

		return await ExecuteWithResilienceAsync(
			operation: () => _client.IndexAsync(request, combinedToken.Token),
			operationType: "Index",
			createException: (ex, attempts) => new ElasticsearchIndexingException(
				"unknown", typeof(TDocument), $"Index operation failed after {attempts} attempts: {ex.Message}", ex),
			cancellationToken: combinedToken.Token,
			callerToken: cancellationToken).ConfigureAwait(false);
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

		using var timeout = new CancellationTokenSource(_settings.Timeouts.IndexTimeout, _timeProvider);
		using var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

		return await ExecuteWithResilienceAsync(
			operation: () => _client.UpdateAsync(request, combinedToken.Token),
			operationType: "Update",
			createException: (ex, attempts) => new ElasticsearchUpdateException(
				"unknown",
				typeof(TDocument),
				$"Update operation failed after {attempts} attempts: {ex.Message}",
				ex),
			cancellationToken: combinedToken.Token,
			callerToken: cancellationToken).ConfigureAwait(false);
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

		using var timeout = new CancellationTokenSource(_settings.Timeouts.DeleteTimeout, _timeProvider);
		using var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

		return await ExecuteWithResilienceAsync(
			operation: () => _client.DeleteAsync(request, combinedToken.Token),
			operationType: "Delete",
			createException: (ex, attempts) => new ElasticsearchDeleteException(
				"unknown",
				documentType: null,
				$"Delete operation failed after {attempts} attempts: {ex.Message}",
				ex),
			cancellationToken: combinedToken.Token,
			callerToken: cancellationToken).ConfigureAwait(false);
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

		using var timeout = new CancellationTokenSource(_settings.Timeouts.BulkTimeout, _timeProvider);
		using var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

		return await ExecuteWithResilienceAsync(
			operation: () => _client.BulkAsync(request, combinedToken.Token),
			operationType: "Bulk",
			createException: (ex, attempts) => new ElasticsearchIndexingException(
				"unknown", typeof(object), $"Bulk operation failed after {attempts} attempts: {ex.Message}", ex),
			cancellationToken: combinedToken.Token,
			callerToken: cancellationToken).ConfigureAwait(false);
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

		using var timeout = new CancellationTokenSource(_settings.Timeouts.SearchTimeout, _timeProvider);
		using var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

		return await ExecuteWithResilienceAsync(
			operation: () => _client.GetAsync<TDocument>(request, combinedToken.Token),
			operationType: "Get",
			createException: (ex, attempts) => new ElasticsearchGetByIdException(
				"unknown",
				typeof(TDocument),
				$"Get operation failed after {attempts} attempts: {ex.Message}",
				ex),
			cancellationToken: combinedToken.Token,
			callerToken: cancellationToken).ConfigureAwait(false);
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
	/// Executes an operation with full resilience patterns including retry and circuit breaker.
	/// </summary>
	/// <typeparam name="TResponse"> The type of response expected from the operation. </typeparam>
	/// <param name="operation"> The operation to execute. </param>
	/// <param name="operationType"> The type of operation for logging and monitoring. </param>
	/// <param name="createException"> Function to create an appropriate exception when all retries are exhausted. </param>
	/// <param name="cancellationToken"> The combined token linking caller cancellation and the operation timeout. </param>
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
						// An unsuccessful response is a failure the pipeline must see. Returning it
						// would hide the outcome from both the retry and the breaker, so the call
						// would neither be retried nor counted.
						throw new ElasticsearchInvalidResponseException(operationType, result.ApiCallDetails?.HttpStatusCode);
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
			_logger.LogWarning(ex, "{OperationType} operation failed after {Attempts} attempt(s)",
				operationType, attempts);
			throw createException(ex, attempts);
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
			throw new ObjectDisposedException(nameof(ResilientElasticsearchClient));
		}
	}
}
