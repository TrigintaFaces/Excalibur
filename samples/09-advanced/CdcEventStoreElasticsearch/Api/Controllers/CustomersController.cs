// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using CdcEventStoreElasticsearch.Api.Models;
using CdcEventStoreElasticsearch.Projections;

using Excalibur.EventSourcing.Abstractions;

using Microsoft.AspNetCore.Mvc;

namespace CdcEventStoreElasticsearch.Api.Controllers;

/// <summary>
/// API controller for customer search and retrieval.
/// Queries the Elasticsearch CustomerSearchProjection.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class CustomersController : ControllerBase
{
	private readonly IProjectionStore<CustomerSearchProjection> _projectionStore;
	private readonly ILogger<CustomersController> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="CustomersController"/> class.
	/// </summary>
	public CustomersController(
		IProjectionStore<CustomerSearchProjection> projectionStore,
		ILogger<CustomersController> logger)
	{
		_projectionStore = projectionStore;
		_logger = logger;
	}

	/// <summary>
	/// Gets a customer by ID.
	/// </summary>
	/// <param name="id">The customer ID.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The customer projection if found.</returns>
	[HttpGet("{id}")]
	[ProducesResponseType(typeof(CustomerSearchProjection), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetById(string id, CancellationToken cancellationToken)
	{
		var customer = await _projectionStore.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);

		if (customer is null)
		{
			return NotFound(new { Message = $"Customer {id} not found" });
		}

		return Ok(customer);
	}

	/// <summary>
	/// Searches customers with filters.
	/// </summary>
	/// <param name="request">The search request with filters.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Paged list of matching customers.</returns>
	[HttpGet]
	[ProducesResponseType(typeof(PagedResult<CustomerSearchProjection>), StatusCodes.Status200OK)]
	public async Task<IActionResult> Search([FromQuery] CustomerSearchRequest request, CancellationToken cancellationToken)
	{
		var filters = new Dictionary<string, object>();

		// Build filters from request
		if (!string.IsNullOrWhiteSpace(request.Tier))
		{
			filters["tier"] = request.Tier;
		}

		if (request.IsActive.HasValue)
		{
			filters["isActive"] = request.IsActive.Value;
		}

		if (request.MinTotalSpent.HasValue)
		{
			filters["totalSpent:gte"] = request.MinTotalSpent.Value;
		}

		if (request.MaxTotalSpent.HasValue)
		{
			filters["totalSpent:lte"] = request.MaxTotalSpent.Value;
		}

		if (request.Tags is { Length: > 0 })
		{
			filters["tags:in"] = request.Tags;
		}

		// Full-text search
		if (!string.IsNullOrWhiteSpace(request.Query))
		{
			filters["name:contains"] = request.Query;
		}

		var skip = (request.Page - 1) * request.PageSize;
		var options = new QueryOptions(Skip: skip, Take: request.PageSize);

		var items = await _projectionStore
			.QueryAsync(filters, options, cancellationToken)
			.ConfigureAwait(false);

		var totalCount = await _projectionStore
			.CountAsync(filters, cancellationToken)
			.ConfigureAwait(false);

		var result = new PagedResult<CustomerSearchProjection>
		{
			Items = items,
			TotalCount = (int)totalCount,
			Page = request.Page,
			PageSize = request.PageSize
		};

		_logger.LogDebug(
			"Customer search returned {Count} of {Total} results",
			result.Items.Count,
			totalCount);

		return Ok(result);
	}

	/// <summary>
	/// Full-text search for customers by name or email.
	/// </summary>
	/// <param name="q">The search query.</param>
	/// <param name="limit">Maximum results to return.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Matching customers.</returns>
	[HttpGet("search")]
	[ProducesResponseType(typeof(IEnumerable<CustomerSearchProjection>), StatusCodes.Status200OK)]
	public async Task<IActionResult> FullTextSearch(
		[FromQuery] string q,
		[FromQuery] int limit = 10,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(q))
		{
			return BadRequest(new { Message = "Search query 'q' is required" });
		}

		var filters = new Dictionary<string, object> { ["name:contains"] = q };

		var options = new QueryOptions(Take: limit);

		var items = await _projectionStore
			.QueryAsync(filters, options, cancellationToken)
			.ConfigureAwait(false);

		return Ok(items);
	}

	/// <summary>
	/// Gets customers by tier.
	/// </summary>
	/// <param name="tier">The customer tier (Bronze, Silver, Gold, Platinum).</param>
	/// <param name="page">Page number.</param>
	/// <param name="pageSize">Page size.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Customers in the specified tier.</returns>
	[HttpGet("by-tier/{tier}")]
	[ProducesResponseType(typeof(PagedResult<CustomerSearchProjection>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetByTier(
		string tier,
		[FromQuery] int page = 1,
		[FromQuery] int pageSize = 20,
		CancellationToken cancellationToken = default)
	{
		var filters = new Dictionary<string, object> { ["tier"] = tier, ["isActive"] = true };

		var skip = (page - 1) * pageSize;
		var options = new QueryOptions(Skip: skip, Take: pageSize);

		var items = await _projectionStore
			.QueryAsync(filters, options, cancellationToken)
			.ConfigureAwait(false);

		var totalCount = await _projectionStore
			.CountAsync(filters, cancellationToken)
			.ConfigureAwait(false);

		var result = new PagedResult<CustomerSearchProjection>
		{
			Items = items,
			TotalCount = (int)totalCount,
			Page = page,
			PageSize = pageSize
		};

		return Ok(result);
	}
}
