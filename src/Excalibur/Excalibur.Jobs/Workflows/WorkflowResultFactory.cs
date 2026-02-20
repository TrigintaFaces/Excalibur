// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Jobs.Workflows;

/// <summary>
/// Provides factory helpers for creating <see cref="WorkflowResult{TOutput}" /> instances.
/// </summary>
public static class WorkflowResultFactory
{
	/// <summary>
	/// Creates a successful workflow result.
	/// </summary>
	/// <typeparam name="TOutput">The type of workflow output.</typeparam>
	/// <param name="output">The output data from the workflow.</param>
	/// <returns>A successful workflow result.</returns>
	public static WorkflowResult<TOutput> Success<TOutput>(TOutput output) => new(IsSuccess: true, Output: output);

	/// <summary>
	/// Creates a failed workflow result.
	/// </summary>
	/// <typeparam name="TOutput">The type of workflow output.</typeparam>
	/// <param name="error">The error that caused the workflow to fail.</param>
	/// <returns>A failed workflow result.</returns>
	/// <exception cref="ArgumentNullException">Thrown when the operation fails.</exception>
	public static WorkflowResult<TOutput> Failure<TOutput>(Exception error) =>
		new(IsSuccess: false, Error: error ?? throw new ArgumentNullException(nameof(error)), Status: WorkflowStatus.Failed);

	/// <summary>
	/// Creates a suspended workflow result that indicates the workflow is waiting for external input.
	/// </summary>
	/// <typeparam name="TOutput">The type of workflow output.</typeparam>
	/// <param name="reason">Optional reason for suspension.</param>
	/// <returns>A suspended workflow result.</returns>
	public static WorkflowResult<TOutput> Suspended<TOutput>(string? reason = null) =>
		new(IsSuccess: false, Error: reason is not null ? new InvalidOperationException(reason) : null, Status: WorkflowStatus.Suspended);
}
