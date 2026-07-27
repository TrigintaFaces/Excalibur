// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;

using Azure;
using Azure.ResourceManager;
using Azure.ResourceManager.Logic;

using Excalibur.Jobs.Azure.Internal;

using Microsoft.Extensions.Logging;

namespace Excalibur.Jobs.Azure;

/// <summary>
/// Provides Azure Logic Apps integration for Excalibur background jobs.
/// </summary>
/// <remarks>
/// Does not implement <see cref="IDisposable"/>: the injected <see cref="ArmClient"/> (and the
/// <see cref="IArmClientSeam"/> adapter over it) owns no disposable resource of its own — its lifetime
/// and any underlying HTTP pipeline are owned by the caller/DI container that constructed it, matching
/// the Azure SDK's own <c>ArmClient</c> contract.
/// </remarks>
public sealed partial class AzureLogicAppsJobProvider : IJobSchedulerProvider
{
	private readonly IArmClientSeam _armClient;
	private readonly AzureLogicAppsOptions _options;
	private readonly ILogger<AzureLogicAppsJobProvider> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="AzureLogicAppsJobProvider"/> class
	/// with an existing <see cref="ArmClient"/>.
	/// </summary>
	/// <param name="armClient">The Azure Resource Manager client.</param>
	/// <param name="options">Configuration options for Azure Logic Apps.</param>
	/// <param name="logger">Logger for this provider.</param>
	[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
		Justification = "Adapter is stored in _armClient field and lives for the provider's lifetime.")]
	public AzureLogicAppsJobProvider(
		ArmClient armClient,
		AzureLogicAppsOptions options,
		ILogger<AzureLogicAppsJobProvider> logger)
		: this(CreateAdapter(armClient), options, logger)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="AzureLogicAppsJobProvider"/>
	/// class using a pre-built adapter. Used by tests to substitute the SDK via
	/// the <see cref="IArmClientSeam"/> seam.
	/// </summary>
	internal AzureLogicAppsJobProvider(
		IArmClientSeam armClient,
		AzureLogicAppsOptions options,
		ILogger<AzureLogicAppsJobProvider> logger)
	{
		_armClient = armClient ?? throw new ArgumentNullException(nameof(armClient));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	private static IArmClientSeam CreateAdapter(ArmClient armClient)
	{
		ArgumentNullException.ThrowIfNull(armClient);
		return new ArmClientAdapter(armClient);
	}

	/// <summary>
	/// Creates a scheduled Logic App workflow for a background job.
	/// </summary>
	/// <typeparam name="TJob"> The type of job to schedule. </typeparam>
	/// <param name="jobName"> The name of the job. </param>
	/// <param name="cronExpression">
	/// The 5-field cron expression describing the recurrence schedule. Mapped to an Azure Logic Apps
	/// recurrence trigger; expressions outside the supported subset throw <see cref="NotSupportedException"/>.
	/// </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> A task that represents the asynchronous operation. </returns>
	/// <exception cref="ArgumentException"> <paramref name="cronExpression"/> is not a valid cron expression. </exception>
	/// <exception cref="NotSupportedException">
	/// <paramref name="cronExpression"/> cannot be represented as an Azure Logic Apps recurrence trigger.
	/// </exception>
	public async Task ScheduleJobAsync<TJob>(string jobName, string cronExpression, CancellationToken cancellationToken)
		where TJob : class, IBackgroundJob
	{
		var recurrence = CronRecurrenceMapper.Map(cronExpression);

		try
		{
			var subscription = await _armClient.GetDefaultSubscriptionAsync(cancellationToken).ConfigureAwait(false);
			var resourceGroup = await subscription.GetResourceGroupAsync(_options.ResourceGroupName, cancellationToken)
				.ConfigureAwait(false);

			var workflowDefinition = CreateWorkflowDefinition<TJob>(jobName, recurrence);

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
	/// Creates a Logic App workflow definition with a recurrence trigger honoring the given schedule.
	/// </summary>
	/// <typeparam name="TJob"> The job type. </typeparam>
	/// <param name="jobName"> The job name. </param>
	/// <param name="recurrence"> The recurrence trigger shape mapped from the configured cron expression. </param>
	/// <returns> The workflow definition as a BinaryData object. </returns>
	/// <remarks>
	/// Built as a hand-assembled <see cref="JsonObject"/> tree (never via reflection-based object
	/// serialization) so this method requires no unreferenced-code or dynamic-code capability and is
	/// safe under trimming and Native AOT.
	/// </remarks>
	private BinaryData CreateWorkflowDefinition<TJob>(string jobName, AzureRecurrence recurrence)
		where TJob : class, IBackgroundJob
	{
		var recurrenceValue = new JsonObject
		{
			["frequency"] = recurrence.Frequency,
			["interval"] = recurrence.Interval,
		};

		if (recurrence.Hours is not null || recurrence.Minutes is not null || recurrence.WeekDays is not null)
		{
			recurrenceValue["schedule"] = new JsonObject
			{
				["hours"] = ToJsonArray(recurrence.Hours),
				["minutes"] = ToJsonArray(recurrence.Minutes),
				["weekDays"] = ToJsonArray(recurrence.WeekDays),
			};
		}

		var definition = new JsonObject
		{
			["contentVersion"] = "1.0.0.0",
			["parameters"] = new JsonObject(),
			["triggers"] = new JsonObject
			{
				["recurrence"] = new JsonObject
				{
					["type"] = "recurrence",
					["recurrence"] = recurrenceValue,
				},
			},
			["actions"] = new JsonObject
			{
				["http_request"] = new JsonObject
				{
					["type"] = "Http",
					["inputs"] = new JsonObject
					{
						["method"] = "POST",
						["uri"] = _options.JobExecutionEndpoint,
						["headers"] = new JsonObject { ["ContentType"] = "application/json" },
						["body"] = new JsonObject
						{
							["jobType"] = typeof(TJob).AssemblyQualifiedName,
							["jobName"] = jobName,
						},
					},
				},
			},
		};

		return BinaryData.FromString(definition.ToJsonString());
	}

	private static JsonArray? ToJsonArray(int[]? values) =>
		values is null ? null : new JsonArray([.. values.Select(value => (JsonNode?)JsonValue.Create(value))]);

	private static JsonArray? ToJsonArray(string[]? values) =>
		values is null ? null : new JsonArray([.. values.Select(value => (JsonNode?)JsonValue.Create(value))]);
}
