// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using OpenSearch.Client;

namespace Excalibur.Data.OpenSearch.Resilience;

/// <summary>
/// Provides a resilient wrapper around OpenSearch client operations with retry, circuit breaker, and dead letter handling capabilities.
/// </summary>
#pragma warning disable RS0016 // Analyzer cannot resolve nullable annotations for OpenSearch.Client generic types
public interface IResilientOpenSearchClient
{
	/// <summary>
	/// Gets a value indicating whether the circuit breaker is currently in the open state.
	/// </summary>
	/// <value> A <see cref="bool" /> indicating whether the circuit breaker is in open state. </value>
	bool IsCircuitBreakerOpen { get; }

	/// <summary>
	/// Executes an OpenSearch search operation with resilience patterns.
	/// </summary>
	/// <typeparam name="TDocument"> The type of document to search for. </typeparam>
	/// <param name="selector"> The search descriptor selector. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the search response wrapped in resilience handling. </returns>
	Task<ISearchResponse<TDocument>> SearchAsync<TDocument>(
		Func<SearchDescriptor<TDocument>, ISearchRequest> selector,
		CancellationToken cancellationToken)
		where TDocument : class;

	/// <summary>
	/// Executes an OpenSearch index operation with resilience patterns.
	/// </summary>
	/// <typeparam name="TDocument"> The type of document to index. </typeparam>
	/// <param name="document"> The document to index. </param>
	/// <param name="selector"> The index descriptor selector. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the index response wrapped in resilience handling. </returns>
	Task<IndexResponse> IndexAsync<TDocument>(
		TDocument document,
		Func<IndexDescriptor<TDocument>, IIndexRequest<TDocument>> selector,
		CancellationToken cancellationToken)
		where TDocument : class;

	/// <summary>
	/// Executes an OpenSearch delete operation with resilience patterns.
	/// </summary>
	/// <typeparam name="TDocument"> The type of document to delete. </typeparam>
	/// <param name="id"> The document ID to delete. </param>
	/// <param name="selector"> The delete descriptor selector. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the delete response wrapped in resilience handling. </returns>
	Task<DeleteResponse> DeleteAsync<TDocument>(
		DocumentPath<TDocument> id,
		Func<DeleteDescriptor<TDocument>, IDeleteRequest>? selector,
		CancellationToken cancellationToken)
		where TDocument : class;

	/// <summary>
	/// Executes an OpenSearch bulk operation with resilience patterns.
	/// </summary>
	/// <param name="selector"> The bulk descriptor selector. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the bulk response wrapped in resilience handling. </returns>
	Task<BulkResponse> BulkAsync(
		Func<BulkDescriptor, IBulkRequest> selector,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets a document by ID with resilience patterns.
	/// </summary>
	/// <typeparam name="TDocument"> The type of document to retrieve. </typeparam>
	/// <param name="id"> The document ID to retrieve. </param>
	/// <param name="selector"> The get descriptor selector. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the get response wrapped in resilience handling. </returns>
	Task<GetResponse<TDocument>> GetAsync<TDocument>(
		DocumentPath<TDocument> id,
		Func<GetDescriptor<TDocument>, IGetRequest>? selector,
		CancellationToken cancellationToken)
		where TDocument : class;

	/// <summary>
	/// Gets the current health status of the OpenSearch cluster.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the cluster health status. </returns>
	Task<bool> IsHealthyAsync(CancellationToken cancellationToken);
}
