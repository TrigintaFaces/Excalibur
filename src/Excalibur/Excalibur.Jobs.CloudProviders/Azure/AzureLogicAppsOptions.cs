// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Azure.Core;

namespace Excalibur.Jobs.CloudProviders.Azure;

/// <summary>
/// Configuration options for Azure Logic Apps integration.
/// </summary>
public sealed class AzureLogicAppsOptions
{
	/// <summary>
	/// Gets or sets the name of the Azure resource group containing the Logic Apps.
	/// </summary>
	/// <value> The resource group name. </value>
	public required string ResourceGroupName { get; set; }

	/// <summary>
	/// Gets or sets the Azure subscription ID.
	/// </summary>
	/// <value> The subscription ID. </value>
	public required string SubscriptionId { get; set; }

	/// <summary>
	/// Gets or sets the Azure location for Logic Apps.
	/// </summary>
	/// <value> The Azure location. Defaults to "East US". </value>
	public AzureLocation Location { get; set; } = AzureLocation.EastUS;

	/// <summary>
	/// Gets or sets the HTTP endpoint that Logic Apps will call to execute jobs.
	/// </summary>
	/// <value> The job execution endpoint URL. </value>
	public required string JobExecutionEndpoint { get; set; }

	/// <summary>
	/// Gets or sets additional tags to apply to created Logic Apps.
	/// </summary>
	/// <value> A dictionary of tags. </value>
	public Dictionary<string, string> Tags { get; set; } = [];
}
