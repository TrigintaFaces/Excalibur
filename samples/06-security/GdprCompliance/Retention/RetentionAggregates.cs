// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Compliance;
using Excalibur.Dispatch;
using Excalibur.Domain.Model;

namespace GdprCompliance.Retention;

/// <summary>
/// The aggregate that holds a person's identifying data. Nothing declares a retention for it, so an
/// erasure tombstones every one of its events.
/// </summary>
public sealed class CustomerProfile : AggregateRoot<Guid>
{
	/// <summary>Initializes a new instance for rehydration from events.</summary>
	public CustomerProfile()
	{
	}

	/// <summary>Initializes a new instance with an identifier.</summary>
	/// <param name="id">The customer identifier.</param>
	public CustomerProfile(Guid id) : base(id)
	{
	}

	/// <summary>Gets the customer's full name.</summary>
	[PersonalData(Category = PersonalDataCategory.Identity)]
	public string FullName { get; private set; } = string.Empty;

	/// <summary>Gets the customer's email address.</summary>
	[PersonalData(Category = PersonalDataCategory.ContactInfo)]
	public string EmailAddress { get; private set; } = string.Empty;

	/// <summary>Opens a customer profile.</summary>
	/// <param name="id">The customer identifier.</param>
	/// <param name="fullName">The customer's full name.</param>
	/// <param name="emailAddress">The customer's email address.</param>
	/// <returns>The new profile, with its registration event uncommitted.</returns>
	public static CustomerProfile Register(Guid id, string fullName, string emailAddress)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
		ArgumentException.ThrowIfNullOrWhiteSpace(emailAddress);

		var profile = new CustomerProfile(id);
		profile.RaiseEvent(new CustomerProfileRegistered(id, fullName, emailAddress));
		return profile;
	}

	/// <inheritdoc/>
	protected override bool ApplyEventInternal(IDomainEvent @event)
	{
		switch (@event)
		{
			case CustomerProfileRegistered e:
				Id = e.CustomerId;
				FullName = e.FullName;
				EmailAddress = e.EmailAddress;
				return true;
			default:
				return false;
		}
	}
}

/// <summary>
/// A vehicle sales record. Tax, warranty and product-recall obligations attach to this record as a
/// whole, so the deployment declares a retention for it and an erasure leaves it standing.
/// </summary>
/// <remarks>
/// <para>
/// The buyer's details live <b>here</b>, on the sales record's own events. A record that reached its
/// buyer through a reference into <see cref="CustomerProfile"/> would break the moment that profile was
/// erased — the reference would survive and resolve to a tombstone. A retention protects the aggregate
/// types you name and nothing they point at.
/// </para>
/// <para>
/// The type name is what the declaration matches, ordinally. Declaring it as
/// <c>nameof(SalesRecord)</c> keeps the declaration and the class from drifting apart; a declaration of
/// <c>salesrecord</c> would not match, and the record would be destroyed.
/// </para>
/// </remarks>
public sealed class SalesRecord : AggregateRoot<Guid>
{
	/// <summary>Initializes a new instance for rehydration from events.</summary>
	public SalesRecord()
	{
	}

	/// <summary>Initializes a new instance with an identifier.</summary>
	/// <param name="id">The sale identifier.</param>
	public SalesRecord(Guid id) : base(id)
	{
	}

	/// <summary>Gets the identifier of the customer profile this sale was made to.</summary>
	public Guid BuyerId { get; private set; }

	/// <summary>Gets the buyer's name, as recorded on the sale.</summary>
	[PersonalData(Category = PersonalDataCategory.Identity)]
	public string BuyerName { get; private set; } = string.Empty;

	/// <summary>Gets the buyer's address, as recorded on the sale.</summary>
	[PersonalData(Category = PersonalDataCategory.ContactInfo)]
	public string BuyerAddress { get; private set; } = string.Empty;

	/// <summary>Gets the vehicle identification number.</summary>
	public string VehicleIdentificationNumber { get; private set; } = string.Empty;

	/// <summary>Gets the sale price.</summary>
	public decimal SalePrice { get; private set; }

	/// <summary>Records a vehicle sale.</summary>
	/// <param name="id">The sale identifier.</param>
	/// <param name="buyerId">The buyer's customer-profile identifier.</param>
	/// <param name="buyerName">The buyer's name, copied onto this record.</param>
	/// <param name="buyerAddress">The buyer's address, copied onto this record.</param>
	/// <param name="vin">The vehicle identification number.</param>
	/// <param name="salePrice">The sale price.</param>
	/// <returns>The new sales record, with its sale event uncommitted.</returns>
	public static SalesRecord Record(
		Guid id,
		Guid buyerId,
		string buyerName,
		string buyerAddress,
		string vin,
		decimal salePrice)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(buyerName);
		ArgumentException.ThrowIfNullOrWhiteSpace(buyerAddress);
		ArgumentException.ThrowIfNullOrWhiteSpace(vin);

		var sale = new SalesRecord(id);
		sale.RaiseEvent(new VehicleSold(id, buyerId, buyerName, buyerAddress, vin, salePrice));
		return sale;
	}

	/// <inheritdoc/>
	protected override bool ApplyEventInternal(IDomainEvent @event)
	{
		switch (@event)
		{
			case VehicleSold e:
				Id = e.SaleId;
				BuyerId = e.BuyerId;
				BuyerName = e.BuyerName;
				BuyerAddress = e.BuyerAddress;
				VehicleIdentificationNumber = e.VehicleIdentificationNumber;
				SalePrice = e.SalePrice;
				return true;
			default:
				return false;
		}
	}
}
