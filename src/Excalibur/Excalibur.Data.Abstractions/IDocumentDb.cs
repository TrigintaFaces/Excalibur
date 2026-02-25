// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.Abstractions;

/// <summary>
/// Defines a contract for document database access operations with mandatory partition keys.
/// </summary>
/// <remarks>
/// <para>
/// This interface provides a unified API for document databases
/// (CosmosDB, DynamoDB, MongoDB, Firestore) that differs from relational databases.
/// All operations require a partition key for correctness and performance.
/// </para>
/// <para>
/// Unlike <see cref="IDb"/> which uses <see cref="System.Data.IDbConnection"/>,
/// document databases use HTTP/SDK-based connections and don't map to ADO.NET patterns.
/// </para>
/// <para>
/// Cross-partition operations (without explicit partition key) are available via
/// <see cref="GetService"/> with <c>typeof(IDocumentDbCrossPartition)</c>.
/// </para>
/// </remarks>
public interface IDocumentDb
{
	/// <summary>
	/// Gets a document by its ID and partition key.
	/// </summary>
	/// <typeparam name="T">The document type.</typeparam>
	/// <param name="id">The document ID.</param>
	/// <param name="partitionKey">The partition key for the document.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The document if found; otherwise, <see langword="null"/>.</returns>
	Task<T?> GetAsync<T>(string id, string partitionKey, CancellationToken cancellationToken) where T : class;

	/// <summary>
	/// Upserts (inserts or updates) a document with an explicit partition key.
	/// </summary>
	/// <typeparam name="T">The document type.</typeparam>
	/// <param name="document">The document to upsert.</param>
	/// <param name="partitionKey">The partition key for the document.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	Task UpsertAsync<T>(T document, string partitionKey, CancellationToken cancellationToken) where T : class;

	/// <summary>
	/// Deletes a document by its ID and partition key.
	/// </summary>
	/// <param name="id">The document ID.</param>
	/// <param name="partitionKey">The partition key for the document.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	Task DeleteAsync(string id, string partitionKey, CancellationToken cancellationToken);

	/// <summary>
	/// Queries documents within a specific partition.
	/// </summary>
	/// <typeparam name="T">The document type.</typeparam>
	/// <param name="query">The query string (SQL for CosmosDB, expression for DynamoDB, etc.).</param>
	/// <param name="partitionKey">The partition key to scope the query.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>A list of matching documents.</returns>
	/// <remarks>
	/// <para>
	/// Query syntax is provider-specific:
	/// </para>
	/// <list type="bullet">
	/// <item><description>CosmosDB: SQL-like syntax</description></item>
	/// <item><description>DynamoDB: Key condition expressions</description></item>
	/// <item><description>MongoDB: JSON query documents</description></item>
	/// <item><description>Firestore: Where clause chains</description></item>
	/// </list>
	/// </remarks>
	Task<IReadOnlyList<T>> QueryAsync<T>(string query, string partitionKey, CancellationToken cancellationToken) where T : class;

	/// <summary>
	/// Gets an implementation-specific service. Use to access advanced features
	/// such as <see cref="IDocumentDbCrossPartition"/> for operations without explicit partition keys.
	/// </summary>
	/// <param name="serviceType">The type of the requested service.</param>
	/// <returns>The service instance, or <see langword="null"/> if not supported.</returns>
	object? GetService(Type serviceType) => null;
}

/// <summary>
/// Provides cross-partition document operations for scenarios where the partition key
/// is unknown or derived from the document. Obtain via
/// <see cref="IDocumentDb.GetService"/> with <c>typeof(IDocumentDbCrossPartition)</c>.
/// </summary>
/// <remarks>
/// <para>
/// Cross-partition operations are less efficient for partitioned databases as they may
/// require fan-out queries. Prefer the partition-keyed overloads on <see cref="IDocumentDb"/>
/// when the partition key is known.
/// </para>
/// </remarks>
public interface IDocumentDbCrossPartition
{
	/// <summary>
	/// Gets a document by its ID without specifying a partition key.
	/// May require a cross-partition query.
	/// </summary>
	/// <typeparam name="T">The document type.</typeparam>
	/// <param name="id">The document ID.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The document if found; otherwise, <see langword="null"/>.</returns>
	Task<T?> GetAsync<T>(string id, CancellationToken cancellationToken) where T : class;

	/// <summary>
	/// Upserts (inserts or updates) a document, deriving the partition key from the document.
	/// </summary>
	/// <typeparam name="T">The document type.</typeparam>
	/// <param name="document">The document to upsert.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	/// <remarks>
	/// The partition key is derived from the document using the configured partition key path.
	/// </remarks>
	Task UpsertAsync<T>(T document, CancellationToken cancellationToken) where T : class;

	/// <summary>
	/// Queries documents using a provider-specific query string without partition scoping.
	/// May require a cross-partition query.
	/// </summary>
	/// <typeparam name="T">The document type.</typeparam>
	/// <param name="query">The query string.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>A list of matching documents.</returns>
	Task<IReadOnlyList<T>> QueryAsync<T>(string query, CancellationToken cancellationToken) where T : class;
}
