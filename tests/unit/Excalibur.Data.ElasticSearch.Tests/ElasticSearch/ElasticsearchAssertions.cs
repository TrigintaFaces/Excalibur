// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Elastic.Transport.Products.Elasticsearch;

using Excalibur.Data.ElasticSearch;

namespace Excalibur.Data.Tests.ElasticSearch.Assertions;

/// <summary>
///     Provides assertion helpers for Elasticsearch responses and data.
/// </summary>
public static class ElasticsearchAssertions
{
	/// <summary>
	///     Asserts that an Elasticsearch response is successful.
	/// </summary>
	/// <typeparam name="TResponse"> The response type. </typeparam>
	/// <param name="response"> The response to assert. </param>
	/// <param name="message"> Optional custom message. </param>
	/// <returns> The response for chaining. </returns>
	public static TResponse ShouldBeSuccessful<TResponse>(this TResponse response, string? message = null)
		where TResponse : class
	{
		_ = response.ShouldNotBeNull(message ?? "Response should not be null");

		// Use dynamic to access IsValidResponse property
		dynamic dynamicResponse = response;
		bool isValid = dynamicResponse.IsValidResponse;
		string? debugInfo = null;

		try
		{
			debugInfo = dynamicResponse.DebugInformation?.ToString();
		}
		catch
		{
			// DebugInformation might not exist on all response types
		}

		isValid.ShouldBeTrue(
			message ?? $"Response should be successful. Debug: {debugInfo ?? "N/A"}");

		return response;
	}

	/// <summary>
	///     Asserts that an Elasticsearch response failed.
	/// </summary>
	/// <typeparam name="TResponse"> The response type. </typeparam>
	/// <param name="response"> The response to assert. </param>
	/// <param name="message"> Optional custom message. </param>
	/// <returns> The response for chaining. </returns>
	public static TResponse ShouldBeFailed<TResponse>(this TResponse response, string? message = null)
		where TResponse : ElasticsearchResponse
	{
		_ = response.ShouldNotBeNull(message ?? "Response should not be null");
		response.IsValidResponse.ShouldBeFalse(message ?? "Response should have failed");

		return response;
	}

	/// <summary>
	///     Asserts that a search response contains the expected number of documents.
	/// </summary>
	/// <typeparam name="TDocument"> The document type. </typeparam>
	/// <param name="response"> The search response. </param>
	/// <param name="expectedCount"> The expected document count. </param>
	/// <param name="message"> Optional custom message. </param>
	/// <returns> The response for chaining. </returns>
	public static SearchResponse<TDocument> ShouldHaveDocumentCount<TDocument>(
		this SearchResponse<TDocument> response,
		int expectedCount,
		string? message = null) where TDocument : class
	{
		_ = response.ShouldBeSuccessful();
		response.Documents.Count.ShouldBe(
			expectedCount,
			message ?? $"Expected {expectedCount} documents but got {response.Documents.Count}");

		return response;
	}

	/// <summary>
	///     Asserts that a search response contains at least the specified number of documents.
	/// </summary>
	/// <typeparam name="TDocument"> The document type. </typeparam>
	/// <param name="response"> The search response. </param>
	/// <param name="minimumCount"> The minimum expected document count. </param>
	/// <param name="message"> Optional custom message. </param>
	/// <returns> The response for chaining. </returns>
	public static SearchResponse<TDocument> ShouldHaveAtLeastDocuments<TDocument>(
		this SearchResponse<TDocument> response,
		int minimumCount,
		string? message = null) where TDocument : class
	{
		_ = response.ShouldBeSuccessful();
		response.Documents.Count.ShouldBeGreaterThanOrEqualTo(
			minimumCount,
			message ?? $"Expected at least {minimumCount} documents but got {response.Documents.Count}");

		return response;
	}

	/// <summary>
	///     Asserts that a search response contains documents.
	/// </summary>
	/// <typeparam name="TDocument"> The document type. </typeparam>
	/// <param name="response"> The search response. </param>
	/// <param name="message"> Optional custom message. </param>
	/// <returns> The response for chaining. </returns>
	public static SearchResponse<TDocument> ShouldHaveDocuments<TDocument>(
		this SearchResponse<TDocument> response,
		string? message = null) where TDocument : class
	{
		_ = response.ShouldBeSuccessful();
		response.Documents.ShouldNotBeEmpty(message ?? "Expected documents but none were found");

		return response;
	}

	/// <summary>
	///     Asserts that a search response contains no documents.
	/// </summary>
	/// <typeparam name="TDocument"> The document type. </typeparam>
	/// <param name="response"> The search response. </param>
	/// <param name="message"> Optional custom message. </param>
	/// <returns> The response for chaining. </returns>
	public static SearchResponse<TDocument> ShouldHaveNoDocuments<TDocument>(
		this SearchResponse<TDocument> response,
		string? message = null) where TDocument : class
	{
		_ = response.ShouldBeSuccessful();
		response.Documents.ShouldBeEmpty(message ?? "Expected no documents but some were found");

		return response;
	}

