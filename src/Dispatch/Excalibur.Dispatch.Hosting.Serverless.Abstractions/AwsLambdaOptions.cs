// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Hosting.Serverless;

/// <summary>
/// AWS Lambda specific configuration options.
/// </summary>
public sealed class AwsLambdaOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether to enable provisioned concurrency.
	/// </summary>
	/// <value><see langword="true"/> to enable provisioned concurrency; otherwise, <see langword="false"/>.</value>
	public bool EnableProvisionedConcurrency { get; set; }

	/// <summary>
	/// Gets or sets the reserved concurrency limit.
	/// </summary>
	/// <value>The reserved concurrency limit, or <see langword="null"/> to use no limit.</value>
	[Range(1, int.MaxValue)]
	public int? ReservedConcurrency { get; set; }

	/// <summary>
	/// Gets or sets the Lambda runtime.
	/// </summary>
	/// <value>The Lambda runtime.</value>
	[Required]
	public string Runtime { get; set; } = "dotnet8";

	/// <summary>
	/// Gets or sets the handler name.
	/// </summary>
	/// <value>The handler name.</value>
	public string? Handler { get; set; }

	/// <summary>
	/// Gets or sets the deployment package type.
	/// </summary>
	/// <value>The deployment package type.</value>
	[Required]
	public string PackageType { get; set; } = "Zip";
}
