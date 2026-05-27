// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

// ============================================================================
// Order Queries — CQRS read-side operations dispatched through the pipeline
// ============================================================================
//
// Each query is a distinct type implementing IQuery<TResult> so handlers are
// resolved and invoked through the Dispatch pipeline. This enables middleware
// (logging, validation, caching, telemetry) to observe every read path
// without coupling the controller to infrastructure concerns.
//
// The controller dispatches these; the handlers resolve the appropriate
// repository or projection store.
// ============================================================================

using CdcEventStoreElasticsearch.Api.Models;
using CdcEventStoreElasticsearch.Projections;

using Elastic.Clients.Elasticsearch;

using Excalibur.Application.Requests.Queries;
using Excalibur.EventSourcing;

namespace CdcEventStoreElasticsearch.Queries;

// ─── Projection Store Queries (portable, dictionary-based filters) ───────────

/// <summary>
/// Gets a single order projection by its document ID.
/// </summary>
public sealed class GetOrderByIdQuery(string orderId) : QueryBase<OrderSearchProjection?>
{
	/// <summary>Gets the order document ID.</summary>
	public string OrderId { get; } = orderId;
}

/// <summary>
/// Searches orders using the portable <c>IProjectionStore</c> dictionary-based filters.
/// Supports filtering by customer, status, amount range, date range, and tags.
/// </summary>
public sealed class SearchOrdersQuery : QueryBase<PagedResult<OrderSearchProjection>>
{
	/// <summary>Gets or sets the full-text search query.</summary>
	public string? Query { get; init; }

	/// <summary>Gets or sets the customer ID filter.</summary>
	public Guid? CustomerId { get; init; }

	/// <summary>Gets or sets the status filter.</summary>
	public string? Status { get; init; }

	/// <summary>Gets or sets the minimum order amount.</summary>
	public decimal? MinAmount { get; init; }

	/// <summary>Gets or sets the maximum order amount.</summary>
	public decimal? MaxAmount { get; init; }

	/// <summary>Gets or sets the minimum order date.</summary>
	public DateTime? FromDate { get; init; }

	/// <summary>Gets or sets the maximum order date.</summary>
	public DateTime? ToDate { get; init; }

	/// <summary>Gets or sets the tags filter (any match).</summary>
	public string[]? Tags { get; init; }

	/// <summary>Gets or sets the page number (1-based).</summary>
	public int Page { get; init; } = 1;

	/// <summary>Gets or sets the page size.</summary>
	public int PageSize { get; init; } = 20;
}

/// <summary>
/// Gets orders for a specific customer with offset paging.
/// </summary>
public sealed class GetOrdersByCustomerQuery(Guid customerId) : QueryBase<PagedResult<OrderSearchProjection>>
{
	/// <summary>Gets the customer ID.</summary>
	public Guid CustomerId { get; } = customerId;

	/// <summary>Gets or sets the page number (1-based).</summary>
	public int Page { get; init; } = 1;

	/// <summary>Gets or sets the page size.</summary>
	public int PageSize { get; init; } = 20;
}

/// <summary>
/// Gets orders by status with offset paging.
/// </summary>
public sealed class GetOrdersByStatusQuery(string status) : QueryBase<PagedResult<OrderSearchProjection>>
{
	/// <summary>Gets the order status to filter by.</summary>
	public string Status { get; } = status;

	/// <summary>Gets or sets the page number (1-based).</summary>
	public int Page { get; init; } = 1;

	/// <summary>Gets or sets the page size.</summary>
	public int PageSize { get; init; } = 20;
}

/// <summary>
/// Gets recent orders within a specified number of days.
/// </summary>
public sealed class GetRecentOrdersQuery : QueryBase<IReadOnlyList<OrderSearchProjection>>
{
	/// <summary>Gets or sets the number of days to look back.</summary>
	public int Days { get; init; } = 7;

	/// <summary>Gets or sets the maximum number of results.</summary>
	public int Limit { get; init; } = 50;
}

// ─── Elasticsearch-Native Queries (full-text search, aggregations) ───────────

/// <summary>
/// Performs a full-text search across order fields using Elasticsearch's
/// <c>multi_match</c> query with cursor-based pagination.
/// </summary>
public sealed class FullTextSearchOrdersQuery(string searchText) : QueryBase<CursorPagedResult<OrderSearchProjection>>
{
	/// <summary>Gets the search text.</summary>
	public string SearchText { get; } = searchText;

	/// <summary>Gets or sets the maximum results per page.</summary>
	public int Limit { get; init; } = 20;

	/// <summary>Gets or sets the opaque cursor from the previous page.</summary>
	public string? Cursor { get; init; }

	/// <summary>Gets or sets the navigation direction.</summary>
	public PageNavigation Navigation { get; init; } = PageNavigation.Next;
}

/// <summary>
/// Advanced search combining full-text queries with structured filters.
/// Uses cursor-based pagination via Elasticsearch <c>search_after</c>.
/// </summary>
public sealed class AdvancedSearchOrdersQuery : QueryBase<CursorPagedResult<OrderSearchProjection>>
{
	/// <summary>Gets or sets the optional full-text search query.</summary>
	public string? SearchText { get; init; }

	/// <summary>Gets or sets the customer ID filter.</summary>
	public Guid? CustomerId { get; init; }

	/// <summary>Gets or sets the optional status filter.</summary>
	public string? Status { get; init; }

	/// <summary>Gets or sets the optional minimum order amount.</summary>
	public decimal? MinAmount { get; init; }

	/// <summary>Gets or sets the optional maximum order amount.</summary>
	public decimal? MaxAmount { get; init; }

	/// <summary>Gets or sets the minimum order date.</summary>
	public DateTime? FromDate { get; init; }

	/// <summary>Gets or sets the maximum order date.</summary>
	public DateTime? ToDate { get; init; }

	/// <summary>Gets or sets the tags filter (any match).</summary>
	public string[]? Tags { get; init; }

	/// <summary>Gets or sets the page size.</summary>
	public int PageSize { get; init; } = 20;

	/// <summary>Gets or sets the opaque cursor from the previous page.</summary>
	public string? Cursor { get; init; }

	/// <summary>Gets or sets the navigation direction.</summary>
	public PageNavigation Navigation { get; init; } = PageNavigation.Next;
}

/// <summary>
/// Gets aggregated order statistics using Elasticsearch aggregations.
/// </summary>
public sealed class GetOrderStatisticsQuery : QueryBase<SearchResponse<OrderSearchProjection>>
{
}
