// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Domain.Model.ValueObjects;

namespace Excalibur.Tests.Domain.Model.ValueObjects;

/// <summary>
/// Depth coverage tests for <see cref="Address"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Domain")]
public sealed class AddressDepthShould
{
	[Fact]
	public void Constructor_SetsAllProperties()
	{
		// Arrange & Act
		var address = new Address("123 Main St", "Springfield", "IL", "US", "62701");

		// Assert
		address.Street.ShouldBe("123 Main St");
		address.City.ShouldBe("Springfield");
		address.State.ShouldBe("IL");
		address.Country.ShouldBe("US");
		address.PostalCode.ShouldBe("62701");
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenStreetIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new Address(null!, "City", "State", "Country", "Zip"));
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenCityIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new Address("Street", null!, "State", "Country", "Zip"));
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenStateIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new Address("Street", "City", null!, "Country", "Zip"));
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenCountryIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new Address("Street", "City", "State", null!, "Zip"));
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenPostalCodeIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new Address("Street", "City", "State", "Country", null!));
	}

	[Fact]
	public void GetEqualityComponents_ReturnsAllFields()
	{
		// Arrange
		var address = new Address("123 Main", "City", "ST", "US", "12345");

		// Act
		var components = address.GetEqualityComponents().ToList();

		// Assert
		components.Count.ShouldBe(5);
		components[0].ShouldBe("123 Main");
		components[1].ShouldBe("City");
		components[2].ShouldBe("ST");
		components[3].ShouldBe("US");
		components[4].ShouldBe("12345");
	}

	[Fact]
	public void ToString_FormatsCorrectly()
	{
		// Arrange
		var address = new Address("123 Main St", "Springfield", "IL", "US", "62701");

		// Act & Assert
		address.ToString().ShouldBe("123 Main St, Springfield, IL 62701, US");
	}

	[Fact]
	public void Equality_SameValues_AreEqual()
	{
		var a1 = new Address("123 Main", "City", "ST", "US", "12345");
		var a2 = new Address("123 Main", "City", "ST", "US", "12345");

		a1.Equals(a2).ShouldBeTrue();
		(a1 == a2).ShouldBeTrue();
	}

	[Fact]
	public void Equality_DifferentValues_AreNotEqual()
	{
		var a1 = new Address("123 Main", "City", "ST", "US", "12345");
		var a2 = new Address("456 Elm", "City", "ST", "US", "12345");

		a1.Equals(a2).ShouldBeFalse();
		(a1 != a2).ShouldBeTrue();
	}

	[Fact]
	public void GetHashCode_SameForEqualAddresses()
	{
		var a1 = new Address("123 Main", "City", "ST", "US", "12345");
		var a2 = new Address("123 Main", "City", "ST", "US", "12345");

		a1.GetHashCode().ShouldBe(a2.GetHashCode());
	}
}
