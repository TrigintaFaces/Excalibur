// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Jobs.Workflows;

/// <summary>
/// Represents a workflow that can be executed with orchestration capabilities.
/// </summary>
/// <typeparam name="TInput"> The type of input data for the workflow. </typeparam>
/// <typeparam name="TOutput"> The type of output data from the workflow. </typeparam>
public interface IWorkflow<in TInput, TOutput>
{
	/// <summary>
	/// Executes the workflow with the specified input data.
	/// </summary>
	/// <param name="input"> The input data for the workflow. </param>
	/// <param name="context"> The workflow execution context. </param>
	/// <param name="cancellationToken"> A token to monitor for cancellation requests. </param>
	/// <returns> A task that represents the asynchronous operation, containing the workflow result. </returns>
	Task<WorkflowResult<TOutput>> ExecuteAsync(TInput input, IWorkflowContext context, CancellationToken cancellationToken);
}

/// <summary>
/// Represents a simple workflow with no input parameters.
/// </summary>
/// <typeparam name="TOutput"> The type of output data from the workflow. </typeparam>
public interface IWorkflow<TOutput> : IWorkflow<object?, TOutput>;
