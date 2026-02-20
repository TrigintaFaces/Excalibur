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

namespace examples.Excalibur.Patterns.EventSourcing.Examples.Projections;

/// <summary>
///     Read model for customer activity projection.
/// </summary>
public class CustomerActivityReadModel
{
	/// <summary>
	///     Gets or sets the customer ID.
	/// </summary>
	public string CustomerId { get; set; } = string.Empty;

	/// <summary>
	///     Gets or sets the customer name.
	/// </summary>
	public string CustomerName { get; set; } = string.Empty;

	/// <summary>
	///     Gets or sets the total order count.
	/// </summary>
	public int TotalOrders { get; set; }

	/// <summary>
	///     Gets or sets the total amount spent.
	/// </summary>
	public decimal TotalSpent { get; set; }

	/// <summary>
	///     Gets or sets the average order value.
	/// </summary>
	public decimal AverageOrderValue => TotalOrders > 0 ? TotalSpent / TotalOrders : 0;

	/// <summary>
	///     Gets or sets the last order date.
	/// </summary>
	public DateTime? LastOrderDate { get; set; }

	/// <summary>
	///     Gets or sets the first order date.
	/// </summary>
	public DateTime? FirstOrderDate { get; set; }

	/// <summary>
	///     Gets or sets the customer lifetime in days.
	/// </summary>
	public int CustomerLifetimeDays => FirstOrderDate.HasValue
		? (DateTime.UtcNow - FirstOrderDate.Value).Days
		: 0;

	/// <summary>
	///     Gets or sets the recent orders.
	/// </summary>
	public List<RecentOrderInfo> RecentOrders { get; set; } = new();

	/// <summary>
	///     Gets or sets the favorite products.
	/// </summary>
	public Dictionary<string, int> FavoriteProducts { get; set; } = new();

	/// <summary>
	///     Gets or sets the cancelled order count.
	/// </summary>
	public int CancelledOrders { get; set; }

	/// <summary>
	///     Gets the cancellation rate.
	/// </summary>
	public decimal CancellationRate => TotalOrders > 0
		? (decimal)CancelledOrders / TotalOrders * 100
		: 0;
}
