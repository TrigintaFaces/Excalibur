// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Saga.Abstractions;

namespace Excalibur.Saga;

/// <summary>
/// Summary information about a saga.
/// </summary>
public sealed class SagaSummary
{
	/// <summary>
	/// Gets or initializes the saga identifier.
	/// </summary>
	/// <value>or initializes the saga identifier.</value>
	public string SagaId { get; init; } = string.Empty;

	/// <summary>
	/// Gets or initializes the saga type name.
	/// </summary>
	/// <value>or initializes the saga type name.</value>
	public string SagaType { get; init; } = string.Empty;

	/// <summary>
	/// Gets or initializes the current state of the saga.
	/// </summary>
	/// <value>or initializes the current state of the saga.</value>
	public SagaState State { get; init; }

	/// <summary>
	/// Gets or initializes when the saga started.
	/// </summary>
	/// <value>or initializes when the saga started.</value>
	public DateTimeOffset StartedAt { get; init; }

	/// <summary>
	/// Gets or initializes when the saga completed.
	/// </summary>
	/// <value>or initializes when the saga completed.</value>
	public DateTimeOffset? CompletedAt { get; init; }

	/// <summary>
	/// Gets or initializes the current step number.
	/// </summary>
	/// <value>or initializes the current step number.</value>
	public int CurrentStep { get; init; }

	/// <summary>
	/// Gets or initializes the total number of steps.
	/// </summary>
	/// <value>or initializes the total number of steps.</value>
	public int TotalSteps { get; init; }
}

