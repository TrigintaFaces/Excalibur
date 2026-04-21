// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// Licensed under multiple licenses - see LICENSE files in project root.

using EcommerceOrderVersioning.Events;

using Excalibur.Dispatch.Abstractions;

namespace EcommerceOrderVersioning.Upcasters;

/// <summary>
/// Upcasts OrderPlacedEventV1 to V2: Adds CustomerId.
/// </summary>
/// <remarks>
/// <para>
/// <b>Migration Strategy:</b> Legacy orders (before customer tracking)
/// are assigned an empty CustomerId. This allows:
/// </para>
/// <list type="bullet">
///   <item>Distinguishing legacy orders from known customers</item>
///   <item>Running analytics that filters out pre-customer-program orders</item>
///   <item>Maintaining referential integrity (no null foreign keys)</item>
/// </list>
/// </remarks>
public sealed class OrderPlacedV1ToV2Upcaster : IMessageUpcaster<OrderPlacedEventV1, OrderPlacedEventV2>
{
	/// <inheritdoc/>
	public int FromVersion => 1;

	/// <inheritdoc/>
	public int ToVersion => 2;

	/// <inheritdoc/>
	public OrderPlacedEventV2 Upcast(OrderPlacedEventV1 oldMessage)
	{
		return new OrderPlacedEventV2
		{
			OrderId = oldMessage.OrderId,
			CustomerId = Guid.Empty, // Legacy orders have no customer attribution
			Total = oldMessage.Total
		};
	}
}

/// <summary>
/// Upcasts OrderPlacedEventV2 to V3: Splits Total into Subtotal + Tax.
/// </summary>
/// <remarks>
/// <para>
/// <b>Migration Strategy:</b> Historical orders cannot reliably reconstruct
/// their original tax amounts, so we set:
/// </para>
/// <list type="bullet">
///   <item>Subtotal = original Total (treat entire amount as product cost)</item>
///   <item>Tax = 0 (cannot determine historical tax)</item>
/// </list>
/// <para>
/// <b>Alternative approaches considered:</b>
/// </para>
/// <list type="bullet">
///   <item>Apply estimated tax rate - rejected: would create false accounting data</item>
///   <item>Leave Tax as null - rejected: complicates downstream processing</item>
///   <item>Store "TaxUnknown" flag - rejected: unnecessary complexity</item>
/// </list>
/// <para>
/// The chosen approach is transparent: legacy orders have Tax=0, which
/// can be queried and filtered appropriately in reports.
/// </para>
/// </remarks>
public sealed class OrderPlacedV2ToV3Upcaster : IMessageUpcaster<OrderPlacedEventV2, OrderPlacedEventV3>
{
	/// <inheritdoc/>
	public int FromVersion => 2;

	/// <inheritdoc/>
	public int ToVersion => 3;

	/// <inheritdoc/>
	public OrderPlacedEventV3 Upcast(OrderPlacedEventV2 oldMessage)
	{
		return new OrderPlacedEventV3
		{
			OrderId = oldMessage.OrderId,
			CustomerId = oldMessage.CustomerId,
			Subtotal = oldMessage.Total, // Entire total becomes subtotal
			Tax = 0m                      // Historical tax unknown
		};
	}
}
