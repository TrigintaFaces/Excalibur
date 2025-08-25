// Copyright (c) 2025 The Excalibur Project Authors
//
// Licensed under multiple licenses:
// - Excalibur License 1.0 (see LICENSE-EXCALIBUR.txt)
// - GNU Affero General Public License v3.0 or later (AGPL-3.0) (see LICENSE-AGPL-3.0.txt)
// - Server Side Public License v1.0 (SSPL-1.0) (see LICENSE-SSPL-1.0.txt)
// - Apache License 2.0 (see LICENSE-APACHE-2.0.txt)
//
// You may not use this file except in compliance with the License terms above. You may obtain copies of the licenses in
// the project root or online.
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on
// an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.

using System.Net;

using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;

using Excalibur.DataAccess.ElasticSearch.Exceptions;

namespace Excalibur.DataAccess.ElasticSearch;

/// <summary>
///   Provides a base implementation for interacting with Elasticsearch for a specific document type.
/// </summary>
/// <typeparam name="TDocument"> The type of the document to manage in Elasticsearch. </typeparam>
/// <remarks>
///   This class includes operations for adding, updating, retrieving, deleting, and searching documents, as well as
///   initializing indices in Elasticsearch.
/// </remarks>
public abstract class ElasticRepositoryBase<TDocument> : IInitializeElasticIndex, IElasticRepositoryBase<TDocument> where TDocument : class
{
	private readonly ElasticsearchClient _client;
	private readonly string _indexName;

	/// <summary>
	///   Initializes a new instance of the <see cref="ElasticRepositoryBase{TDocument}" /> class with the specified
	///   Elasticsearch client and index name.
	/// </summary>
	/// <param name="client"> The <see cref="ElasticsearchClient" /> instance used to communicate with Elasticsearch. </param>
	/// <param name="indexName"> The name of the Elasticsearch index that this repository will operate on. </param>
	/// <exception cref="ArgumentNullException">
	///   Thrown if <paramref name="client" /> or <paramref name="indexName" /> is <c> null </c> or empty.
	/// </exception>
	protected ElasticRepositoryBase(ElasticsearchClient client, string indexName)
	{
		ArgumentNullException.ThrowIfNull(client);
		ArgumentException.ThrowIfNullOrWhiteSpace(indexName);

		_client = client;
		_indexName = indexName;
	}

	/// <inheritdoc />
	public virtual async Task<TDocument?> GetByIdAsync(string documentId, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(documentId);

		var response = await _client.GetAsync<TDocument>(documentId, idx => idx.Index(_indexName), cancellationToken).ConfigureAwait(false);

		if (response is { IsValidResponse: true, Found: true })
		{
			return response.Source;
		}

		if (!response.Found || response.ApiCallDetails?.HttpStatusCode == (int)HttpStatusCode.NotFound)
		{
			return null;
		}

		_ = response.TryGetOriginalException(out var exception);
		var details = response.ApiCallDetails?.ToString() ?? "No API details available";
		throw new ElasticsearchGetByIdException(documentId, typeof(TDocument), details, exception);
	}

	/// <inheritdoc />
	public virtual async Task<bool> AddOrUpdateAsync(string documentId, TDocument document, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
		ArgumentNullException.ThrowIfNull(document);

		var response = await _client
			.IndexAsync(document, idx => idx
				.Index(_indexName)
				.Id(documentId)
				.Refresh(Refresh.WaitFor), cancellationToken).ConfigureAwait(false);

		if (response.IsValidResponse)
		{
			return true;
		}

		_ = response.TryGetOriginalException(out var exception);
		var details = response.ApiCallDetails?.ToString() ?? "No API details available";
		throw new ElasticsearchIndexingException(_indexName, typeof(TDocument), details, exception);
	}

	/// <inheritdoc />
	public virtual async Task<bool> UpdateAsync(string documentId, Dictionary<string, object> updatedFields,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
		ArgumentNullException.ThrowIfNull(updatedFields);

		var updateRequest = new UpdateRequest<TDocument, object>(_indexName, documentId) { Doc = updatedFields, Refresh = Refresh.WaitFor };

		var response = await _client.UpdateAsync(updateRequest, cancellationToken).ConfigureAwait(false);

		if (response.IsValidResponse)
		{
			return true;
		}

		_ = response.TryGetOriginalException(out var exception);
		var details = response.ApiCallDetails?.ToString() ?? "No API details available";
		throw new ElasticsearchUpdateException(_indexName, typeof(TDocument), details, exception);
	}

	/// <inheritdoc />
	public virtual async Task<bool> BulkAddOrUpdateAsync(
		IEnumerable<TDocument> documents,
		Func<TDocument, string> idSelector,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(documents);
		ArgumentNullException.ThrowIfNull(idSelector);

		var response = await _client
			.BulkAsync(b => b
				.IndexMany(documents, (idx, doc) => idx
					.Index(_indexName)
					.Id(idSelector(doc))
				).Refresh(Refresh.WaitFor), cancellationToken)
			.ConfigureAwait(false);

		if (response.IsValidResponse)
		{
			return true;
		}

		_ = response.TryGetOriginalException(out var exception);
		var failedItems = response.ItemsWithErrors.Select(item => item.Id).ToList();
		var details = response.ApiCallDetails?.ToString() ?? "No API details available";
		throw new ElasticsearchIndexingException(
			_indexName,
			typeof(TDocument),
			$"Failed items: {string.Join(", ", failedItems)}. API details: {details}",
			exception
		);
	}

	/// <inheritdoc />
	public virtual async Task<bool> RemoveAsync(string documentId, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(documentId);

		var response = await _client
			.DeleteAsync<TDocument>(documentId, idx => idx
				.Index(_indexName)
				.Refresh(Refresh.WaitFor), cancellationToken).ConfigureAwait(false);

		if (response.IsValidResponse)
		{
			return true;
		}

		_ = response.TryGetOriginalException(out var exception);
		var details = response.ApiCallDetails?.ToString() ?? "No API details available";
		throw new ElasticsearchDeleteException(documentId, typeof(TDocument), details, exception);
	}

	/// <inheritdoc />
	public virtual async Task<SearchResponse<TDocument>> SearchAsync(SearchRequest<TDocument> searchRequest,
		CancellationToken cancellationToken = default)
	{
		var response = await _client.SearchAsync<TDocument>(searchRequest, cancellationToken).ConfigureAwait(false);

		if (response.IsValidResponse)
		{
			return response;
		}

		_ = response.TryGetOriginalException(out var exception);
		var details = response.ApiCallDetails?.ToString() ?? "No API details available";
		throw new ElasticsearchSearchException(_indexName, typeof(TDocument), details, exception);
	}

	/// <inheritdoc />
	public abstract Task InitializeIndexAsync(CancellationToken cancellationToken = default);

	/// <summary>
	///   Initializes the Elasticsearch index using a specific <see cref="CreateIndexRequest" />.
	/// </summary>
	/// <param name="createIndexRequest"> The request configuration for creating the index. </param>
	/// <param name="cancellationToken"> The cancellation token to cancel the operation if required. </param>
	protected async Task InitializeIndexAsync(CreateIndexRequest createIndexRequest, CancellationToken cancellationToken = default)
	{
		var exists = await _client.Indices.ExistsAsync(_indexName, cancellationToken).ConfigureAwait(false);
		if (!exists.Exists)
		{
			_ = await _client.Indices.CreateAsync(createIndexRequest, cancellationToken).ConfigureAwait(false);
		}
	}
}
