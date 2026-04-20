// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace EnterpriseOrderProcessing.Domain.Events;

public sealed record OrderCreated(
	Guid OrderId,
	Guid CustomerId,
	string CustomerName) : DomainEvent;

public sealed record OrderLineAdded(
	Guid OrderId,
	string ProductId,
	int Quantity,
	decimal UnitPrice) : DomainEvent;

public sealed record OrderSubmitted(Guid OrderId) : DomainEvent;

public sealed record OrderShipped(
	Guid OrderId,
	string TrackingNumber) : DomainEvent;

public sealed record OrderCancelled(
	Guid OrderId,
	string Reason) : DomainEvent;
