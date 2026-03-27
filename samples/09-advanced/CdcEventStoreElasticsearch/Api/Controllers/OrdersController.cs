// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using CdcEventStoreElasticsearch.Api.Models;
using CdcEventStoreElasticsearch.Projections;
using CdcEventStoreElasticsearch.Repositories;

using Excalibur.EventSourcing.Abstractions;

using Microsoft.AspNetCore.Mvc;

namespace CdcEventStoreElasticsearch.Api.Controllers;

/// <summary>
/// API controller for order search and retrieval.
/// </summary>
/// <remarks>
/// <para>
/// Demonstrates the <b>two-tier query pattern</b> for Elasticsearch projections:
/// </para>
/// <list type="bullet">
/// <item>
/// <b>IProjectionStore&lt;T&gt;</b> — Portable dictionary-based filters for simple
/// equality, range, and contains queries. Works across all backends.
/// Used by: <c>GET /api/orders</c>, <c>GET /api/orders/{id}</c>,
/// <c>GET /api/orders/by-customer</c>, <c>GET /api/orders/by-status</c>.
/// </item>
/// <item>
/// <b>OrderFullTextSearchRepository</b> — Native Elasticsearch queries for
/// full-text search, aggregations, fuzzy matching, and relevance scoring.
/// Used by: <c>GET /api/orders/search</c>, <c>GET /api/orders/statistics</c>.
/// </item>
/// </list>
/// <para>
/// Both target the <b>same Elasticsearch index</b> (resolved via
/// <c>ElasticSearchProjectionIndexConvention</c>), kept in sync by the
/// projection handlers processing the same event stream.
/// </para>
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class OrdersController : ControllerBase
{
	private readonly IProjectionStore<OrderSearchProjection> _projectionStore;
	private readonly OrderFullTextSearchRepository _searchRepository;
	private readonly ILogger<OrdersController> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="OrdersController"/> class.
	/// </summary>
	public OrdersController(
		IProjectionStore<OrderSearchProjection> projectionStore,
		OrderFullTextSearchRepository searchRepository,
		ILogger<OrdersController> logger)
	{
		_projectionStore = projectionStore;
		_searchRepository = searchRepository;
		_logger = logger;
	}

	/// <summary>
	/// Gets an order by ID.
	/// </summary>
	/// <param name="id">The order ID.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The order projection if found.</returns>
	[HttpGet("{id}")]
	[ProducesResponseType(typeof(OrderSearchProjection), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetById(string id, CancellationToken cancellationToken)
	{
		var order = await _projectionStore.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);

		if (order is null)
		{
			return NotFound(new { Message = $"Order {id} not found" });
		}

		return Ok(order);
	}

	/// <summary>
	/// Searches orders with filters.
	/// </summary>
	/// <param name="request">The search request with filters.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Paged list of matching orders.</returns>
	[HttpGet]
	[ProducesResponseType(typeof(PagedResult<OrderSearchProjection>), StatusCodes.Status200OK)]
	public async Task<IActionResult> Search([FromQuery] OrderSearchRequest request, CancellationToken cancellationToken)
	{
		var filters = new Dictionary<string, object>();

		// Build filters from request
		if (request.CustomerId.HasValue)
		{
			filters["customerId"] = request.CustomerId.Value.ToString();
		}

		if (!string.IsNullOrWhiteSpace(request.Status))
		{
			filters["status"] = request.Status;
		}

		if (request.MinAmount.HasValue)
		{
			filters["totalAmount:gte"] = request.MinAmount.Value;
		}

		if (request.MaxAmount.HasValue)
		{
			filters["totalAmount:lte"] = request.MaxAmount.Value;
		}

		if (request.FromDate.HasValue)
		{
			filters["orderDate:gte"] = request.FromDate.Value;
		}

		if (request.ToDate.HasValue)
		{
			filters["orderDate:lte"] = request.ToDate.Value;
		}

		if (request.Tags is { Length: > 0 })
		{
			filters["tags:in"] = request.Tags;
		}

		var skip = (request.Page - 1) * request.PageSize;
		var options = new QueryOptions(Skip: skip, Take: request.PageSize);

		var items = await _projectionStore
			.QueryAsync(filters, options, cancellationToken)
			.ConfigureAwait(false);

		var totalCount = await _projectionStore
			.CountAsync(filters, cancellationToken)
			.ConfigureAwait(false);

		var result = new PagedResult<OrderSearchProjection>
		{
			Items = items,
			TotalCount = (int)totalCount,
			Page = request.Page,
			PageSize = request.PageSize
		};

		_logger.LogDebug(
			"Order search returned {Count} of {Total} results",
			result.Items.Count,
			totalCount);

		return Ok(result);
	}

	/// <summary>
	/// Gets orders for a specific customer.
	/// </summary>
	/// <param name="customerId">The customer ID.</param>
	/// <param name="page">Page number.</param>
	/// <param name="pageSize">Page size.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Orders for the customer.</returns>
	[HttpGet("by-customer/{customerId}")]
	[ProducesResponseType(typeof(PagedResult<OrderSearchProjection>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetByCustomer(
		Guid customerId,
		[FromQuery] int page = 1,
		[FromQuery] int pageSize = 20,
		CancellationToken cancellationToken = default)
	{
		var filters = new Dictionary<string, object> { ["customerId"] = customerId.ToString() };

		var skip = (page - 1) * pageSize;
		var options = new QueryOptions(Skip: skip, Take: pageSize);

		var items = await _projectionStore
			.QueryAsync(filters, options, cancellationToken)
			.ConfigureAwait(false);

		var totalCount = await _projectionStore
			.CountAsync(filters, cancellationToken)
			.ConfigureAwait(false);

		var result = new PagedResult<OrderSearchProjection>
		{
			Items = items,
			TotalCount = (int)totalCount,
			Page = page,
			PageSize = pageSize
		};

		return Ok(result);
	}

	/// <summary>
	/// Gets orders by status.
	/// </summary>
	/// <param name="status">The order status.</param>
	/// <param name="page">Page number.</param>
	/// <param name="pageSize">Page size.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Orders with the specified status.</returns>
	[HttpGet("by-status/{status}")]
	[ProducesResponseType(typeof(PagedResult<OrderSearchProjection>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetByStatus(
		string status,
		[FromQuery] int page = 1,
		[FromQuery] int pageSize = 20,
		CancellationToken cancellationToken = default)
	{
		var filters = new Dictionary<string, object> { ["status"] = status };

		var skip = (page - 1) * pageSize;
		var options = new QueryOptions(Skip: skip, Take: pageSize);

		var items = await _projectionStore
			.QueryAsync(filters, options, cancellationToken)
			.ConfigureAwait(false);

		var totalCount = await _projectionStore
			.CountAsync(filters, cancellationToken)
			.ConfigureAwait(false);

		var result = new PagedResult<OrderSearchProjection>
		{
			Items = items,
			TotalCount = (int)totalCount,
			Page = page,
			PageSize = pageSize
		};

		return Ok(result);
	}

	/// <summary>
	/// Gets recent orders within a date range.
	/// </summary>
	/// <param name="days">Number of days to look back.</param>
	/// <param name="limit">Maximum results to return.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Recent orders.</returns>
	[HttpGet("recent")]
	[ProducesResponseType(typeof(IEnumerable<OrderSearchProjection>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetRecent(
		[FromQuery] int days = 7,
		[FromQuery] int limit = 50,
		CancellationToken cancellationToken = default)
	{
		var fromDate = DateTime.UtcNow.AddDays(-days);

		var filters = new Dictionary<string, object> { ["orderDate:gte"] = fromDate };

		var options = new QueryOptions(Take: limit, OrderBy: "orderDate", Descending: true);

		var items = await _projectionStore
			.QueryAsync(filters, options, cancellationToken)
			.ConfigureAwait(false);

		return Ok(items);
	}

	// ========================================================================
	// Native Elasticsearch Queries (via OrderFullTextSearchRepository)
	// ========================================================================
	// The endpoints below use ElasticRepositoryBase<T> for capabilities that
	// IProjectionStore<T>'s dictionary filters cannot express: full-text search
	// with relevance scoring, fuzzy matching, and server-side aggregations.
	//
	// Both query paths (IProjectionStore + ElasticRepositoryBase) target the
	// SAME index, resolved via ElasticSearchProjectionIndexConvention.
	// ========================================================================

	/// <summary>
	/// Full-text search across orders using native Elasticsearch queries.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Searches across customer name (boosted 3x), status (boosted 2x),
	/// product names, tags, and external order IDs using Elasticsearch's
	/// <c>multi_match</c> query with fuzzy matching.
	/// </para>
	/// <para>
	/// This endpoint uses <see cref="OrderFullTextSearchRepository"/> (native ES)
	/// instead of <c>IProjectionStore&lt;T&gt;</c> because dictionary-based filters
	/// cannot express full-text search with relevance scoring and field boosting.
	/// </para>
	/// </remarks>
	/// <param name="q">The search text.</param>
	/// <param name="limit">Maximum results to return.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Orders ranked by relevance.</returns>
	[HttpGet("search")]
	[ProducesResponseType(typeof(IEnumerable<OrderSearchProjection>), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> FullTextSearch(
		[FromQuery] string q,
		[FromQuery] int limit = 20,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(q))
		{
			return BadRequest(new { Message = "Search query 'q' is required" });
		}

		var response = await _searchRepository
			.FullTextSearchAsync(q, limit, cancellationToken)
			.ConfigureAwait(false);

		if (!response.IsValidResponse)
		{
			_logger.LogWarning("Elasticsearch full-text search failed: {Error}", response.DebugInformation);
			return Ok(Array.Empty<OrderSearchProjection>());
		}

		_logger.LogDebug(
			"Full-text search for '{Query}' returned {Count} results in {ElapsedMs}ms",
			q,
			response.Documents.Count,
			response.Took);

		return Ok(response.Documents);
	}

	/// <summary>
	/// Advanced search combining full-text queries with structured filters.
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
	/// This is the recommended pattern when consumers need both search and filtering.
	/// Results are sorted by relevance score first, then by date.
	/// </para>
	/// </remarks>
	/// <param name="q">Optional full-text search query.</param>
	/// <param name="status">Optional status filter.</param>
	/// <param name="minAmount">Optional minimum order amount.</param>
	/// <param name="maxAmount">Optional maximum order amount.</param>
	/// <param name="page">Page number.</param>
	/// <param name="pageSize">Page size.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Orders matching the combined search criteria.</returns>
	[HttpGet("search/advanced")]
	[ProducesResponseType(typeof(IEnumerable<OrderSearchProjection>), StatusCodes.Status200OK)]
	public async Task<IActionResult> AdvancedSearch(
		[FromQuery] string? q = null,
		[FromQuery] string? status = null,
		[FromQuery] decimal? minAmount = null,
		[FromQuery] decimal? maxAmount = null,
		[FromQuery] int page = 1,
		[FromQuery] int pageSize = 20,
		CancellationToken cancellationToken = default)
	{
		var skip = (page - 1) * pageSize;

		var response = await _searchRepository
			.AdvancedSearchAsync(q, status, minAmount, maxAmount, skip, pageSize, cancellationToken)
			.ConfigureAwait(false);

		if (!response.IsValidResponse)
		{
			_logger.LogWarning("Elasticsearch advanced search failed: {Error}", response.DebugInformation);
			return Ok(Array.Empty<OrderSearchProjection>());
		}

		_logger.LogDebug(
			"Advanced search returned {Count} of {Total} results",
			response.Documents.Count,
			response.Total);

		return Ok(new PagedResult<OrderSearchProjection>
		{
			Items = response.Documents.ToList(),
			TotalCount = (int)response.Total,
			Page = page,
			PageSize = pageSize
		});
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
	/// <para>
	/// Aggregations are a native Elasticsearch feature with no equivalent in
	/// <c>IProjectionStore&lt;T&gt;</c>. This endpoint demonstrates when to
	/// "graduate" from the generic interface to native queries.
	/// </para>
	/// </remarks>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Aggregated order statistics.</returns>
	[HttpGet("statistics")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	public async Task<IActionResult> GetStatistics(CancellationToken cancellationToken)
	{
		var response = await _searchRepository
			.GetOrderStatisticsAsync(cancellationToken)
			.ConfigureAwait(false);

		if (!response.IsValidResponse)
		{
			_logger.LogWarning("Elasticsearch aggregation query failed: {Error}", response.DebugInformation);
			return Ok(new { Error = "Statistics unavailable" });
		}

		// Extract aggregation results into a consumer-friendly shape
		var result = new
		{
			TotalRevenue = response.Aggregations?.GetSum("total_revenue")?.Value ?? 0d,
			AverageOrderValue = response.Aggregations?.GetAverage("avg_order_value")?.Value ?? 0d,
			ByStatus = response.Aggregations?.GetStringTerms("by_status")?.Buckets
				.Select(b => new { Status = b.Key.Value?.ToString(), Count = b.DocCount })
				.ToList(),
			MonthlyTrend = response.Aggregations?.GetDateHistogram("orders_over_time")?.Buckets
				.Select(b => new { Month = b.KeyAsString, Count = b.DocCount })
				.ToList()
		};

		return Ok(result);
	}
}
