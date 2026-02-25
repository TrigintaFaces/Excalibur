// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Saga.Models;

/// <summary>
/// Represents the state of a single saga step.
/// </summary>
public sealed class SagaStepState
{
	/// <summary>
	/// Gets or sets the step name.
	/// </summary>
	/// <value>the step name.</value>
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the step status.
	/// </summary>
	/// <value>the step status.</value>
	public StepStatus Status { get; set; }

	/// <summary>
	/// Gets or sets when the step started.
	/// </summary>
	/// <value>when the step started.</value>
	public DateTime? StartedAt { get; set; }

	/// <summary>
	/// Gets or sets when the step completed.
	/// </summary>
	/// <value>when the step completed.</value>
	public DateTime? CompletedAt { get; set; }

	/// <summary>
	/// Gets or sets the number of attempts.
	/// </summary>
	/// <value>the number of attempts.</value>
	public int Attempts { get; set; }

	/// <summary>
	/// Gets or sets the error message if failed.
	/// </summary>
	/// <value>the error message if failed.</value>
	public string? ErrorMessage { get; set; }

	/// <summary>
	/// Gets or sets the compensation status.
	/// </summary>
	/// <value>the compensation status.</value>
	public CompensationStatus CompensationStatus { get; set; }

	/// <summary>
	/// Gets or sets when compensation started.
	/// </summary>
	/// <value>when compensation started.</value>
	public DateTime? CompensationStartedAt { get; set; }

	/// <summary>
	/// Gets or sets when compensation completed.
	/// </summary>
	/// <value>when compensation completed.</value>
	public DateTime? CompensationCompletedAt { get; set; }

	/// <summary>
	/// Gets or sets the compensation error if failed.
	/// </summary>
	/// <value>the compensation error if failed.</value>
	public string? CompensationError { get; set; }

	/// <summary>
	/// Gets or sets step-specific data.
	/// </summary>
	/// <value>step-specific data.</value>
	public string? StepDataJson { get; set; }
}

