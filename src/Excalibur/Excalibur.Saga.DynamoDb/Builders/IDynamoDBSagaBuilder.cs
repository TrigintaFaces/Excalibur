// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.DynamoDBv2;
using Amazon;

namespace Excalibur.Saga.DynamoDb;

/// <summary>
/// Fluent builder for configuring DynamoDB saga settings.
/// </summary>
public interface IDynamoDBSagaBuilder
{
	/// <summary>Sets the DynamoDB service URL (for LocalStack/DynamoDB Local).</summary>
	IDynamoDBSagaBuilder ServiceUrl(string serviceUrl);

	/// <summary>Sets the AWS region explicitly.</summary>
	IDynamoDBSagaBuilder Region(RegionEndpoint region);

	/// <summary>Sets a pre-configured <see cref="IAmazonDynamoDB"/> client.</summary>
	IDynamoDBSagaBuilder Client(IAmazonDynamoDB client);

	/// <summary>Sets a factory that resolves an <see cref="IAmazonDynamoDB"/> from DI.</summary>
	IDynamoDBSagaBuilder ClientFactory(Func<IServiceProvider, IAmazonDynamoDB> clientFactory);

	/// <summary>Binds options from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.</summary>
	IDynamoDBSagaBuilder BindConfiguration(string sectionPath);

	/// <summary>Sets the table name.</summary>
	IDynamoDBSagaBuilder TableName(string tableName);

	/// <summary>Sets a prefix for table names (environment isolation).</summary>
	IDynamoDBSagaBuilder TablePrefix(string prefix);

}
