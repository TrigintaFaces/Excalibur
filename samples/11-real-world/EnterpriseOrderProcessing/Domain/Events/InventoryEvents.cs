// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace EnterpriseOrderProcessing.Domain.Events;

public sealed record InventoryItemCreated(
	string ProductId,
	string ProductName,
	int InitialQuantity) : DomainEvent;

public sealed record InventoryReserved(
	string ProductId,
	Guid OrderId,
	int Quantity) : DomainEvent;

public sealed record InventoryReservationReleased(
	string ProductId,
	Guid OrderId,
	int Quantity) : DomainEvent;

public sealed record InventoryReplenished(
	string ProductId,
	int Quantity) : DomainEvent;
