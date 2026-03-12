// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

using Amazon;

using Excalibur.Data.DynamoDb;

namespace Excalibur.Inbox.DynamoDb;

/// <summary>
/// Configuration options for the DynamoDB inbox store.
/// </summary>
/// <remarks>
/// <para>
/// Connection properties (ServiceUrl, Region, credentials) are in <see cref="Connection"/>.
/// This follows the <c>AmazonDynamoDBConfig</c> pattern of separating connection from table configuration.
/// </para>
/// </remarks>
public sealed class DynamoDbInboxOptions
{
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
	/// Gets or sets the connection and credential options.
	/// </summary>
	public DynamoDbConnectionOptions Connection { get; set; } = new();

	/// <summary>
	/// Gets the AWS region endpoint.
	/// </summary>
	/// <returns>The AWS region endpoint, or null if not configured.</returns>
	public RegionEndpoint? GetRegionEndpoint() =>
		string.IsNullOrWhiteSpace(Connection.Region) ? null : RegionEndpoint.GetBySystemName(Connection.Region);

	/// <summary>
	/// Validates the options and throws if invalid.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown when required options are missing.</exception>
	public void Validate()
	{
		var hasLocalConfig = !string.IsNullOrWhiteSpace(Connection.ServiceUrl);
		var hasAwsConfig = !string.IsNullOrWhiteSpace(Connection.Region);

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
