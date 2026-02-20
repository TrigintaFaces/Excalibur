// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.CosmosDb.Outbox;

/// <summary>
/// Optimizes cross-partition queries for the Cosmos DB outbox store.
/// </summary>
/// <remarks>
/// <para>
/// Provides efficient FeedIterator-based reading with configurable parallelism,
/// buffering, and pagination for outbox queries that span multiple partitions.
/// </para>
/// <para>
/// The primary use case is <c>GetStatisticsAsync</c> which requires aggregation
/// across all status partitions. Single-partition queries (e.g., getting staged
/// messages from the <c>staged</c> partition) should use the standard
/// <see cref="CosmosDbOutboxStore"/> methods, which are already optimized.
/// </para>
/// <para>
/// Reference: <see href="https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/query/pagination">
/// Azure Cosmos DB query pagination</see>.
/// </para>
/// </remarks>
public sealed partial class CosmosDbOutboxQueryOptimizer
{
	private readonly CosmosDbOutboxQueryOptions _queryOptions;
	private readonly ILogger<CosmosDbOutboxQueryOptimizer> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="CosmosDbOutboxQueryOptimizer"/> class.
	/// </summary>
	/// <param name="queryOptions"> The query optimization options. </param>
	/// <param name="logger"> The logger instance. </param>
	public CosmosDbOutboxQueryOptimizer(
		IOptions<CosmosDbOutboxQueryOptions> queryOptions,
		ILogger<CosmosDbOutboxQueryOptimizer> logger)
	{
		ArgumentNullException.ThrowIfNull(queryOptions);
		ArgumentNullException.ThrowIfNull(logger);

		_queryOptions = queryOptions.Value;
		_logger = logger;
	}

	/// <summary>
	/// Reads outbox documents using an optimized FeedIterator with parallelism and buffering.
	/// </summary>
	/// <param name="container"> The Cosmos DB container to query. </param>
	/// <param name="queryDefinition"> The query definition to execute. </param>
	/// <param name="partitionKey"> Optional partition key to scope the query. When null, cross-partition query is used. </param>
	/// <param name="maxItems"> Maximum number of items to return. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A list of outbound messages matching the query. </returns>
	public async Task<IReadOnlyList<OutboundMessage>> ReadWithFeedIteratorAsync(
		Container container,
		QueryDefinition queryDefinition,
		string? partitionKey,
		int maxItems,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(container);
		ArgumentNullException.ThrowIfNull(queryDefinition);
		ArgumentOutOfRangeException.ThrowIfLessThan(maxItems, 1);

		var isCrossPartition = partitionKey == null;

		if (isCrossPartition && !_queryOptions.EnableCrossPartitionQuery)
		{
			throw new InvalidOperationException(
				"Cross-partition queries are disabled. " +
				"Set CosmosDbOutboxQueryOptions.EnableCrossPartitionQuery to true, " +
				"or provide a partition key.");
		}

		var requestOptions = CreateQueryRequestOptions(partitionKey, maxItems);

		LogQueryStarting(isCrossPartition, maxItems, _queryOptions.MaxConcurrency);

		var messages = new List<OutboundMessage>();
		var totalRUs = 0.0;
		var pageCount = 0;

		using var iterator = container.GetItemQueryIterator<CosmosDbOutboxDocument>(
			queryDefinition,
			requestOptions: requestOptions);

		while (iterator.HasMoreResults && messages.Count < maxItems)
		{
			var response = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
			pageCount++;
			totalRUs += response.RequestCharge;

			foreach (var document in response)
			{
				if (messages.Count >= maxItems)
				{
					break;
				}

				messages.Add(document.ToOutboundMessage());
			}

			if (!_queryOptions.UseContinuationTokens)
			{
				break;
			}
		}

		LogQueryCompleted(messages.Count, pageCount, totalRUs, isCrossPartition);

		return messages;
	}

	/// <summary>
	/// Reads outbox documents across all partitions for aggregation queries.
	/// </summary>
	/// <param name="container"> The Cosmos DB container to query. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A dictionary mapping partition key values to their document counts. </returns>
	public async Task<IReadOnlyDictionary<string, int>> GetPartitionCountsAsync(
		Container container,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(container);

		var query = new QueryDefinition(
			"SELECT c.partitionKey, COUNT(1) as count FROM c GROUP BY c.partitionKey");

		var requestOptions = new QueryRequestOptions
		{
			MaxConcurrency = _queryOptions.MaxConcurrency,
			MaxBufferedItemCount = _queryOptions.MaxBufferedItemCount > 0
				? _queryOptions.MaxBufferedItemCount
				: -1,
			MaxItemCount = _queryOptions.PreferredPageSize
		};

		LogCrossPartitionCountStarting();

		var counts = new Dictionary<string, int>();
		var totalRUs = 0.0;

		using var iterator = container.GetItemQueryIterator<System.Text.Json.JsonElement>(
			query,
			requestOptions: requestOptions);

		while (iterator.HasMoreResults)
		{
			var response = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
			totalRUs += response.RequestCharge;

			foreach (var item in response)
			{
				var pk = item.GetProperty("partitionKey").GetString() ?? string.Empty;
				var count = item.GetProperty("count").GetInt32();
				counts[pk] = count;
			}
		}

		LogCrossPartitionCountCompleted(counts.Count, totalRUs);

		return counts;
	}

	/// <summary>
	/// Creates optimized <see cref="QueryRequestOptions"/> for the configured query strategy.
	/// </summary>
	/// <param name="partitionKey"> Optional partition key to scope the query. </param>
	/// <param name="maxItems"> Maximum number of items per page. </param>
	/// <returns> Configured query request options. </returns>
	public QueryRequestOptions CreateQueryRequestOptions(string? partitionKey, int maxItems)
	{
		var options = new QueryRequestOptions
		{
			MaxItemCount = Math.Min(maxItems, _queryOptions.PreferredPageSize)
		};

		if (partitionKey != null)
		{
			options.PartitionKey = new PartitionKey(partitionKey);
		}
		else
		{
			// Cross-partition query settings
			options.MaxConcurrency = _queryOptions.MaxConcurrency;

			if (_queryOptions.MaxBufferedItemCount > 0)
			{
				options.MaxBufferedItemCount = _queryOptions.MaxBufferedItemCount;
			}
		}

		return options;
	}

	[LoggerMessage(102350, LogLevel.Debug,
		"Starting outbox query (crossPartition={IsCrossPartition}, maxItems={MaxItems}, concurrency={MaxConcurrency})")]
	private partial void LogQueryStarting(bool isCrossPartition, int maxItems, int maxConcurrency);

	[LoggerMessage(102351, LogLevel.Debug,
		"Outbox query completed: {ItemCount} items in {PageCount} pages, {TotalRUs:F2} RUs consumed (crossPartition={IsCrossPartition})")]
	private partial void LogQueryCompleted(int itemCount, int pageCount, double totalRUs, bool isCrossPartition);

	[LoggerMessage(102352, LogLevel.Debug,
		"Starting cross-partition count aggregation")]
	private partial void LogCrossPartitionCountStarting();

	[LoggerMessage(102353, LogLevel.Debug,
		"Cross-partition count completed: {PartitionCount} partitions, {TotalRUs:F2} RUs consumed")]
	private partial void LogCrossPartitionCountCompleted(int partitionCount, double totalRUs);
}
