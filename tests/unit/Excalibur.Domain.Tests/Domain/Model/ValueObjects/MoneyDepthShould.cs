// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Domain.Model.ValueObjects;

namespace Excalibur.Tests.Domain.Model.ValueObjects;

/// <summary>
/// Depth coverage tests for <see cref="Money"/>.
/// Covers all operators, static factory methods, currency-specific factories, and edge cases.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Domain")]
public sealed class MoneyDepthShould
{
	[Fact]
	public void Constructor_WithAmount_SetsDefaults()
	{
		// Arrange & Act
		var money = new Money(100m);

		// Assert
		money.Amount.ShouldBe(100m);
		money.CultureName.ShouldBe("en-US");
		money.CurrencySymbol.ShouldBe("$");
		money.ISOCurrencyCode.ShouldBe("USD");
		money.Denomination.ShouldBeNull();
		money.UnitCount.ShouldBe(0);
	}

	[Fact]
	public void Constructor_WithNullCulture_DefaultsToEnUS()
	{
		// Arrange & Act
		var money = new Money(50m, null);

		// Assert
		money.CultureName.ShouldBe("en-US");
	}

	[Fact]
	public void Constructor_WithDenomination_CalculatesUnitCount()
	{
		// Arrange & Act
		var money = new Money(100m, "en-US", 20m);

		// Assert
		money.Denomination.ShouldBe(20m);
		money.UnitCount.ShouldBe(5); // 100 / 20
	}

	[Fact]
	public void Constructor_WithZeroDenomination_UnitCountIsZero()
	{
		// Arrange & Act
		var money = new Money(100m, "en-US", 0m);

		// Assert
		money.UnitCount.ShouldBe(0);
	}

	[Fact]
	public void IsBillsOnly_True_WhenDenominationGtEq1()
	{
		// Arrange
		var money = new Money(100m, "en-US", 5m);

		// Assert
		money.IsBillsOnly.ShouldBeTrue();
		money.IsCoinsOnly.ShouldBeFalse();
	}

	[Fact]
	public void IsCoinsOnly_True_WhenDenominationLt1()
	{
		// Arrange
		var money = new Money(5m, "en-US", 0.25m);

		// Assert
		money.IsCoinsOnly.ShouldBeTrue();
		money.IsBillsOnly.ShouldBeFalse();
	}

	[Fact]
	public void IsBillsOnly_False_WhenNoDenomination()
	{
		// Arrange
		var money = new Money(100m);

		// Assert
		money.IsBillsOnly.ShouldBeFalse();
		money.IsCoinsOnly.ShouldBeFalse();
	}

	// Static factory methods
	[Fact]
	public void From_Decimal_CreatesCorrectly()
	{
		var money = Money.From(99.99m);
		money.Amount.ShouldBe(99.99m);
	}

	[Fact]
	public void From_Double_CreatesCorrectly()
	{
		var money = Money.From(99.99);
		money.Amount.ShouldBe(99.99m);
	}

	[Fact]
	public void From_Float_CreatesCorrectly()
	{
		var money = Money.From(50.5f);
		money.Amount.ShouldBe(50.5m);
	}

	[Fact]
	public void From_Int_CreatesCorrectly()
	{
		var money = Money.From(42);
		money.Amount.ShouldBe(42m);
	}

	[Fact]
	public void From_Long_CreatesCorrectly()
	{
		var money = Money.From(1000L);
		money.Amount.ShouldBe(1000m);
	}

	[Fact]
	public void From_String_ParsesCorrectly()
	{
		var money = Money.From("$100.50", "en-US");
		money.Amount.ShouldBe(100.50m);
	}

	[Fact]
	public void From_String_ThrowsFormatException_WhenInvalid()
	{
		Should.Throw<FormatException>(() => Money.From("not-a-number"));
	}

	// Currency-specific factories
	[Fact]
	public void USD_CreatesUSDollar()
	{
		var money = Money.USD(50m);
		money.ISOCurrencyCode.ShouldBe("USD");
	}

	[Fact]
	public void EUR_CreatesEuro()
	{
		var money = Money.EUR(50m);
		money.ISOCurrencyCode.ShouldBe("EUR");
	}

	[Fact]
	public void GBP_CreatesBritishPound()
	{
		var money = Money.GBP(50m);
		money.ISOCurrencyCode.ShouldBe("GBP");
	}

	[Fact]
	public void JPY_CreatesYen()
	{
		var money = Money.JPY(1000m);
		money.ISOCurrencyCode.ShouldBe("JPY");
	}

	[Fact]
	public void CHF_CreatesSwissFranc()
	{
		var money = Money.CHF(75m);
		money.ISOCurrencyCode.ShouldBe("CHF");
	}

	[Fact]
	public void CAD_CreatesCanadianDollar()
	{
		var money = Money.CAD(60m);
		money.ISOCurrencyCode.ShouldBe("CAD");
	}

	[Fact]
	public void AUD_CreatesAustralianDollar()
	{
		var money = Money.AUD(80m);
		money.ISOCurrencyCode.ShouldBe("AUD");
	}

	// Operators
	[Fact]
	public void AddOperator_AddsTwoAmounts()
	{
		var result = Money.USD(50m) + Money.USD(30m);
		result.Amount.ShouldBe(80m);
	}

