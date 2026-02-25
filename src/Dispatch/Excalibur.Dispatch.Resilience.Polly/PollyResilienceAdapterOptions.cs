// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Options.Resilience;

namespace Excalibur.Dispatch.Resilience.Polly;

/// <summary>
/// Configuration options for Polly resilience adapters registered via
/// <c>AddPollyResilienceAdapters()</c> extension methods.
/// </summary>
/// <remarks>
/// <para>
/// This options class allows consumers to configure all Polly adapter settings in one place
/// when calling <c>AddPollyResilienceAdapters()</c>.
/// </para>
/// <para>
/// Example usage:
/// </para>
/// <code>
/// services.AddDispatch()
///     .AddPollyResilienceAdapters(options =>
///     {
///         options.RetryOptions = new RetryOptions
///         {
///             MaxRetries = 5,
///             BaseDelay = TimeSpan.FromMilliseconds(100),
///             BackoffStrategy = BackoffStrategy.Exponential,
///             UseJitter = true
///         };
///         options.CircuitBreakerOptions = new CircuitBreakerOptions
///         {
///             FailureThreshold = 5,
///             OpenDuration = TimeSpan.FromSeconds(30)
///         };
///     });
/// </code>
/// </remarks>
public sealed class PollyResilienceAdapterOptions
{
	/// <summary>
	/// Gets or sets the retry policy options for <see cref="PollyRetryPolicyAdapter"/>
	/// and <see cref="PollyBackoffCalculatorAdapter"/>.
	/// </summary>
	/// <remarks>
	/// If not set, default values from <see cref="RetryOptions"/> will be used.
	/// </remarks>
	public RetryOptions? RetryOptions { get; set; }

	/// <summary>
	/// Gets or sets the circuit breaker options for <see cref="PollyCircuitBreakerPolicyAdapter"/>
	/// and <see cref="PollyTransportCircuitBreakerRegistry"/>.
	/// </summary>
	/// <remarks>
	/// If not set, default values from <see cref="CircuitBreakerOptions"/> will be used.
	/// </remarks>
	public CircuitBreakerOptions? CircuitBreakerOptions { get; set; }

	/// <summary>
	/// Gets or sets the maximum delay cap for backoff calculations.
	/// </summary>
	/// <remarks>
	/// This caps the maximum delay returned by <see cref="PollyBackoffCalculatorAdapter"/>
	/// regardless of the calculated exponential delay.
	/// </remarks>
	public TimeSpan MaxBackoffDelay { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets whether to enable Polly telemetry integration.
	/// </summary>
	/// <remarks>
	/// When enabled, Polly will emit telemetry events that can be collected
	/// by OpenTelemetry or other monitoring systems.
	/// </remarks>
	public bool EnableTelemetry { get; set; }
}
