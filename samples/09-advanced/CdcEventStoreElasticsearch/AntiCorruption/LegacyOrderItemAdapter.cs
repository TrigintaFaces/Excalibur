// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Data.SqlServer.Cdc;

namespace CdcEventStoreElasticsearch.AntiCorruption;

/// <summary>
/// Adapts legacy order item schema to domain model.
/// Handles schema evolution (column renames, type changes) as an Anti-Corruption Layer.
/// </summary>
/// <remarks>
/// <para>
/// The legacy database has evolved over time with different column naming conventions:
/// </para>
/// <list type="bullet">
/// <item>V1 (2015): LineNum, ProdName, Qty, Price</item>
/// <item>V2 (2020): ExternalItemId, ProductName, Quantity, UnitPrice</item>
/// </list>
/// <para>
/// This adapter normalizes both schemas to a consistent domain model.
/// </para>
/// </remarks>
public sealed class LegacyOrderItemAdapter
{
	/// <summary>
	/// Adapts a CDC data change event to a normalized order item model.
	/// </summary>
	/// <param name="changeEvent">The CDC change event from the legacy database.</param>
	/// <returns>The adapted order item data, or null if the event cannot be adapted.</returns>
	public AdaptedOrderItemData? Adapt(DataChangeEvent changeEvent)
	{
		ArgumentNullException.ThrowIfNull(changeEvent);

		if (changeEvent.TableName != "LegacyOrderItems")
		{
			return null;
		}

		var data = new AdaptedOrderItemData
		{
			ChangeType = changeEvent.ChangeType
		};

		foreach (var change in changeEvent.Changes)
		{
			// Normalize column names from different schema versions
			var normalizedColumn = NormalizeColumnName(change.ColumnName);
			var value = changeEvent.ChangeType == DataChangeType.Delete
				? change.OldValue
				: change.NewValue;

			switch (normalizedColumn)
			{
				case "ExternalItemId":
					data.ExternalItemId = value?.ToString();
					break;
				case "ExternalOrderId":
					data.ExternalOrderId = value?.ToString();
					break;
				case "ProductName":
					data.ProductName = value?.ToString();
					break;
				case "Quantity":
					if (int.TryParse(value?.ToString(), out var qty))
					{
						data.Quantity = qty;
					}
					break;
				case "UnitPrice":
					if (decimal.TryParse(value?.ToString(), out var price))
					{
						data.UnitPrice = price;
					}
					break;
			}

			// Track old values for update detection
			if (changeEvent.ChangeType == DataChangeType.Update && change.OldValue is not null)
			{
				switch (normalizedColumn)
				{
					case "Quantity":
						if (int.TryParse(change.OldValue.ToString(), out var oldQty))
						{
							data.PreviousQuantity = oldQty;
						}
						break;
					case "UnitPrice":
						if (decimal.TryParse(change.OldValue.ToString(), out var oldPrice))
						{
							data.PreviousUnitPrice = oldPrice;
						}
						break;
				}
			}
		}

		// Validate required fields
		if (string.IsNullOrWhiteSpace(data.ExternalItemId) ||
			string.IsNullOrWhiteSpace(data.ExternalOrderId))
		{
			return null;
		}

		return data;
	}

	/// <summary>
	/// Normalizes column names from different schema versions.
	/// </summary>
	private static string NormalizeColumnName(string columnName) => columnName switch
	{
		// V1 schema (2015)
		"LineNum" => "ExternalItemId",
		"OrderNum" => "ExternalOrderId",
		"ProdName" => "ProductName",
		"Qty" => "Quantity",
		"Price" => "UnitPrice",

		// V2 schema (2020) - current
		_ => columnName
	};
}

/// <summary>
/// Normalized order item data from the legacy system.
/// </summary>
public sealed class AdaptedOrderItemData
{
	/// <summary>Gets or sets the type of change (Insert, Update, Delete).</summary>
	public DataChangeType ChangeType { get; set; }

	/// <summary>Gets or sets the external item ID from the legacy system.</summary>
	public string? ExternalItemId { get; set; }

	/// <summary>Gets or sets the external order ID this item belongs to.</summary>
	public string? ExternalOrderId { get; set; }

	/// <summary>Gets or sets the product name.</summary>
	public string? ProductName { get; set; }

	/// <summary>Gets or sets the quantity ordered.</summary>
	public int Quantity { get; set; }

	/// <summary>Gets or sets the previous quantity (for updates).</summary>
	public int? PreviousQuantity { get; set; }

	/// <summary>Gets or sets the unit price.</summary>
	public decimal UnitPrice { get; set; }

	/// <summary>Gets or sets the previous unit price (for updates).</summary>
	public decimal? PreviousUnitPrice { get; set; }
}
