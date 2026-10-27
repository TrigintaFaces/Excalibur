// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch;

namespace EnterpriseOrderProcessing.Domain.Events;

[MessageName("Contoso.Inventory.InventoryItemCreated")]
public sealed record InventoryItemCreated(
	string ProductId,
	string ProductName,
	int InitialQuantity) : DomainEvent;

[MessageName("Contoso.Inventory.InventoryReserved")]
public sealed record InventoryReserved(
	string ProductId,
	Guid OrderId,
	int Quantity) : DomainEvent;

[MessageName("Contoso.Inventory.InventoryReservationReleased")]
public sealed record InventoryReservationReleased(
	string ProductId,
	Guid OrderId,
	int Quantity) : DomainEvent;

[MessageName("Contoso.Inventory.InventoryReplenished")]
public sealed record InventoryReplenished(
	string ProductId,
	int Quantity) : DomainEvent;
