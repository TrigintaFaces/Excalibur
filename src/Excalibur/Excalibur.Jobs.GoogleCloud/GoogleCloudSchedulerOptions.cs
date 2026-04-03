// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Jobs.GoogleCloud;

/// <summary>
/// Configuration options for Google Cloud Scheduler integration.
/// </summary>
public sealed class GoogleCloudSchedulerOptions
{
	/// <summary>
	/// Gets or sets the Google Cloud project ID.
	/// </summary>
	/// <value> The GCP project ID. </value>
	[Required]
	public required string ProjectId { get; set; }

	/// <summary>
	/// Gets or sets the Google Cloud location ID (e.g., "us-central1").
	/// </summary>
	/// <value> The location ID. </value>
	[Required]
	public required string LocationId { get; set; }

	/// <summary>
	/// Gets or sets the time zone for schedule expressions.
	/// </summary>
	/// <value> The time zone. Defaults to "UTC". </value>
	[Required]
	public string TimeZone { get; set; } = "UTC";

	/// <summary>
	/// Gets or sets the HTTP target URL for job execution.
	/// </summary>
	/// <value> The target URL that Cloud Scheduler will call. </value>
	[Required]
	public required string TargetUrl { get; set; }
}
