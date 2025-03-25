using Excalibur.Core.Exceptions;

namespace Excalibur.DataAccess.ElasticSearch.Exceptions;

/// <summary>
///     Represents an exception that is thrown when an Elasticsearch search query fails.
/// </summary>
/// <remarks>
///     This exception provides detailed information about the failed search operation, including the index name, document type, and API
///     call details.
/// </remarks>
[Serializable]
public class ElasticsearchSearchException : ApiException
{
	/// <summary>
	///     Initializes a new instance of the <see cref="ElasticsearchSearchException" /> class with the specified details.
	/// </summary>
	/// <param name="indexName"> The name of the Elasticsearch index where the search query failed. </param>
	/// <param name="documentType"> The type of the document involved in the search query. </param>
	/// <param name="apiCallDetails"> Details about the API call that failed. </param>
	/// <param name="innerException"> The inner exception that caused this exception, if any. </param>
	/// <exception cref="ArgumentException">
	///     Thrown if <paramref name="indexName" /> or <paramref name="apiCallDetails" /> is null, empty, or whitespace.
	/// </exception>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="documentType" /> is null. </exception>
	public ElasticsearchSearchException(string indexName, Type documentType, string apiCallDetails, Exception? innerException = null) :
		base(500, $"Search query failed on index '{indexName}' for document type '{documentType?.Name}'. API details: {apiCallDetails}",
			innerException)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(indexName);
		ArgumentNullException.ThrowIfNull(documentType);
		ArgumentException.ThrowIfNullOrWhiteSpace(apiCallDetails);

		IndexName = indexName;
		DocumentType = documentType;
		ApiCallDetails = apiCallDetails;
	}

	/// <summary>
	///     Gets the name of the Elasticsearch index where the search query failed.
	/// </summary>
	public string IndexName { get; }

	/// <summary>
	///     Gets the type of the document involved in the search query.
	/// </summary>
	public Type DocumentType { get; }

	/// <summary>
	///     Gets details about the API call that failed.
	/// </summary>
	public string ApiCallDetails { get; }
}
