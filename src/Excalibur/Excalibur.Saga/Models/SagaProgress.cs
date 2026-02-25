// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Saga.Models;

/// <summary>
/// Represents the progress and status of a saga execution.
/// </summary>
/// <remarks>
/// This class tracks execution progress (steps completed, current step) rather than the final result.
/// For typed saga results with data, use <see cref="Abstractions.SagaResult{TSagaData}"/>.
/// </remarks>
public sealed class SagaProgress
{
	/// <summary>
	/// Gets or sets the saga identifier.
	/// </summary>
	/// <value>the saga identifier.</value>
	public string SagaId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the saga status.
	/// </summary>
	/// <value>the saga status.</value>
	public SagaStatus Status { get; set; }

	/// <summary>
	/// Gets a value indicating whether the saga completed successfully.
	/// </summary>
	/// <value><see langword="true"/> if the saga completed successfully; otherwise, <see langword="false"/>.</value>
	public bool IsSuccess => Status == SagaStatus.Completed;

	/// <summary>
	/// Gets or sets when the saga started.
	/// </summary>
	/// <value>When the saga started.</value>
	public DateTime StartedAt { get; set; }

	/// <summary>
	/// Gets or sets when the saga completed.
	/// </summary>
	/// <value>When the saga completed, or <see langword="null"/> if not yet completed.</value>
	public DateTime? CompletedAt { get; set; }

	/// <summary>
	/// Gets the total duration.
	/// </summary>
	/// <value>The total duration from start to completion, or <see langword="null"/> if not yet completed.</value>
	public TimeSpan? Duration => CompletedAt.HasValue
		? CompletedAt.Value - StartedAt
		: null;

	/// <summary>
	/// Gets or sets the current step being executed.
	/// </summary>
	/// <value>the current step being executed.</value>
	public string? CurrentStep { get; set; }

	/// <summary>
	/// Gets or sets the error message if failed.
	/// </summary>
	/// <value>the error message if failed.</value>
	public string? ErrorMessage { get; set; }

	/// <summary>
	/// Gets or sets the number of completed steps.
	/// </summary>
	/// <value>the number of completed steps.</value>
	public int CompletedSteps { get; set; }

	/// <summary>
	/// Gets or sets the total number of steps.
	/// </summary>
	/// <value>the total number of steps.</value>
	public int TotalSteps { get; set; }
}
