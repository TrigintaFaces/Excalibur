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

namespace examples.Excalibur.Patterns.CQRS.Examples.EventSourcingIntegration;

/// <summary>
///     Read model for product catalog projection.
/// </summary>
public class ProductCatalogReadModel
{
	/// <summary>
	///     Gets or sets the product ID.
	/// </summary>
	public string ProductId { get; set; } = string.Empty;

	/// <summary>
	///     Gets or sets the product name.
	/// </summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>
	///     Gets or sets the product description.
	/// </summary>
	public string Description { get; set; } = string.Empty;

	/// <summary>
	///     Gets or sets the current price.
	/// </summary>
	public decimal CurrentPrice { get; set; }

	/// <summary>
	///     Gets or sets the stock quantity.
	/// </summary>
	public int StockQuantity { get; set; }

	/// <summary>
	///     Gets or sets whether the product is active.
	/// </summary>
	public bool IsActive { get; set; }

	/// <summary>
	///     Gets or sets the price history.
	/// </summary>
	public List<PriceChange> PriceHistory { get; set; } = new();

	/// <summary>
	///     Gets or sets the stock adjustments.
	/// </summary>
	public List<StockAdjustment> StockAdjustments { get; set; } = new();

	/// <summary>
	///     Gets or sets the creation date.
	/// </summary>
	public DateTime CreatedAt { get; set; }

	/// <summary>
	///     Gets or sets the last update date.
	/// </summary>
	public DateTime LastUpdated { get; set; }

	/// <summary>
	///     Gets the availability status.
	/// </summary>
	public string AvailabilityStatus =>
		!IsActive ? "Discontinued" :
		StockQuantity == 0 ? "Out of Stock" :
		StockQuantity < 10 ? "Low Stock" :
		"In Stock";
}
