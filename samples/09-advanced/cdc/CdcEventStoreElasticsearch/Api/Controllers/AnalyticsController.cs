// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using CdcEventStoreElasticsearch.Projections;

using Excalibur.EventSourcing.Abstractions;

using Microsoft.AspNetCore.Mvc;

namespace CdcEventStoreElasticsearch.Api.Controllers;

/// <summary>
/// API controller for analytics and aggregated data.
/// Queries the Elasticsearch analytics projections.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class AnalyticsController : ControllerBase
{
	private readonly IProjectionStore<CustomerTierSummaryProjection> _tierSummaryStore;
	private readonly IProjectionStore<OrderAnalyticsProjection> _orderAnalyticsStore;
	private readonly IProjectionStore<DailyOrderSummaryProjection> _dailySummaryStore;
	private readonly ILogger<AnalyticsController> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="AnalyticsController"/> class.
	/// </summary>
	public AnalyticsController(
		IProjectionStore<CustomerTierSummaryProjection> tierSummaryStore,
		IProjectionStore<OrderAnalyticsProjection> orderAnalyticsStore,
		IProjectionStore<DailyOrderSummaryProjection> dailySummaryStore,
		ILogger<AnalyticsController> logger)
	{
		_tierSummaryStore = tierSummaryStore;
		_orderAnalyticsStore = orderAnalyticsStore;
		_dailySummaryStore = dailySummaryStore;
		_logger = logger;
	}

	/// <summary>
	/// Gets customer tier summaries.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Customer tier summary projections.</returns>
	[HttpGet("customer-tiers")]
	[ProducesResponseType(typeof(IEnumerable<CustomerTierSummaryProjection>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetCustomerTiers(CancellationToken cancellationToken)
	{
		var options = new QueryOptions(Take: 10);

		var items = await _tierSummaryStore
			.QueryAsync(null, options, cancellationToken)
			.ConfigureAwait(false);

		_logger.LogDebug("Retrieved {Count} customer tier summaries", items.Count);

		return Ok(items);
	}

	/// <summary>
	/// Gets a specific customer tier summary.
	/// </summary>
	/// <param name="tier">The tier name (Bronze, Silver, Gold, Platinum).</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The tier summary if found.</returns>
	[HttpGet("customer-tiers/{tier}")]
	[ProducesResponseType(typeof(CustomerTierSummaryProjection), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetCustomerTier(string tier, CancellationToken cancellationToken)
	{
		var id = tier.ToUpperInvariant();
		var summary = await _tierSummaryStore.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);

		if (summary is null)
		{
			return NotFound(new { Message = $"Tier summary for '{tier}' not found" });
		}

		return Ok(summary);
	}

	/// <summary>
	/// Gets global order analytics.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Global order analytics projection.</returns>
	[HttpGet("order-analytics")]
	[ProducesResponseType(typeof(OrderAnalyticsProjection), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetOrderAnalytics(CancellationToken cancellationToken)
	{
		var analytics = await _orderAnalyticsStore
			.GetByIdAsync("global", cancellationToken)
			.ConfigureAwait(false);

		if (analytics is null)
		{
			// Return empty analytics if not yet populated
			return Ok(new OrderAnalyticsProjection { Id = "global" });
		}

		return Ok(analytics);
	}

	/// <summary>
	/// Gets daily order summaries for a date range.
	/// </summary>
	/// <param name="days">Number of days to retrieve (default: 30).</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Daily order summaries.</returns>
	[HttpGet("daily-orders")]
	[ProducesResponseType(typeof(IEnumerable<DailyOrderSummaryProjection>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetDailyOrders(
		[FromQuery] int days = 30,
		CancellationToken cancellationToken = default)
	{
		var fromDate = DateTime.UtcNow.Date.AddDays(-days);

		var filters = new Dictionary<string, object> { ["date:gte"] = fromDate };

		var options = new QueryOptions(Take: days + 1, OrderBy: "date");

		var items = await _dailySummaryStore
			.QueryAsync(filters, options, cancellationToken)
			.ConfigureAwait(false);

		_logger.LogDebug("Retrieved {Count} daily order summaries for last {Days} days", items.Count, days);

		return Ok(items);
	}

	/// <summary>
	/// Gets order summary for a specific date.
	/// </summary>
	/// <param name="date">The date (format: yyyy-MM-dd).</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Daily order summary for the date.</returns>
	[HttpGet("daily-orders/{date}")]
	[ProducesResponseType(typeof(DailyOrderSummaryProjection), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetDailyOrderSummary(string date, CancellationToken cancellationToken)
	{
		var summary = await _dailySummaryStore.GetByIdAsync(date, cancellationToken).ConfigureAwait(false);

		if (summary is null)
		{
			return NotFound(new { Message = $"Daily summary for '{date}' not found" });
		}

		return Ok(summary);
	}

	/// <summary>
	/// Gets a combined dashboard view with key metrics.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Combined dashboard data.</returns>
	[HttpGet("dashboard")]
	[ProducesResponseType(typeof(DashboardResponse), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetDashboard(CancellationToken cancellationToken)
	{
		// Fetch all analytics in parallel
		var orderAnalyticsTask = _orderAnalyticsStore.GetByIdAsync("global", cancellationToken);

		var tierOptions = new QueryOptions(Take: 10);
		var tierSummaryTask = _tierSummaryStore.QueryAsync(null, tierOptions, cancellationToken);

		var dailyFilters = new Dictionary<string, object> { ["date:gte"] = DateTime.UtcNow.Date.AddDays(-7) };
		var dailyOptions = new QueryOptions(Take: 8, OrderBy: "date", Descending: true);
		var recentDailyTask = _dailySummaryStore.QueryAsync(dailyFilters, dailyOptions, cancellationToken);

		await Task.WhenAll(orderAnalyticsTask, tierSummaryTask, recentDailyTask).ConfigureAwait(false);

		var orderAnalytics = await orderAnalyticsTask.ConfigureAwait(false);
		var tierSummaries = await tierSummaryTask.ConfigureAwait(false);
		var dailySummaries = await recentDailyTask.ConfigureAwait(false);

		var dashboard = new DashboardResponse
		{
			TotalOrders = orderAnalytics?.TotalOrders ?? 0,
			TotalRevenue = orderAnalytics?.TotalRevenue ?? 0,
			AverageOrderValue = orderAnalytics?.AverageOrderValue ?? 0,
			OrdersByStatus = orderAnalytics?.OrdersByStatus ?? [],
			TopProducts = orderAnalytics?.TopProducts ?? [],
			CustomerTierSummaries = tierSummaries.ToList(),
			RecentDailySummaries = dailySummaries.OrderByDescending(x => x.Date).ToList(),
			GeneratedAt = DateTime.UtcNow
		};

		return Ok(dashboard);
	}
}

/// <summary>
/// Combined dashboard response.
/// </summary>
public sealed class DashboardResponse
{
	/// <summary>Gets or sets the total number of orders.</summary>
	public int TotalOrders { get; set; }

	/// <summary>Gets or sets the total revenue.</summary>
	public decimal TotalRevenue { get; set; }

	/// <summary>Gets or sets the average order value.</summary>
	public decimal AverageOrderValue { get; set; }

	/// <summary>Gets or sets orders by status.</summary>
	public Dictionary<string, int> OrdersByStatus { get; set; } = [];

	/// <summary>Gets or sets the top selling products.</summary>
	public List<TopProductProjection> TopProducts { get; set; } = [];

	/// <summary>Gets or sets customer tier summaries.</summary>
	public List<CustomerTierSummaryProjection> CustomerTierSummaries { get; set; } = [];

	/// <summary>Gets or sets recent daily summaries.</summary>
	public List<DailyOrderSummaryProjection> RecentDailySummaries { get; set; } = [];

	/// <summary>Gets or sets when the dashboard was generated.</summary>
	public DateTime GeneratedAt { get; set; }
}
