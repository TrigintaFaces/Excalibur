// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Options.Resilience;

/// <summary>
/// Configuration options for circuit breaker.
/// </summary>
/// <remarks>
/// <para>
/// This is the canonical <c>CircuitBreakerOptions</c> for the Excalibur framework,
/// consolidating Options.Middleware.CircuitBreakerOptions.
/// </para>
/// <para>
/// For per-service mesh configurations with callbacks, see
/// <c>Excalibur.Dispatch.Transport.Abstractions.ServiceMesh.CircuitBreakerOptions</c>.
/// </para>
/// </remarks>
public sealed class CircuitBreakerOptions
{
	/// <summary>
	/// Gets or sets the number of consecutive failures that opens the circuit.
	/// </summary>
	/// <remarks>
	/// Read by count-based providers, which open as soon as this many failures occur in a row and
	/// reset the run on any success. A value of <c>1</c> opens the circuit on the first failure.
	/// Ratio-based providers ignore this and use <see cref="MinimumThroughput" /> with
	/// <see cref="FailureRatio" /> instead.
	/// </remarks>
	/// <value>Default is 5.</value>
	[Range(1, int.MaxValue)]
	public int ConsecutiveFailureThreshold { get; set; } = 5;

	/// <summary>
	/// Gets or sets the number of calls that must be observed within <see cref="SamplingDuration" />
	/// before <see cref="FailureRatio" /> is evaluated.
	/// </summary>
	/// <remarks>
	/// Read by ratio-based providers. Below this many observed calls the circuit stays closed however
	/// many of them failed, so a burst of failures early in a window does not trip it. At least two
	/// calls are required for a ratio to be meaningful. Count-based providers ignore this and use
	/// <see cref="ConsecutiveFailureThreshold" /> instead.
	/// </remarks>
	/// <value>Default is 5.</value>
	[Range(2, int.MaxValue)]
	public int MinimumThroughput { get; set; } = 5;

	/// <summary>
	/// Gets or sets the proportion of failed calls within <see cref="SamplingDuration" /> that opens the circuit.
	/// </summary>
	/// <remarks>
	/// Used only by ratio-based providers, and only once <see cref="MinimumThroughput" /> calls have been
	/// observed within the sampling window. Count-based providers ignore this value because they open on a
	/// consecutive-failure count instead. A value of <c>1.0</c> requires every observed call in the window
	/// to have failed.
	/// </remarks>
	/// <value>Default is 0.5 (50% of observed calls).</value>
	[Range(0.0, 1.0)]
	public double FailureRatio { get; set; } = 0.5;

	/// <summary>
	/// Gets or sets the rolling window over which <see cref="FailureRatio" /> is measured.
	/// </summary>
	/// <value>Default is 30 seconds.</value>
	/// <remarks>
	/// <para>
	/// Used only by ratio-based providers; count-based providers ignore it. Ratio-based providers require a
	/// window of at least 500 milliseconds.
	/// </para>
	/// <para>
	/// Must be between 500ms and 1 day: the value is forwarded verbatim to the resilience provider,
	/// which enforces that range itself and would otherwise reject it when the pipeline is built.
	/// </para>
	/// <para>
	/// The bound is enforced in <c>CircuitBreakerOptionsValidator</c> rather than by a
	/// <c>[Range]</c> attribute, and deliberately so. The only <c>RangeAttribute</c> overload that
	/// accepts a <see cref="TimeSpan"/> takes <c>(Type, string, string)</c> and parses via a
	/// TypeConverter, which carries <c>RequiresUnreferencedCode</c>; this package treats IL2026 as an
	/// error, so adding the attribute trades an AOT-honesty regression for a duplicate of a check the
	/// validator already performs. The int-typed siblings above can use the attribute because their
	/// overload needs no converter.
	/// </para>
	/// </remarks>
	public TimeSpan SamplingDuration { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets the duration to keep the circuit open before the next probe.
	/// </summary>
	/// <value>Default is 30 seconds.</value>
	/// <remarks>
	/// Must be between 500ms and 1 day, enforced by the validator for the reason given on
	/// <see cref="SamplingDuration"/>.
	/// </remarks>
	public TimeSpan BreakDuration { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets the timeout applied to individual operations executed under the circuit breaker.
	/// </summary>
	/// <value>Default is 5 seconds.</value>
	/// <remarks>
	/// Must be between 10ms and 1 day — the provider's timeout strategy has a lower floor than the
	/// circuit windows above. Enforced by the validator, for the reason given on
	/// <see cref="SamplingDuration"/>.
	/// </remarks>
	public TimeSpan OperationTimeout { get; set; } = TimeSpan.FromSeconds(5);

	/// <summary>
	/// Gets or sets the function to determine the circuit key for a message.
	/// </summary>
	/// <remarks>
	/// When set, this function is used to determine which circuit a message belongs to,
	/// enabling per-message-type circuit isolation. If <see langword="null"/>, the message
	/// type name is used as the circuit key.
	/// </remarks>
	/// <value>Default is <see langword="null"/> (uses message type name).</value>
	public Func<IDispatchMessage, string>? CircuitKeySelector { get; set; }
}
