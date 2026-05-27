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
using Excalibur.EventSourcing;

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
	/// Uses Elasticsearch's <c>search_after</c> for efficient cursor-based pagination
	/// with bidirectional navigation support.
	/// </summary>
	/// <param name="searchText">The search text to match against multiple fields.</param>
	/// <param name="take">Number of results to return per page.</param>
	/// <param name="searchAfter">
	/// Cursor from a previous page's boundary hit (<see cref="Elastic.Clients.Elasticsearch.Core.Search.Hit{T}.Sort"/>).
	/// Pass <c>null</c> for the first or last page.
	/// </param>
	/// <param name="navigation">
	/// The page navigation direction. <see cref="PageNavigation.Previous"/> and
	/// <see cref="PageNavigation.Last"/> reverse the sort order; the caller must
	/// reverse the returned results to restore the expected display order.
	/// </param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Search response with matching orders and metadata (scores, highlights).</returns>
	/// <remarks>
	/// <para>
	/// Bidirectional cursor pagination works by reversing the sort direction:
	/// <list type="bullet">
	/// <item><b>First</b> — No cursor, normal sort order.</item>
	/// <item><b>Next</b> — <c>search_after</c> with last item's sort values, normal sort.</item>
	/// <item><b>Previous</b> — <c>search_after</c> with first item's sort values, reversed sort. Caller reverses results.</item>
	/// <item><b>Last</b> — No cursor, reversed sort. Caller reverses results.</item>
	/// </list>
	/// </para>
	/// </remarks>
	public async Task<SearchResponse<OrderSearchProjection>> FullTextSearchAsync(
		string searchText,
		int take = 20,
		IList<FieldValue>? searchAfter = null,
		PageNavigation navigation = PageNavigation.Next,
		CancellationToken cancellationToken = default)
	{
		var reverse = navigation is PageNavigation.Previous or PageNavigation.Last;
		var scoreOrder = reverse ? SortOrder.Asc : SortOrder.Desc;
		var tiebreakerOrder = reverse ? SortOrder.Desc : SortOrder.Asc;

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
					.Type(TextQueryType.BestFields)))
			.Sort(so => so
				.Score(sc => sc.Order(scoreOrder))
				.Field("orderId.keyword", f => f.Order(tiebreakerOrder)));

		// First/Last navigate without a cursor; Next/Previous use the cursor
		if (navigation is PageNavigation.Next or PageNavigation.Previous
			&& searchAfter is { Count: > 0 })
		{
			request.SearchAfter(searchAfter);
		}

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
	/// Searches orders with combined full-text and structured filter criteria.
	/// Uses Elasticsearch's <c>search_after</c> for efficient cursor-based pagination.
	/// </summary>
	/// <param name="searchText">Optional full-text search query.</param>
	/// <param name="customerId">Optional customer ID filter.</param>
	/// <param name="status">Optional status filter.</param>
	/// <param name="minAmount">Optional minimum order amount.</param>
	/// <param name="maxAmount">Optional maximum order amount.</param>
	/// <param name="fromDate">Optional minimum order date.</param>
	/// <param name="toDate">Optional maximum order date.</param>
	/// <param name="tags">Optional tags filter (any match).</param>
	/// <param name="take">Number of results to return per page.</param>
	/// <param name="searchAfter">
	/// Cursor from the previous page's last hit (<see cref="Elastic.Clients.Elasticsearch.Core.Search.Hit{T}.Sort"/>).
	/// Pass <c>null</c> for the first page.
	/// </param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Search response with matching orders.</returns>
	/// <remarks>
	/// <para>
	/// Demonstrates combining a full-text <c>must</c> clause with structured
	/// <c>filter</c> clauses in a <c>bool</c> query. Filters don't affect
	/// relevance scoring and are cacheable by Elasticsearch.
	/// </para>
	/// <para>
	/// Cursor-based pagination via <c>search_after</c> replaces offset-based
	/// <c>from</c>/<c>size</c> paging. Offset paging degrades for deep pages because
	/// Elasticsearch must score and discard all preceding documents. With
	/// <c>search_after</c>, each page resumes directly from the last sort value,
	/// keeping query cost constant regardless of page depth.
	/// </para>
	/// <para>
	/// To iterate pages: capture the <c>Sort</c> array from the last
	/// <see cref="Elastic.Clients.Elasticsearch.Core.Search.Hit{T}"/> in the response
	/// and pass it as <paramref name="searchAfter"/> on the next call.
	/// </para>
	/// </remarks>
	public async Task<SearchResponse<OrderSearchProjection>> AdvancedSearchAsync(
		string? searchText = null,
		Guid? customerId = null,
		string? status = null,
		decimal? minAmount = null,
		decimal? maxAmount = null,
		DateTime? fromDate = null,
		DateTime? toDate = null,
		string[]? tags = null,
		int take = 20,
		IList<FieldValue>? searchAfter = null,
		PageNavigation navigation = PageNavigation.Next,
		CancellationToken cancellationToken = default)
	{
		var reverse = navigation is PageNavigation.Previous or PageNavigation.Last;

		var request = new SearchRequestDescriptor<OrderSearchProjection>();
		request
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

					if (customerId.HasValue)
					{
						filters.Add(f => f.Term(t => t
							.Field("customerId.keyword")
							.Value(customerId.Value.ToString())));
					}

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

					if (fromDate.HasValue || toDate.HasValue)
					{
						filters.Add(f => f.Range(r => r
							.DateRange(dr =>
							{
								dr.Field("orderDate");
								if (fromDate.HasValue)
								{
									dr.Gte(fromDate.Value);
								}
								if (toDate.HasValue)
								{
									dr.Lte(toDate.Value);
								}
							})));
					}

					if (tags is { Length: > 0 })
					{
						filters.Add(f => f.Terms(t => t
							.Field("tags.keyword")
							.Terms(new TermsQueryField(tags.Select(tag => FieldValue.String(tag)).ToArray()))));
					}

					if (filters.Count > 0)
					{
						b.Filter(filters.ToArray());
					}
				}))
			.Sort(so => so
				.Score(sc => sc.Order(reverse ? SortOrder.Asc : SortOrder.Desc))
				.Field("orderDate", f => f.Order(reverse ? SortOrder.Asc : SortOrder.Desc))
				.Field("orderId.keyword", f => f.Order(reverse ? SortOrder.Desc : SortOrder.Asc)));

		// First/Last navigate without a cursor; Next/Previous use the cursor
		if (navigation is PageNavigation.Next or PageNavigation.Previous
			&& searchAfter is { Count: > 0 })
		{
			request.SearchAfter(searchAfter);
		}

		return await SearchAsync(request, cancellationToken).ConfigureAwait(false);
	}
}
