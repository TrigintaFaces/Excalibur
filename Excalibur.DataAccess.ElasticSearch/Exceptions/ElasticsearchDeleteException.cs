using Excalibur.Core.Exceptions;

namespace Excalibur.DataAccess.ElasticSearch.Exceptions;

/// <summary>
///     Represents an exception thrown when a delete operation in Elasticsearch fails.
/// </summary>
/// <remarks>
///     This exception is typically used to capture and relay information about failed delete operations, including the document ID,
///     document type, and API call details.
/// </remarks>
[Serializable]
public class ElasticsearchDeleteException : ApiException
{
	/// <summary>
	///     Initializes a new instance of the <see cref="ElasticsearchDeleteException" /> class with the specified details.
	/// </summary>
	/// <param name="documentId"> The ID of the document that failed to delete. </param>
	/// <param name="documentType"> The type of the document that failed to delete. </param>
	/// <param name="apiCallDetails"> Detailed information about the API call that failed. </param>
	/// <param name="innerException"> The inner exception that caused this exception, if any. </param>
	/// <exception cref="ArgumentException">
	///     Thrown if <paramref name="documentId" /> or <paramref name="apiCallDetails" /> is null, empty, or whitespace.
	/// </exception>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="documentType" /> is null. </exception>
	public ElasticsearchDeleteException(string documentId, Type documentType, string apiCallDetails, Exception? innerException = null) :
		base(500, $"Failed to delete document of type '{documentType?.Name}' with ID '{documentId}'. API details: {apiCallDetails}",
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
	///     Gets the ID of the document that failed to delete.
	/// </summary>
	public string DocumentId { get; }

	/// <summary>
	///     Gets the type of the document that failed to delete.
	/// </summary>
	public Type DocumentType { get; }

	/// <summary>
	///     Gets detailed information about the API call that failed.
	/// </summary>
	public string ApiCallDetails { get; }
}
