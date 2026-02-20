// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Hosting.Serverless;

/// <summary>
/// Azure Functions specific configuration options.
/// </summary>
public sealed class AzureFunctionsOptions
{
	/// <summary>
	/// Gets or sets the hosting plan type.
	/// </summary>
	/// <value>The hosting plan type.</value>
	[Required]
	public string HostingPlan { get; set; } = "Consumption";

	/// <summary>
	/// Gets or sets the Functions runtime version.
	/// </summary>
	/// <value>The Functions runtime version.</value>
	[Required]
	public string RuntimeVersion { get; set; } = "~4";

	/// <summary>
	/// Gets or sets a value indicating whether to enable Durable Functions.
	/// </summary>
	/// <value><see langword="true"/> to enable Durable Functions; otherwise, <see langword="false"/>.</value>
	public bool EnableDurableFunctions { get; set; }

	/// <summary>
	/// Gets or sets the storage account connection string.
	/// </summary>
	/// <value>The storage account connection string.</value>
	public string? StorageConnectionString { get; set; }
}
