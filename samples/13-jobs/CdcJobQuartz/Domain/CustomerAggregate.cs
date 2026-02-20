// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch.Abstractions;

using Excalibur.Domain.Model;

namespace CdcJobQuartz.Domain;

/// <summary>
/// Customer aggregate demonstrating event sourcing with SQL Server.
/// This aggregate receives commands translated from CDC events via the Anti-Corruption Layer.
/// </summary>
public sealed class CustomerAggregate : AggregateRoot<Guid>
{
	/// <summary>
	/// Initializes a new instance for rehydration from events.
	/// </summary>
	public CustomerAggregate()
	{
	}

	/// <summary>
	/// Initializes a new instance with an identifier.
	/// </summary>
	public CustomerAggregate(Guid id) : base(id)
	{
	}

	/// <summary>Gets the customer's external ID from the legacy system.</summary>
	public string ExternalId { get; private set; } = string.Empty;

	/// <summary>Gets the customer's name.</summary>
	public string Name { get; private set; } = string.Empty;

	/// <summary>Gets the customer's email address.</summary>
	public string Email { get; private set; } = string.Empty;

	/// <summary>Gets the customer's phone number.</summary>
	public string? Phone { get; private set; }

	/// <summary>Gets the total number of orders placed.</summary>
	public int OrderCount { get; private set; }

	/// <summary>Gets the total amount spent.</summary>
	public decimal TotalSpent { get; private set; }

	/// <summary>Gets whether the customer is active.</summary>
	public bool IsActive { get; private set; }

	/// <summary>Gets the customer tier based on spending.</summary>
	public CustomerTier Tier { get; private set; }

	/// <summary>Gets when the customer was created.</summary>
	public DateTimeOffset CreatedAt { get; private set; }

	/// <summary>Gets when the customer was last updated.</summary>
	public DateTimeOffset? UpdatedAt { get; private set; }

	/// <summary>
	/// Creates a new customer from CDC data.
	/// </summary>
	public static CustomerAggregate Create(
		Guid id,
		string externalId,
		string name,
		string email,
		string? phone = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(externalId);
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentException.ThrowIfNullOrWhiteSpace(email);

		var customer = new CustomerAggregate(id);
		customer.RaiseEvent(new CustomerCreated(id, externalId, name, email, phone, customer.Version));
		return customer;
	}

	/// <summary>
	/// Updates customer information.
	/// </summary>
	public void UpdateInfo(string name, string email, string? phone)
	{
		EnsureActive();
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentException.ThrowIfNullOrWhiteSpace(email);

		if (Name == name && Email == email && Phone == phone)
		{
			return; // No change
		}

		RaiseEvent(new CustomerInfoUpdated(Id, name, email, phone, Version));
	}

	/// <summary>
	/// Records an order placed by the customer.
	/// </summary>
	public void RecordOrder(Guid orderId, decimal amount)
	{
		EnsureActive();
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);

		RaiseEvent(new CustomerOrderPlaced(Id, orderId, amount, Version));
	}

	/// <summary>
	/// Deactivates the customer (soft delete from CDC).
	/// </summary>
	public void Deactivate(string reason)
	{
		EnsureActive();
		RaiseEvent(new CustomerDeactivated(Id, reason, Version));
	}

	/// <inheritdoc/>
	protected override void ApplyEventInternal(IDomainEvent @event) => _ = @event switch
	{
		CustomerCreated e => ApplyCustomerCreated(e),
		CustomerInfoUpdated e => ApplyCustomerInfoUpdated(e),
		CustomerOrderPlaced e => ApplyCustomerOrderPlaced(e),
		CustomerDeactivated e => ApplyCustomerDeactivated(e),
		_ => throw new InvalidOperationException($"Unknown event type: {@event.GetType().Name}")
	};

	private void EnsureActive()
	{
		if (!IsActive)
		{
			throw new InvalidOperationException("Customer is not active");
		}
	}

	private CustomerTier CalculateTier(decimal totalSpent) => totalSpent switch
	{
		>= 10000m => CustomerTier.Platinum,
		>= 5000m => CustomerTier.Gold,
		>= 1000m => CustomerTier.Silver,
		_ => CustomerTier.Bronze
	};

	private bool ApplyCustomerCreated(CustomerCreated e)
	{
		Id = e.CustomerId;
		ExternalId = e.ExternalId;
		Name = e.Name;
		Email = e.Email;
		Phone = e.Phone;
		IsActive = true;
		Tier = CustomerTier.Bronze;
		CreatedAt = e.OccurredAt;
		return true;
	}

	private bool ApplyCustomerInfoUpdated(CustomerInfoUpdated e)
	{
		Name = e.Name;
		Email = e.Email;
		Phone = e.Phone;
		UpdatedAt = e.OccurredAt;
		return true;
	}

	private bool ApplyCustomerOrderPlaced(CustomerOrderPlaced e)
	{
		OrderCount++;
		TotalSpent += e.Amount;
		Tier = CalculateTier(TotalSpent);
		UpdatedAt = e.OccurredAt;
		return true;
	}

	private bool ApplyCustomerDeactivated(CustomerDeactivated e)
	{
		IsActive = false;
		UpdatedAt = e.OccurredAt;
		return true;
	}
}

