// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
//
// Licensed under multiple licenses:
// - Excalibur License 1.0 (see LICENSE-EXCALIBUR.txt)
// - GNU Affero General Public License v3.0 or later (AGPL-3.0) (see LICENSE-AGPL-3.0.txt)
// - Server Side Public License v1.0 (SSPL-1.0) (see LICENSE-SSPL-1.0.txt)
// - Apache License 2.0 (see LICENSE-APACHE-2.0.txt)
//
// You may not use this file except in compliance with the License terms above. You may obtain copies of the licenses in the project root or online.
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.

namespace examples.Excalibur.Patterns.MaterializedViews.Examples;

/// <summary>
///     Sales analytics view that aggregates sales data by various dimensions.
/// </summary>
public class SalesAnalyticsView
{
	/// <summary>
	///     Gets or sets the total revenue.
	/// </summary>
	public decimal TotalRevenue { get; set; }

	/// <summary>
	///     Gets or sets the total number of orders.
	/// </summary>
	public int TotalOrders { get; set; }

	/// <summary>
	///     Gets or sets the average order value.
	/// </summary>
	public decimal AverageOrderValue => TotalOrders > 0 ? TotalRevenue / TotalOrders : 0;

	/// <summary>
	///     Gets or sets the revenue by product category.
	/// </summary>
	public Dictionary<string, decimal> RevenueByCategory { get; set; } = new();

	/// <summary>
	///     Gets or sets the revenue by region.
	/// </summary>
	public Dictionary<string, decimal> RevenueByRegion { get; set; } = new();

	/// <summary>
	///     Gets or sets the revenue by month.
	/// </summary>
	public Dictionary<string, decimal> RevenueByMonth { get; set; } = new();

	/// <summary>
	///     Gets or sets the top products by revenue.
	/// </summary>
	public List<ProductRevenue> TopProducts { get; set; } = new();

	/// <summary>
	///     Gets or sets the top customers by spending.
	/// </summary>
	public List<CustomerSpending> TopCustomers { get; set; } = new();

	/// <summary>
	///     Gets or sets daily sales trends.
	/// </summary>
	public List<DailySalesTrend> DailyTrends { get; set; } = new();

	/// <summary>
	///     Gets or sets the last update timestamp.
	/// </summary>
	public DateTime LastUpdated { get; set; }

	/// <summary>
	///     Gets or sets the data period start.
	/// </summary>
	public DateTime PeriodStart { get; set; }

	/// <summary>
	///     Gets or sets the data period end.
	/// </summary>
	public DateTime PeriodEnd { get; set; }
}