	/// <summary>
	///     Asserts that a bulk response was completely successful.
	/// </summary>
	/// <param name="response"> The bulk response. </param>
	/// <param name="message"> Optional custom message. </param>
	/// <returns> The response for chaining. </returns>
	public static BulkResponse ShouldBeCompletelySuccessful(
		this BulkResponse response,
		string? message = null)
	{
		_ = response.ShouldBeSuccessful();
		response.Errors.ShouldBeFalse(message ?? "Bulk operation should not have errors");
		response.Items.ShouldAllBe(
			static item => item.Status >= 200 && item.Status < 300,
			message ?? "All bulk items should have successful status codes");

		return response;
	}

	/// <summary>
	///     Asserts that an index response created a document.
	/// </summary>
	/// <param name="response"> The index response. </param>
	/// <param name="message"> Optional custom message. </param>
	/// <returns> The response for chaining. </returns>
	public static IndexResponse ShouldHaveCreatedDocument(
		this IndexResponse response,
		string? message = null)
	{
		_ = response.ShouldBeSuccessful();
		response.Result.ShouldBe(
			Result.Created,
			message ?? $"Expected document to be created but result was {response.Result}");

		return response;
	}

	/// <summary>
	///     Asserts that an index response updated a document.
	/// </summary>
	/// <param name="response"> The index response. </param>
	/// <param name="message"> Optional custom message. </param>
	/// <returns> The response for chaining. </returns>
	public static IndexResponse ShouldHaveUpdatedDocument(
		this IndexResponse response,
		string? message = null)
	{
		_ = response.ShouldBeSuccessful();
		response.Result.ShouldBe(
			Result.Updated,
			message ?? $"Expected document to be updated but result was {response.Result}");

		return response;
	}

	/// <summary>
	///     Asserts that a delete response deleted a document.
	/// </summary>
	/// <param name="response"> The delete response. </param>
	/// <param name="message"> Optional custom message. </param>
	/// <returns> The response for chaining. </returns>
	public static DeleteResponse ShouldHaveDeletedDocument(
		this DeleteResponse response,
		string? message = null)
	{
		_ = response.ShouldBeSuccessful();
		response.Result.ShouldBe(
			Result.Deleted,
			message ?? $"Expected document to be deleted but result was {response.Result}");

		return response;
	}

	/// <summary>
	///     Asserts that a get response found a document.
	/// </summary>
	/// <typeparam name="TDocument"> The document type. </typeparam>
	/// <param name="response"> The get response. </param>
	/// <param name="message"> Optional custom message. </param>
	/// <returns> The response for chaining. </returns>
	public static GetResponse<TDocument> ShouldHaveFoundDocument<TDocument>(
		this GetResponse<TDocument> response,
		string? message = null) where TDocument : class
	{
		_ = response.ShouldBeSuccessful();
		response.Found.ShouldBeTrue(message ?? "Document should have been found");
		_ = response.Source.ShouldNotBeNull(message ?? "Document source should not be null");

		return response;
	}

	/// <summary>
	///     Asserts that a get response did not find a document.
	/// </summary>
	/// <typeparam name="TDocument"> The document type. </typeparam>
	/// <param name="response"> The get response. </param>
	/// <param name="message"> Optional custom message. </param>
	/// <returns> The response for chaining. </returns>
	public static GetResponse<TDocument> ShouldNotHaveFoundDocument<TDocument>(
		this GetResponse<TDocument> response,
		string? message = null) where TDocument : class
	{
		_ = response.ShouldBeSuccessful();
		response.Found.ShouldBeFalse(message ?? "Document should not have been found");

		return response;
	}

	/// <summary>
	///     Asserts that documents match a predicate.
	/// </summary>
	/// <typeparam name="TDocument"> The document type. </typeparam>
	/// <param name="documents"> The documents to assert. </param>
	/// <param name="predicate"> The predicate to match. </param>
	/// <param name="message"> Optional custom message. </param>
	/// <returns> The documents for chaining. </returns>
	public static IReadOnlyCollection<TDocument> ShouldAllMatch<TDocument>(
		this IReadOnlyCollection<TDocument> documents,
		Func<TDocument, bool> predicate,
		string? message = null) where TDocument : class
	{
		documents.All(predicate).ShouldBeTrue(message ?? "Not all documents match the predicate");
		return documents;
	}

	/// <summary>
	///     Asserts that at least one document matches a predicate.
	/// </summary>
	/// <typeparam name="TDocument"> The document type. </typeparam>
	/// <param name="documents"> The documents to assert. </param>
	/// <param name="predicate"> The predicate to match. </param>
	/// <param name="message"> Optional custom message. </param>
	/// <returns> The documents for chaining. </returns>
	public static IReadOnlyCollection<TDocument> ShouldContainMatch<TDocument>(
		this IReadOnlyCollection<TDocument> documents,
		Func<TDocument, bool> predicate,
		string? message = null) where TDocument : class
	{
		documents.Any(predicate).ShouldBeTrue(
			message ?? "No documents match the predicate");
		return documents;
	}

