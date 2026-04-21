// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

// ============================================================================
// Custom ElasticSearch Repository for Native Query Features
// ============================================================================
//
// This repository demonstrates the "graduate to ElasticRepositoryBase<T>"
// pattern. When IProjectionStore<T>'s dictionary-based filters aren't enough
// (e.g., you need full-text search, aggregations, or geo queries), extend
// ElasticRepositoryBase<T> and use the native Elasticsearch client.
//
// KEY: Use ElasticSearchProjectionIndexConvention to resolve the same index
// name that IProjectionStore<T> uses. This prevents divergence if the
// IndexPrefix changes in options.
//
// The two query paths work together:
//   IProjectionStore<T>         → Simple CRUD, dictionary filters (portable)
//   ElasticRepositoryBase<T>    → Full-text search, aggregations (ES-native)
//
// Both target the SAME Elasticsearch index -- events keep them in sync.
// ============================================================================

using CdcEventStoreElasticsearch.Projections;

using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Aggregations;
using Elastic.Clients.Elasticsearch.QueryDsl;

using Excalibur.Data.ElasticSearch;
using Excalibur.Data.ElasticSearch.Projections;

using Microsoft.Extensions.Options;

namespace CdcEventStoreElasticsearch.Repositories;

/// <summary>
/// Custom repository for order full-text search using native Elasticsearch queries.
/// Extends <see cref="ElasticRepositoryBase{TDocument}"/> for access to the full
/// Elasticsearch Query DSL while sharing the same index as
/// <c>IProjectionStore&lt;OrderSearchProjection&gt;</c>.
/// </summary>
/// <remarks>
/// <para>
/// Use this repository when you need capabilities beyond
/// <c>IProjectionStore&lt;T&gt;</c>'s dictionary-based filters:
/// <list type="bullet">
/// <item>Full-text search across multiple fields</item>
/// <item>Aggregations (e.g., order counts by status, revenue by month)</item>
/// <item>Fuzzy matching and autocomplete</item>
/// <item>Geo queries and distance-based filtering</item>
/// <item>Scoring and relevance ranking</item>
/// </list>
/// </para>
/// <para>
/// <see cref="ElasticSearchProjectionIndexConvention"/> ensures this repository
/// resolves to the same index as the projection store, even when the
/// <see cref="ElasticSearchProjectionStoreOptions.IndexPrefix"/> changes.
/// </para>
/// </remarks>
public sealed class OrderFullTextSearchRepository : ElasticRepositoryBase<OrderSearchProjection>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="OrderFullTextSearchRepository"/> class.
	/// </summary>
	/// <param name="client">The Elasticsearch client.</param>
	/// <param name="optionsMonitor">
	/// Options monitor for resolving the projection index name.
	/// The named options key is the projection type name (<c>"OrderSearchProjection"</c>).
	/// </param>
	/// <remarks>
	/// Uses <see cref="ElasticSearchProjectionIndexConvention"/> to resolve the
	/// index name from the same options that <c>IProjectionStore&lt;OrderSearchProjection&gt;</c>
	/// uses, ensuring both query paths target the same index.
	/// </remarks>
	public OrderFullTextSearchRepository(
		ElasticsearchClient client,
		IOptionsMonitor<ElasticSearchProjectionStoreOptions> optionsMonitor)
		: base(
			client,
			ElasticSearchProjectionIndexConvention.GetIndexName<OrderSearchProjection>(
				optionsMonitor.Get(nameof(OrderSearchProjection))))
	{
	}

	/// <inheritdoc />
	/// <remarks>
	/// Index initialization is handled by <c>IProjectionStore&lt;OrderSearchProjection&gt;</c>
	/// (configured with <c>CreateIndexOnInitialize = true</c>). This repository reads from
	/// the same index, so no separate initialization is needed.
	/// </remarks>
	public override Task InitializeIndexAsync(CancellationToken cancellationToken)
		=> Task.CompletedTask;

	/// <summary>
	/// Performs a full-text search across order fields (customer name, status, product names, tags).
	/// </summary>
	/// <param name="searchText">The search text to match against multiple fields.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Search response with matching orders and metadata (scores, highlights).</returns>
	/// <remarks>
	/// This uses Elasticsearch's <c>multi_match</c> query, which is not available
	/// through <c>IProjectionStore&lt;T&gt;</c>'s dictionary-based filter API.
	/// Results are ranked by relevance score.
	/// </remarks>
	public async Task<SearchResponse<OrderSearchProjection>> FullTextSearchAsync(
		string searchText,
		int take = 20,
		CancellationToken cancellationToken = default)
	{
		var request = new SearchRequestDescriptor<OrderSearchProjection>();
		request
			.Size(take)
			.Query(q => q
				.MultiMatch(mm => mm
					.Query(searchText)
					.Fields(new[]
					{
						"customerName^3",    // Boost customer name matches
						"status^2",          // Boost status matches
						"lineItems.productName",
						"tags",
						"externalOrderId"
					})
					.Fuzziness(new Fuzziness("AUTO"))
					.Type(TextQueryType.BestFields)));

		return await SearchAsync(request, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Gets aggregated order statistics using Elasticsearch aggregations.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>
	/// Search response with aggregation buckets for status distribution,
	/// revenue statistics, and monthly trends.
	/// </returns>
	/// <remarks>
	/// Aggregations are a native Elasticsearch feature with no equivalent in
	/// <c>IProjectionStore&lt;T&gt;</c>. Use this for dashboards, reports,
	/// and analytics that need server-side computation.
	/// </remarks>
	public async Task<SearchResponse<OrderSearchProjection>> GetOrderStatisticsAsync(
		CancellationToken cancellationToken = default)
	{
		var request = new SearchRequestDescriptor<OrderSearchProjection>();
		request
			.Size(0) // We only want aggregations, not documents
			.Aggregations(aggs => aggs
				.Add("by_status", agg => agg
					.Terms(t => t
						.Field("status.keyword")))
				.Add("total_revenue", agg => agg
					.Sum(sum => sum
						.Field("totalAmount")))
				.Add("avg_order_value", agg => agg
					.Avg(avg => avg
						.Field("totalAmount")))
				.Add("orders_over_time", agg => agg
					.DateHistogram(dh => dh
						.Field("orderDate")
						.CalendarInterval(CalendarInterval.Month))));

		return await SearchAsync(request, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Searches orders with combined full-text and filter criteria.
	/// </summary>
	/// <param name="searchText">Optional full-text search query.</param>
	/// <param name="status">Optional status filter.</param>
	/// <param name="minAmount">Optional minimum order amount.</param>
	/// <param name="maxAmount">Optional maximum order amount.</param>
	/// <param name="skip">Number of results to skip.</param>
	/// <param name="take">Number of results to return.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Search response with matching orders.</returns>
	/// <remarks>
	/// Demonstrates combining a full-text <c>must</c> clause with structured
	/// <c>filter</c> clauses in a <c>bool</c> query. Filters don't affect
	/// relevance scoring and are cacheable by Elasticsearch.
	/// </remarks>
	public async Task<SearchResponse<OrderSearchProjection>> AdvancedSearchAsync(
		string? searchText = null,
		string? status = null,
		decimal? minAmount = null,
		decimal? maxAmount = null,
		int skip = 0,
		int take = 20,
		CancellationToken cancellationToken = default)
	{
		var request = new SearchRequestDescriptor<OrderSearchProjection>();
		request
			.From(skip)
			.Size(take)
			.Query(q => q
				.Bool(b =>
				{
					// Full-text search in "must" (affects scoring)
					if (!string.IsNullOrWhiteSpace(searchText))
					{
						b.Must(m => m
							.MultiMatch(mm => mm
								.Query(searchText)
								.Fields(new[] { "customerName^3", "lineItems.productName", "tags" })
								.Fuzziness(new Fuzziness("AUTO"))));
					}

					// Structured filters in "filter" (cacheable, no scoring)
					var filters = new List<Action<QueryDescriptor<OrderSearchProjection>>>();

					if (!string.IsNullOrWhiteSpace(status))
					{
						filters.Add(f => f.Term(t => t
							.Field("status.keyword")
							.Value(status)));
					}

					if (minAmount.HasValue || maxAmount.HasValue)
					{
						filters.Add(f => f.Range(r => r
							.NumberRange(nr =>
							{
								nr.Field("totalAmount");
								if (minAmount.HasValue)
								{
									nr.Gte((double)minAmount.Value);
								}
								if (maxAmount.HasValue)
								{
									nr.Lte((double)maxAmount.Value);
								}
							})));
					}

					if (filters.Count > 0)
					{
						b.Filter(filters.ToArray());
					}
				}))
			.Sort(so => so
				.Score(sc => sc.Order(SortOrder.Desc))
				.Field("orderDate", f => f.Order(SortOrder.Desc)));

		return await SearchAsync(request, cancellationToken).ConfigureAwait(false);
	}
}
