// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Jobs.Abstractions;

using Microsoft.Extensions.Logging;

namespace Excalibur.Jobs.Workflows;

/// <summary>
/// A job that executes workflows with orchestration capabilities.
/// </summary>
/// <typeparam name="TWorkflow"> The type of workflow to execute. </typeparam>
/// <typeparam name="TInput"> The type of input data for the workflow. </typeparam>
/// <typeparam name="TOutput"> The type of output data from the workflow. </typeparam>
/// <remarks> Initializes a new instance of the <see cref="WorkflowJob{TWorkflow, TInput, TOutput}" /> class. </remarks>
/// <param name="workflow"> The workflow to execute. </param>
/// <param name="logger"> The logger for this job. </param>
#pragma warning disable CA1005 // Intentional: 3 type parameters required for workflow/input/output type safety
public sealed class WorkflowJob<TWorkflow, TInput, TOutput>(TWorkflow workflow, ILogger<WorkflowJob<TWorkflow, TInput, TOutput>> logger)
#pragma warning restore CA1005
	: IBackgroundJob<WorkflowJobContext<TInput>>
	where TWorkflow : class, IWorkflow<TInput, TOutput>
{
	private readonly TWorkflow _workflow = workflow ?? throw new ArgumentNullException(nameof(workflow));
	private readonly ILogger<WorkflowJob<TWorkflow, TInput, TOutput>> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	/// <inheritdoc />
	public async Task ExecuteAsync(WorkflowJobContext<TInput> context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(context);

		WorkflowJobLog.JobStarting(_logger, typeof(TWorkflow).Name, context.InstanceId);

		var workflowContext = new WorkflowContext(context.InstanceId, context.CorrelationId);

		try
		{
			var result = await _workflow.ExecuteAsync(context.Input, workflowContext, cancellationToken).ConfigureAwait(false);

			if (result.IsSuccess)
			{
				WorkflowJobLog.JobCompleted(_logger, typeof(TWorkflow).Name, context.InstanceId);
			}
			else
			{
				WorkflowJobLog.JobFailed(_logger, result.Error, typeof(TWorkflow).Name, context.InstanceId, result.Status.ToString());
			}
		}
		catch (Exception ex)
		{
			WorkflowJobLog.UnhandledException(_logger, ex, typeof(TWorkflow).Name, context.InstanceId);
			throw;
		}
	}
}
