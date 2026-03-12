// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Data.DynamoDb;

/// <summary>
/// Connection and credential configuration options for AWS DynamoDB.
/// </summary>
/// <remarks>
/// This sub-options class is part of the <see cref="DynamoDbOptions"/> ISP split
/// to keep each class within the 10-property gate.
/// </remarks>
public sealed class DynamoDbConnectionOptions
{
	/// <summary>
	/// Gets or sets the AWS service URL (for local development with DynamoDB Local).
	/// </summary>
	public string? ServiceUrl { get; set; }

	/// <summary>
	/// Gets or sets the AWS region.
	/// </summary>
	public string? Region { get; set; }

	/// <summary>
	/// Gets or sets the AWS access key (optional if using IAM roles).
	/// </summary>
	public string? AccessKey { get; set; }

	/// <summary>
	/// Gets or sets the AWS secret key (optional if using IAM roles).
	/// </summary>
	public string? SecretKey { get; set; }

	/// <summary>
	/// Gets or sets the maximum retry attempts.
	/// </summary>
	[Range(1, int.MaxValue)]
	public int MaxRetryAttempts { get; set; } = 3;

	/// <summary>
	/// Gets or sets the timeout in seconds.
	/// </summary>
	[Range(1, int.MaxValue)]
	public int TimeoutInSeconds { get; set; } = 30;

	/// <summary>
	/// Gets or sets the read capacity units for on-demand scaling hints.
	/// </summary>
	public int? ReadCapacityUnits { get; set; }

	/// <summary>
	/// Gets or sets the write capacity units for on-demand scaling hints.
	/// </summary>
	public int? WriteCapacityUnits { get; set; }
}
