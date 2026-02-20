// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using CdcEventStoreElasticsearch.Api.Models;
using CdcEventStoreElasticsearch.Projections;

using Excalibur.EventSourcing.Abstractions;

using Microsoft.AspNetCore.Mvc;

namespace CdcEventStoreElasticsearch.Api.Controllers;

/// <summary>
/// API controller for order search and retrieval.
/// Queries the Elasticsearch OrderSearchProjection.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class OrdersController : ControllerBase
{
	private readonly IProjectionStore<OrderSearchProjection> _projectionStore;
	private readonly ILogger<OrdersController> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="OrdersController"/> class.
	/// </summary>
	public OrdersController(
		IProjectionStore<OrderSearchProjection> projectionStore,
		ILogger<OrdersController> logger)
	{
		_projectionStore = projectionStore;
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
}
