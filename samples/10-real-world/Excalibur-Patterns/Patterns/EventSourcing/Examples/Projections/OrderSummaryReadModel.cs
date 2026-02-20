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
///     Read model for order summary projection.
/// </summary>
public class OrderSummaryReadModel
{
	/// <summary>
	///     Gets or sets the order ID.
	/// </summary>
	public string OrderId { get; set; } = string.Empty;

	/// <summary>
	///     Gets or sets the customer ID.
	/// </summary>
	public string CustomerId { get; set; } = string.Empty;

	/// <summary>
	///     Gets or sets the order status.
	/// </summary>
	public string Status { get; set; } = "Unknown";

	/// <summary>
	///     Gets or sets the total amount.
	/// </summary>
	public decimal TotalAmount { get; set; }

	/// <summary>
	///     Gets or sets the items in the order.
	/// </summary>
	public List<OrderItemSummary> Items { get; set; } = new();

	/// <summary>
	///     Gets or sets the order creation date.
	/// </summary>
	public DateTime CreatedAt { get; set; }

	/// <summary>
	///     Gets or sets the last update date.
	/// </summary>
	public DateTime LastUpdated { get; set; }

	/// <summary>
	///     Gets or sets the shipping address.
	/// </summary>
	public string ShippingAddress { get; set; } = string.Empty;

	/// <summary>
	///     Gets or sets the payment method.
	/// </summary>
	public string PaymentMethod { get; set; } = string.Empty;

	/// <summary>
	///     Gets the item count.
	/// </summary>
	public int ItemCount => Items.Sum(i => i.Quantity);
}
