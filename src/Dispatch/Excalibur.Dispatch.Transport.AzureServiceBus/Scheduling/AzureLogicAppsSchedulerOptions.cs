// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// Configuration options for Azure Logic Apps scheduler.
/// </summary>
public sealed class AzureLogicAppsSchedulerOptions
{
	/// <summary>
	/// Gets or sets the subscription ID.
	/// </summary>
	/// <value>
	/// The subscription ID.
	/// </value>
	public string? SubscriptionId { get; set; }

	/// <summary>
	/// Gets or sets the resource group name.
	/// </summary>
	/// <value>
	/// The resource group name.
	/// </value>
	public string? ResourceGroupName { get; set; }

	/// <summary>
	/// Gets or sets the Logic App name.
	/// </summary>
	/// <value>
	/// The Logic App name.
	/// </value>
	public string? LogicAppName { get; set; }

	/// <summary>
	/// Gets or sets the workflow name.
	/// </summary>
	/// <value>
	/// The workflow name.
	/// </value>
	public string? WorkflowName { get; set; }

	/// <summary>
	/// Gets or sets the callback URL.
	/// </summary>
	/// <value>
	/// The callback URL.
	/// </value>
	public Uri? CallbackUrl { get; set; }

	/// <summary>
	/// Gets or sets the trigger name to resolve the callback URL when it is not provided.
	/// </summary>
	/// <value>
	/// The trigger name for the workflow.
	/// </value>
	public string? TriggerName { get; set; }

	/// <summary>
	/// Gets or sets the maximum number of retries.
	/// </summary>
	/// <value>
	/// The maximum number of retries.
	/// </value>
	public int MaxRetries { get; set; } = 3;

	/// <summary>
	/// Gets or sets the retry delay in seconds.
	/// </summary>
	/// <value>
	/// The retry delay in seconds.
	/// </value>
	public int RetryDelaySeconds { get; set; } = 60;
}