	[Fact]
	public void SubtractOperator_SubtractsAmounts()
	{
		var result = Money.USD(50m) - Money.USD(30m);
		result.Amount.ShouldBe(20m);
	}

	[Fact]
	public void MultiplyOperator_ScalesAmount()
	{
		var result = Money.USD(10m) * 3m;
		result.Amount.ShouldBe(30m);
	}

	[Fact]
	public void MultiplyOperator_Commutative()
	{
		var result = 3m * Money.USD(10m);
		result.Amount.ShouldBe(30m);
	}

	[Fact]
	public void DivideOperator_DividesAmount()
	{
		var result = Money.USD(100m) / 4m;
		result.Amount.ShouldBe(25m);
	}

	[Fact]
	public void DivideOperator_ThrowsDivideByZero()
	{
		Should.Throw<DivideByZeroException>(() => Money.USD(100m) / 0m);
	}

	[Fact]
	public void GreaterThan_ReturnsCorrectly()
	{
		(Money.USD(100m) > Money.USD(50m)).ShouldBeTrue();
		(Money.USD(50m) > Money.USD(100m)).ShouldBeFalse();
	}

	[Fact]
	public void LessThan_ReturnsCorrectly()
	{
		(Money.USD(50m) < Money.USD(100m)).ShouldBeTrue();
		(Money.USD(100m) < Money.USD(50m)).ShouldBeFalse();
	}

	[Fact]
	public void GreaterThanOrEqual_ReturnsCorrectly()
	{
		(Money.USD(100m) >= Money.USD(100m)).ShouldBeTrue();
		(Money.USD(100m) >= Money.USD(50m)).ShouldBeTrue();
		(Money.USD(50m) >= Money.USD(100m)).ShouldBeFalse();
	}

	[Fact]
	public void LessThanOrEqual_ReturnsCorrectly()
	{
		(Money.USD(100m) <= Money.USD(100m)).ShouldBeTrue();
		(Money.USD(50m) <= Money.USD(100m)).ShouldBeTrue();
		(Money.USD(100m) <= Money.USD(50m)).ShouldBeFalse();
	}

	[Fact]
	public void MixedCurrency_ThrowsInvalidOperationException()
	{
		Should.Throw<InvalidOperationException>(() => Money.USD(50m) + Money.EUR(30m));
	}

	// Static method alternatives
	[Fact]
	public void Add_StaticMethod_Works()
	{
		var result = Money.Add(Money.USD(10m), Money.USD(20m));
		result.Amount.ShouldBe(30m);
	}

	[Fact]
	public void Subtract_StaticMethod_Works()
	{
		var result = Money.Subtract(Money.USD(30m), Money.USD(10m));
		result.Amount.ShouldBe(20m);
	}

	[Fact]
	public void Multiply_StaticMethod_Works()
	{
		var result = Money.Multiply(Money.USD(10m), 5m);
		result.Amount.ShouldBe(50m);
	}

	[Fact]
	public void Divide_StaticMethod_Works()
	{
		var result = Money.Divide(Money.USD(100m), 5m);
		result.Amount.ShouldBe(20m);
	}

	// CompareTo
	[Fact]
	public void CompareTo_Null_ReturnsPositive()
	{
		Money.USD(100m).CompareTo(null).ShouldBeGreaterThan(0);
	}

	[Fact]
	public void CompareTo_Greater_ReturnsPositive()
	{
		Money.USD(100m).CompareTo(Money.USD(50m)).ShouldBeGreaterThan(0);
	}

	[Fact]
	public void CompareTo_Equal_ReturnsZero()
	{
		Money.USD(100m).CompareTo(Money.USD(100m)).ShouldBe(0);
	}

	[Fact]
	public void CompareTo_Less_ReturnsNegative()
	{
		Money.USD(50m).CompareTo(Money.USD(100m)).ShouldBeLessThan(0);
	}

	// ToString
	[Fact]
	public void ToString_FormatsWithCulture()
	{
		var money = Money.USD(1234.56m);
		money.ToString().ShouldContain("1,234.56");
	}

	[Fact]
	public void ToString_WithFormat_UsesFormat()
	{
		var money = Money.USD(1234.56m);
		var formatted = money.ToString("N2");
		formatted.ShouldContain("1,234.56");
	}

	// GetEqualityComponents
	[Fact]
	public void GetEqualityComponents_IncludesAmountCurrencyDenomination()
	{
		var money = new Money(100m, "en-US", 20m);
		var components = money.GetEqualityComponents().ToList();

		components.Count.ShouldBe(3);
		components[0].ShouldBe(100m);
		components[1].ShouldBe("USD");
		components[2].ShouldBe(20m);
	}

	[Fact]
	public void Equality_SameAmountAndCurrency_AreEqual()
	{
		var m1 = Money.USD(100m);
		var m2 = Money.USD(100m);

		m1.Equals(m2).ShouldBeTrue();
	}

	[Fact]
	public void Equality_DifferentAmount_AreNotEqual()
	{
		var m1 = Money.USD(100m);
		var m2 = Money.USD(200m);

		m1.Equals(m2).ShouldBeFalse();
	}
}
