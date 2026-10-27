// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch;

namespace EnterpriseOrderProcessing.Domain.Events;

[MessageName("Contoso.Orders.OrderCreated")]
public sealed record OrderCreated(
	Guid OrderId,
	Guid CustomerId,
	string CustomerName) : DomainEvent;

[MessageName("Contoso.Orders.OrderLineAdded")]
public sealed record OrderLineAdded(
	Guid OrderId,
	string ProductId,
	int Quantity,
	decimal UnitPrice) : DomainEvent;

[MessageName("Contoso.Orders.OrderSubmitted")]
public sealed record OrderSubmitted(Guid OrderId) : DomainEvent;

[MessageName("Contoso.Orders.OrderShipped")]
public sealed record OrderShipped(
	Guid OrderId,
	string TrackingNumber) : DomainEvent;

[MessageName("Contoso.Orders.OrderCancelled")]
public sealed record OrderCancelled(
	Guid OrderId,
	string Reason) : DomainEvent;
