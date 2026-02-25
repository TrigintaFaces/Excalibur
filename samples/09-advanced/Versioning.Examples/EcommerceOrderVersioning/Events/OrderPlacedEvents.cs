// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// Licensed under multiple licenses - see LICENSE files in project root.

using Excalibur.Dispatch.Abstractions;
namespace EcommerceOrderVersioning.Events;

#region OrderPlacedEvent Version History
// This file demonstrates the evolution of an OrderPlacedEvent over time:
//
// V1 (Original): Basic order with OrderId and Total
//     - Used from initial launch
//     - All orders assumed to be in USD
//
// V2 (Added CustomerId): Included CustomerId for order attribution
//     - Migration: CustomerId defaults to empty Guid for legacy orders
//
// V3 (Split Total into Subtotal + Tax): Separated pricing components
//     - Migration: Tax defaults to 0, Subtotal equals original Total
//     - Enables accurate tax reporting and refund calculations
#endregion

/// <summary>
/// V1: Original order event with basic information.
/// </summary>
/// <remarks>
/// This version was used from initial launch. All orders were assumed
/// to be domestic (USD) with tax included in the total.
/// </remarks>
public sealed record OrderPlacedEventV1 : IDispatchMessage, IVersionedMessage
{
	/// <summary>Unique identifier for the order.</summary>
	public Guid OrderId { get; init; }

	/// <summary>Total order amount (tax included, assumed USD).</summary>
	public decimal Total { get; init; }

	/// <inheritdoc/>
	public int Version => 1;

	/// <inheritdoc/>
	public string MessageType => "OrderPlacedEvent";
}

/// <summary>
/// V2: Added CustomerId for customer attribution and loyalty tracking.
/// </summary>
/// <remarks>
/// This version was introduced when the customer loyalty program launched.
/// Legacy orders (V1) are upcasted with an empty CustomerId.
/// </remarks>
public sealed record OrderPlacedEventV2 : IDispatchMessage, IVersionedMessage
{
	/// <summary>Unique identifier for the order.</summary>
	public Guid OrderId { get; init; }

	/// <summary>Customer who placed the order.</summary>
	public Guid CustomerId { get; init; }

	/// <summary>Total order amount (tax included, assumed USD).</summary>
	public decimal Total { get; init; }

	/// <inheritdoc/>
	public int Version => 2;

	/// <inheritdoc/>
	public string MessageType => "OrderPlacedEvent";
}

/// <summary>
/// V3: Split Total into Subtotal and Tax for tax compliance.
/// </summary>
/// <remarks>
/// <para>
/// This version was introduced for tax compliance requirements.
/// The separation enables:
/// </para>
/// <list type="bullet">
///   <item>Accurate tax reporting per jurisdiction</item>
///   <item>Proper refund calculations (some refunds exclude tax)</item>
///   <item>Tax-exempt order handling</item>
/// </list>
/// <para>
/// Legacy orders (V1/V2) are upcasted with Tax=0 and Subtotal=Total
/// (historical tax cannot be reliably reconstructed).
/// </para>
/// </remarks>
public sealed record OrderPlacedEventV3 : IDispatchMessage, IVersionedMessage
{
	/// <summary>Unique identifier for the order.</summary>
	public Guid OrderId { get; init; }

	/// <summary>Customer who placed the order.</summary>
	public Guid CustomerId { get; init; }

	/// <summary>Order subtotal before tax.</summary>
	public decimal Subtotal { get; init; }

	/// <summary>Tax amount.</summary>
	public decimal Tax { get; init; }

	/// <summary>Total order amount (Subtotal + Tax).</summary>
	public decimal Total => Subtotal + Tax;

	/// <inheritdoc/>
	public int Version => 3;

	/// <inheritdoc/>
	public string MessageType => "OrderPlacedEvent";
}
