// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


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
	/// Gets or sets the minimum number of failures required before the circuit can open.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Count-based providers open the circuit as soon as this many <em>consecutive</em> failures occur.
	/// </para>
	/// <para>
	/// Ratio-based providers (such as the Polly adapters) treat this as the minimum number of calls that
	/// must be observed within <see cref="SamplingDuration" /> before <see cref="FailureRatio" /> is
	/// evaluated at all. On those providers a run of failures interleaved with enough successes keeps the
	/// ratio below the threshold and the circuit stays closed. Set <see cref="FailureRatio" /> to
	/// <c>1.0</c> to require that every observed call in the window failed.
	/// </para>
	/// <para>
	/// Ratio-based providers require at least two observed calls, so they reject a value below 2 at
	/// construction. Count-based providers accept 1, which opens the circuit on the first failure.
	/// </para>
	/// </remarks>
	/// <value>Default is 5.</value>
	[Range(1, int.MaxValue)]
	public int FailureThreshold { get; set; } = 5;

	/// <summary>
	/// Gets or sets the proportion of failed calls within <see cref="SamplingDuration" /> that opens the circuit.
	/// </summary>
	/// <remarks>
	/// Used only by ratio-based providers, and only once <see cref="FailureThreshold" /> calls have been
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
	/// <remarks>
	/// Used only by ratio-based providers; count-based providers ignore it. Ratio-based providers require a
	/// window of at least 500 milliseconds.
	/// </remarks>
	/// <value>Default is 30 seconds.</value>
	public TimeSpan SamplingDuration { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets the duration to keep the circuit open before the next probe.
	/// </summary>
	/// <value>Default is 30 seconds.</value>
	public TimeSpan OpenDuration { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets the timeout applied to individual operations executed under the circuit breaker.
	/// </summary>
	/// <value>Default is 5 seconds.</value>
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
