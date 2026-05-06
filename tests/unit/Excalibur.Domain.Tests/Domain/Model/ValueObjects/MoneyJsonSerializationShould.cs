// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Excalibur.Domain.Model.ValueObjects;

namespace Excalibur.Tests.Domain.Model.ValueObjects;

/// <summary>
/// Tests verifying that the [JsonConstructor] on <see cref="Money"/> enables
/// correct System.Text.Json serialization round-trips including denomination and unitCount.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Domain")]
public sealed class MoneyJsonSerializationShould
{
	private static readonly JsonSerializerOptions Options = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
	};

	[Fact]
	public void RoundTrip_SimpleAmount()
	{
		// Arrange
		var original = new Money(99.95m, "USD");

		// Act
		var json = JsonSerializer.Serialize(original, Options);
		var deserialized = JsonSerializer.Deserialize<Money>(json, Options);

		// Assert
		deserialized.ShouldNotBeNull();
		deserialized.Amount.ShouldBe(99.95m);
		deserialized.CurrencyCode.ShouldBe("USD");
		deserialized.Denomination.ShouldBeNull();
		deserialized.UnitCount.ShouldBe(0);
	}

	[Fact]
	public void RoundTrip_WithDenomination()
	{
		// Arrange — 100 USD in 20-dollar bills = 5 units
		var original = new Money(100m, "USD", 20m);

		// Act
		var json = JsonSerializer.Serialize(original, Options);
		var deserialized = JsonSerializer.Deserialize<Money>(json, Options);

		// Assert
		deserialized.ShouldNotBeNull();
		deserialized.Amount.ShouldBe(100m);
		deserialized.CurrencyCode.ShouldBe("USD");
		deserialized.Denomination.ShouldBe(20m);
		deserialized.UnitCount.ShouldBe(5);
	}

	[Fact]
	public void RoundTrip_WithDifferentCurrency()
	{
		// Arrange
		var original = new Money(1500m, "EUR");

		// Act
		var json = JsonSerializer.Serialize(original, Options);
		var deserialized = JsonSerializer.Deserialize<Money>(json, Options);

		// Assert
		deserialized.ShouldNotBeNull();
		deserialized.Amount.ShouldBe(1500m);
		deserialized.CurrencyCode.ShouldBe("EUR");
	}

	[Fact]
	public void Deserialize_FromExplicitJson_WithDenomination()
	{
		// Arrange — simulates payload from an external system
		const string json = """
			{
				"amount": 200.0,
				"currencyCode": "GBP",
				"denomination": 50.0,
				"unitCount": 4
			}
			""";

		// Act
		var money = JsonSerializer.Deserialize<Money>(json, Options);

		// Assert — unitCount is recalculated from denomination when present
		money.ShouldNotBeNull();
		money.Amount.ShouldBe(200m);
		money.CurrencyCode.ShouldBe("GBP");
		money.Denomination.ShouldBe(50m);
		money.UnitCount.ShouldBe(4); // 200/50 = 4
	}

	[Fact]
	public void Deserialize_FromExplicitJson_WithoutDenomination()
	{
		// Arrange — no denomination means unitCount is passed through as-is
		const string json = """
			{
				"amount": 42.50,
				"currencyCode": "JPY",
				"denomination": null,
				"unitCount": 0
			}
			""";

		// Act
		var money = JsonSerializer.Deserialize<Money>(json, Options);

		// Assert
		money.ShouldNotBeNull();
		money.Amount.ShouldBe(42.50m);
		money.CurrencyCode.ShouldBe("JPY");
		money.Denomination.ShouldBeNull();
		money.UnitCount.ShouldBe(0);
	}

	[Fact]
	public void Deserialize_NullCurrencyCode_DefaultsToUsd()
	{
		// Arrange — the JsonConstructor handles null currencyCode by defaulting to USD
		const string json = """
			{
				"amount": 10.0,
				"currencyCode": null,
				"denomination": null,
				"unitCount": 0
			}
			""";

		// Act
		var money = JsonSerializer.Deserialize<Money>(json, Options);

		// Assert
		money.ShouldNotBeNull();
		money.CurrencyCode.ShouldBe("USD");
	}

	[Fact]
	public void RoundTrip_WithPascalCase()
	{
		// Arrange — default serialization (PascalCase)
		var original = new Money(250m, "CHF", 50m);
		var json = JsonSerializer.Serialize(original);

		// Act
		var deserialized = JsonSerializer.Deserialize<Money>(json);

		// Assert
		deserialized.ShouldNotBeNull();
		deserialized.Amount.ShouldBe(250m);
		deserialized.CurrencyCode.ShouldBe("CHF");
		deserialized.Denomination.ShouldBe(50m);
		deserialized.UnitCount.ShouldBe(5);
	}

	[Fact]
	public void RoundTrip_ZeroAmount()
	{
		// Arrange — boundary: zero amount
		var original = new Money(0m, "USD");

		// Act
		var json = JsonSerializer.Serialize(original, Options);
		var deserialized = JsonSerializer.Deserialize<Money>(json, Options);

		// Assert
		deserialized.ShouldNotBeNull();
		deserialized.Amount.ShouldBe(0m);
	}

	[Fact]
	public void RoundTrip_NegativeAmount()
	{
		// Arrange — edge case: negative (refund)
		var original = new Money(-50.25m, "USD");

		// Act
		var json = JsonSerializer.Serialize(original, Options);
		var deserialized = JsonSerializer.Deserialize<Money>(json, Options);

		// Assert
		deserialized.ShouldNotBeNull();
		deserialized.Amount.ShouldBe(-50.25m);
	}
}