	/// <summary>
	///     Asserts that search hits have the expected scores.
	/// </summary>
	/// <typeparam name="TDocument"> The document type. </typeparam>
	/// <param name="response"> The search response. </param>
	/// <param name="minimumScore"> The minimum expected score. </param>
	/// <param name="message"> Optional custom message. </param>
	/// <returns> The response for chaining. </returns>
	public static SearchResponse<TDocument> ShouldHaveMinimumScore<TDocument>(
		this SearchResponse<TDocument> response,
		double minimumScore,
		string? message = null) where TDocument : class
	{
		_ = response.ShouldBeSuccessful();
		_ = response.Hits.ShouldNotBeNull();
		response.Hits.Where(h => h.Score.HasValue)
			.ShouldAllBe(
				h => h.Score >= minimumScore,
				message ?? $"All hits should have a score of at least {minimumScore}");

		return response;
	}

	/// <summary>
	///     Asserts that aggregation results are present.
	/// </summary>
	/// <typeparam name="TDocument"> The document type. </typeparam>
	/// <param name="response"> The search response. </param>
	/// <param name="aggregationName"> The aggregation name to check. </param>
	/// <param name="message"> Optional custom message. </param>
	/// <returns> The response for chaining. </returns>
	public static SearchResponse<TDocument> ShouldHaveAggregation<TDocument>(
		this SearchResponse<TDocument> response,
		string aggregationName,
		string? message = null) where TDocument : class
	{
		_ = response.ShouldBeSuccessful();
		_ = response.Aggregations.ShouldNotBeNull(message ?? "Aggregations should not be null");
		response.Aggregations.ContainsKey(aggregationName).ShouldBeTrue(
			message ?? $"Aggregation '{aggregationName}' should be present");

		return response;
	}

	/// <summary>
	///     Asserts that a response took less than the specified time.
	/// </summary>
	/// <typeparam name="TResponse"> The response type. </typeparam>
	/// <param name="response"> The response to assert. </param>
	/// <param name="maxMilliseconds"> The maximum time in milliseconds. </param>
	/// <param name="message"> Optional custom message. </param>
	/// <returns> The response for chaining. </returns>
	public static TResponse ShouldBeFasterThan<TResponse>(
		this TResponse response,
		long maxMilliseconds,
		string? message = null) where TResponse : ElasticsearchResponse
	{
		_ = response.ShouldBeSuccessful();

		if (response is SearchResponse<object> searchResponse)
		{
			searchResponse.Took.ShouldBeLessThanOrEqualTo(
				maxMilliseconds,
				message ?? $"Response took {searchResponse.Took}ms but should be faster than {maxMilliseconds}ms");
		}

		return response;
	}

	/// <summary>
	///     Asserts that documents are sorted in the expected order.
	/// </summary>
	/// <typeparam name="TDocument"> The document type. </typeparam>
	/// <typeparam name="TKey"> The key type for sorting. </typeparam>
	/// <param name="documents"> The documents to assert. </param>
	/// <param name="keySelector"> The key selector for sorting. </param>
	/// <param name="ascending"> Whether the sort should be ascending. </param>
	/// <param name="message"> Optional custom message. </param>
	/// <returns> The documents for chaining. </returns>
	public static IReadOnlyCollection<TDocument> ShouldBeSortedBy<TDocument, TKey>(
		this IReadOnlyCollection<TDocument> documents,
		Func<TDocument, TKey> keySelector,
		bool ascending = true,
		string? message = null) where TDocument : class
	{
		var sorted = ascending
			? documents.OrderBy(keySelector).ToList()
			: [.. documents.OrderByDescending(keySelector)];

		documents.SequenceEqual(sorted).ShouldBeTrue(
			message ?? $"Documents should be sorted {(ascending ? "ascending" : "descending")}");

		return documents;
	}
}

/// <summary>
///     Extensions for asserting on Elasticsearch exceptions.
/// </summary>
public static class ElasticsearchExceptionAssertions
{
	/// <summary>
	///     Asserts that an action throws an Elasticsearch exception.
	/// </summary>
	/// <param name="action"> The action to execute. </param>
	/// <param name="message"> Optional custom message. </param>
	/// <returns> The exception for further assertions. </returns>
	public static TransportException ShouldThrowElasticsearchException(
		this Action action,
		string? message = null)
	{
		var exception = Should.Throw<TransportException>(
			action,
			message ?? "Expected Elasticsearch exception to be thrown");
		return exception;
	}

	/// <summary>
	///     Asserts that an async action throws an Elasticsearch exception.
	/// </summary>
	/// <param name="action"> The async action to execute. </param>
	/// <param name="message"> Optional custom message. </param>
	/// <returns> The exception for further assertions. </returns>
	public static async Task<TransportException> ShouldThrowElasticsearchExceptionAsync(
		this Func<Task> action,
		string? message = null)
	{
		var exception = await Should.ThrowAsync<TransportException>(
			action,
			message ?? "Expected Elasticsearch exception to be thrown").ConfigureAwait(false);
		return exception;
	}
}
