// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.CloudNative;
using Excalibur.Dispatch.Options.Resilience;

namespace Excalibur.Dispatch.Resilience.Polly;

/// <summary>
/// Wrapper to adapt PollyCircuitBreakerAdapter to CircuitBreakerPattern interface.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="PollyCircuitBreakerWrapper" /> class. </remarks>
/// <param name="adapter"> The Polly circuit breaker adapter to wrap. </param>
internal sealed class PollyCircuitBreakerWrapper(PollyCircuitBreakerAdapter adapter)
	: CircuitBreakerPattern((adapter ?? throw new ArgumentNullException(nameof(adapter))).Name, new CircuitBreakerOptions())
{
	/// <summary>
	/// Gets the current resilience state of the circuit breaker.
	/// </summary>
	/// <value> The resilience state reported by the wrapped adapter. </value>
	public new ResilienceState State => adapter.State;

	/// <summary>
	/// Gets the health status of the circuit breaker pattern.
	/// </summary>
	/// <value> The health status computed by the adapter using recent metrics. </value>
	public new PatternHealthStatus HealthStatus => adapter.HealthStatus;

	/// <summary>
	/// Gets the configuration dictionary for the circuit breaker.
	/// </summary>
	/// <value> A read-only dictionary exposing the adapter's configuration settings. </value>
	public new IReadOnlyDictionary<string, object> Configuration => adapter.Configuration;

	/// <summary>
	/// Executes an asynchronous operation with circuit breaker protection.
	/// </summary>
	/// <typeparam name="T"> The type of result returned by the operation. </typeparam>
	/// <param name="operation"> The operation to execute. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The result of the operation. </returns>
	public new Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken) =>
		adapter.ExecuteAsync(operation, cancellationToken);

	/// <summary>
	/// Executes an asynchronous operation with circuit breaker protection and fallback.
	/// </summary>
	/// <typeparam name="T"> The type of result returned by the operation. </typeparam>
	/// <param name="operation"> The primary operation to execute. </param>
	/// <param name="fallback"> The fallback operation if the primary fails. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The result of the operation or fallback. </returns>
	public new Task<T> ExecuteAsync<T>(Func<Task<T>> operation, Func<Task<T>> fallback, CancellationToken cancellationToken) =>
		adapter.ExecuteAsync(operation, fallback, cancellationToken);

	/// <summary>
	/// Resets the circuit breaker to closed state.
	/// </summary>
	public new void Reset() => adapter.Reset();

	/// <summary>
	/// Gets the current metrics for the circuit breaker pattern.
	/// </summary>
	/// <returns> The pattern metrics. </returns>
	public new PatternMetrics GetMetrics() => adapter.GetMetrics();

	/// <summary>
	/// Initializes the circuit breaker with the specified configuration.
	/// </summary>
	/// <param name="configuration"> The configuration dictionary. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the initialization operation. </returns>
	public new Task InitializeAsync(IReadOnlyDictionary<string, object> configuration, CancellationToken cancellationToken) =>
		adapter.InitializeAsync(configuration, cancellationToken);

	/// <summary>
	/// Starts the circuit breaker pattern.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the start operation. </returns>
	public new Task StartAsync(CancellationToken cancellationToken) =>
		adapter.StartAsync(cancellationToken);

	/// <summary>
	/// Stops the circuit breaker pattern.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the stop operation. </returns>
	public new Task StopAsync(CancellationToken cancellationToken) =>
		adapter.StopAsync(cancellationToken);

	/// <summary>
	/// Disposes the circuit breaker asynchronously.
	/// </summary>
	/// <returns> A value task representing the dispose operation. </returns>
	public new ValueTask DisposeAsync() => adapter.DisposeAsync();
}
