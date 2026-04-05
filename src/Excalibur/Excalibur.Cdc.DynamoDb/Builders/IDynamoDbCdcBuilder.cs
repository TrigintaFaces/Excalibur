// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.DynamoDBv2;

namespace Excalibur.Cdc.DynamoDb;

/// <summary>
/// Fluent builder interface for configuring DynamoDB CDC settings.
/// </summary>
/// <remarks>
/// <para>
/// This builder configures DynamoDB-specific CDC options such as table name,
/// stream ARN, processor name, and state store connections.
/// All methods return <c>this</c> for method chaining.
/// </para>
/// <para>
/// DynamoDB does not use connection strings; authentication is handled via
/// AWS SDK credential resolution (environment variables, IAM roles, profiles).
/// </para>
/// </remarks>
public interface IDynamoDbCdcBuilder
{
	/// <summary>
	/// Sets the DynamoDB table name for CDC processing.
	/// </summary>
	/// <param name="tableName">The DynamoDB table name.</param>
	/// <returns>The builder for fluent chaining.</returns>
	IDynamoDbCdcBuilder TableName(string tableName);

	/// <summary>
	/// Sets the DynamoDB stream ARN for CDC processing.
	/// </summary>
	/// <param name="streamArn">The stream ARN.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// If not specified, the stream ARN is auto-discovered from the table.
	/// </para>
	/// </remarks>
	IDynamoDbCdcBuilder StreamArn(string streamArn);

	/// <summary>
	/// Sets the unique processor name for this CDC instance.
	/// </summary>
	/// <param name="processorName">The processor name.</param>
	/// <returns>The builder for fluent chaining.</returns>
	IDynamoDbCdcBuilder ProcessorName(string processorName);

	/// <summary>
	/// Configures a separate DynamoDB client factory for CDC state persistence with state store configuration.
	/// </summary>
	/// <param name="clientFactory">A factory function that creates a DynamoDB client for state storage.</param>
	/// <param name="configure">An action to configure state store table settings.</param>
	/// <returns>The builder for fluent chaining.</returns>
	IDynamoDbCdcBuilder WithStateStore(
		Func<IServiceProvider, IAmazonDynamoDB> clientFactory,
		Action<ICdcStateStoreBuilder> configure);

	/// <summary>
	/// Binds DynamoDB CDC source options from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.
	/// </summary>
	/// <param name="sectionPath">The configuration section path (e.g., "Cdc:DynamoDb").</param>
	/// <returns>The builder for fluent chaining.</returns>
	IDynamoDbCdcBuilder BindConfiguration(string sectionPath);
}
