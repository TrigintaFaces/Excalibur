// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Jobs.Workflows;

/// <summary>
/// Represents the result of a workflow execution.
/// </summary>
/// <typeparam name="TOutput"> The type of output data from the workflow. </typeparam>
/// <param name="IsSuccess"> Indicates whether the workflow executed successfully. </param>
/// <param name="Output"> The output data from the workflow execution. </param>
/// <param name="Error"> Optional error information if the workflow failed. </param>
/// <param name="Status"> The final status of the workflow execution. </param>
public sealed record WorkflowResult<TOutput>(
	bool IsSuccess,
	TOutput? Output = default,
	Exception? Error = null,
	WorkflowStatus Status = WorkflowStatus.Completed)
{
	/// <summary>
	/// Gets the time when the workflow completed.
	/// </summary>
	/// <value>
	/// The time when the workflow completed.
	/// </value>
	public DateTimeOffset CompletedAt { get; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Creates a successful workflow result.
	/// </summary>
	/// <param name="output"> The output data from the workflow. </param>
	/// <returns> A successful workflow result. </returns>
	public static WorkflowResult<TOutput> Success(TOutput output) => new(IsSuccess: true, Output: output);

	/// <summary>
	/// Creates a failed workflow result.
	/// </summary>
	/// <param name="error"> The error that caused the workflow to fail. </param>
	/// <returns> A failed workflow result. </returns>
	public static WorkflowResult<TOutput> Failure(Exception error) => new(IsSuccess: false, Error: error, Status: WorkflowStatus.Failed);

	/// <summary>
	/// Creates a suspended workflow result that indicates the workflow is waiting for external input.
	/// </summary>
	/// <param name="reason"> Optional reason for suspension. </param>
	/// <returns> A suspended workflow result. </returns>
	public static WorkflowResult<TOutput> Suspended(string? reason = null) =>
		new(IsSuccess: false, Error: reason != null ? new InvalidOperationException(reason) : null, Status: WorkflowStatus.Suspended);
}
