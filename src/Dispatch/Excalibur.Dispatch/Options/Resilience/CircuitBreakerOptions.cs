// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Options.Resilience;

/// <summary>
/// Configuration options for circuit breaker.
/// </summary>
/// <remarks>
/// <para>
/// This is the canonical <c>CircuitBreakerOptions</c> for the Dispatch framework,
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
	/// Gets or sets the number of consecutive failures before opening the circuit.
	/// </summary>
	/// <value>Default is 5.</value>
	[Range(1, int.MaxValue)]
	public int FailureThreshold { get; set; } = 5;

	/// <summary>
	/// Gets or sets the number of consecutive successes required to close the circuit from half-open.
	/// </summary>
	/// <value>Default is 3.</value>
	[Range(1, int.MaxValue)]
	public int SuccessThreshold { get; set; } = 3;

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
	/// Gets or sets the maximum number of concurrent probe executions while in the half-open state.
	/// </summary>
	/// <value>Default is 3.</value>
	[Range(1, int.MaxValue)]
	public int MaxHalfOpenTests { get; set; } = 3;

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
