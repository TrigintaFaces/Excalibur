// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.DynamoDBv2;
using Amazon;

namespace Excalibur.Outbox.DynamoDb;

/// <summary>
/// Fluent builder for configuring DynamoDB outbox settings.
/// </summary>
public interface IDynamoDBOutboxBuilder
{
	/// <summary>Sets the DynamoDB service URL (for LocalStack/DynamoDB Local).</summary>
	IDynamoDBOutboxBuilder ServiceUrl(string serviceUrl);

	/// <summary>Sets the AWS region explicitly.</summary>
	IDynamoDBOutboxBuilder Region(RegionEndpoint region);

	/// <summary>Sets a pre-configured <see cref="IAmazonDynamoDB"/> client.</summary>
	IDynamoDBOutboxBuilder Client(IAmazonDynamoDB client);

	/// <summary>Sets a factory that resolves an <see cref="IAmazonDynamoDB"/> from DI.</summary>
	IDynamoDBOutboxBuilder ClientFactory(Func<IServiceProvider, IAmazonDynamoDB> clientFactory);

	/// <summary>Binds options from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.</summary>
	IDynamoDBOutboxBuilder BindConfiguration(string sectionPath);

	/// <summary>Sets the table name.</summary>
	IDynamoDBOutboxBuilder TableName(string tableName);

	/// <summary>Sets a prefix for table names (environment isolation).</summary>
	IDynamoDBOutboxBuilder TablePrefix(string prefix);

}
