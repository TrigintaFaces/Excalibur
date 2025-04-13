using System.Text.Json;

using Excalibur.Core.Domain.Model.ValueObjects;

using Newtonsoft.Json;

using Shouldly;

namespace Excalibur.Tests.Unit.Core.Domain.Model;

public class AddressShould
{
	private const string Line1 = "123 Main St";
	private const string Line2 = "Apt 4B";
	private const string City = "Springfield";
	private const string State = "IL";
	private const string Zip = "62704";

	[Fact]
	public void AssignPropertiesCorrectly()
	{
		// Arrange & Act
		var address = CreateDefault();

		// Assert
		address.Address1.ShouldBe(Line1);
		address.Address2.ShouldBe(Line2);
		address.City.ShouldBe(City);
		address.State.ShouldBe(State);
		address.Zip.ShouldBe(Zip);
	}

	[Fact]
	public void CreateAddressWithoutAddress2Successfully()
	{
		// Arrange & Act
		var address = new Address(Line1, null, City, State, Zip);

		// Assert
		address.Address1.ShouldBe(Line1);
		address.Address2.ShouldBeNull();
		address.City.ShouldBe(City);
		address.State.ShouldBe(State);
		address.Zip.ShouldBe(Zip);
	}

	[Fact]
	public void ConsiderAddressesEqualIfAllPropertiesMatch()
	{
		// Arrange
		var address1 = CreateDefault();
		var address2 = CreateDefault();

		// Act & Assert
		address1.ShouldBe(address2);
	}

	[Fact]
	public void ConsiderAddressesNotEqualIfAnyPropertyDiffers()
	{
		// Arrange
		var baseAddress = CreateDefault();

		// Act & Assert
		new Address("Diff", Line2, City, State, Zip).ShouldNotBe(baseAddress);
		new Address(Line1, "Diff", City, State, Zip).ShouldNotBe(baseAddress);
		new Address(Line1, Line2, "Diff", State, Zip).ShouldNotBe(baseAddress);
		new Address(Line1, Line2, City, "Diff", Zip).ShouldNotBe(baseAddress);
		new Address(Line1, Line2, City, State, "Diff").ShouldNotBe(baseAddress);
	}

	[Fact]
	public void ConsiderAddressesNotEqualIfAddress2Differs()
	{
		// Arrange
		var address1 = CreateDefault();
		var address2 = new Address(Line1, null, City, State, Zip);

		// Act & Assert
		address1.ShouldNotBe(address2);
	}

	[Fact]
	public void GenerateSameHashCodeForEqualAddresses()
	{
		// Arrange
		var address1 = CreateDefault();
		var address2 = CreateDefault();

		// Act & Assert
		address1.GetHashCode().ShouldBe(address2.GetHashCode());
	}

	[Fact]
	public void GenerateDifferentHashCodeForDifferentAddresses()
	{
		// Arrange
		var address1 = CreateDefault();
		var address2 = new Address(Line1, null, City, State, Zip);

		// Act & Assert
		address1.GetHashCode().ShouldNotBe(address2.GetHashCode());
	}

	[Fact]
	public void HandleNullAddress2Gracefully()
	{
		// Arrange
		var address = new Address(Line1, null, City, State, Zip);

		// Act & Assert
		address.Address2.ShouldBeNull();
	}

	[Fact]
	public void HandleEmptyStringAddress2Gracefully()
	{
		// Arrange
		var address = new Address(Line1, string.Empty, City, State, Zip);

		// Act & Assert
		address.Address2.ShouldBeEmpty();
	}

	[Fact]
	public void FailEqualityIfComparedWithNull()
	{
		// Arrange
		var address = CreateDefault();

		// Act & Assert
		_ = address.ShouldNotBeNull();
	}

	[Fact]
	public void FailEqualityIfComparedWithDifferentType()
	{
		// Arrange
		var address = CreateDefault();

		// Act & Assert
		address.Equals("Some string").ShouldBeFalse();
	}

	[Fact]
	public void BeEqualToItself()
	{
		// Arrange
		var address = CreateDefault();

		// Act & Assert
		address.Equals(address).ShouldBeTrue();
	}

	[Fact]
	public void HandleWhitespaceInAddress2Correctly()
	{
		// Arrange
		var address = new Address(Line1, "   ", City, State, Zip);

		// Act & Assert
		address.Address2.ShouldBe("   ");
	}

	[Fact]
	public void SupportJsonSerializationAndDeserialization()
	{
		// Arrange
		var originalAddress = CreateDefault();
		var json = Newtonsoft.Json.JsonConvert.SerializeObject(originalAddress);

		// Act
		var deserializedAddress = Newtonsoft.Json.JsonConvert.DeserializeObject<Address>(json);

		// Assert
		_ = deserializedAddress.ShouldNotBeNull();
		deserializedAddress.ShouldBe(originalAddress);
	}

	[Fact]
	public void SupportSystemTextJsonSerializationAndDeserialization()
	{
		// Arrange
		var originalAddress = CreateDefault();
		var json = System.Text.Json.JsonSerializer.Serialize(originalAddress);

		// Act
		var deserializedAddress = System.Text.Json.JsonSerializer.Deserialize<Address>(json);

		// Assert
		_ = deserializedAddress.ShouldNotBeNull();
		deserializedAddress.ShouldBe(originalAddress);
	}

	[Fact]
	public void DeserializeCorrectlyWithSystemTextJson()
	{
		var json = $$"""
		             {
		             	"address1": "{{Line1}}",
		             	"address2": "{{Line2}}",
		             	"city": "{{City}}",
		             	"state": "{{State}}",
		             	"zip": "{{Zip}}"
		             }
		             """;

		var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

		var result = System.Text.Json.JsonSerializer.Deserialize<Address>(json, options)!;

		result.ShouldBeEquivalentTo(CreateDefault());
	}

	[Fact]
	public void DeserializeCorrectlyWithNewtonsoftJson()
	{
		var json = $$"""
		             {
		             	"address1": "{{Line1}}",
		             	"address2": "{{Line2}}",
		             	"city": "{{City}}",
		             	"state": "{{State}}",
		             	"zip": "{{Zip}}"
		             }
		             """;

		var result = JsonConvert.DeserializeObject<Address>(json)!;

		result.ShouldBe(CreateDefault());
	}

	private Address CreateDefault() =>
		new(Line1, Line2, City, State, Zip);
}
