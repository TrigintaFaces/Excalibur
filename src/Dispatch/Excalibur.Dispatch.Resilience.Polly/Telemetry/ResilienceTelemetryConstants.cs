// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Dispatch.Resilience.Polly;

/// <summary>
/// Telemetry constants for resilience pipeline metrics and tracing.
/// </summary>
[SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Logical grouping of telemetry constants")]
public static class ResilienceTelemetryConstants
{
	/// <summary>
	/// The meter name for resilience pipeline metrics.
	/// </summary>
	public const string MeterName = "Excalibur.Dispatch.Resilience";

	/// <summary>
	/// The ActivitySource name for resilience pipeline tracing.
	/// </summary>
	public const string ActivitySourceName = "Excalibur.Dispatch.Resilience";

	/// <summary>
	/// The version for ActivitySource instances, derived from the assembly informational version.
	/// </summary>
	public static readonly string Version =
		typeof(ResilienceTelemetryConstants).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

	/// <summary>
	/// Metric instrument names for resilience operations.
	/// </summary>
	public static class Instruments
	{
		/// <summary>
		/// Counter for total retry attempts.
		/// </summary>
		public const string RetryAttempts = "dispatch.resilience.retry.attempts";

		/// <summary>
		/// Counter for circuit breaker state transitions.
		/// </summary>
		public const string CircuitBreakerTransitions = "dispatch.resilience.circuit_breaker.transitions";

		/// <summary>
		/// Histogram for resilience operation duration in milliseconds.
		/// </summary>
		public const string OperationDuration = "dispatch.resilience.operation.duration";

		/// <summary>
		/// Counter for timeout occurrences.
		/// </summary>
		public const string Timeouts = "dispatch.resilience.timeouts";

		/// <summary>
		/// Counter for operations executed through the resilience pipeline.
		/// </summary>
		public const string OperationsExecuted = "dispatch.resilience.operations.executed";
	}

	/// <summary>
	/// Tag names for resilience telemetry attributes.
	/// </summary>
	public static class Tags
	{
		/// <summary>
		/// The name of the resilience pipeline.
		/// </summary>
		public const string PipelineName = "resilience.pipeline.name";

		/// <summary>
		/// The type of resilience strategy (retry, circuit_breaker, timeout, hedging).
		/// </summary>
		public const string StrategyType = "resilience.strategy.type";

		/// <summary>
		/// The outcome of the resilience operation (success, failure, timeout).
		/// </summary>
		public const string Outcome = "resilience.outcome";

		/// <summary>
		/// The circuit breaker state (closed, open, half_open).
		/// </summary>
		public const string CircuitState = "resilience.circuit_breaker.state";

		/// <summary>
		/// The retry attempt number.
		/// </summary>
		public const string RetryAttempt = "resilience.retry.attempt";
	}
}
