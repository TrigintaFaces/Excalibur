using Elastic.Clients.Elasticsearch;

using Excalibur.DataAccess.ElasticSearch.Exceptions;

namespace Excalibur.DataAccess.ElasticSearch;

/// <summary>
///     Provides base repository functionality for interacting with Elasticsearch for a specific document type.
/// </summary>
/// <typeparam name="TDocument"> The type of the document to manage in Elasticsearch. </typeparam>
public interface IElasticRepositoryBase<TDocument> where TDocument : class
{
	/// <summary>
	///     Adds or updates a document in the Elasticsearch index.
	/// </summary>
	/// <param name="documentId"> The unique identifier for the document. </param>
	/// <param name="document"> The document to be added or updated. </param>
	/// <param name="cancellationToken"> The cancellation token to cancel the operation if required. </param>
	/// <returns> A <see cref="Task{TResult}" /> indicating whether the operation succeeded. </returns>
	Task<bool> AddOrUpdateAsync(string documentId, TDocument document, CancellationToken cancellationToken = default);

	/// <summary>
	///     Updates specific fields of a document in the Elasticsearch index.
	/// </summary>
	/// <param name="documentId"> The unique identifier of the document to update. </param>
	/// <param name="updatedFields"> A dictionary containing the fields to update and their new values. </param>
	/// <param name="cancellationToken"> A <see cref="CancellationToken" /> to observe while waiting for the operation to complete. </param>
	/// <returns>
	///     A <see cref="Task{TResult}" /> that resolves to <c> true </c> if the update operation succeeded, otherwise an exception is thrown.
	/// </returns>
	/// <exception cref="ArgumentException"> Thrown if <paramref name="documentId" /> is <c> null </c>, empty, or whitespace. </exception>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="updatedFields" /> is <c> null </c>. </exception>
	/// <exception cref="ElasticsearchUpdateException">
	///     Thrown if the update operation fails. Includes details of the Elasticsearch API response.
	/// </exception>
	Task<bool> UpdateAsync(string documentId, Dictionary<string, object> updatedFields, CancellationToken cancellationToken = default);

	/// <summary>
	///     Performs a bulk operation to add or update multiple documents in the Elasticsearch index. Documents are uniquely identified by
	///     an ID provided by the <paramref name="idSelector" /> function.
	/// </summary>
	/// <param name="documents"> The collection of documents to add or update. </param>
	/// <param name="idSelector">
	///     A function used to select the unique identifier (Elasticsearch document _id) for each document. This identifier ensures
	///     documents are correctly created or updated without duplication.
	/// </param>
	/// <param name="cancellationToken"> The cancellation token to cancel the operation if required. </param>
	/// <returns> A <see cref="Task{TResult}" /> indicating whether the bulk operation succeeded. </returns>
	Task<bool> BulkAddOrUpdateAsync(
		IEnumerable<TDocument> documents,
		Func<TDocument, string> idSelector,
		CancellationToken cancellationToken = default);

	/// <summary>
	///     Removes a document from the Elasticsearch index.
	/// </summary>
	/// <param name="documentId"> The unique identifier of the document to remove. </param>
	/// <param name="cancellationToken"> The cancellation token to cancel the operation if required. </param>
	/// <returns> A <see cref="Task{TResult}" /> indicating whether the operation succeeded. </returns>
	Task<bool> RemoveAsync(string documentId, CancellationToken cancellationToken = default);

	/// <summary>
	///     Retrieves a document from Elasticsearch by its ID.
	/// </summary>
	/// <param name="documentId"> The unique identifier of the document. </param>
	/// <param name="cancellationToken"> The cancellation token to cancel the operation if required. </param>
	/// <returns> A <see cref="Task{TResult}" /> containing the document if found, or <c> null </c> if not found. </returns>
	Task<TDocument?> GetByIdAsync(string documentId, CancellationToken cancellationToken = default);

	/// <summary>
	///     Executes a search query against the Elasticsearch index.
	/// </summary>
	/// <param name="searchRequest"> The search request containing query details. </param>
	/// <param name="cancellationToken"> The cancellation token to cancel the operation if required. </param>
	/// <returns> A <see cref="Task{TResult}" /> containing the search response, including matched documents and metadata. </returns>
	Task<SearchResponse<TDocument>> SearchAsync(SearchRequest<TDocument> searchRequest,
		CancellationToken cancellationToken = default);
}
