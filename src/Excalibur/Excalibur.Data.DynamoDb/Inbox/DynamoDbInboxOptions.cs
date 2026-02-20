// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

using Amazon;

namespace Excalibur.Data.DynamoDb.Inbox;

/// <summary>
/// Configuration options for the DynamoDB inbox store.
/// </summary>
public sealed class DynamoDbInboxOptions
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
	/// Gets or sets the table name for inbox messages.
	/// </summary>
	[Required]
	public string TableName { get; set; } = "inbox_messages";

	/// <summary>
	/// Gets or sets the partition key attribute name.
	/// </summary>
	/// <remarks>
	/// Uses handler_type as partition key for optimal query patterns where
	/// messages are typically queried by handler type.
	/// </remarks>
	[Required]
	public string PartitionKeyAttribute { get; set; } = "handler_type";

	/// <summary>
	/// Gets or sets the sort key attribute name.
	/// </summary>
	/// <remarks>
	/// Uses message_id as sort key for uniqueness within a handler.
	/// </remarks>
	[Required]
	public string SortKeyAttribute { get; set; } = "message_id";

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
	/// Gets or sets a value indicating whether to use consistent reads.
	/// </summary>
	public bool UseConsistentReads { get; set; } = true;

	/// <summary>
	/// Gets or sets the default TTL in seconds for processed entries.
	/// </summary>
	/// <remarks>
	/// Set to 0 for no expiration. Defaults to 7 days (604800 seconds).
	/// Requires TTL to be enabled on the DynamoDB table.
	/// </remarks>
	[Range(0, int.MaxValue)]
	public int DefaultTtlSeconds { get; set; } = 604800;

	/// <summary>
	/// Gets or sets the name of the TTL attribute on the table.
	/// </summary>
	[Required]
	public string TtlAttributeName { get; set; } = "ttl";

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
