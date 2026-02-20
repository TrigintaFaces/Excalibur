// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Data.SqlServer.Cdc;

namespace CdcEventStoreElasticsearch.AntiCorruption;

/// <summary>
/// Adapts legacy order schema to domain model.
/// Handles schema evolution (column renames, type changes) as an Anti-Corruption Layer.
/// </summary>
/// <remarks>
/// <para>
/// The legacy database has evolved over time with different column naming conventions:
/// </para>
/// <list type="bullet">
/// <item>V1 (2015): OrderNum, CustId, OrderDt, OrderAmt</item>
/// <item>V2 (2020): ExternalOrderId, CustomerExternalId, OrderDate, TotalAmount</item>
/// </list>
/// <para>
/// This adapter normalizes both schemas to a consistent domain model.
/// </para>
/// </remarks>
public sealed class LegacyOrderAdapter
{
	/// <summary>
	/// Adapts a CDC data change event to a normalized order model.
	/// </summary>
	/// <param name="changeEvent">The CDC change event from the legacy database.</param>
	/// <returns>The adapted order data, or null if the event cannot be adapted.</returns>
	public AdaptedOrderData? Adapt(DataChangeEvent changeEvent)
	{
		ArgumentNullException.ThrowIfNull(changeEvent);

		if (changeEvent.TableName != "LegacyOrders")
		{
			return null;
		}

		var data = new AdaptedOrderData
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
				case "ExternalOrderId":
					data.ExternalOrderId = value?.ToString();
					break;
				case "CustomerExternalId":
					data.CustomerExternalId = value?.ToString();
					break;
				case "OrderDate":
					if (DateTime.TryParse(value?.ToString(), out var orderDate))
					{
						data.OrderDate = orderDate;
					}
					break;
				case "Status":
					data.Status = value?.ToString();
					break;
				case "ShippedDate":
					if (DateTime.TryParse(value?.ToString(), out var shippedDate))
					{
						data.ShippedDate = shippedDate;
					}
					break;
				case "DeliveredDate":
					if (DateTime.TryParse(value?.ToString(), out var deliveredDate))
					{
						data.DeliveredDate = deliveredDate;
					}
					break;
			}

			// Track old values for update detection
			if (changeEvent.ChangeType == DataChangeType.Update && change.OldValue is not null)
			{
				switch (normalizedColumn)
				{
					case "Status":
						data.PreviousStatus = change.OldValue.ToString();
						break;
				}
			}
		}

		// Validate required fields
		if (string.IsNullOrWhiteSpace(data.ExternalOrderId))
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
		"OrderNum" => "ExternalOrderId",
		"CustId" => "CustomerExternalId",
		"OrderDt" => "OrderDate",
		"OrderAmt" => "TotalAmount",
		"ShipDt" => "ShippedDate",
		"DelivDt" => "DeliveredDate",

		// V2 schema (2020) - current
		_ => columnName
	};
}

/// <summary>
/// Normalized order data from the legacy system.
/// </summary>
public sealed class AdaptedOrderData
{
	/// <summary>Gets or sets the type of change (Insert, Update, Delete).</summary>
	public DataChangeType ChangeType { get; set; }

	/// <summary>Gets or sets the external order ID from the legacy system.</summary>
	public string? ExternalOrderId { get; set; }

	/// <summary>Gets or sets the customer's external ID from the legacy system.</summary>
	public string? CustomerExternalId { get; set; }

	/// <summary>Gets or sets the order date.</summary>
	public DateTime? OrderDate { get; set; }

	/// <summary>Gets or sets the order status.</summary>
	public string? Status { get; set; }

	/// <summary>Gets or sets the previous status (for updates).</summary>
	public string? PreviousStatus { get; set; }

	/// <summary>Gets or sets the shipped date.</summary>
	public DateTime? ShippedDate { get; set; }

	/// <summary>Gets or sets the delivered date.</summary>
	public DateTime? DeliveredDate { get; set; }
}
