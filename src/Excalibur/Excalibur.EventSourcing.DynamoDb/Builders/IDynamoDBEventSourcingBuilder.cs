// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon;
using Amazon.DynamoDBStreams;
using Amazon.DynamoDBv2;

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

	/// <summary>
	/// Sets a pre-configured <see cref="IAmazonDynamoDBStreams"/> client, enabling the change feed.
	/// </summary>
	/// <param name="streamsClient"> The Streams client to use for change-feed reads. </param>
	/// <returns> The builder for fluent chaining. </returns>
	/// <remarks>
	/// <para>
	/// Only needed alongside <see cref="Client(IAmazonDynamoDB)"/> or
	/// <see cref="ClientFactory(Func{IServiceProvider, IAmazonDynamoDB})"/>: when the connection is
	/// configured by service URL or region, a matching Streams client is built automatically. The event
	/// store appends, loads, and reads versions without one; it is required only to consume a change feed.
	/// </para>
	/// <para>
	/// Unlike the connection methods, this is orthogonal to the connection mode: selecting or changing a
	/// connection method neither sets nor clears the Streams client. It is last-wins against
	/// <see cref="StreamsClientFactory(Func{IServiceProvider, IAmazonDynamoDBStreams})"/>.
	/// </para>
	/// </remarks>
	IDynamoDBEventSourcingBuilder StreamsClient(IAmazonDynamoDBStreams streamsClient);

	/// <summary>
	/// Sets a factory that resolves an <see cref="IAmazonDynamoDBStreams"/> from DI, enabling the change feed.
	/// </summary>
	/// <param name="streamsClientFactory"> The factory that resolves the Streams client from the container. </param>
	/// <returns> The builder for fluent chaining. </returns>
	/// <remarks>
	/// <para>
	/// The deferred form of <see cref="StreamsClient(IAmazonDynamoDBStreams)"/>, for a client whose
	/// configuration is not known until the container is built. The same rule applies: it is needed only
	/// alongside <see cref="Client(IAmazonDynamoDB)"/> or
	/// <see cref="ClientFactory(Func{IServiceProvider, IAmazonDynamoDB})"/>, because a connection
	/// configured by service URL or region builds a matching Streams client automatically, and the event
	/// store is fully functional for appends, loads, and version queries without one.
	/// </para>
	/// <para>
	/// Unlike the connection methods, this is orthogonal to the connection mode: selecting or changing a
	/// connection method neither sets nor clears the Streams client. It is last-wins against
	/// <see cref="StreamsClient(IAmazonDynamoDBStreams)"/>.
	/// </para>
	/// </remarks>
	IDynamoDBEventSourcingBuilder StreamsClientFactory(Func<IServiceProvider, IAmazonDynamoDBStreams> streamsClientFactory);

	/// <summary>Binds options from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.</summary>
	IDynamoDBEventSourcingBuilder BindConfiguration(string sectionPath);

	/// <summary>Sets the table name.</summary>
	IDynamoDBEventSourcingBuilder TableName(string tableName);

	/// <summary>Sets a prefix for table names (environment isolation).</summary>
	IDynamoDBEventSourcingBuilder TablePrefix(string prefix);

}
