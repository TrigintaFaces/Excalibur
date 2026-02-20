// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using EventUpcasting.Events;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Domain.Model;

namespace EventUpcasting.Domain;

/// <summary>
/// User profile aggregate demonstrating event upcasting.
/// </summary>
/// <remarks>
/// This aggregate only handles V3 events (the current version).
/// Older event versions are automatically upgraded by the upcasting pipeline
/// before being applied to the aggregate.
/// </remarks>
public class UserProfileAggregate : AggregateRoot<string>
{
	public UserProfileAggregate()
	{
	}

	public UserProfileAggregate(string id) : base(id)
	{
	}

	/// <summary>Gets the user's name.</summary>
	public string Name { get; private set; } = string.Empty;

	/// <summary>Gets the user's email.</summary>
	public string Email { get; private set; } = string.Empty;

	/// <summary>Gets the user's street address.</summary>
	public string? Street { get; private set; }

	/// <summary>Gets the user's city.</summary>
	public string? City { get; private set; }

	/// <summary>Gets the user's postal code.</summary>
	public string? PostalCode { get; private set; }

	/// <summary>Gets the user's country.</summary>
	public string? Country { get; private set; }

	/// <summary>Gets the formatted full address.</summary>
	public string? FullAddress =>
		string.IsNullOrWhiteSpace(Street)
			? null
			: string.Join(", ",
				new[] { Street, City, PostalCode, Country }
					.Where(s => !string.IsNullOrWhiteSpace(s)));

	/// <summary>
	/// Creates a new user profile.
	/// </summary>
	public static UserProfileAggregate Create(
		string userId,
		string name,
		string email,
		string? street = null,
		string? city = null,
		string? postalCode = null,
		string? country = null)
	{
		var user = new UserProfileAggregate(userId);
		user.RaiseEvent(new UserCreatedV3(
			userId,
			user.Version,
			name,
			email,
			street,
			city,
			postalCode,
			country));
		return user;
	}

	/// <summary>
	/// Changes the user's name.
	/// </summary>
	public void ChangeName(string newName)
	{
		if (string.Equals(Name, newName, StringComparison.Ordinal))
		{
			return;
		}

		RaiseEvent(new UserNameChangedV3(Id, Version, Name, newName));
	}

	/// <summary>
	/// Changes the user's address.
	/// </summary>
	public void ChangeAddress(string? street, string? city, string? postalCode, string? country)
	{
		RaiseEvent(new UserAddressChangedV3(
			Id,
			Version,
			OldStreet: Street,
			OldCity: City,
			OldPostalCode: PostalCode,
			OldCountry: Country,
			NewStreet: street,
			NewCity: city,
			NewPostalCode: postalCode,
			NewCountry: country));
	}

	/// <inheritdoc/>
	protected override void ApplyEventInternal(IDomainEvent @event) => _ = @event switch
	{
		// Only handle V3 events - older versions are automatically upcasted
		UserCreatedV3 e => ApplyUserCreated(e),
		UserNameChangedV3 e => ApplyNameChanged(e),
		UserAddressChangedV3 e => ApplyAddressChanged(e),

		// These should never reach the aggregate (they get upcasted)
		// but we handle them gracefully for demonstration
		UserCreatedV1 e => ApplyLegacyUserCreatedV1(e),
		UserCreatedV2 e => ApplyLegacyUserCreatedV2(e),

		_ => throw new InvalidOperationException($"Unknown event type: {@event.GetType().Name}")
	};

	private bool ApplyUserCreated(UserCreatedV3 e)
	{
		Name = e.Name;
		Email = e.Email;
		Street = e.Street;
		City = e.City;
		PostalCode = e.PostalCode;
		Country = e.Country;
		return true;
	}

	private bool ApplyNameChanged(UserNameChangedV3 e)
	{
		Name = e.NewName;
		return true;
	}

	private bool ApplyAddressChanged(UserAddressChangedV3 e)
	{
		Street = e.NewStreet;
		City = e.NewCity;
		PostalCode = e.NewPostalCode;
		Country = e.NewCountry;
		return true;
	}

	// Legacy handlers - these would only be called if upcasting is disabled
	private bool ApplyLegacyUserCreatedV1(UserCreatedV1 e)
	{
		Name = e.Name;
		Email = e.Email;
		// No address in V1
		return true;
	}

	private bool ApplyLegacyUserCreatedV2(UserCreatedV2 e)
	{
		Name = e.Name;
		Email = e.Email;
		// V2 had flat address - we can't split it without the upgrader
		// Store in Street for fallback
		Street = e.Address;
		return true;
	}
}
