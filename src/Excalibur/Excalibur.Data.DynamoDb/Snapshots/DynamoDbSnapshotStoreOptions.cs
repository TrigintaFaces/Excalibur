// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

using Amazon;

namespace Excalibur.Data.DynamoDb.Snapshots;

/// <summary>
/// Configuration options for the DynamoDB snapshot store.
/// </summary>
public sealed class DynamoDbSnapshotStoreOptions
{
	/// <summary>
	/// Gets or sets the AWS service URL (for local development with DynamoDB Local or LocalStack).
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
	/// Gets or sets the table name for snapshots.
	/// </summary>
	/// <value>Defaults to "snapshots".</value>
	[Required]
	public string TableName { get; set; } = "snapshots";

	/// <summary>
	/// Gets or sets the maximum retry attempts for DynamoDB operations.
	/// </summary>
	/// <value>Defaults to 3.</value>
	[Range(1, int.MaxValue)]
	public int MaxRetryAttempts { get; set; } = 3;

	/// <summary>
	/// Gets or sets the timeout in seconds for DynamoDB operations.
	/// </summary>
	/// <value>Defaults to 30 seconds.</value>
	[Range(1, int.MaxValue)]
	public int TimeoutInSeconds { get; set; } = 30;

	/// <summary>
	/// Gets or sets a value indicating whether to use consistent reads.
	/// </summary>
	/// <value>Defaults to <see langword="true"/>.</value>
	public bool UseConsistentReads { get; set; } = true;

	/// <summary>
	/// Gets or sets the default TTL in seconds for snapshots.
	/// </summary>
	/// <remarks>
	/// Set to 0 for no expiration. When set, snapshots will be automatically
	/// deleted by DynamoDB after the specified time.
	/// Requires TTL to be enabled on the DynamoDB table with the "ttl" attribute.
	/// </remarks>
	/// <value>Defaults to 0 (no TTL).</value>
	[Range(0, int.MaxValue)]
	public int DefaultTtlSeconds { get; set; }

	/// <summary>
	/// Gets or sets the name of the TTL attribute on the table.
	/// </summary>
	/// <value>Defaults to "ttl".</value>
	[Required]
	public string TtlAttributeName { get; set; } = "ttl";

	/// <summary>
	/// Gets or sets a value indicating whether to create the table if it doesn't exist.
	/// </summary>
	/// <value>Defaults to <see langword="true"/>.</value>
	public bool CreateTableIfNotExists { get; set; } = true;

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

		if (string.IsNullOrWhiteSpace(TableName))
		{
			throw new InvalidOperationException("TableName is required.");
		}
	}
}
