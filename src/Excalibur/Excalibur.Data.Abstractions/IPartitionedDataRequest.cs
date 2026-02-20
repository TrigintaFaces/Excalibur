// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.Abstractions;

/// <summary>
/// Represents a data request with an explicit partition key for cloud-native databases.
/// </summary>
/// <remarks>
/// <para>
/// Partition keys are critical for horizontal scalability in cloud-native
/// databases like CosmosDB, DynamoDB, and sharded MongoDB.
/// </para>
/// <para>
/// Partition key strategies by provider:
/// </para>
/// <list type="bullet">
/// <item><description>CosmosDB: Logical partition via path like <c>/tenantId</c></description></item>
/// <item><description>DynamoDB: Hash key (PK) with optional sort key (SK)</description></item>
/// <item><description>MongoDB: Shard key on <c>tenantId</c> or compound key</description></item>
/// <item><description>Firestore: Collection path like <c>/tenants/{id}/events</c></description></item>
/// </list>
/// </remarks>
public interface IPartitionedDataRequest : IDataRequest
{
	/// <summary>
	/// Gets the partition key for this request.
	/// </summary>
	/// <value>
	/// The partition key that identifies the logical partition for this data.
	/// </value>
	/// <remarks>
	/// <para>
	/// Best practices for partition keys:
	/// </para>
	/// <list type="bullet">
	/// <item><description>Use tenant ID for multi-tenant applications</description></item>
	/// <item><description>Use aggregate ID for event sourcing</description></item>
	/// <item><description>Avoid hot partitions by ensuring even distribution</description></item>
	/// <item><description>Consider composite keys for complex access patterns</description></item>
	/// </list>
	/// </remarks>
	string PartitionKey { get; }
}

/// <summary>
/// Represents a data request with an explicit partition key and typed result.
/// </summary>
/// <typeparam name="TResult">The type of result returned by this request.</typeparam>
/// <remarks>
/// <para>
/// Extends the base <see cref="IPartitionedDataRequest"/> with a strongly-typed result.
/// Use this interface for document database operations that return typed documents.
/// </para>
/// </remarks>
public interface IPartitionedDataRequest<TResult> : IPartitionedDataRequest
{
	/// <summary>
	/// Gets the function responsible for resolving the request result.
	/// </summary>
	/// <value>
	/// A function that executes the request and returns the result.
	/// </value>
	Func<IDocumentDb, Task<TResult>> ResolveAsync { get; }
}
