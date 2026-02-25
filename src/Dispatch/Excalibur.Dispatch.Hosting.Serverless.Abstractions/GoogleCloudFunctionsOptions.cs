// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Hosting.Serverless;

/// <summary>
/// Google Cloud Functions specific configuration options.
/// </summary>
public sealed class GoogleCloudFunctionsOptions
{
	/// <summary>
	/// Gets or sets the runtime environment.
	/// </summary>
	/// <value>The runtime environment.</value>
	[Required]
	public string Runtime { get; set; } = "dotnet6";

	/// <summary>
	/// Gets or sets the minimum instance count.
	/// </summary>
	/// <value>The minimum instance count, or <see langword="null"/> to use no minimum.</value>
	[Range(0, int.MaxValue)]
	public int? MinInstances { get; set; }

	/// <summary>
	/// Gets or sets the maximum instance count.
	/// </summary>
	/// <value>The maximum instance count, or <see langword="null"/> to use no maximum.</value>
	[Range(1, int.MaxValue)]
	public int? MaxInstances { get; set; }

	/// <summary>
	/// Gets or sets the ingress settings.
	/// </summary>
	/// <value>The ingress settings.</value>
	[Required]
	public string IngressSettings { get; set; } = "ALLOW_ALL";

	/// <summary>
	/// Gets or sets the VPC connector.
	/// </summary>
	/// <value>The VPC connector.</value>
	public string? VpcConnector { get; set; }
}
