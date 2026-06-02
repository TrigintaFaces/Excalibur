// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon;
using Amazon.DynamoDBv2;

namespace Excalibur.Data.DynamoDb;

/// <summary>
/// Fluent builder for configuring DynamoDB data settings.
/// </summary>
public interface IDynamoDBDataBuilder
{
	/// <summary>Sets the DynamoDB service URL (for LocalStack/DynamoDB Local).</summary>
	IDynamoDBDataBuilder ServiceUrl(string serviceUrl);

	/// <summary>Sets the AWS region explicitly.</summary>
	IDynamoDBDataBuilder Region(RegionEndpoint region);

	/// <summary>Sets a pre-configured <see cref="IAmazonDynamoDB"/> client.</summary>
	IDynamoDBDataBuilder Client(IAmazonDynamoDB client);

	/// <summary>Sets a factory that resolves an <see cref="IAmazonDynamoDB"/> from DI.</summary>
	IDynamoDBDataBuilder ClientFactory(Func<IServiceProvider, IAmazonDynamoDB> clientFactory);

	/// <summary>Binds options from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.</summary>
	IDynamoDBDataBuilder BindConfiguration(string sectionPath);

	/// <summary>Sets the table name.</summary>
	IDynamoDBDataBuilder TableName(string tableName);

	/// <summary>Sets a prefix for table names (environment isolation).</summary>
	IDynamoDBDataBuilder TablePrefix(string prefix);

}
