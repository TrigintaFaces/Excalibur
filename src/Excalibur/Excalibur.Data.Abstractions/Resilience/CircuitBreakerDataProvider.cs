// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Persistence;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.Abstractions.Resilience;

/// <summary>
/// Decorator that wraps a persistence provider with circuit breaker protection.
/// Uses the <see cref="DelegatingPersistenceProvider"/> base to add resilience
/// without modifying the inner provider.
/// </summary>
/// <remarks>
/// Reference: Polly v8 <c>CircuitBreakerResilienceStrategy</c> — failure counting
/// within a sampling window, automatic open/half-open/closed transitions.
/// </remarks>
#pragma warning disable IDE0330 // Multi-target net8.0/net9.0/net10.0: System.Threading.Lock not available on all TFMs

public sealed partial class CircuitBreakerDataProvider : DelegatingPersistenceProvider
{
	private readonly DataProviderCircuitBreakerOptions _options;
	private readonly ILogger<CircuitBreakerDataProvider> _logger;
	private readonly object _syncLock = new();
	private DataProviderCircuitState _state = DataProviderCircuitState.Closed;
	private int _failureCount;
	private DateTimeOffset _lastFailureTime;
	private DateTimeOffset _openedAt;

	/// <summary>
	/// Initializes a new instance of the <see cref="CircuitBreakerDataProvider"/> class.
	/// </summary>
	/// <param name="innerProvider">The inner persistence provider to protect.</param>
	/// <param name="options">The circuit breaker options.</param>
	/// <param name="logger">The logger instance.</param>
	public CircuitBreakerDataProvider(
		IPersistenceProvider innerProvider,
		IOptions<DataProviderCircuitBreakerOptions> options,
		ILogger<CircuitBreakerDataProvider> logger)
		: base(innerProvider)
	{
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <summary>
	/// Gets the current state of the circuit breaker.
	/// </summary>
	/// <value>The current <see cref="DataProviderCircuitState"/>.</value>
	public DataProviderCircuitState State
	{
		get
		{
			lock (_syncLock)
			{
				if (_state == DataProviderCircuitState.Open &&
					DateTimeOffset.UtcNow - _openedAt >= _options.BreakDuration)
				{
					_state = DataProviderCircuitState.HalfOpen;
					LogCircuitHalfOpen(Name);
				}

				return _state;
			}
		}
	}

	/// <inheritdoc />
	public override async Task<TResult> ExecuteAsync<TConnection, TResult>(
		IDataRequest<TConnection, TResult> request,
		CancellationToken cancellationToken)
	{
		EnsureCircuitAllowsExecution();

		try
		{
			var result = await base.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
			OnSuccess();
			return result;
		}
		catch (Exception ex) when (IsTransientFailure(ex))
		{
			OnFailure();
			throw;
		}
	}

	/// <inheritdoc />
	public override async Task InitializeAsync(
		IPersistenceOptions options,
		CancellationToken cancellationToken)
	{
		EnsureCircuitAllowsExecution();

		try
		{
			await base.InitializeAsync(options, cancellationToken).ConfigureAwait(false);
			OnSuccess();
		}
		catch (Exception ex) when (IsTransientFailure(ex))
		{
			OnFailure();
			throw;
		}
	}

	/// <inheritdoc />
	public override object? GetService(Type serviceType)
	{
		if (serviceType == typeof(IDataProviderCircuitBreaker))
		{
			return this;
		}

		return base.GetService(serviceType);
	}

	private void EnsureCircuitAllowsExecution()
	{
		var currentState = State;
		if (currentState == DataProviderCircuitState.Open)
		{
			var retryAfter = _options.BreakDuration - (DateTimeOffset.UtcNow - _openedAt);
			throw new CircuitBreakerOpenException(
				$"Circuit breaker for provider '{Name}' is open. Retry after {retryAfter.TotalSeconds:F1}s.")
			{
				RetryAfter = retryAfter > TimeSpan.Zero ? retryAfter : null
			};
		}
	}

	private void OnSuccess()
	{
		lock (_syncLock)
		{
			if (_state == DataProviderCircuitState.HalfOpen)
			{
				LogCircuitClosed(Name);
			}

			_state = DataProviderCircuitState.Closed;
			_failureCount = 0;
		}
	}

	private void OnFailure()
	{
		lock (_syncLock)
		{
			var now = DateTimeOffset.UtcNow;

			// Reset failure count if outside sampling window
			if (now - _lastFailureTime > _options.SamplingWindow)
			{
				_failureCount = 0;
			}

			_failureCount++;
			_lastFailureTime = now;

			if (_state == DataProviderCircuitState.HalfOpen ||
				_failureCount >= _options.FailureThreshold)
			{
				_state = DataProviderCircuitState.Open;
				_openedAt = now;
				LogCircuitOpened(Name, _failureCount);
			}
		}
	}

	private static bool IsTransientFailure(Exception ex) =>
		ex is TimeoutException or
			OperationCanceledException or
			HttpRequestException or
			IOException;

	[LoggerMessage(3000, LogLevel.Warning,
		"Circuit breaker for provider '{ProviderName}' opened after {FailureCount} failures")]
	private partial void LogCircuitOpened(string providerName, int failureCount);

	[LoggerMessage(3001, LogLevel.Information,
		"Circuit breaker for provider '{ProviderName}' transitioned to half-open")]
	private partial void LogCircuitHalfOpen(string providerName);

	[LoggerMessage(3002, LogLevel.Information,
		"Circuit breaker for provider '{ProviderName}' closed (recovered)")]
	private partial void LogCircuitClosed(string providerName);
}
