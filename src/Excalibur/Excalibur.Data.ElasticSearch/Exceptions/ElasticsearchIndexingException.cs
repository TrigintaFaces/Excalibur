// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Data.ElasticSearch.Exceptions;

/// <summary>
/// Represents an exception that is thrown when an attempt to index a document in Elasticsearch fails.
/// </summary>
/// <remarks>
/// This exception provides detailed information about the failed operation, including the index name, document type, and details about
/// the API call.
/// </remarks>
[Serializable]
public sealed class ElasticsearchIndexingException : ApiException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ElasticsearchIndexingException" /> class with the specified details.
	/// </summary>
	/// <param name="indexName"> The name of the Elasticsearch index where the operation failed. </param>
	/// <param name="documentType"> The type of the document that failed to index. </param>
	/// <param name="apiCallDetails"> Details about the API call that failed. </param>
	/// <param name="innerException"> The inner exception that caused this exception, if any. </param>
	/// <exception cref="ArgumentException">
	/// Thrown if <paramref name="indexName" /> or <paramref name="apiCallDetails" /> is null, empty, or whitespace.
	/// </exception>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="documentType" /> is null. </exception>
	public ElasticsearchIndexingException(string indexName, Type? documentType, string apiCallDetails, Exception? innerException = null)
		: base(500, $"Failed to index document of type '{documentType?.Name}' in index '{indexName}'. API details: {apiCallDetails}",
			innerException)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(indexName);
		ArgumentException.ThrowIfNullOrWhiteSpace(apiCallDetails);

		IndexName = indexName;
		DocumentType = documentType;
		ApiCallDetails = apiCallDetails;
	}

	/// <inheritdoc/>
	public ElasticsearchIndexingException() : base()
	{
	}

	/// <inheritdoc/>
	public ElasticsearchIndexingException(string message) : base(message)
	{
	}

	/// <inheritdoc/>
	public ElasticsearchIndexingException(string message, Exception? innerException) : base(message, innerException)
	{
	}

	/// <inheritdoc/>
	public ElasticsearchIndexingException(int statusCode, string? message, Exception? innerException) : base(statusCode, message, innerException)
	{
	}

	/// <summary>
	/// Gets the name of the Elasticsearch index where the operation failed.
	/// </summary>
	/// <value>
	/// The name of the Elasticsearch index where the operation failed.
	/// </value>
	public string IndexName { get; }

	/// <summary>
	/// Gets the type of the document that failed to index.
	/// </summary>
	/// <value>
	/// The type of the document that failed to index.
	/// </value>
	public Type? DocumentType { get; }

	/// <summary>
	/// Gets details about the API call that failed.
	/// </summary>
	/// <value>
	/// Details about the API call that failed.
	/// </value>
	public string ApiCallDetails { get; }
}
