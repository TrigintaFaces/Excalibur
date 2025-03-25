using Excalibur.Core.Exceptions;

namespace Excalibur.DataAccess.ElasticSearch.Exceptions;

/// <summary>
///     Represents an exception thrown when an attempt to retrieve a document by ID from Elasticsearch fails.
/// </summary>
/// <remarks>
///     This exception is designed to capture relevant information about the failed operation, including the document ID, document type, and
///     API call details, to aid in diagnosing the failure.
/// </remarks>
[Serializable]
public class ElasticsearchGetByIdException : ApiException
{
	/// <summary>
	///     Initializes a new instance of the <see cref="ElasticsearchGetByIdException" /> class with the specified details.
	/// </summary>
	/// <param name="documentId"> The ID of the document that could not be retrieved. </param>
	/// <param name="documentType"> The type of the document that could not be retrieved. </param>
	/// <param name="apiCallDetails"> Details of the API call that failed. </param>
	/// <param name="innerException"> The inner exception that caused this exception, if any. </param>
	/// <exception cref="ArgumentException">
	///     Thrown if <paramref name="documentId" /> or <paramref name="apiCallDetails" /> is null, empty, or whitespace.
	/// </exception>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="documentType" /> is null. </exception>
	public ElasticsearchGetByIdException(string documentId, Type documentType, string apiCallDetails, Exception? innerException = null) :
		base(500, $"Failed to retrieve document of type '{documentType?.Name}' with ID '{documentId}'. API details: {apiCallDetails}",
			innerException)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
		ArgumentNullException.ThrowIfNull(documentType);
		ArgumentException.ThrowIfNullOrWhiteSpace(apiCallDetails);

		DocumentId = documentId;
		DocumentType = documentType;
		ApiCallDetails = apiCallDetails;
	}

	/// <summary>
	///     Gets the ID of the document that could not be retrieved.
	/// </summary>
	public string DocumentId { get; }

	/// <summary>
	///     Gets the type of the document that could not be retrieved.
	/// </summary>
	public Type DocumentType { get; }

	/// <summary>
	///     Gets details of the API call that failed.
	/// </summary>
	public string ApiCallDetails { get; }
}
