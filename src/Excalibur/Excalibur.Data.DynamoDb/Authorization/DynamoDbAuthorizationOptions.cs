// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

using Amazon;

namespace Excalibur.Data.DynamoDb.Authorization;

/// <summary>
/// Configuration options for DynamoDB authorization stores.
/// </summary>
public sealed class DynamoDbAuthorizationOptions
{
	/// <summary>
	/// Gets or sets the AWS service URL (for local development with LocalStack or DynamoDB Local).
	/// </summary>
	/// <value>The service URL. If set, takes precedence over Region.</value>
	public string? ServiceUrl { get; set; }

	/// <summary>
	/// Gets or sets the AWS region.
	/// </summary>
	/// <value>The AWS region name (e.g., "us-east-1").</value>
	public string? Region { get; set; }

	/// <summary>
	/// Gets or sets the AWS access key (optional if using IAM roles).
	/// </summary>
	/// <value>The AWS access key.</value>
	public string? AccessKey { get; set; }

	/// <summary>
	/// Gets or sets the AWS secret key (optional if using IAM roles).
	/// </summary>
	/// <value>The AWS secret key.</value>
	public string? SecretKey { get; set; }

	/// <summary>
	/// Gets or sets the table name for grants.
	/// </summary>
	/// <value>The grants table name. Defaults to "authorization_grants".</value>
	[Required]
	public string GrantsTableName { get; set; } = "authorization_grants";

	/// <summary>
	/// Gets or sets the table name for activity groups.
	/// </summary>
	/// <value>The activity groups table name. Defaults to "authorization_activity_groups".</value>
	[Required]
	public string ActivityGroupsTableName { get; set; } = "authorization_activity_groups";

	/// <summary>
	/// Gets or sets the name of the Global Secondary Index for user-based queries.
	/// </summary>
	/// <value>The GSI name. Defaults to "UserIndex".</value>
	[Required]
	public string UserIndexName { get; set; } = "UserIndex";

	/// <summary>
	/// Gets or sets the maximum retry attempts for transient failures.
	/// </summary>
	/// <value>The maximum retry attempts. Defaults to 3.</value>
	[Range(1, int.MaxValue)]
	public int MaxRetryAttempts { get; set; } = 3;

	/// <summary>
	/// Gets or sets the request timeout in seconds.
	/// </summary>
	/// <value>The timeout in seconds. Defaults to 30.</value>
	[Range(1, int.MaxValue)]
	public int TimeoutInSeconds { get; set; } = 30;

	/// <summary>
	/// Gets or sets a value indicating whether to use consistent reads.
	/// </summary>
	/// <value><see langword="true"/> to use consistent reads; otherwise, <see langword="false"/>. Defaults to true.</value>
	public bool UseConsistentReads { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to create tables if they don't exist.
	/// </summary>
	/// <value><see langword="true"/> to create tables if not exists; otherwise, <see langword="false"/>. Defaults to false.</value>
	/// <remarks>
	/// This is useful for local development and testing. In production, tables should be
	/// provisioned through infrastructure as code.
	/// </remarks>
	public bool CreateTableIfNotExists { get; set; }

	/// <summary>
	/// Gets the AWS region endpoint.
	/// </summary>
	/// <returns>The AWS region endpoint, or null if not configured.</returns>
	public RegionEndpoint? GetRegionEndpoint() =>
		string.IsNullOrWhiteSpace(Region) ? null : RegionEndpoint.GetBySystemName(Region);

	/// <summary>
	/// Validates the options and throws if invalid.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown when required options are missing.</exception>
	public void Validate()
	{
		var hasLocalConfig = !string.IsNullOrWhiteSpace(ServiceUrl);
		var hasAwsConfig = !string.IsNullOrWhiteSpace(Region);

		if (!hasLocalConfig && !hasAwsConfig)
		{
			throw new InvalidOperationException(
				"Either ServiceUrl (for local development) or Region (for AWS) must be provided.");
		}

		if (string.IsNullOrWhiteSpace(GrantsTableName))
		{
			throw new InvalidOperationException($"{nameof(GrantsTableName)} is required.");
		}

		if (string.IsNullOrWhiteSpace(ActivityGroupsTableName))
		{
			throw new InvalidOperationException($"{nameof(ActivityGroupsTableName)} is required.");
		}

		if (string.IsNullOrWhiteSpace(UserIndexName))
		{
			throw new InvalidOperationException($"{nameof(UserIndexName)} is required.");
		}
	}
}
