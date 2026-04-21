// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

// ============================================================================
// Order Query Handlers — Read-side implementations
// ============================================================================
//
// These handlers are resolved by the Dispatch pipeline when the controller
// dispatches query objects. Each handler encapsulates the data access logic
// for its query type, keeping the controller thin and focused on HTTP concerns.
//
// Two tiers of data access are used (matching the "graduate to native" pattern):
//   IProjectionStore<T>              → Portable dictionary-based filters
//   OrderFullTextSearchRepository    → Native Elasticsearch queries
// ============================================================================

using CdcEventStoreElasticsearch.Api.Models;
using CdcEventStoreElasticsearch.Projections;
using CdcEventStoreElasticsearch.Queries;
using CdcEventStoreElasticsearch.Repositories;

using Elastic.Clients.Elasticsearch;

using Excalibur.Application.Requests.Queries;
using Excalibur.EventSourcing.Abstractions;

namespace CdcEventStoreElasticsearch.Handlers;

// ─── Projection Store Handlers ───��──────────────────────────────────────────

/// <summary>
/// Handles <see cref="GetOrderByIdQuery"/> by looking up a single projection document.
/// </summary>
public sealed class GetOrderByIdQueryHandler(
	IProjectionStore<OrderSearchProjection> projectionStore)
	: IQueryHandler<GetOrderByIdQuery, OrderSearchProjection?>
{
	/// <inheritdoc />
	public async Task<OrderSearchProjection?> HandleAsync(
		GetOrderByIdQuery message,
		CancellationToken cancellationToken)
	{
		return await projectionStore
			.GetByIdAsync(message.OrderId, cancellationToken)
			.ConfigureAwait(false);
	}
}

/// <summary>
/// Handles <see cref="SearchOrdersQuery"/> using portable dictionary-based filters.
/// </summary>
public sealed class SearchOrdersQueryHandler(
	IProjectionStore<OrderSearchProjection> projectionStore)
	: IQueryHandler<SearchOrdersQuery, PagedResult<OrderSearchProjection>>
{
	/// <inheritdoc />
	public async Task<PagedResult<OrderSearchProjection>> HandleAsync(
		SearchOrdersQuery message,
		CancellationToken cancellationToken)
	{
		var filters = BuildFilters(message);
		var options = new QueryOptions(
			Skip: (message.Page - 1) * message.PageSize,
			Take: message.PageSize,
			OrderBy: "orderDate",
			Descending: true);

		var items = await projectionStore
			.QueryAsync(filters, options, cancellationToken)
			.ConfigureAwait(false);

		var totalCount = await projectionStore
			.CountAsync(filters, cancellationToken)
			.ConfigureAwait(false);

		return new PagedResult<OrderSearchProjection>(items, message.Page, message.PageSize, totalCount);
	}

	private static Dictionary<string, object> BuildFilters(SearchOrdersQuery message)
	{
		var filters = new Dictionary<string, object>();

		if (message.CustomerId.HasValue)
		{
			filters["customerId"] = message.CustomerId.Value.ToString();
		}

		if (!string.IsNullOrWhiteSpace(message.Status))
		{
			filters["status"] = message.Status;
		}

		if (message.MinAmount.HasValue)
		{
			filters["totalAmount:gte"] = message.MinAmount.Value;
		}

		if (message.MaxAmount.HasValue)
		{
			filters["totalAmount:lte"] = message.MaxAmount.Value;
		}

		if (message.FromDate.HasValue)
		{
			filters["orderDate:gte"] = message.FromDate.Value;
		}

		if (message.ToDate.HasValue)
		{
			filters["orderDate:lte"] = message.ToDate.Value;
		}

		if (message.Tags is { Length: > 0 })
		{
			filters["tags:in"] = message.Tags;
		}

		return filters;
	}
}

/// <summary>
/// Handles <see cref="GetOrdersByCustomerQuery"/> using the projection store.
/// </summary>
public sealed class GetOrdersByCustomerQueryHandler(
	IProjectionStore<OrderSearchProjection> projectionStore)
	: IQueryHandler<GetOrdersByCustomerQuery, PagedResult<OrderSearchProjection>>
{
	/// <inheritdoc />
	public async Task<PagedResult<OrderSearchProjection>> HandleAsync(
		GetOrdersByCustomerQuery message,
		CancellationToken cancellationToken)
	{
		var filters = new Dictionary<string, object>
		{
			["customerId"] = message.CustomerId.ToString()
		};

		var options = new QueryOptions(
			Skip: (message.Page - 1) * message.PageSize,
			Take: message.PageSize,
			OrderBy: "orderDate",
			Descending: true);

		var items = await projectionStore
			.QueryAsync(filters, options, cancellationToken)
			.ConfigureAwait(false);

		var totalCount = await projectionStore
			.CountAsync(filters, cancellationToken)
			.ConfigureAwait(false);

		return new PagedResult<OrderSearchProjection>(items, message.Page, message.PageSize, totalCount);
	}
}

