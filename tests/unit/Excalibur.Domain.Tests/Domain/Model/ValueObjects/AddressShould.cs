// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Domain.Model.ValueObjects;

namespace Excalibur.Tests.Domain.Model.ValueObjects;

/// <summary>
/// Unit tests for <see cref="Address"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class AddressShould
{
	#region T419.9: Address Value Object Tests

	[Fact]
	public void Constructor_SetsAllProperties()
	{
		// Arrange & Act
		var address = new Address(
			street: "123 Main St",
			city: "Springfield",
			state: "IL",
			country: "USA",
			postalCode: "62701");

		// Assert
		address.Street.ShouldBe("123 Main St");
		address.City.ShouldBe("Springfield");
		address.State.ShouldBe("IL");
		address.Country.ShouldBe("USA");
		address.PostalCode.ShouldBe("62701");
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenStreetIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new Address(
			street: null!,
			city: "City",
			state: "State",
			country: "Country",
			postalCode: "12345"));
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenCityIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new Address(
			street: "Street",
			city: null!,
			state: "State",
			country: "Country",
			postalCode: "12345"));
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenStateIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new Address(
			street: "Street",
			city: "City",
			state: null!,
			country: "Country",
			postalCode: "12345"));
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenCountryIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new Address(
			street: "Street",
			city: "City",
			state: "State",
			country: null!,
			postalCode: "12345"));
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenPostalCodeIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new Address(
			street: "Street",
			city: "City",
			state: "State",
			country: "Country",
			postalCode: null!));
	}

	[Fact]
	public void Equality_ReturnsTrue_ForIdenticalAddresses()
	{
		// Arrange
		var address1 = new Address("123 Main St", "City", "ST", "Country", "12345");
		var address2 = new Address("123 Main St", "City", "ST", "Country", "12345");

		// Act & Assert
		(address1 == address2).ShouldBeTrue();
		address1.Equals(address2).ShouldBeTrue();
	}

	[Fact]
	public void Equality_ReturnsFalse_WhenStreetDiffers()
	{
		// Arrange
		var address1 = new Address("123 Main St", "City", "ST", "Country", "12345");
		var address2 = new Address("456 Oak Ave", "City", "ST", "Country", "12345");

		// Act & Assert
		(address1 == address2).ShouldBeFalse();
	}

	[Fact]
	public void Equality_ReturnsFalse_WhenCityDiffers()
	{
		// Arrange
		var address1 = new Address("123 Main St", "City1", "ST", "Country", "12345");
		var address2 = new Address("123 Main St", "City2", "ST", "Country", "12345");

		// Act & Assert
		(address1 == address2).ShouldBeFalse();
	}

	[Fact]
	public void GetEqualityComponents_ReturnsAllFiveComponents()
	{
		// Arrange
		var address = new Address("Street", "City", "State", "Country", "PostalCode");

		// Act
		var components = address.GetEqualityComponents().ToList();

		// Assert
		components.Count.ShouldBe(5);
		components.ShouldContain("Street");
		components.ShouldContain("City");
		components.ShouldContain("State");
		components.ShouldContain("Country");
		components.ShouldContain("PostalCode");
	}

	[Fact]
	public void ToString_ReturnsFormattedAddress()
	{
		// Arrange
		var address = new Address("123 Main St", "Springfield", "IL", "USA", "62701");

		// Act
		var result = address.ToString();

		// Assert
		result.ShouldBe("123 Main St, Springfield, IL 62701, USA");
	}

	[Fact]
	public void GetHashCode_IsConsistent_ForEqualAddresses()
	{
		// Arrange
		var address1 = new Address("123 Main St", "City", "ST", "Country", "12345");
		var address2 = new Address("123 Main St", "City", "ST", "Country", "12345");

		// Act & Assert
		address1.GetHashCode().ShouldBe(address2.GetHashCode());
	}

	#endregion T419.9: Address Value Object Tests
}
