// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.ComponentModel.DataAnnotations;

using Amazon;

namespace Excalibur.Data.DynamoDb.Snapshots;

/// <summary>
/// Configuration options for the DynamoDB snapshot store.
/// </summary>
public sealed class DynamoDbSnapshotStoreOptions
{
	/// <summary>
	/// Gets or sets the connection and credential options for DynamoDB.
	/// </summary>
	/// <value>Connection options including service URL, region, and credentials.</value>
	public DynamoDbConnectionOptions Connection { get; set; } = new();

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
				"Either Connection.ServiceUrl (for local development) or Connection.Region (for AWS) must be provided.");
		}

		if (string.IsNullOrWhiteSpace(TableName))
		{
			throw new InvalidOperationException("TableName is required.");
		}
	}

	/// <summary>
	/// Gets or sets the source-generated type-info resolver used to serialize snapshot state and metadata.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Snapshot state and metadata are consumer types the framework cannot source-generate, so with no
	/// resolver the store serializes them through the reflection-based
	/// <see cref="System.Text.Json.JsonSerializer"/>. That works under the JIT, but a native-AOT application
	/// published with reflection-based serialization disabled has no reflection path to fall back on. Rather
	/// than fail on the first save, the store refuses at construction and names this option.
	/// </para>
	/// <para>
	/// The stored wire format does not vary with this setting. The resolver supplies type metadata only, so a
	/// snapshot written with a resolver is byte-identical to one written without and remains readable by a
	/// host configured either way.
	/// </para>
	/// <para>
	/// Metadata values are typed <see cref="object"/> and are therefore written as their runtime type. Declare
	/// each closed value type the application actually stores -- <c>string</c>, <c>int</c>, <c>bool</c> and so
	/// on. Do not declare <c>Dictionary&lt;string, object&gt;</c> as a shortcut: it compiles and then throws on
	/// the values it was meant to cover.
	/// </para>
	/// </remarks>
	/// <value>The consumer's snapshot type-info resolver, or <see langword="null"/> to serialize through
	/// reflection.</value>
	public System.Text.Json.Serialization.Metadata.IJsonTypeInfoResolver? SnapshotTypeInfoResolver { get; set; }
}
