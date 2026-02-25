using Excalibur.Domain.Model.ValueObjects;

namespace Excalibur.Tests.Domain.Model.ValueObjects;

[Trait("Category", "Unit")]
[Trait("Component", "Domain")]
public sealed class AddressExtendedShould
{
	[Fact]
	public void StoreAllProperties()
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
	public void ThrowOnNullStreet()
	{
		Should.Throw<ArgumentNullException>(() => new Address(null!, "City", "State", "Country", "12345"));
	}

	[Fact]
	public void ThrowOnNullCity()
	{
		Should.Throw<ArgumentNullException>(() => new Address("Street", null!, "State", "Country", "12345"));
	}

	[Fact]
	public void ThrowOnNullState()
	{
		Should.Throw<ArgumentNullException>(() => new Address("Street", "City", null!, "Country", "12345"));
	}

	[Fact]
	public void ThrowOnNullCountry()
	{
		Should.Throw<ArgumentNullException>(() => new Address("Street", "City", "State", null!, "12345"));
	}

	[Fact]
	public void ThrowOnNullPostalCode()
	{
		Should.Throw<ArgumentNullException>(() => new Address("Street", "City", "State", "Country", null!));
	}

	[Fact]
	public void ToString_ReturnsFormattedAddress()
	{
		// Arrange
		var address = new Address("123 Main St", "Springfield", "IL", "US", "62701");

		// Act
		var result = address.ToString();

		// Assert
		result.ShouldBe("123 Main St, Springfield, IL 62701, US");
	}

	[Fact]
	public void EqualAddresses_AreEqual()
	{
		// Arrange
		var a = new Address("123 Main St", "Springfield", "IL", "US", "62701");
		var b = new Address("123 Main St", "Springfield", "IL", "US", "62701");

		// Assert
		a.Equals(b).ShouldBeTrue();
		(a == b).ShouldBeTrue();
	}

	[Fact]
	public void DifferentAddresses_AreNotEqual()
	{
		// Arrange
		var a = new Address("123 Main St", "Springfield", "IL", "US", "62701");
		var b = new Address("456 Oak Ave", "Springfield", "IL", "US", "62701");

		// Assert
		a.Equals(b).ShouldBeFalse();
		(a != b).ShouldBeTrue();
	}

	[Fact]
	public void GetEqualityComponents_ReturnsAllFields()
	{
		// Arrange
		var address = new Address("123 Main St", "Springfield", "IL", "US", "62701");

		// Act
		var components = address.GetEqualityComponents().ToList();

		// Assert
		components.Count.ShouldBe(5);
		components.ShouldContain("123 Main St");
		components.ShouldContain("Springfield");
		components.ShouldContain("IL");
		components.ShouldContain("US");
		components.ShouldContain("62701");
	}

	[Fact]
	public void EqualAddresses_HaveSameHashCode()
	{
		// Arrange
		var a = new Address("123 Main St", "Springfield", "IL", "US", "62701");
		var b = new Address("123 Main St", "Springfield", "IL", "US", "62701");

		// Assert
		a.GetHashCode().ShouldBe(b.GetHashCode());
	}
}
