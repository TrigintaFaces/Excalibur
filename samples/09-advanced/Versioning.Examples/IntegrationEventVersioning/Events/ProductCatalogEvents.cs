// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// Licensed under multiple licenses - see LICENSE files in project root.

using Excalibur.Dispatch.Abstractions;
namespace IntegrationEventVersioning.Events;

#region ProductPriceChanged Integration Event Version History
// This file demonstrates the evolution of a cross-service integration event:
//
// V1 (Original): Basic price change notification
//     - ProductId, NewPrice
//     - Consumed by: Pricing Service, Cart Service
//
// V2 (Multi-tenant): Added TenantId for multi-tenant support
//     - Migration: Default tenant "default" for legacy events
//     - Enables multi-tenant marketplace platform
//
// V3 (Currency Support): Added Currency and OldPrice for tracking
//     - Migration: Currency defaults to "USD", OldPrice defaults to NewPrice
//     - Enables price change analytics and notifications
//
// This demonstrates the challenge of integration events: multiple downstream
// services may be at different versions. The upcasting pipeline ensures
// backward compatibility when a newer service receives older messages.
#endregion

/// <summary>
/// V1: Original price change notification.
/// </summary>
/// <remarks>
/// <para>
/// This integration event was published by the Catalog Service and consumed by:
/// </para>
/// <list type="bullet">
///   <item>Pricing Service - to update price caches</item>
///   <item>Cart Service - to recalculate cart totals</item>
///   <item>Search Service - to update product indices</item>
/// </list>
/// </remarks>
public sealed record ProductPriceChangedV1 : IDispatchMessage, IVersionedMessage
{
	/// <summary>The product whose price changed.</summary>
	public Guid ProductId { get; init; }

	/// <summary>The new price.</summary>
	public decimal NewPrice { get; init; }

	/// <inheritdoc/>
	public int Version => 1;

	/// <inheritdoc/>
	public string MessageType => "ProductPriceChanged";
}

/// <summary>
/// V2: Multi-tenant support added.
/// </summary>
/// <remarks>
/// <para>
/// When the platform evolved to support multiple tenants (marketplace model),
/// each price change needed tenant context. Legacy V1 events are upcasted
/// with the "default" tenant ID.
/// </para>
/// <para>
/// <b>Breaking change consideration:</b> Adding TenantId changes the routing
/// logic in consuming services. Services that haven't upgraded ignore the
/// TenantId field, while upgraded services use it for data isolation.
/// </para>
/// </remarks>
public sealed record ProductPriceChangedV2 : IDispatchMessage, IVersionedMessage
{
	/// <summary>The tenant owning this product.</summary>
	public string TenantId { get; init; } = string.Empty;

	/// <summary>The product whose price changed.</summary>
	public Guid ProductId { get; init; }

	/// <summary>The new price.</summary>
	public decimal NewPrice { get; init; }

	/// <inheritdoc/>
	public int Version => 2;

	/// <inheritdoc/>
	public string MessageType => "ProductPriceChanged";
}

/// <summary>
/// V3: Added currency support and price change tracking.
/// </summary>
/// <remarks>
/// <para>
/// International expansion required currency support. Additionally,
/// tracking the old price enables:
/// </para>
/// <list type="bullet">
///   <item>Price drop notifications to customers</item>
///   <item>Price change analytics and reporting</item>
///   <item>Cart price guarantee calculations</item>
/// </list>
/// <para>
/// Legacy events default to USD currency and set OldPrice = NewPrice
/// (indicating no change history available).
/// </para>
/// </remarks>
public sealed record ProductPriceChangedV3 : IDispatchMessage, IVersionedMessage
{
	/// <summary>The tenant owning this product.</summary>
	public string TenantId { get; init; } = string.Empty;

	/// <summary>The product whose price changed.</summary>
	public Guid ProductId { get; init; }

	/// <summary>The previous price (null for initial pricing).</summary>
	public decimal? OldPrice { get; init; }

	/// <summary>The new price.</summary>
	public decimal NewPrice { get; init; }

	/// <summary>The currency code (ISO 4217).</summary>
	public string Currency { get; init; } = "USD";

	/// <summary>Price change percentage (negative = decrease).</summary>
	public decimal ChangePercentage =>
		OldPrice.HasValue && OldPrice.Value != 0
			? Math.Round((NewPrice - OldPrice.Value) / OldPrice.Value * 100, 2)
			: 0;

	/// <inheritdoc/>
	public int Version => 3;

	/// <inheritdoc/>
	public string MessageType => "ProductPriceChanged";
}
