// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon;
using Amazon.DynamoDBv2;

namespace Excalibur.Inbox.DynamoDb;

/// <summary>
/// Fluent builder for configuring DynamoDB inbox settings.
/// </summary>
public interface IDynamoDBInboxBuilder
{
	/// <summary>Sets the DynamoDB service URL (for LocalStack/DynamoDB Local).</summary>
	IDynamoDBInboxBuilder ServiceUrl(string serviceUrl);

	/// <summary>Sets the AWS region explicitly.</summary>
	IDynamoDBInboxBuilder Region(RegionEndpoint region);

	/// <summary>Sets a pre-configured <see cref="IAmazonDynamoDB"/> client.</summary>
	IDynamoDBInboxBuilder Client(IAmazonDynamoDB client);

	/// <summary>Sets a factory that resolves an <see cref="IAmazonDynamoDB"/> from DI.</summary>
	IDynamoDBInboxBuilder ClientFactory(Func<IServiceProvider, IAmazonDynamoDB> clientFactory);

	/// <summary>Binds options from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.</summary>
	IDynamoDBInboxBuilder BindConfiguration(string sectionPath);

	/// <summary>Sets the table name.</summary>
	IDynamoDBInboxBuilder TableName(string tableName);

	/// <summary>Sets a prefix for table names (environment isolation).</summary>
	IDynamoDBInboxBuilder TablePrefix(string prefix);

}
