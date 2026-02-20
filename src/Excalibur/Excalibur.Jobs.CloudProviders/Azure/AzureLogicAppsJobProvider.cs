// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Azure;
using Azure.ResourceManager;
using Azure.ResourceManager.Logic;

using Excalibur.Jobs.Abstractions;

using Microsoft.Extensions.Logging;

namespace Excalibur.Jobs.CloudProviders.Azure;

/// <summary>
/// Provides Azure Logic Apps integration for Excalibur background jobs.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="AzureLogicAppsJobProvider" /> class. </remarks>
/// <param name="armClient"> The Azure Resource Manager client. </param>
/// <param name="options"> Configuration options for Azure Logic Apps. </param>
/// <param name="logger"> Logger for this provider. </param>
public partial class AzureLogicAppsJobProvider(
	ArmClient armClient,
	AzureLogicAppsOptions options,
	ILogger<AzureLogicAppsJobProvider> logger)
{
	private readonly ArmClient _armClient = armClient ?? throw new ArgumentNullException(nameof(armClient));
	private readonly AzureLogicAppsOptions _options = options ?? throw new ArgumentNullException(nameof(options));
	private readonly ILogger<AzureLogicAppsJobProvider> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	/// <summary>
	/// Creates a scheduled Logic App workflow for a background job.
	/// </summary>
	/// <typeparam name="TJob"> The type of job to schedule. </typeparam>
	/// <param name="jobName"> The name of the job. </param>
	/// <param name="cronExpressionUnused"> Cron expression (unused - Logic Apps uses recurrence triggers). </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> A task that represents the asynchronous operation. </returns>
#pragma warning disable IDE0060 // Intentional: Parameter required by interface but Logic Apps doesn't use cron
	public async Task ScheduleJobAsync<TJob>(string jobName, string cronExpressionUnused, CancellationToken cancellationToken)
#pragma warning restore IDE0060
		where TJob : class, IBackgroundJob
	{
		try
		{
			var subscription = await _armClient.GetDefaultSubscriptionAsync(cancellationToken).ConfigureAwait(false);
			var resourceGroup = await subscription.GetResourceGroupAsync(_options.ResourceGroupName, cancellationToken)
				.ConfigureAwait(false);

			var workflowDefinition = CreateWorkflowDefinition<TJob>(jobName);

			var workflow = new LogicWorkflowData(_options.Location) { Definition = workflowDefinition };

			var workflowName = $"EXCALIBUR-JOB-{jobName.ToUpperInvariant()}";
			_ = await resourceGroup.Value.GetLogicWorkflows().CreateOrUpdateAsync(
				WaitUntil.Completed,
				workflowName,
				workflow,
				cancellationToken).ConfigureAwait(false);

			LogCreatedWorkflowSuccess(workflowName, typeof(TJob).Name);
		}
		catch (Exception ex)
		{
			LogFailedToCreateWorkflow(jobName, typeof(TJob).Name, ex);
			throw;
		}
	}

	/// <summary>
	/// Deletes a Logic App workflow.
	/// </summary>
	/// <param name="jobName"> The name of the job to delete. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> A task that represents the asynchronous operation. </returns>
	public async Task DeleteJobAsync(string jobName, CancellationToken cancellationToken)
	{
		try
		{
			var subscription = await _armClient.GetDefaultSubscriptionAsync(cancellationToken).ConfigureAwait(false);
			var resourceGroup = await subscription.GetResourceGroupAsync(_options.ResourceGroupName, cancellationToken)
				.ConfigureAwait(false);

			var workflowName = $"EXCALIBUR-JOB-{jobName.ToUpperInvariant()}";
			var workflow = await resourceGroup.Value.GetLogicWorkflowAsync(workflowName, cancellationToken).ConfigureAwait(false);

			if (workflow.HasValue)
			{
				_ = await workflow.Value.DeleteAsync(WaitUntil.Completed, cancellationToken).ConfigureAwait(false);
				LogDeletedWorkflowSuccess(workflowName);
			}
			else
			{
				LogWorkflowNotFoundForDeletion(workflowName);
			}
		}
		catch (Exception ex)
		{
			LogFailedToDeleteWorkflow(jobName, ex);
			throw;
		}
	}

	/// <summary>
	/// Creates a Logic App workflow definition with recurrence trigger.
	/// </summary>
	/// <typeparam name="TJob"> The job type. </typeparam>
	/// <param name="jobName"> The job name. </param>
	/// <returns> The workflow definition as a BinaryData object. </returns>
	[RequiresUnreferencedCode("Calls System.BinaryData.FromObjectAsJson<T>(T, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.BinaryData.FromObjectAsJson<T>(T, JsonSerializerOptions)")]
	private BinaryData CreateWorkflowDefinition<TJob>(string jobName)
		where TJob : class, IBackgroundJob
	{
		var definition = new
		{
			contentVersion = "1.0.0.0",
			parameters = new { },
			triggers = new
			{
				recurrence = new
				{
					type = "recurrence",
					recurrence = new
					{
						frequency = "minute",
						interval = 5, // This would need proper cron parsing
					},
				},
			},
			actions = new
			{
				http_request = new
				{
					type = "Http",
					inputs = new
					{
						method = "POST",
						uri = _options.JobExecutionEndpoint,
						headers = new { ContentType = "application/json" },
						body = new { jobType = typeof(TJob).AssemblyQualifiedName, jobName },
					},
				},
			},
		};

		return BinaryData.FromObjectAsJson(definition);
	}
}
