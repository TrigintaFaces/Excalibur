// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Data.ElasticSearch.Exceptions;

/// <summary>
/// Represents an exception that is thrown when an Elasticsearch search query fails.
/// </summary>
/// <remarks>
/// This exception provides detailed information about the failed search operation, including the index name, document type, and API
/// call details.
/// </remarks>
[Serializable]
public sealed class ElasticsearchSearchException : ApiException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ElasticsearchSearchException" /> class with the specified details.
	/// </summary>
	/// <param name="indexName"> The name of the Elasticsearch index where the search query failed. </param>
	/// <param name="documentType"> The type of the document involved in the search query. </param>
	/// <param name="apiCallDetails"> Details about the API call that failed. </param>
	/// <param name="innerException"> The inner exception that caused this exception, if any. </param>
	/// <exception cref="ArgumentException">
	/// Thrown if <paramref name="indexName" /> or <paramref name="apiCallDetails" /> is null, empty, or whitespace.
	/// </exception>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="documentType" /> is null. </exception>
	public ElasticsearchSearchException(string indexName, Type? documentType, string apiCallDetails, Exception? innerException = null)
		: base(500, $"Search query failed on index '{indexName}' for document type '{documentType?.Name}'. API details: {apiCallDetails}",
			innerException)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(indexName);
		ArgumentException.ThrowIfNullOrWhiteSpace(apiCallDetails);

		IndexName = indexName;
		DocumentType = documentType;
		ApiCallDetails = apiCallDetails;
	}

	/// <inheritdoc/>
	public ElasticsearchSearchException() : base()
	{
	}

	/// <inheritdoc/>
	public ElasticsearchSearchException(string message) : base(message)
	{
	}

	/// <inheritdoc/>
	public ElasticsearchSearchException(string message, Exception? innerException) : base(message, innerException)
	{
	}

	/// <inheritdoc/>
	public ElasticsearchSearchException(int statusCode, string? message, Exception? innerException) : base(statusCode, message, innerException)
	{
	}

	/// <summary>
	/// Gets the name of the Elasticsearch index where the search query failed.
	/// </summary>
	/// <value>
	/// The name of the Elasticsearch index where the search query failed.
	/// </value>
	public string IndexName { get; }

	/// <summary>
	/// Gets the type of the document involved in the search query.
	/// </summary>
	/// <value>
	/// The type of the document involved in the search query.
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
