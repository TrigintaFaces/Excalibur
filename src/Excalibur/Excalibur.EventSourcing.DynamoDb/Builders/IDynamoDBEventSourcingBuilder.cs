// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.DynamoDBv2;
using Amazon;

namespace Excalibur.EventSourcing.DynamoDb;

/// <summary>
/// Fluent builder for configuring DynamoDB eventsourcing settings.
/// </summary>
public interface IDynamoDBEventSourcingBuilder
{
	/// <summary>Sets the DynamoDB service URL (for LocalStack/DynamoDB Local).</summary>
	IDynamoDBEventSourcingBuilder ServiceUrl(string serviceUrl);

	/// <summary>Sets the AWS region explicitly.</summary>
	IDynamoDBEventSourcingBuilder Region(RegionEndpoint region);

	/// <summary>Sets a pre-configured <see cref="IAmazonDynamoDB"/> client.</summary>
	IDynamoDBEventSourcingBuilder Client(IAmazonDynamoDB client);

	/// <summary>Sets a factory that resolves an <see cref="IAmazonDynamoDB"/> from DI.</summary>
	IDynamoDBEventSourcingBuilder ClientFactory(Func<IServiceProvider, IAmazonDynamoDB> clientFactory);

	/// <summary>Binds options from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.</summary>
	IDynamoDBEventSourcingBuilder BindConfiguration(string sectionPath);

	/// <summary>Sets the table name.</summary>
	IDynamoDBEventSourcingBuilder TableName(string tableName);

	/// <summary>Sets a prefix for table names (environment isolation).</summary>
	IDynamoDBEventSourcingBuilder TablePrefix(string prefix);

}
