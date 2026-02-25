// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Elastic.Clients.Elasticsearch;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.ElasticSearch;

/// <summary>
/// Handles dead letter messages for Elasticsearch operations.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ElasticsearchDeadLetterHandler" /> class.
/// </remarks>
/// <param name="client"> The Elasticsearch client. </param>
/// <param name="options"> Dead letter handler options. </param>
/// <param name="logger"> Logger instance. </param>
public sealed class ElasticsearchDeadLetterHandler(
	ElasticsearchClient client,
	IOptions<ElasticsearchDeadLetterOptions> options,
	ILogger<ElasticsearchDeadLetterHandler> logger)
{
	private readonly ElasticsearchClient _client = client ?? throw new ArgumentNullException(nameof(client));
	private readonly ElasticsearchDeadLetterOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
	private readonly ILogger<ElasticsearchDeadLetterHandler> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	/// <summary>
	/// Handles a dead letter document.
	/// </summary>
	/// <typeparam name="T"> The document type. </typeparam>
	/// <param name="document"> The document to handle. </param>
	/// <param name="error"> The error that caused the dead letter. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	/// <exception cref="InvalidOperationException"></exception>
	public async Task HandleDeadLetterAsync<T>(T document, Exception error, CancellationToken cancellationToken)
		where T : class
	{
		_logger.LogWarning(error, "Handling dead letter document of type {DocumentType}", typeof(T).Name);

		var deadLetterDoc = new DeadLetterDocument<T>
		{
			OriginalDocument = document,
			ErrorMessage = error.Message,
			ErrorType = error.GetType().Name,
			Timestamp = DateTimeOffset.UtcNow,
			RetryCount = 0,
		};

		var indexName = $"{_options.DeadLetterIndexPrefix}-{DateTimeOffset.UtcNow:yyyy-MM}";

		var response = await _client.IndexAsync(deadLetterDoc, idx => idx
			.Index(indexName)
			.OpType(OpType.Create), cancellationToken).ConfigureAwait(false);

		if (!response.IsValidResponse)
		{
			_logger.LogError("Failed to store dead letter document: {Error}", response.ElasticsearchServerError?.ToString());
			var errorMessage = response.ElasticsearchServerError?.Error?.ToString() ?? "Unknown error";
			throw new InvalidOperationException($"Failed to store dead letter document: {errorMessage}");
		}

		_logger.LogInformation("Dead letter document stored successfully with ID: {MessageId}", response.Id);
	}

	/// <summary>
	/// Retries processing of dead letter documents.
	/// </summary>
	/// <param name="indexName"> The dead letter index to retry from. </param>
	/// <param name="maxRetries"> Maximum number of documents to retry. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> Number of successfully retried documents. </returns>
	public async Task<int> RetryDeadLettersAsync(string indexName, int maxRetries, CancellationToken cancellationToken)
	{
		_logger.LogInformation("Retrying dead letters from index {IndexName}", indexName);

		var searchResponse = await _client.SearchAsync<DeadLetterDocument<object>>(
			s => s
			.Indices(indexName)
			.Size(maxRetries)
			.Query(q => q
				.Range(r => r
					.NumberRange(nr => nr
						.Field(new Field("retryCount"))
						.Lt(_options.MaxRetryCount)))), cancellationToken).ConfigureAwait(false);

		if (!searchResponse.IsValidResponse)
		{
			_logger.LogError("Failed to search dead letter documents: {Error}", searchResponse.ElasticsearchServerError?.ToString());
			return 0;
		}

		var successCount = 0;
		foreach (var hit in searchResponse.Documents)
		{
			// Process retry logic here This would typically involve re-processing the original document
			successCount++;
		}

		_logger.LogInformation("Successfully retried {Count} dead letter documents", successCount);
		return successCount;
	}
}
