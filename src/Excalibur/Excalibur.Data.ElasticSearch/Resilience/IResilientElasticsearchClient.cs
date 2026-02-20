// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Elastic.Clients.Elasticsearch;

using Excalibur.Data.ElasticSearch.Exceptions;

namespace Excalibur.Data.ElasticSearch.Resilience;

/// <summary>
/// Provides a resilient wrapper around Elasticsearch client operations with retry, circuit breaker, and dead letter handling capabilities.
/// </summary>
public interface IResilientElasticsearchClient
{
	/// <summary>
	/// Gets a value indicating whether checks if the circuit breaker is currently open.
	/// </summary>
	/// <value> A <see cref="bool" /> indicating whether the circuit breaker is in open state. </value>
	bool IsCircuitBreakerOpen { get; }

	/// <summary>
	/// Executes an Elasticsearch search operation with resilience patterns.
	/// </summary>
	/// <typeparam name="TDocument"> The type of document to search for. </typeparam>
	/// <param name="request"> The search request to execute. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the search response wrapped in resilience handling. </returns>
	/// <exception cref="ElasticsearchSearchException"> Thrown when the search operation fails after all retry attempts. </exception>
	Task<SearchResponse<TDocument>> SearchAsync<TDocument>(
		SearchRequest request,
		CancellationToken cancellationToken);

	/// <summary>
	/// Executes an Elasticsearch index operation with resilience patterns.
	/// </summary>
	/// <typeparam name="TDocument"> The type of document to index. </typeparam>
	/// <param name="request"> The index request to execute. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the index response wrapped in resilience handling. </returns>
	/// <exception cref="ElasticsearchIndexingException"> Thrown when the index operation fails after all retry attempts. </exception>
	Task<IndexResponse> IndexAsync<TDocument>(
		IndexRequest<TDocument> request,
		CancellationToken cancellationToken);

	/// <summary>
	/// Executes an Elasticsearch update operation with resilience patterns.
	/// </summary>
	/// <typeparam name="TDocument"> The type of document to update. </typeparam>
	/// <param name="request"> The update request to execute. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the update response wrapped in resilience handling. </returns>
	/// <exception cref="ElasticsearchUpdateException"> Thrown when the update operation fails after all retry attempts. </exception>
	Task<UpdateResponse<TDocument>> UpdateAsync<TDocument>(
		UpdateRequest<TDocument, object> request,
		CancellationToken cancellationToken);

	/// <summary>
	/// Executes an Elasticsearch delete operation with resilience patterns.
	/// </summary>
	/// <param name="request"> The delete request to execute. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the delete response wrapped in resilience handling. </returns>
	/// <exception cref="ElasticsearchDeleteException"> Thrown when the delete operation fails after all retry attempts. </exception>
	Task<DeleteResponse> DeleteAsync(
		DeleteRequest request,
		CancellationToken cancellationToken);

	/// <summary>
	/// Executes an Elasticsearch bulk operation with resilience patterns.
	/// </summary>
	/// <param name="request"> The bulk request to execute. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the bulk response wrapped in resilience handling. </returns>
	/// <exception cref="ElasticsearchIndexingException"> Thrown when the bulk operation fails after all retry attempts. </exception>
	Task<BulkResponse> BulkAsync(
		BulkRequest request,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets a document by ID with resilience patterns.
	/// </summary>
	/// <typeparam name="TDocument"> The type of document to retrieve. </typeparam>
	/// <param name="request"> The get request to execute. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the get response wrapped in resilience handling. </returns>
	/// <exception cref="ElasticsearchGetByIdException"> Thrown when the get operation fails after all retry attempts. </exception>
	Task<GetResponse<TDocument>> GetAsync<TDocument>(
		GetRequest request,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets the current health status of the Elasticsearch cluster.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the cluster health status. </returns>
	Task<bool> IsHealthyAsync(CancellationToken cancellationToken);
}
