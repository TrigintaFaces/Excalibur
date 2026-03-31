// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Domain.Model;

using EnterpriseOrderProcessing.Domain.Events;

namespace EnterpriseOrderProcessing.Domain;

public sealed class InventoryAggregate : AggregateRoot<string>
{
	private readonly Dictionary<Guid, int> _reservations = [];

	public override string AggregateType => "Inventory";

	public string ProductName { get; private set; } = string.Empty;
	public int AvailableQuantity { get; private set; }
	public int ReservedQuantity => _reservations.Values.Sum();
	public int TotalQuantity => AvailableQuantity + ReservedQuantity;

	public void Create(string productId, string productName, int initialQuantity)
	{
		if (Version > 0)
			throw new InvalidOperationException("Inventory item already created.");

		ArgumentException.ThrowIfNullOrWhiteSpace(productId);
		ArgumentException.ThrowIfNullOrWhiteSpace(productName);
		ArgumentOutOfRangeException.ThrowIfNegative(initialQuantity);

		Id = productId;
		RaiseEvent(new InventoryItemCreated(productId, productName, initialQuantity));
	}

	public void Reserve(Guid orderId, int quantity)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

		if (quantity > AvailableQuantity)
			throw new InvalidOperationException(
				$"Insufficient inventory. Requested: {quantity}, Available: {AvailableQuantity}");

		RaiseEvent(new InventoryReserved(Id, orderId, quantity));
	}

	public void ReleaseReservation(Guid orderId)
	{
		if (!_reservations.TryGetValue(orderId, out var quantity))
			throw new InvalidOperationException($"No reservation found for order {orderId}.");

		RaiseEvent(new InventoryReservationReleased(Id, orderId, quantity));
	}

	public void Replenish(int quantity)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

		RaiseEvent(new InventoryReplenished(Id, quantity));
	}

	protected override void ApplyEventInternal(IDomainEvent @event)
	{
		switch (@event)
		{
			case InventoryItemCreated e: Apply(e); break;
			case InventoryReserved e: Apply(e); break;
			case InventoryReservationReleased e: Apply(e); break;
			case InventoryReplenished e: Apply(e); break;
		}
	}

	private void Apply(InventoryItemCreated e)
	{
		Id = e.ProductId;
		ProductName = e.ProductName;
		AvailableQuantity = e.InitialQuantity;
	}

	private void Apply(InventoryReserved e)
	{
		AvailableQuantity -= e.Quantity;
		_reservations[e.OrderId] = _reservations.GetValueOrDefault(e.OrderId) + e.Quantity;
	}

	private void Apply(InventoryReservationReleased e)
	{
		AvailableQuantity += e.Quantity;
		_reservations.Remove(e.OrderId);
	}

	private void Apply(InventoryReplenished e)
	{
		AvailableQuantity += e.Quantity;
	}
}
