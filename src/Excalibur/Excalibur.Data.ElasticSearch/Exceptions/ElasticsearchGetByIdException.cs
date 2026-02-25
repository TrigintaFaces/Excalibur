// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Data.ElasticSearch.Exceptions;

/// <summary>
/// Represents an exception thrown when an attempt to retrieve a document by ID from Elasticsearch fails.
/// </summary>
/// <remarks>
/// This exception is designed to capture relevant information about the failed operation, including the document ID, document type, and
/// API call details, to aid in diagnosing the failure.
/// </remarks>
[Serializable]
public sealed class ElasticsearchGetByIdException : ApiException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ElasticsearchGetByIdException" /> class with the specified details.
	/// </summary>
	/// <param name="documentId"> The ID of the document that could not be retrieved. </param>
	/// <param name="documentType"> The type of the document that could not be retrieved. </param>
	/// <param name="apiCallDetails"> Details of the API call that failed. </param>
	/// <param name="innerException"> The inner exception that caused this exception, if any. </param>
	/// <exception cref="ArgumentException">
	/// Thrown if <paramref name="documentId" /> or <paramref name="apiCallDetails" /> is null, empty, or whitespace.
	/// </exception>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="documentType" /> is null. </exception>
	public ElasticsearchGetByIdException(string documentId, Type? documentType, string apiCallDetails, Exception? innerException = null)
		: base(500, $"Failed to retrieve document of type '{documentType?.Name}' with ID '{documentId}'. API details: {apiCallDetails}",
			innerException)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
		ArgumentException.ThrowIfNullOrWhiteSpace(apiCallDetails);

		DocumentId = documentId;
		DocumentType = documentType;
		ApiCallDetails = apiCallDetails;
	}

	/// <inheritdoc/>
	public ElasticsearchGetByIdException() : base()
	{
	}

	/// <inheritdoc/>
	public ElasticsearchGetByIdException(string message) : base(message)
	{
	}

	/// <inheritdoc/>
	public ElasticsearchGetByIdException(string message, Exception? innerException) : base(message, innerException)
	{
	}

	/// <inheritdoc/>
	public ElasticsearchGetByIdException(int statusCode, string? message, Exception? innerException) : base(statusCode, message, innerException)
	{
	}

	/// <summary>
	/// Gets the ID of the document that could not be retrieved.
	/// </summary>
	/// <value>
	/// The ID of the document that could not be retrieved.
	/// </value>
	public string DocumentId { get; }

	/// <summary>
	/// Gets the type of the document that could not be retrieved.
	/// </summary>
	/// <value>
	/// The type of the document that could not be retrieved.
	/// </value>
	public Type? DocumentType { get; }

	/// <summary>
	/// Gets details of the API call that failed.
	/// </summary>
	/// <value>
	/// Details of the API call that failed.
	/// </value>
	public string ApiCallDetails { get; }
}
