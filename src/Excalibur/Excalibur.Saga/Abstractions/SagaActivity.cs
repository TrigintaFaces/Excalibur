// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Saga.Abstractions;

/// <summary>
/// Represents an activity log entry in a saga.
/// </summary>
public sealed class SagaActivity
{
	/// <summary>
	/// Gets or initializes the timestamp of the activity.
	/// </summary>
	/// <value> The time at which the activity occurred. </value>
	public DateTimeOffset Timestamp { get; init; }

	/// <summary>
	/// Gets or initializes the activity message.
	/// </summary>
	/// <value> The human-readable description of the activity. </value>
	public string Message { get; init; } = string.Empty;

	/// <summary>
	/// Gets or initializes additional details about the activity.
	/// </summary>
	/// <value> The optional data payload associated with the activity. </value>
	public object? Details { get; init; }

	/// <summary>
	/// Gets or initializes the name of the step associated with this activity.
	/// </summary>
	/// <value> The step name when the activity is tied to a specific saga step. </value>
	public string? StepName { get; init; }
}
