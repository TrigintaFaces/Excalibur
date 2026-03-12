// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Outbox.DynamoDb;

/// <summary>
/// Connection options for the DynamoDB outbox store.
/// </summary>
public sealed class DynamoDbOutboxConnectionOptions
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
}