/// <summary>
/// Customer tier based on spending.
/// </summary>
public enum CustomerTier
{
	/// <summary>Default tier for new customers.</summary>
	Bronze = 0,

	/// <summary>Tier for customers with $1,000+ spent.</summary>
	Silver = 1,

	/// <summary>Tier for customers with $5,000+ spent.</summary>
	Gold = 2,

	/// <summary>Tier for customers with $10,000+ spent.</summary>
	Platinum = 3
}

#region Domain Events

/// <summary>
/// Event raised when a customer is created.
/// </summary>
public sealed record CustomerCreated : DomainEvent
{
	/// <summary>
	/// Initializes a new instance of the <see cref="CustomerCreated"/> class.
	/// </summary>
	public CustomerCreated(
		Guid customerId,
		string externalId,
		string name,
		string email,
		string? phone,
		long version)
		: base(customerId.ToString(), version)
	{
		CustomerId = customerId;
		ExternalId = externalId;
		Name = name;
		Email = email;
		Phone = phone;
	}

	/// <summary>Gets the customer identifier.</summary>
	public Guid CustomerId { get; init; }

	/// <summary>Gets the external ID from the legacy system.</summary>
	public string ExternalId { get; init; }

	/// <summary>Gets the customer name.</summary>
	public string Name { get; init; }

	/// <summary>Gets the customer email.</summary>
	public string Email { get; init; }

	/// <summary>Gets the customer phone.</summary>
	public string? Phone { get; init; }
}

/// <summary>
/// Event raised when customer information is updated.
/// </summary>
public sealed record CustomerInfoUpdated : DomainEvent
{
	/// <summary>
	/// Initializes a new instance of the <see cref="CustomerInfoUpdated"/> class.
	/// </summary>
	public CustomerInfoUpdated(Guid customerId, string name, string email, string? phone, long version)
		: base(customerId.ToString(), version)
	{
		CustomerId = customerId;
		Name = name;
		Email = email;
		Phone = phone;
	}

	/// <summary>Gets the customer identifier.</summary>
	public Guid CustomerId { get; init; }

	/// <summary>Gets the updated name.</summary>
	public string Name { get; init; }

	/// <summary>Gets the updated email.</summary>
	public string Email { get; init; }

	/// <summary>Gets the updated phone.</summary>
	public string? Phone { get; init; }
}

/// <summary>
/// Event raised when a customer places an order.
/// </summary>
public sealed record CustomerOrderPlaced : DomainEvent
{
	/// <summary>
	/// Initializes a new instance of the <see cref="CustomerOrderPlaced"/> class.
	/// </summary>
	public CustomerOrderPlaced(Guid customerId, Guid orderId, decimal amount, long version)
		: base(customerId.ToString(), version)
	{
		CustomerId = customerId;
		OrderId = orderId;
		Amount = amount;
	}

	/// <summary>Gets the customer identifier.</summary>
	public Guid CustomerId { get; init; }

	/// <summary>Gets the order identifier.</summary>
	public Guid OrderId { get; init; }

	/// <summary>Gets the order amount.</summary>
	public decimal Amount { get; init; }
}

/// <summary>
/// Event raised when a customer is deactivated.
/// </summary>
public sealed record CustomerDeactivated : DomainEvent
{
	/// <summary>
	/// Initializes a new instance of the <see cref="CustomerDeactivated"/> class.
	/// </summary>
	public CustomerDeactivated(Guid customerId, string reason, long version)
		: base(customerId.ToString(), version)
	{
		CustomerId = customerId;
		Reason = reason;
	}

	/// <summary>Gets the customer identifier.</summary>
	public Guid CustomerId { get; init; }

	/// <summary>Gets the deactivation reason.</summary>
	public string Reason { get; init; }
}

#endregion
