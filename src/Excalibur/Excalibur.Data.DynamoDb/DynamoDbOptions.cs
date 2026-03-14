// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

using Amazon;

namespace Excalibur.Data.DynamoDb;

/// <summary>
/// Configuration options for the AWS DynamoDB data provider.
/// </summary>
/// <remarks>
/// Connection and credential settings are in <see cref="DynamoDbConnectionOptions"/> via the <see cref="Connection"/> property.
/// </remarks>
public sealed class DynamoDbOptions
{
	/// <summary>
	/// Gets or sets the name of the provider instance.
	/// </summary>
	[Required]
	public string Name { get; set; } = "DynamoDb";

	/// <summary>
	/// Gets or sets the default table name.
	/// </summary>
	public string? DefaultTableName { get; set; }

	/// <summary>
	/// Gets or sets the default partition key attribute name.
	/// </summary>
	[Required]
	public string DefaultPartitionKeyAttribute { get; set; } = "pk";

	/// <summary>
	/// Gets or sets the default sort key attribute name.
	/// </summary>
	[Required]
	public string DefaultSortKeyAttribute { get; set; } = "sk";

	/// <summary>
	/// Gets or sets a value indicating whether to use consistent reads by default.
	/// </summary>
	public bool UseConsistentReads { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to enable DynamoDB Streams.
	/// </summary>
	public bool EnableStreams { get; set; }

	/// <summary>
	/// Gets or sets the stream view type when streams are enabled.
	/// </summary>
	[Required]
	public string StreamViewType { get; set; } = "NEW_AND_OLD_IMAGES";

	/// <summary>
	/// Gets or sets the connection and credential configuration options.
	/// </summary>
	/// <value>The connection options. Never <see langword="null"/>.</value>
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
		// For local development, ServiceUrl is sufficient
		// For AWS, either explicit credentials or IAM role is required
		var hasLocalConfig = !string.IsNullOrWhiteSpace(Connection.ServiceUrl);
		var hasAwsConfig = !string.IsNullOrWhiteSpace(Connection.Region);
		var hasExplicitCredentials = !string.IsNullOrWhiteSpace(Connection.AccessKey) && !string.IsNullOrWhiteSpace(Connection.SecretKey);

		if (!hasLocalConfig && !hasAwsConfig)
		{
			throw new InvalidOperationException(
				"Either ServiceUrl (for local development) or Region (for AWS) must be provided.");
		}

		if (hasExplicitCredentials && !hasAwsConfig && !hasLocalConfig)
		{
			throw new InvalidOperationException(
				"When using explicit credentials, either ServiceUrl or Region must also be provided.");
		}
	}
}
