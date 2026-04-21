// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.EventSourcing.AwsS3.DependencyInjection;

/// <summary>
/// Configuration options for the AWS S3 cold event store.
/// </summary>
public sealed class AwsS3ColdEventStoreOptions
{
	/// <summary>
	/// Gets or sets the S3 bucket name for cold event storage.
	/// </summary>
	[Required]
	public string? BucketName { get; set; }

	/// <summary>
	/// Gets or sets the key prefix for archived event objects.
	/// </summary>
	/// <value>Default is "excalibur-cold-events".</value>
	public string KeyPrefix { get; set; } = "excalibur-cold-events";

	/// <summary>
	/// Gets or sets the AWS region for the S3 bucket.
	/// </summary>
	/// <value>If null, uses the default SDK region configuration.</value>
	public string? Region { get; set; }
}
