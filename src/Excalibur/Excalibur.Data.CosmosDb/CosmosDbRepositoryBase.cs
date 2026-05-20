// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Net;

using Microsoft.Azure.Cosmos;

namespace Excalibur.Data.CosmosDb;

/// <summary>
/// Provides a base implementation for interacting with Cosmos DB for a specific document type.
/// </summary>
/// <typeparam name="TDocument">The type of the document to manage in Cosmos DB.</typeparam>
/// <remarks>
/// <para>
/// This class includes operations for adding, updating, retrieving, deleting, and querying
/// documents, as well as initializing containers in Cosmos DB.
/// </para>
/// <para>
/// Documents are stored flat in Cosmos DB. System.Text.Json (used by the Cosmos DB SDK)
/// silently ignores unknown properties during deserialization, so metadata fields
/// (<c>id</c>, <c>projectionType</c>, <c>updatedAt</c>, <c>_rid</c>, <c>_ts</c>, etc.)
/// do not interfere with consumer document types.
/// </para>
/// <para>
/// Use this base class to build custom query repositories that share Cosmos DB containers
/// with <c>IProjectionStore&lt;T&gt;</c> or other stores.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class OrderSearchRepository : CosmosDbRepositoryBase&lt;OrderProjection&gt;
/// {
///     public OrderSearchRepository(CosmosClient client, IOptionsMonitor&lt;CosmosDbProjectionStoreOptions&gt; options)
///         : base(client,
///                options.Get(nameof(OrderProjection)).DatabaseName,
///                CosmosDbProjectionContainerConvention.GetContainerName&lt;OrderProjection&gt;(
///                    options.Get(nameof(OrderProjection))))
///     {
///     }
///
///     public override Task InitializeContainerAsync(CancellationToken ct) => Task.CompletedTask;
/// }
/// </code>
/// </example>
public abstract class CosmosDbRepositoryBase<TDocument> : ICosmosDbRepositoryBase<TDocument>, ICosmosDbRepositoryBaseQuery<TDocument>
	where TDocument : class
{
	/// <summary>
	/// Initializes a new instance of the <see cref="CosmosDbRepositoryBase{TDocument}"/> class.
	/// </summary>
	/// <param name="client">The Cosmos DB client instance.</param>
	/// <param name="databaseName">The name of the database to operate on.</param>
	/// <param name="containerName">The name of the container to operate on.</param>
	protected CosmosDbRepositoryBase(CosmosClient client, string databaseName, string containerName)
	{
		ArgumentNullException.ThrowIfNull(client);
		ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);
		ArgumentException.ThrowIfNullOrWhiteSpace(containerName);

		ContainerName = containerName;
		Container = client.GetContainer(databaseName, containerName);
	}

	/// <summary>
	/// Gets the name of the container this repository operates on.
	/// </summary>
	protected string ContainerName { get; }

	/// <summary>
	/// Gets the underlying Cosmos DB container.
	/// </summary>
	protected Container Container { get; }

	/// <inheritdoc />
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "JSON serialization is used with known types at runtime")]
	public virtual async Task<TDocument?> GetByIdAsync(string documentId, string partitionKey, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
		ArgumentException.ThrowIfNullOrWhiteSpace(partitionKey);

		try
		{
			// NOTE: Unlike CosmosDbProjectionStore.StripAndDeserialize, this base class
			// deserializes directly to TDocument without stripping '_projection' metadata.
			// The base class is a general-purpose repository that doesn't know about
			// projection metadata. STJ's default behavior silently ignores unknown
			// properties, so any metadata fields are harmless during deserialization.
			var response = await Container.ReadItemAsync<TDocument>(
				documentId,
				new PartitionKey(partitionKey),
				cancellationToken: cancellationToken).ConfigureAwait(false);

			return response.Resource;
		}
		catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
		{
			return null;
		}
	}

	/// <inheritdoc />
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "JSON serialization is used with known types at runtime")]
	public virtual async Task<bool> AddOrUpdateAsync(string documentId, TDocument document, string partitionKey, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
		ArgumentNullException.ThrowIfNull(document);
		ArgumentException.ThrowIfNullOrWhiteSpace(partitionKey);

		var response = await Container.UpsertItemAsync(
			document,
			new PartitionKey(partitionKey),
			cancellationToken: cancellationToken).ConfigureAwait(false);

		return response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created;
	}

	/// <inheritdoc />
	public virtual async Task<bool> RemoveAsync(string documentId, string partitionKey, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
		ArgumentException.ThrowIfNullOrWhiteSpace(partitionKey);

		try
		{
			await Container.DeleteItemAsync<object>(
				documentId,
				new PartitionKey(partitionKey),
				cancellationToken: cancellationToken).ConfigureAwait(false);

			return true;
		}
		catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
		{
			return false;
		}
	}

	/// <inheritdoc />
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "JSON serialization is used with known types at runtime")]
	public virtual async Task<IReadOnlyList<TDocument>> QueryAsync(
		string sql,
		IReadOnlyDictionary<string, object>? parameters,
		string partitionKey,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sql);
		ArgumentException.ThrowIfNullOrWhiteSpace(partitionKey);

		var queryDefinition = new QueryDefinition(sql);

		if (parameters is not null)
		{
			foreach (var (name, value) in parameters)
			{
				queryDefinition = queryDefinition.WithParameter(name, value);
			}
		}

		var requestOptions = new QueryRequestOptions { PartitionKey = new PartitionKey(partitionKey) };
		var results = new List<TDocument>();
		using var iterator = Container.GetItemQueryIterator<TDocument>(queryDefinition, requestOptions: requestOptions);

		while (iterator.HasMoreResults)
		{
			var response = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
			results.AddRange(response);
		}

		return results;
	}

	/// <summary>
	/// Initializes the Cosmos DB container (creates if needed, etc.).
	/// </summary>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	public abstract Task InitializeContainerAsync(CancellationToken cancellationToken);
}
