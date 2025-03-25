using Excalibur.Core.Exceptions;

namespace Excalibur.DataAccess.ElasticSearch.Exceptions;

/// <summary>
///     Represents an exception that is thrown when an Elasticsearch update operation fails.
/// </summary>
/// <remarks>
///     This exception provides detailed information about the failed update operation, including the document ID, document type, and API
///     call details.
/// </remarks>
[Serializable]
public class ElasticsearchUpdateException : ApiException
{
	/// <summary>
	///     Initializes a new instance of the <see cref="ElasticsearchUpdateException" /> class with the specified details.
	/// </summary>
	/// <param name="documentId"> The ID of the document that failed to update. </param>
	/// <param name="documentType"> The type of the document involved in the update operation. </param>
	/// <param name="apiCallDetails"> Details about the API call that failed. </param>
	/// <param name="innerException"> The inner exception that caused this exception, if any. </param>
	/// <exception cref="ArgumentException">
	///     Thrown if <paramref name="documentId" /> or <paramref name="apiCallDetails" /> is null, empty, or whitespace.
	/// </exception>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="documentType" /> is null. </exception>
	public ElasticsearchUpdateException(string documentId, Type documentType, string apiCallDetails, Exception? innerException = null) :
		base(500, $"Failed to update document of type '{documentType?.Name}' with ID '{documentId}'. API details: {apiCallDetails}",
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
	///     Gets the ID of the document that failed to update.
	/// </summary>
	public string DocumentId { get; }

	/// <summary>
	///     Gets the type of the document that failed to update.
	/// </summary>
	public Type DocumentType { get; }

	/// <summary>
	///     Gets details about the API call that failed.
	/// </summary>
	public string ApiCallDetails { get; }
}
