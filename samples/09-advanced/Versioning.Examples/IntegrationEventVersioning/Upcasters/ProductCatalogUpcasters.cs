// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// Licensed under multiple licenses - see LICENSE files in project root.

using Excalibur.Dispatch.Abstractions;

using IntegrationEventVersioning.Events;

namespace IntegrationEventVersioning.Upcasters;

/// <summary>
/// Upcasts ProductPriceChangedV1 to V2: Adds TenantId for multi-tenant support.
/// </summary>
/// <remarks>
/// <para>
/// <b>Migration Strategy:</b> Legacy single-tenant events are assigned to the
/// "default" tenant. This enables:
/// </para>
/// <list type="bullet">
///   <item>Backward compatibility with single-tenant deployments</item>
///   <item>Clear identification of pre-multi-tenant data</item>
///   <item>Gradual migration of products to specific tenants</item>
/// </list>
/// <para>
/// <b>Cross-service impact:</b> Downstream services that haven't upgraded yet
/// simply ignore the TenantId field (they use the default database partition).
/// Upgraded services use TenantId for proper data isolation.
/// </para>
/// </remarks>
public sealed class ProductPriceChangedV1ToV2Upcaster
	: IMessageUpcaster<ProductPriceChangedV1, ProductPriceChangedV2>
{
	/// <summary>
	/// The default tenant ID assigned to legacy single-tenant events.
	/// </summary>
	public const string DefaultTenantId = "default";

	/// <inheritdoc/>
	public int FromVersion => 1;

	/// <inheritdoc/>
	public int ToVersion => 2;

	/// <inheritdoc/>
	public ProductPriceChangedV2 Upcast(ProductPriceChangedV1 oldMessage)
	{
		return new ProductPriceChangedV2
		{
			TenantId = DefaultTenantId,
			ProductId = oldMessage.ProductId,
			NewPrice = oldMessage.NewPrice
		};
	}
}

/// <summary>
/// Upcasts ProductPriceChangedV2 to V3: Adds currency and price history.
/// </summary>
/// <remarks>
/// <para>
/// <b>Migration Strategy:</b>
/// </para>
/// <list type="bullet">
///   <item>Currency defaults to "USD" (original market)</item>
///   <item>OldPrice set to NewPrice (no historical data available)</item>
/// </list>
/// <para>
/// <b>Analytics impact:</b> Setting OldPrice = NewPrice means the
/// ChangePercentage will be 0% for upcasted events. This is intentional:
/// </para>
/// <list type="bullet">
///   <item>Prevents false price change notifications</item>
///   <item>Analytics can filter by "OldPrice == NewPrice" to exclude legacy data</item>
///   <item>Downstream services won't trigger "price drop" alerts for old events</item>
/// </list>
/// </remarks>
public sealed class ProductPriceChangedV2ToV3Upcaster
	: IMessageUpcaster<ProductPriceChangedV2, ProductPriceChangedV3>
{
	/// <summary>
	/// The default currency for legacy events.
	/// </summary>
	public const string DefaultCurrency = "USD";

	/// <inheritdoc/>
	public int FromVersion => 2;

	/// <inheritdoc/>
	public int ToVersion => 3;

	/// <inheritdoc/>
	public ProductPriceChangedV3 Upcast(ProductPriceChangedV2 oldMessage)
	{
		return new ProductPriceChangedV3
		{
			TenantId = oldMessage.TenantId,
			ProductId = oldMessage.ProductId,
			OldPrice = oldMessage.NewPrice, // No history = OldPrice equals NewPrice
			NewPrice = oldMessage.NewPrice,
			Currency = DefaultCurrency
		};
	}
}
