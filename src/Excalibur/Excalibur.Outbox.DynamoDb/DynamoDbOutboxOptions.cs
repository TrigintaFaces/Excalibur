// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

using Amazon;

namespace Excalibur.Outbox.DynamoDb;

/// <summary>
/// Configuration options for the DynamoDB outbox store.
/// </summary>
public sealed class DynamoDbOutboxOptions
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
	/// Gets or sets the outbox table name.
	/// </summary>
	/// <value>Defaults to "outbox".</value>
	[Required]
	public string TableName { get; set; } = "outbox";

	/// <summary>
	/// Gets or sets the partition key attribute name.
	/// </summary>
	/// <value>Defaults to "pk".</value>
	[Required]
	public string PartitionKeyAttribute { get; set; } = "pk";

	/// <summary>
	/// Gets or sets the sort key attribute name.
	/// </summary>
	/// <value>Defaults to "sk".</value>
	[Required]
	public string SortKeyAttribute { get; set; } = "sk";

	/// <summary>
	/// Gets or sets the TTL attribute name.
	/// </summary>
	/// <value>Defaults to "ttl".</value>
	[Required]
	public string TtlAttribute { get; set; } = "ttl";

	/// <summary>
	/// Gets or sets the default time-to-live for published messages in seconds.
	/// </summary>
	/// <value>Defaults to 7 days (604800 seconds). Set to 0 to disable TTL.</value>
	[Range(0, int.MaxValue)]
	public int DefaultTimeToLiveSeconds { get; set; } = 604800;

	/// <summary>
	/// Gets or sets the maximum retry attempts.
	/// </summary>
	/// <value>Defaults to 3.</value>
	[Range(1, int.MaxValue)]
	public int MaxRetryAttempts { get; set; } = 3;

	/// <summary>
	/// Gets or sets a value indicating whether to create the table if it doesn't exist.
	/// </summary>
	/// <value>Defaults to <see langword="true"/>.</value>
	public bool CreateTableIfNotExists { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to enable DynamoDB Streams on the table.
	/// </summary>
	/// <value>Defaults to <see langword="true"/>.</value>
	public bool EnableStreams { get; set; } = true;

	/// <summary>
	/// Gets the AWS region endpoint.
	/// </summary>
	/// <returns>The AWS region endpoint, or null if not configured.</returns>
	public RegionEndpoint? GetRegionEndpoint() =>
		string.IsNullOrWhiteSpace(Region) ? null : RegionEndpoint.GetBySystemName(Region);

	/// <summary>
	/// Validates the options.
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
	}
}
