// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using CdcEventStoreElasticsearch.Api.Models;
using CdcEventStoreElasticsearch.Projections;
using CdcEventStoreElasticsearch.Queries;

using Elastic.Clients.Elasticsearch;

using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Abstractions;

using Microsoft.AspNetCore.Mvc;

namespace CdcEventStoreElasticsearch.Api.Controllers;

/// <summary>
/// API controller for order search and retrieval.
/// </summary>
/// <remarks>
/// <para>
/// All read operations are dispatched through the Excalibur Dispatch pipeline as
/// <b>queries</b>, enabling cross-cutting concerns (logging, validation, caching,
/// telemetry) to observe every read path without coupling the controller to
/// infrastructure.
/// </para>
/// <para>
/// The controller is responsible only for HTTP concerns:
/// <list type="bullet">
/// <item>Mapping request DTOs to query objects</item>
/// <item>Dispatching queries through the pipeline</item>
/// <item>Mapping handler results to response DTOs</item>
/// <item>Returning appropriate HTTP status codes</item>
/// </list>
/// </para>
/// <para>
/// The handler layer uses the <b>two-tier query pattern</b> for Elasticsearch projections:
/// </para>
/// <list type="bullet">
/// <item>
/// <b>IProjectionStore&lt;T&gt;</b> — Portable dictionary-based filters for simple
/// equality, range, and contains queries. Works across all backends.
/// </item>
/// <item>
/// <b>OrderFullTextSearchRepository</b> — Native Elasticsearch queries for
/// full-text search, aggregations, fuzzy matching, and relevance scoring.
/// </item>
/// </list>
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class OrdersController : ControllerBase
{
	private readonly IDispatcher _dispatcher;
	private readonly ILogger<OrdersController> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="OrdersController"/> class.
	/// </summary>
	/// <param name="dispatcher">The Dispatch pipeline dispatcher.</param>
	/// <param name="logger">The logger.</param>
	public OrdersController(
		IDispatcher dispatcher,
		ILogger<OrdersController> logger)
	{
		_dispatcher = dispatcher;
		_logger = logger;
	}

	/// <summary>
	/// Gets an order by ID.
	/// </summary>
	/// <param name="id">The order document ID.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The order if found.</returns>
	[HttpGet("{id}")]
	[ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetById(string id, CancellationToken cancellationToken)
	{
		var query = new GetOrderByIdQuery(id);
		var result = await DispatchQueryAsync<GetOrderByIdQuery, OrderSearchProjection?>(query, cancellationToken)
			.ConfigureAwait(false);

		if (!result.Succeeded || result.ReturnValue is null)
		{
			return NotFound(new { Message = $"Order {id} not found" });
		}

		return Ok(OrderMapper.ToDto(result.ReturnValue));
	}

	/// <summary>
	/// Searches orders with filters.
	/// </summary>
	/// <param name="request">The search request with filters.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Paged list of matching orders.</returns>
	[HttpGet]
	[ProducesResponseType(typeof(PagedResult<OrderDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> Search([FromQuery] OrderSearchRequest request, CancellationToken cancellationToken)
	{
		var query = new SearchOrdersQuery
		{
			Query = request.Query,
			CustomerId = request.CustomerId,
			Status = request.Status,
			MinAmount = request.MinAmount,
			MaxAmount = request.MaxAmount,
			FromDate = request.FromDate,
			ToDate = request.ToDate,
			Tags = request.Tags,
			Page = request.Page,
			PageSize = request.PageSize
		};

		var result = await DispatchQueryAsync<SearchOrdersQuery, PagedResult<OrderSearchProjection>>(query, cancellationToken)
			.ConfigureAwait(false);

		if (!result.Succeeded)
		{
			return Ok(new PagedResult<OrderDto>([]));
		}

		var dto = OrderMapper.ToDto(result.ReturnValue!);

		_logger.LogDebug(
			"Order search returned {Count} of {Total} results",
			dto.Items.Count,
			dto.TotalItems);

		return Ok(dto);
	}

	/// <summary>
	/// Gets orders for a specific customer.
	/// </summary>
	/// <param name="customerId">The customer ID.</param>
	/// <param name="page">Page number (1-based).</param>
	/// <param name="pageSize">Page size.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Orders for the customer.</returns>
	[HttpGet("by-customer/{customerId}")]
	[ProducesResponseType(typeof(PagedResult<OrderDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetByCustomer(
		Guid customerId,
		[FromQuery] int page = 1,
		[FromQuery] int pageSize = 20,
		CancellationToken cancellationToken = default)
	{
		var query = new GetOrdersByCustomerQuery(customerId) { Page = page, PageSize = pageSize };

		var result = await DispatchQueryAsync<GetOrdersByCustomerQuery, PagedResult<OrderSearchProjection>>(query, cancellationToken)
			.ConfigureAwait(false);

		return Ok(result.Succeeded
			? OrderMapper.ToDto(result.ReturnValue!)
			: new PagedResult<OrderDto>([]));
	}

	/// <summary>
	/// Gets orders by status.
	/// </summary>
	/// <param name="status">The order status.</param>
	/// <param name="page">Page number (1-based).</param>
	/// <param name="pageSize">Page size.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Orders with the specified status.</returns>
	[HttpGet("by-status/{status}")]
	[ProducesResponseType(typeof(PagedResult<OrderDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetByStatus(
		string status,
		[FromQuery] int page = 1,
		[FromQuery] int pageSize = 20,
		CancellationToken cancellationToken = default)
	{
		var query = new GetOrdersByStatusQuery(status) { Page = page, PageSize = pageSize };

		var result = await DispatchQueryAsync<GetOrdersByStatusQuery, PagedResult<OrderSearchProjection>>(query, cancellationToken)
			.ConfigureAwait(false);

		return Ok(result.Succeeded
			? OrderMapper.ToDto(result.ReturnValue!)
			: new PagedResult<OrderDto>([]));
	}

	/// <summary>
	/// Gets recent orders within a date range.
	/// </summary>
	/// <param name="days">Number of days to look back.</param>
	/// <param name="limit">Maximum results to return.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Recent orders.</returns>
	[HttpGet("recent")]
	[ProducesResponseType(typeof(IEnumerable<OrderDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetRecent(
		[FromQuery] int days = 7,
		[FromQuery] int limit = 50,
		CancellationToken cancellationToken = default)
	{
		var query = new GetRecentOrdersQuery { Days = days, Limit = limit };

		var result = await DispatchQueryAsync<GetRecentOrdersQuery, IReadOnlyList<OrderSearchProjection>>(query, cancellationToken)
			.ConfigureAwait(false);

		return Ok(result.Succeeded
			? OrderMapper.ToDto(result.ReturnValue!)
			: Array.Empty<OrderDto>());
	}

	// ========================================================================
	// Native Elasticsearch Queries (via OrderFullTextSearchRepository)
	// ========================================================================

	/// <summary>
	/// Full-text search across orders using native Elasticsearch queries.
	/// Uses cursor-based pagination for efficient deep paging.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Searches across customer name (boosted 3x), status (boosted 2x),
	/// product names, tags, and external order IDs using Elasticsearch's
	/// <c>multi_match</c> query with fuzzy matching.
	/// </para>
	/// <para>
	/// Pass the <c>nextCursor</c> value from the response as the
	/// <c>cursor</c> query parameter to retrieve the next page.
	/// </para>
	/// </remarks>
	/// <param name="request">The full-text search request.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Orders ranked by relevance with a cursor for the next page.</returns>
	[HttpGet("search")]
	[ProducesResponseType(typeof(CursorPagedResult<OrderDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> FullTextSearch(
		[FromQuery] FullTextSearchRequest request,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(request.Q))
		{
			return BadRequest(new { Message = "Search query 'q' is required" });
		}

		var query = new FullTextSearchOrdersQuery(request.Q)
		{
			Limit = request.Limit,
			Cursor = request.Cursor,
			Navigation = request.Navigation
		};

		var result = await DispatchQueryAsync<FullTextSearchOrdersQuery, CursorPagedResult<OrderSearchProjection>>(query, cancellationToken)
			.ConfigureAwait(false);

		if (!result.Succeeded)
		{
			_logger.LogWarning("Full-text search dispatch failed for query '{Query}'", request.Q);
			return Ok(new CursorPagedResult<OrderDto>([], 0, 0));
		}

		var dto = OrderMapper.ToDto(result.ReturnValue!);

		_logger.LogDebug(
			"Full-text search for '{Query}' returned {Count} results",
			request.Q,
			dto.Items.Count());

		return Ok(dto);
	}

	/// <summary>
	/// Advanced search combining full-text queries with structured filters.
	/// Uses cursor-based pagination via Elasticsearch <c>search_after</c>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Demonstrates Elasticsearch <c>bool</c> queries combining:
	/// <list type="bullet">
	/// <item><b>must</b> — Full-text search (affects relevance scoring)</item>
	/// <item><b>filter</b> — Structured equality/range filters (cacheable, no scoring)</item>
	/// </list>
	/// </para>
	/// <para>
	/// Pass the <c>nextCursor</c> value from the response as the
	/// <c>cursor</c> query parameter to retrieve the next page.
	/// </para>
	/// </remarks>
	/// <param name="request">The advanced search request with filters and cursor.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Orders matching the combined search criteria with a cursor for the next page.</returns>
	[HttpGet("search/advanced")]
	[ProducesResponseType(typeof(CursorPagedResult<OrderDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> AdvancedSearch(
		[FromQuery] AdvancedSearchRequest request,
		CancellationToken cancellationToken = default)
	{
		var query = new AdvancedSearchOrdersQuery
		{
			SearchText = request.Q,
			CustomerId = request.CustomerId,
			Status = request.Status,
			MinAmount = request.MinAmount,
			MaxAmount = request.MaxAmount,
			FromDate = request.FromDate,
			ToDate = request.ToDate,
			Tags = request.Tags,
			PageSize = request.PageSize,
			Cursor = request.Cursor,
			Navigation = request.Navigation
		};

		var result = await DispatchQueryAsync<AdvancedSearchOrdersQuery, CursorPagedResult<OrderSearchProjection>>(query, cancellationToken)
			.ConfigureAwait(false);

		if (!result.Succeeded)
		{
			_logger.LogWarning("Advanced search dispatch failed");
			return Ok(new CursorPagedResult<OrderDto>([], 0, 0));
		}

		var dto = OrderMapper.ToDto(result.ReturnValue!);

		_logger.LogDebug(
			"Advanced search returned {Count} of {Total} results",
			dto.Items.Count(),
			dto.TotalRecords);

		return Ok(dto);
	}

	/// <summary>
	/// Gets aggregated order statistics using Elasticsearch aggregations.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Returns server-side computed statistics including:
	/// <list type="bullet">
	/// <item>Order count by status</item>
	/// <item>Total and average revenue</item>
	/// <item>Monthly order trends</item>
	/// </list>
	/// </para>
	/// </remarks>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Aggregated order statistics.</returns>
	[HttpGet("statistics")]
	[ProducesResponseType(typeof(OrderStatisticsDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetStatistics(CancellationToken cancellationToken)
	{
		var query = new GetOrderStatisticsQuery();

		var result = await DispatchQueryAsync<GetOrderStatisticsQuery, SearchResponse<OrderSearchProjection>>(query, cancellationToken)
			.ConfigureAwait(false);

		if (!result.Succeeded || !result.ReturnValue!.IsValidResponse)
		{
			_logger.LogWarning("Elasticsearch aggregation query failed");
			return Ok(new OrderStatisticsDto());
		}

		var response = result.ReturnValue;

		var dto = new OrderStatisticsDto
		{
			TotalRevenue = response.Aggregations?.GetSum("total_revenue")?.Value ?? 0d,
			AverageOrderValue = response.Aggregations?.GetAverage("avg_order_value")?.Value ?? 0d,
			ByStatus = response.Aggregations?.GetStringTerms("by_status")?.Buckets
				.Select(b => new StatusCountDto { Status = b.Key.Value?.ToString(), Count = b.DocCount })
				.ToList() ?? [],
			MonthlyTrend = response.Aggregations?.GetDateHistogram("orders_over_time")?.Buckets
				.Select(b => new MonthlyTrendDto { Month = b.KeyAsString, Count = b.DocCount })
				.ToList() ?? []
		};

		return Ok(dto);
	}

	// ========================================================================
	// Helper: Dispatch a query through the pipeline
	// ========================================================================

	private async Task<IMessageResult<TResult>> DispatchQueryAsync<TQuery, TResult>(
		TQuery query,
		CancellationToken cancellationToken)
		where TQuery : IDispatchAction<TResult>
	{
		using var scope = HttpContext.RequestServices.CreateScope();
		var context = scope.ServiceProvider.GetRequiredService<IMessageContext>();

		return await _dispatcher
			.DispatchAsync<TQuery, TResult>(query, context, cancellationToken)
			.ConfigureAwait(false);
	}
}
