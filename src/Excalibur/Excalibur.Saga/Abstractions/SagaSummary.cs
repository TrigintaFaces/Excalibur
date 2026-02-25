// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Saga.Abstractions;

/// <summary>
/// Summary information about a saga.
/// </summary>
public sealed class SagaSummary
{
	/// <summary>
	/// Gets or initializes the saga identifier.
	/// </summary>
	/// <value> The saga identifier. </value>
	public string SagaId { get; init; } = string.Empty;

	/// <summary>
	/// Gets or initializes the saga type name.
	/// </summary>
	/// <value> The saga type name. </value>
	public string SagaType { get; init; } = string.Empty;

	/// <summary>
	/// Gets or initializes the current state of the saga.
	/// </summary>
	/// <value> The saga state. </value>
	public SagaState State { get; init; }

	/// <summary>
	/// Gets or initializes when the saga started.
	/// </summary>
	/// <value> The saga start timestamp. </value>
	public DateTimeOffset StartedAt { get; init; }

	/// <summary>
	/// Gets or initializes when the saga completed.
	/// </summary>
	/// <value> The saga completion timestamp or <see langword="null" />. </value>
	public DateTimeOffset? CompletedAt { get; init; }

	/// <summary>
	/// Gets or initializes the current step number.
	/// </summary>
	/// <value> The current step index. </value>
	public int CurrentStep { get; init; }

	/// <summary>
	/// Gets or initializes the total number of steps.
	/// </summary>
	/// <value> The total number of steps. </value>
	public int TotalSteps { get; init; }
}