/// <summary>
/// Handles <see cref="GetOrdersByStatusQuery"/> using the projection store.
/// </summary>
public sealed class GetOrdersByStatusQueryHandler(
	IProjectionStore<OrderSearchProjection> projectionStore)
	: IQueryHandler<GetOrdersByStatusQuery, PagedResult<OrderSearchProjection>>
{
	/// <inheritdoc />
	public async Task<PagedResult<OrderSearchProjection>> HandleAsync(
		GetOrdersByStatusQuery message,
		CancellationToken cancellationToken)
	{
		var filters = new Dictionary<string, object>
		{
			["status"] = message.Status
		};

		var options = new QueryOptions(
			Skip: (message.Page - 1) * message.PageSize,
			Take: message.PageSize,
			OrderBy: "orderDate",
			Descending: true);

		var items = await projectionStore
			.QueryAsync(filters, options, cancellationToken)
			.ConfigureAwait(false);

		var totalCount = await projectionStore
			.CountAsync(filters, cancellationToken)
			.ConfigureAwait(false);

		return new PagedResult<OrderSearchProjection>(items, message.Page, message.PageSize, totalCount);
	}
}

/// <summary>
/// Handles <see cref="GetRecentOrdersQuery"/> by filtering orders within a date range.
/// </summary>
public sealed class GetRecentOrdersQueryHandler(
	IProjectionStore<OrderSearchProjection> projectionStore)
	: IQueryHandler<GetRecentOrdersQuery, IReadOnlyList<OrderSearchProjection>>
{
	/// <inheritdoc />
	public async Task<IReadOnlyList<OrderSearchProjection>> HandleAsync(
		GetRecentOrdersQuery message,
		CancellationToken cancellationToken)
	{
		var cutoff = DateTime.UtcNow.AddDays(-message.Days);

		var filters = new Dictionary<string, object>
		{
			["orderDate:gte"] = cutoff
		};

		var options = new QueryOptions(
			Take: message.Limit,
			OrderBy: "orderDate",
			Descending: true);

		var items = await projectionStore
			.QueryAsync(filters, options, cancellationToken)
			.ConfigureAwait(false);

		return items.ToList();
	}
}

// ─── Elasticsearch-Native Handlers ──────��───────────────────────────────────

/// <summary>
/// Handles <see cref="FullTextSearchOrdersQuery"/> using native Elasticsearch
/// <c>multi_match</c> with cursor-based pagination.
/// </summary>
/// <remarks>
/// For <see cref="PageNavigation.Previous"/> and <see cref="PageNavigation.Last"/>,
/// the repository reverses the sort order. The handler then reverses the returned
/// items to restore the expected display order.
/// </remarks>
public sealed class FullTextSearchOrdersQueryHandler(
	OrderFullTextSearchRepository searchRepository)
	: IQueryHandler<FullTextSearchOrdersQuery, CursorPagedResult<OrderSearchProjection>>
{
	/// <inheritdoc />
	public async Task<CursorPagedResult<OrderSearchProjection>> HandleAsync(
		FullTextSearchOrdersQuery message,
		CancellationToken cancellationToken)
	{
		var searchAfter = CursorHelper.DecodeCursor(message.Cursor);

		var response = await searchRepository
			.FullTextSearchAsync(
				message.SearchText,
				message.Limit,
				searchAfter,
				message.Navigation,
				cancellationToken)
			.ConfigureAwait(false);

		if (!response.IsValidResponse)
		{
			return new CursorPagedResult<OrderSearchProjection>([], 0, 0);
		}

		var reverse = message.Navigation is PageNavigation.Previous or PageNavigation.Last;
		return CursorHelper.ToCursorResult(response, message.Limit, reverse);
	}
}

/// <summary>
/// Handles <see cref="AdvancedSearchOrdersQuery"/> using native Elasticsearch
/// <c>bool</c> queries with cursor-based pagination.
/// </summary>
/// <remarks>
/// For <see cref="PageNavigation.Previous"/> and <see cref="PageNavigation.Last"/>,
/// the repository reverses the sort order. The handler then reverses the returned
/// items to restore the expected display order.
/// </remarks>
public sealed class AdvancedSearchOrdersQueryHandler(
	OrderFullTextSearchRepository searchRepository)
	: IQueryHandler<AdvancedSearchOrdersQuery, CursorPagedResult<OrderSearchProjection>>
{
	/// <inheritdoc />
	public async Task<CursorPagedResult<OrderSearchProjection>> HandleAsync(
		AdvancedSearchOrdersQuery message,
		CancellationToken cancellationToken)
	{
		var searchAfter = CursorHelper.DecodeCursor(message.Cursor);

		var response = await searchRepository
			.AdvancedSearchAsync(
				message.SearchText,
				message.CustomerId,
				message.Status,
				message.MinAmount,
				message.MaxAmount,
				message.FromDate,
				message.ToDate,
				message.Tags,
				message.PageSize,
				searchAfter,
				message.Navigation,
				cancellationToken)
			.ConfigureAwait(false);

		if (!response.IsValidResponse)
		{
			return new CursorPagedResult<OrderSearchProjection>([], 0, 0);
		}

		var reverse = message.Navigation is PageNavigation.Previous or PageNavigation.Last;
		return CursorHelper.ToCursorResult(response, message.PageSize, reverse);
	}
}

/// <summary>
/// Handles <see cref="GetOrderStatisticsQuery"/> using native Elasticsearch aggregations.
/// </summary>
public sealed class GetOrderStatisticsQueryHandler(
	OrderFullTextSearchRepository searchRepository)
	: IQueryHandler<GetOrderStatisticsQuery, SearchResponse<OrderSearchProjection>>
{
	/// <inheritdoc />
	public async Task<SearchResponse<OrderSearchProjection>> HandleAsync(
		GetOrderStatisticsQuery message,
		CancellationToken cancellationToken)
	{
		return await searchRepository
			.GetOrderStatisticsAsync(cancellationToken)
			.ConfigureAwait(false);
	}
}
