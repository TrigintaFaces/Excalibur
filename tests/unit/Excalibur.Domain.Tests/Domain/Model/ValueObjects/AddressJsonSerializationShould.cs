// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Excalibur.Domain.Model.ValueObjects;

namespace Excalibur.Tests.Domain.Model.ValueObjects;

/// <summary>
/// Tests verifying that the [JsonConstructor] attribute on <see cref="Address"/> enables
/// correct System.Text.Json serialization round-trips.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Domain")]
public sealed class AddressJsonSerializationShould
{
	private static readonly JsonSerializerOptions Options = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
	};

	[Fact]
	public void RoundTrip_PreservesAllProperties()
	{
		// Arrange
		var original = new Address("123 Main St", "Springfield", "IL", "USA", "62701");

		// Act
		var json = JsonSerializer.Serialize(original, Options);
		var deserialized = JsonSerializer.Deserialize<Address>(json, Options);

		// Assert
		deserialized.ShouldNotBeNull();
		deserialized.Street.ShouldBe("123 Main St");
		deserialized.City.ShouldBe("Springfield");
		deserialized.State.ShouldBe("IL");
		deserialized.Country.ShouldBe("USA");
		deserialized.PostalCode.ShouldBe("62701");
	}

	[Fact]
	public void Deserialize_FromExplicitJson()
	{
		// Arrange — simulates payload from an external system
		const string json = """
			{
				"street": "456 Oak Ave",
				"city": "Portland",
				"state": "OR",
				"country": "USA",
				"postalCode": "97201"
			}
			""";

		// Act
		var address = JsonSerializer.Deserialize<Address>(json, Options);

		// Assert
		address.ShouldNotBeNull();
		address.Street.ShouldBe("456 Oak Ave");
		address.City.ShouldBe("Portland");
		address.State.ShouldBe("OR");
		address.Country.ShouldBe("USA");
		address.PostalCode.ShouldBe("97201");
	}

	[Fact]
	public void Deserialize_WithPascalCaseJson()
	{
		// Arrange — default (non-camelCase) serialization
		var original = new Address("789 Elm Blvd", "Denver", "CO", "USA", "80202");
		var json = JsonSerializer.Serialize(original); // PascalCase by default

		// Act
		var deserialized = JsonSerializer.Deserialize<Address>(json);

		// Assert
		deserialized.ShouldNotBeNull();
		deserialized.Street.ShouldBe("789 Elm Blvd");
		deserialized.City.ShouldBe("Denver");
		deserialized.State.ShouldBe("CO");
		deserialized.Country.ShouldBe("USA");
		deserialized.PostalCode.ShouldBe("80202");
	}

	[Fact]
	public void RoundTrip_WithSpecialCharacters()
	{
		// Arrange — edge case: unicode, accented characters
		var original = new Address("Straße 42", "München", "Bayern", "Deutschland", "80331");

		// Act
		var json = JsonSerializer.Serialize(original, Options);
		var deserialized = JsonSerializer.Deserialize<Address>(json, Options);

		// Assert
		deserialized.ShouldNotBeNull();
		deserialized.Street.ShouldBe("Straße 42");
		deserialized.City.ShouldBe("München");
	}
}
