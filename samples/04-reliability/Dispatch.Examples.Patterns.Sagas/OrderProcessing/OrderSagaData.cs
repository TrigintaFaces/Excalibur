// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Excalibur.Dispatch.Examples.Patterns.Sagas.OrderProcessing;

/// <summary>
/// Data for the order processing saga.
/// </summary>
public class OrderSagaData {
 public string OrderId { get; set; } = string.Empty;
 public string CustomerId { get; set; } = string.Empty;
 public List<OrderItem> Items { get; set; } = new();
 public decimal TotalAmount { get; set; }
 public string PaymentMethod { get; set; } = string.Empty;
 public string ShippingAddress { get; set; } = string.Empty;
 
 // State that gets populated during saga execution
 public string? InventoryReservationId { get; set; }
 public string? PaymentTransactionId { get; set; }
 public string? ShipmentId { get; set; }
}

/// <summary>
/// Represents an item in the order.
/// </summary>
public class OrderItem {
 public string ProductId { get; set; } = string.Empty;
 public string ProductName { get; set; } = string.Empty;
 public int Quantity { get; set; }
 public decimal Price { get; set; }
}
