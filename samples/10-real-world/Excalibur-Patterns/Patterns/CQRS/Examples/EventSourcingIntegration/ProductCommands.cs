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

using Excalibur.Dispatch.Patterns.CQRS.CQRS.Commands;

namespace examples.Excalibur.Patterns.CQRS.Examples.EventSourcingIntegration;

/// <summary>
///     Commands for product catalog management.
/// </summary>
public static class ProductCommands
{
	/// <summary>
	///     Command to create a new product.
	/// </summary>
	public class CreateProduct : CommandBase
	{
		/// <summary>
		///     Gets the product ID.
		/// </summary>
		public string ProductId { get; init; } = string.Empty;

		/// <summary>
		///     Gets the product name.
		/// </summary>
		public string Name { get; init; } = string.Empty;

		/// <summary>
		///     Gets the product description.
		/// </summary>
		public string Description { get; init; } = string.Empty;

		/// <summary>
		///     Gets the product price.
		/// </summary>
		public decimal Price { get; init; }

		/// <summary>
		///     Gets the initial stock quantity.
		/// </summary>
		public int StockQuantity { get; init; }
	}

	/// <summary>
	///     Command to update product price.
	/// </summary>
	public class UpdateProductPrice : CommandBase
	{
		/// <summary>
		///     Gets the product ID.
		/// </summary>
		public string ProductId { get; init; } = string.Empty;

		/// <summary>
		///     Gets the new price.
		/// </summary>
		public decimal NewPrice { get; init; }
	}

	/// <summary>
	///     Command to adjust product stock.
	/// </summary>
	public class AdjustProductStock : CommandBase
	{
		/// <summary>
		///     Gets the product ID.
		/// </summary>
		public string ProductId { get; init; } = string.Empty;

		/// <summary>
		///     Gets the quantity adjustment (positive or negative).
		/// </summary>
		public int QuantityAdjustment { get; init; }

		/// <summary>
		///     Gets the reason for adjustment.
		/// </summary>
		public string Reason { get; init; } = string.Empty;
	}
}
