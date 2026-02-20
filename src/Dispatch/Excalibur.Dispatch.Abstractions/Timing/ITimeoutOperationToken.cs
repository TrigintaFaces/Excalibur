// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Represents a token for tracking timeout operations and collecting performance metrics. R7.4: Operation tracking for timeout monitoring.
/// </summary>
public interface ITimeoutOperationToken : IDisposable
{
	/// <summary>
	/// Gets the unique identifier for this operation.
	/// </summary>
	/// <value> The unique operation identifier. </value>
	Guid OperationId { get; }

	/// <summary>
	/// Gets the type of operation being tracked.
	/// </summary>
	/// <value> The type of operation being tracked. </value>
	TimeoutOperationType OperationType { get; }

	/// <summary>
	/// Gets the context associated with this operation.
	/// </summary>
	/// <value> The associated timeout context. </value>
	TimeoutContext? Context { get; }

	/// <summary>
	/// Gets the timestamp when the operation started.
	/// </summary>
	/// <value> The operation start time. </value>
	DateTimeOffset StartTime { get; }

	/// <summary>
	/// Gets the elapsed time since the operation started.
	/// </summary>
	/// <value> The elapsed time since the operation started. </value>
	TimeSpan Elapsed { get; }

	/// <summary>
	/// Gets a value indicating whether the operation has been completed.
	/// </summary>
	/// <value> <see langword="true" /> when the operation has completed; otherwise, <see langword="false" />. </value>
	bool IsCompleted { get; }

	/// <summary>
	/// Gets a value indicating whether the operation was successful.
	/// </summary>
	/// <value>
	/// <see langword="true" /> when the operation succeeded; <see langword="false" /> when it failed; <see langword="null" /> if unknown.
	/// </value>
	bool? IsSuccessful { get; }

	/// <summary>
	/// Gets a value indicating whether the operation timed out.
	/// </summary>
	/// <value>
	/// <see langword="true" /> when the operation timed out; otherwise, <see langword="false" /> or <see langword="null" /> when undetermined.
	/// </value>
	bool? HasTimedOut { get; }
}
