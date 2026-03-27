// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Domain.Model;

using EnterpriseOrderProcessing.Domain.Events;

namespace EnterpriseOrderProcessing.Domain;

public sealed class Address
{
	public required string Street { get; init; }
	public required string City { get; init; }
	public required string PostalCode { get; init; }
	public required string Country { get; init; }
}

public sealed class CustomerAggregate : AggregateRoot<Guid>
{
	public override string AggregateType => "Customer";

	public string Name { get; private set; } = string.Empty;
	public string Email { get; private set; } = string.Empty;
	public Address? ShippingAddress { get; private set; }
	public bool IsActive { get; private set; }
	public string? DeactivationReason { get; private set; }

	public void Register(Guid customerId, string name, string email)
	{
		if (Version > 0)
			throw new InvalidOperationException("Customer already registered.");

		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentException.ThrowIfNullOrWhiteSpace(email);

		RaiseEvent(new CustomerRegistered(customerId, name, email));
	}

	public void UpdateAddress(string street, string city, string postalCode, string country)
	{
		if (!IsActive)
			throw new InvalidOperationException("Cannot update address of a deactivated customer.");

		ArgumentException.ThrowIfNullOrWhiteSpace(street);
		ArgumentException.ThrowIfNullOrWhiteSpace(city);

		RaiseEvent(new CustomerAddressUpdated(Id, street, city, postalCode, country));
	}

	public void Deactivate(string reason)
	{
		if (!IsActive)
			throw new InvalidOperationException("Customer is already deactivated.");

		ArgumentException.ThrowIfNullOrWhiteSpace(reason);

		RaiseEvent(new CustomerDeactivated(Id, reason));
	}

	protected override void ApplyEventInternal(IDomainEvent @event)
	{
		switch (@event)
		{
			case CustomerRegistered e: Apply(e); break;
			case CustomerAddressUpdated e: Apply(e); break;
			case CustomerDeactivated e: Apply(e); break;
		}
	}

	private void Apply(CustomerRegistered e)
	{
		Id = e.CustomerId;
		Name = e.Name;
		Email = e.Email;
		IsActive = true;
	}

	private void Apply(CustomerAddressUpdated e)
	{
		ShippingAddress = new Address
		{
			Street = e.Street,
			City = e.City,
			PostalCode = e.PostalCode,
			Country = e.Country
		};
	}

	private void Apply(CustomerDeactivated e)
	{
		IsActive = false;
		DeactivationReason = e.Reason;
	}
}
