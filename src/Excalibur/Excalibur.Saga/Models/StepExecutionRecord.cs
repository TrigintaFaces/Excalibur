// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Saga.Models;

/// <summary>
/// Records the execution of a saga step.
/// </summary>
public sealed class StepExecutionRecord
{
	/// <summary>
	/// Gets or sets the step name.
	/// </summary>
	/// <value>the step name.</value>
	public string StepName { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the step index.
	/// </summary>
	/// <value>the step index.</value>
	public int StepIndex { get; set; }

	/// <summary>
	/// Gets or sets when the step started.
	/// </summary>
	/// <value>when the step started.</value>
	public DateTime StartedAt { get; set; }

	/// <summary>
	/// Gets or sets when the step completed.
	/// </summary>
	/// <value>when the step completed.</value>
	public DateTime? CompletedAt { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the step succeeded.
	/// </summary>
	/// <value><see langword="true"/> if the step succeeded.; otherwise, <see langword="false"/>.</value>
	public bool IsSuccess { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether compensation was executed.
	/// </summary>
	/// <value><see langword="true"/> if compensation was executed.; otherwise, <see langword="false"/>.</value>
	public bool WasCompensated { get; set; }

	/// <summary>
	/// Gets or sets the error message if failed.
	/// </summary>
	/// <value>the error message if failed.</value>
	public string? ErrorMessage { get; set; }

	/// <summary>
	/// Gets or sets the number of retry attempts.
	/// </summary>
	/// <value>the number of retry attempts.</value>
	public int RetryCount { get; set; }

	/// <summary>
	/// Gets the execution duration.
	/// </summary>
	/// <value>The execution duration from start to completion, or <see langword="null"/> if not yet completed.</value>
	public TimeSpan? Duration => CompletedAt.HasValue
		? CompletedAt.Value - StartedAt
		: null;
}

