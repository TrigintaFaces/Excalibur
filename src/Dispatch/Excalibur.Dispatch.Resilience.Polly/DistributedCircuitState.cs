// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Resilience.Polly;

/// <summary>
/// Distributed circuit state stored in cache.
/// </summary>
internal sealed class DistributedCircuitState
{
	/// <summary>
	/// Gets or sets the current circuit state.
	/// </summary>
	/// <value>The current circuit state (Closed, Open, or HalfOpen).</value>
	public CircuitState State { get; set; }

	/// <summary>
	/// Gets or sets the timestamp when the circuit was opened.
	/// </summary>
	/// <value>The timestamp when the circuit transitioned to the open state.</value>
	public DateTimeOffset OpenedAt { get; set; }

	/// <summary>
	/// Gets or sets the timestamp until which the circuit should remain open.
	/// </summary>
	/// <value>The timestamp indicating when the circuit can transition to half-open.</value>
	public DateTimeOffset OpenUntil { get; set; }

	/// <summary>
	/// Gets or sets the timestamp of the last state transition.
	/// </summary>
	/// <value>The timestamp when the circuit last changed state.</value>
	public DateTimeOffset TransitionedAt { get; set; }

	/// <summary>
	/// Gets or sets the identifier of the instance that last updated the state.
	/// </summary>
	/// <value>The unique identifier of the instance managing this circuit.</value>
	public string InstanceId { get; set; } = string.Empty;
}
