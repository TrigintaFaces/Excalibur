// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Resilience;

/// <summary>
/// Event arguments for circuit breaker state change events.
/// </summary>
public sealed class CircuitStateChangedEventArgs : EventArgs
{
	/// <summary>
	/// Gets the previous state of the circuit breaker.
	/// </summary>
	public CircuitState PreviousState { get; init; }

	/// <summary>
	/// Gets the new state of the circuit breaker.
	/// </summary>
	public CircuitState NewState { get; init; }

	/// <summary>
	/// Gets the timestamp when the state changed.
	/// </summary>
	public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets the name or identifier of the circuit breaker (e.g., transport name).
	/// </summary>
	public string? CircuitName { get; init; }

	/// <summary>
	/// Gets the exception that triggered the state change, if any.
	/// </summary>
	public Exception? TriggeringException { get; init; }
}
