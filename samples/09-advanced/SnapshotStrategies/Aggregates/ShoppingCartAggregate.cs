// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch.Abstractions;

using Excalibur.Domain.Model;

namespace SnapshotStrategies.Aggregates;

/// <summary>
/// Shopping cart aggregate demonstrating high-velocity event generation.
/// Ideal for interval-based snapshotting (every N events).
/// </summary>
public class ShoppingCartAggregate : AggregateRoot<Guid>
{
	private readonly List<CartItem> _items = [];

	public ShoppingCartAggregate()
	{
	}

	public ShoppingCartAggregate(Guid id) : base(id)
	{
	}

	/// <summary>Gets the cart items.</summary>
	public IReadOnlyList<CartItem> Items => _items;

	/// <summary>Gets the total item count.</summary>
	public int TotalItems => _items.Sum(i => i.Quantity);

	/// <summary>Gets the total price.</summary>
	public decimal TotalPrice => _items.Sum(i => i.Price * i.Quantity);

	/// <summary>Gets whether the cart is checked out.</summary>
	public bool IsCheckedOut { get; private set; }

	/// <summary>
	/// Creates a new shopping cart.
	/// </summary>
	public static ShoppingCartAggregate Create(Guid id)
	{
		var cart = new ShoppingCartAggregate(id);
		cart.RaiseEvent(new CartCreated(id, cart.Version));
		return cart;
	}

	/// <summary>
	/// Adds an item to the cart.
	/// </summary>
	public void AddItem(string productId, string name, decimal price, int quantity = 1)
	{
		if (IsCheckedOut)
		{
			throw new InvalidOperationException("Cannot add items to checked out cart");
		}

		RaiseEvent(new ItemAddedToCart(Id, productId, name, price, quantity, Version));
	}

	/// <summary>
	/// Removes an item from the cart.
	/// </summary>
	public void RemoveItem(string productId)
	{
		if (IsCheckedOut)
		{
			throw new InvalidOperationException("Cannot remove items from checked out cart");
		}

		RaiseEvent(new ItemRemovedFromCart(Id, productId, Version));
	}

	/// <summary>
	/// Updates item quantity.
	/// </summary>
	public void UpdateQuantity(string productId, int newQuantity)
	{
		if (IsCheckedOut)
		{
			throw new InvalidOperationException("Cannot update checked out cart");
		}

		RaiseEvent(new CartItemQuantityUpdated(Id, productId, newQuantity, Version));
	}

	/// <summary>
	/// Checks out the cart.
	/// </summary>
	public void Checkout()
	{
		if (IsCheckedOut)
		{
			throw new InvalidOperationException("Cart already checked out");
		}

		if (_items.Count == 0)
		{
			throw new InvalidOperationException("Cannot checkout empty cart");
		}

		RaiseEvent(new CartCheckedOut(Id, TotalPrice, Version));
	}

	/// <inheritdoc/>
	protected override void ApplyEventInternal(IDomainEvent @event) => _ = @event switch
	{
		CartCreated => true,
		ItemAddedToCart e => ApplyItemAdded(e),
		ItemRemovedFromCart e => ApplyItemRemoved(e),
		CartItemQuantityUpdated e => ApplyQuantityUpdated(e),
		CartCheckedOut => ApplyCheckedOut(),
		_ => throw new InvalidOperationException($"Unknown event: {@event.GetType().Name}")
	};

	private bool ApplyItemAdded(ItemAddedToCart e)
	{
		var existing = _items.FirstOrDefault(i => i.ProductId == e.ProductId);
		if (existing != null)
		{
			existing.Quantity += e.Quantity;
		}
		else
		{
			_items.Add(new CartItem(e.ProductId, e.ProductName, e.Price, e.Quantity));
		}

		return true;
	}

	private bool ApplyItemRemoved(ItemRemovedFromCart e)
	{
		_ = _items.RemoveAll(i => i.ProductId == e.ProductId);
		return true;
	}

	private bool ApplyQuantityUpdated(CartItemQuantityUpdated e)
	{
		var item = _items.FirstOrDefault(i => i.ProductId == e.ProductId);
		if (item != null)
		{
			item.Quantity = e.NewQuantity;
		}

		return true;
	}

	private bool ApplyCheckedOut()
	{
		IsCheckedOut = true;
		return true;
	}
}

/// <summary>
/// Cart item.
/// </summary>
public class CartItem
{
	public CartItem(string productId, string name, decimal price, int quantity)
	{
		ProductId = productId;
		Name = name;
		Price = price;
		Quantity = quantity;
	}

	public string ProductId { get; }
	public string Name { get; }
	public decimal Price { get; }
	public int Quantity { get; set; }
}

#region Events

public sealed record CartCreated(Guid CartId, long Version)
	: DomainEvent(CartId.ToString(), Version);

public sealed record ItemAddedToCart(
	Guid CartId,
	string ProductId,
	string ProductName,
	decimal Price,
	int Quantity,
	long Version) : DomainEvent(CartId.ToString(), Version);

public sealed record ItemRemovedFromCart(Guid CartId, string ProductId, long Version)
	: DomainEvent(CartId.ToString(), Version);

public sealed record CartItemQuantityUpdated(Guid CartId, string ProductId, int NewQuantity, long Version)
	: DomainEvent(CartId.ToString(), Version);

public sealed record CartCheckedOut(Guid CartId, decimal TotalPrice, long Version)
	: DomainEvent(CartId.ToString(), Version);

#endregion
