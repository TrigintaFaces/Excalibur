// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Net;

using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;

using Excalibur.Data.ElasticSearch.Exceptions;

namespace Excalibur.Data.ElasticSearch;

/// <summary>
/// Provides a base implementation for interacting with Elasticsearch for a specific document type.
/// </summary>
/// <typeparam name="TDocument"> The type of the document to manage in Elasticsearch. </typeparam>
/// <remarks>
/// This class includes operations for adding, updating, retrieving, deleting, and searching documents, as well as initializing indices
/// in Elasticsearch.
/// </remarks>
public abstract class ElasticRepositoryBase<TDocument> : IInitializeElasticIndex, IElasticRepositoryBase<TDocument>
	where TDocument : class
{
	private readonly ElasticsearchClient _client;

	private readonly string _indexName;

	/// <summary>
	/// Initializes a new instance of the <see cref="ElasticRepositoryBase{TDocument}" /> class with the specified Elasticsearch client
	/// and index name.
	/// </summary>
	/// <param name="client"> The <see cref="ElasticsearchClient" /> instance used to communicate with Elasticsearch. </param>
	/// <param name="indexName"> The name of the Elasticsearch index that this repository will operate on. </param>
	/// <exception cref="ArgumentNullException">
	/// Thrown if <paramref name="client" /> or <paramref name="indexName" /> is <c> null </c> or empty.
	/// </exception>
	protected ElasticRepositoryBase(ElasticsearchClient client, string indexName)
	{
		ArgumentNullException.ThrowIfNull(client);
		ArgumentException.ThrowIfNullOrWhiteSpace(indexName);

		_client = client;
		_indexName = indexName;
	}

	/// <inheritdoc />
	public virtual Task InitializeAsync(CancellationToken cancellationToken) =>

		// Base implementation does nothing - derived classes can override
		Task.CompletedTask;

	/// <inheritdoc />
	public virtual async Task<TDocument?> GetByIdAsync(string documentId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(documentId);

		var response = await _client.GetAsync<TDocument>(_indexName, new Id(documentId), cancellationToken).ConfigureAwait(false);

		if (response is { IsValidResponse: true, Found: true })
		{
			return response.Source;
		}

		if (!response.Found || response.ApiCallDetails?.HttpStatusCode == (int)HttpStatusCode.NotFound)
		{
			return null;
		}

		var exception = response.ApiCallDetails?.OriginalException;
		throw new ElasticsearchGetByIdException(documentId, typeof(TDocument), response.ApiCallDetails!.ToString(), exception);
	}

	/// <inheritdoc />
	public virtual async Task<bool> AddOrUpdateAsync(string documentId, TDocument document, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
		ArgumentNullException.ThrowIfNull(document);

		var response = await _client
			.IndexAsync(document, (IndexName)_indexName, cancellationToken).ConfigureAwait(false);

		if (response.IsValidResponse)
		{
			return true;
		}

		var exception = response.ApiCallDetails?.OriginalException;
		throw new ElasticsearchIndexingException(_indexName, typeof(TDocument),
			response.ApiCallDetails?.ToString() ?? "No API details available", exception);
	}

	/// <inheritdoc />
	public virtual async Task<bool> UpdateAsync(string documentId, Dictionary<string, object> updatedFields,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
		ArgumentNullException.ThrowIfNull(updatedFields);

		var updateRequest = new UpdateRequest<TDocument, object>(_indexName, documentId) { Doc = updatedFields, Refresh = Refresh.True };

		var response = await _client.UpdateAsync(updateRequest, cancellationToken).ConfigureAwait(false);

		if (response.IsValidResponse)
		{
			return true;
		}

		var exception = response.ApiCallDetails?.OriginalException;
		throw new ElasticsearchUpdateException(_indexName, typeof(TDocument),
			response.ApiCallDetails?.ToString() ?? "No API details available", exception);
	}

	/// <inheritdoc />
	public virtual async Task<bool> BulkAddOrUpdateAsync(
		IEnumerable<TDocument> documents,
		Func<TDocument, string> idSelector,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(documents);
		ArgumentNullException.ThrowIfNull(idSelector);

		var response = await _client
			.BulkAsync(
				b => b
					.IndexMany(documents, (idx, doc) => idx
						.Index(_indexName)
						.Id(idSelector(doc))).Refresh(Refresh.True), cancellationToken)
			.ConfigureAwait(false);

		if (response.IsValidResponse)
		{
			return true;
		}

		var exception = response.ApiCallDetails?.OriginalException;
		var failedItems = response.ItemsWithErrors.Select(item => item.Id).ToList();
		throw new ElasticsearchIndexingException(
			_indexName,
			typeof(TDocument),
			$"Failed items: {string.Join(", ", failedItems)}. API details: {response.ApiCallDetails}",
			exception);
	}

	/// <inheritdoc />
	public virtual async Task<bool> RemoveAsync(string documentId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(documentId);

		var response = await _client
			.DeleteAsync(new DeleteRequest(_indexName, new Id(documentId)), cancellationToken).ConfigureAwait(false);

		if (response.IsValidResponse)
		{
			return true;
		}

		var exception = response.ApiCallDetails?.OriginalException;
		throw new ElasticsearchDeleteException(documentId, typeof(TDocument),
			response.ApiCallDetails?.ToString() ?? "No API details available", exception);
	}

	/// <inheritdoc />
	public virtual async Task<SearchResponse<TDocument>> SearchAsync(
		SearchRequestDescriptor<TDocument> searchRequest,
		CancellationToken cancellationToken)
	{
		var response = await _client.SearchAsync(searchRequest, cancellationToken).ConfigureAwait(false);

		if (response.IsValidResponse)
		{
			return response;
		}

		var exception = response.ApiCallDetails?.OriginalException;
		throw new ElasticsearchSearchException(_indexName, typeof(TDocument),
			response.ApiCallDetails?.ToString() ?? "No API details available", exception);
	}

	/// <inheritdoc />
	public abstract Task InitializeIndexAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Initializes the Elasticsearch index using a specific <see cref="CreateIndexRequest" />.
	/// </summary>
	/// <param name="createIndexRequest"> The request configuration for creating the index. </param>
	/// <param name="cancellationToken"> The cancellation token to cancel the operation if required. </param>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	protected async Task InitializeIndexAsync(CreateIndexRequest createIndexRequest, CancellationToken cancellationToken)
	{
		var exists = await _client.Indices.ExistsAsync(_indexName, cancellationToken).ConfigureAwait(false);
		if (!exists.IsValidResponse)
		{
			_ = await _client.Indices.CreateAsync(createIndexRequest, cancellationToken).ConfigureAwait(false);
		}
	}
}
