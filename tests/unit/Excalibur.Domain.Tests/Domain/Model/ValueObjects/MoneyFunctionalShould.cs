// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Domain.Model.ValueObjects;

namespace Excalibur.Tests.Domain.Model.ValueObjects;

[Trait("Category", "Unit")]
public class MoneyFunctionalShould
{
    [Fact]
    public void Arithmetic_Addition_ShouldWork()
    {
        var a = Money.USD(10.50m);
        var b = Money.USD(5.25m);

        var result = a + b;

        result.Amount.ShouldBe(15.75m);
        result.ISOCurrencyCode.ShouldBe("USD");
    }

    [Fact]
    public void Arithmetic_Subtraction_ShouldWork()
    {
        var a = Money.USD(100m);
        var b = Money.USD(30m);

        var result = a - b;

        result.Amount.ShouldBe(70m);
    }

    [Fact]
    public void Arithmetic_Multiplication_ShouldWork()
    {
        var money = Money.USD(25m);

        var result = money * 3;

        result.Amount.ShouldBe(75m);
    }

    [Fact]
    public void Arithmetic_MultiplicationReversed_ShouldWork()
    {
        var money = Money.USD(25m);

        var result = 3 * money;

        result.Amount.ShouldBe(75m);
    }

    [Fact]
    public void Arithmetic_Division_ShouldWork()
    {
        var money = Money.USD(100m);

        var result = money / 4;

        result.Amount.ShouldBe(25m);
    }

    [Fact]
    public void Arithmetic_DivideByZero_ShouldThrow()
    {
        var money = Money.USD(100m);

        Should.Throw<DivideByZeroException>(() => money / 0);
    }

    [Fact]
    public void MixedCurrency_Addition_ShouldThrow()
    {
        var usd = Money.USD(10m);
        var eur = Money.EUR(10m);

        Should.Throw<InvalidOperationException>(() => usd + eur);
    }

    [Fact]
    public void MixedCurrency_Comparison_ShouldThrow()
    {
        var usd = Money.USD(10m);
        var gbp = Money.GBP(10m);

        Should.Throw<InvalidOperationException>(() => _ = usd > gbp);
    }

    [Fact]
    public void Comparison_GreaterThan_ShouldWork()
    {
        var a = Money.USD(50m);
        var b = Money.USD(30m);

        (a > b).ShouldBeTrue();
        (b > a).ShouldBeFalse();
    }

    [Fact]
    public void Comparison_LessThan_ShouldWork()
    {
        var a = Money.USD(20m);
        var b = Money.USD(50m);

        (a < b).ShouldBeTrue();
        (b < a).ShouldBeFalse();
    }

    [Fact]
    public void Comparison_GreaterThanOrEqual_ShouldWork()
    {
        var a = Money.USD(50m);
        var b = Money.USD(50m);

        (a >= b).ShouldBeTrue();
        (a >= Money.USD(40m)).ShouldBeTrue();
    }

    [Fact]
    public void Comparison_LessThanOrEqual_ShouldWork()
    {
        var a = Money.USD(30m);
        var b = Money.USD(30m);

        (a <= b).ShouldBeTrue();
        (a <= Money.USD(40m)).ShouldBeTrue();
    }

    [Fact]
    public void CompareTo_ShouldReturnCorrectOrder()
    {
        var a = Money.USD(10m);
        var b = Money.USD(20m);
        var c = Money.USD(10m);

        a.CompareTo(b).ShouldBeLessThan(0);
        b.CompareTo(a).ShouldBeGreaterThan(0);
        a.CompareTo(c).ShouldBe(0);
    }

    [Fact]
    public void CompareTo_Null_ShouldReturnPositive()
    {
        var a = Money.USD(10m);
        a.CompareTo(null).ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Equality_SameAmountAndCurrency_ShouldBeEqual()
    {
        var a = Money.USD(42.50m);
        var b = Money.USD(42.50m);

        a.Equals(b).ShouldBeTrue();
        (a == b).ShouldBeTrue();
    }

    [Fact]
    public void Equality_DifferentAmount_ShouldNotBeEqual()
    {
        var a = Money.USD(42.50m);
        var b = Money.USD(42.51m);

        a.Equals(b).ShouldBeFalse();
        (a != b).ShouldBeTrue();
    }

    [Fact]
    public void FactoryMethods_ShouldCreateCorrectCurrencies()
    {
        Money.USD(100m).ISOCurrencyCode.ShouldBe("USD");
        Money.EUR(100m).ISOCurrencyCode.ShouldBe("EUR");
        Money.GBP(100m).ISOCurrencyCode.ShouldBe("GBP");
        Money.JPY(100m).ISOCurrencyCode.ShouldBe("JPY");
        Money.CHF(100m).ISOCurrencyCode.ShouldBe("CHF");
        Money.CAD(100m).ISOCurrencyCode.ShouldBe("CAD");
        Money.AUD(100m).ISOCurrencyCode.ShouldBe("AUD");
    }

    [Fact]
    public void From_String_ShouldParseCorrectly()
    {
        var money = Money.From("$100.50", "en-US");
        money.Amount.ShouldBe(100.50m);
    }

    [Fact]
    public void From_InvalidString_ShouldThrowFormatException()
    {
        Should.Throw<FormatException>(() => Money.From("not-a-number"));
    }

    [Fact]
    public void From_Decimal_ShouldWork()
    {
        var money = Money.From(99.99m, "en-US");
        money.Amount.ShouldBe(99.99m);
        money.ISOCurrencyCode.ShouldBe("USD");
    }

    [Fact]
    public void From_Double_ShouldWork()
    {
        var money = Money.From(49.99, "en-US");
        money.Amount.ShouldBe(49.99m);
    }

    [Fact]
    public void From_Float_ShouldWork()
    {
        var money = Money.From(29.99f, "en-US");
        money.Amount.ShouldBe(29.99m);
    }

    [Fact]
    public void From_Int_ShouldWork()
    {
        var money = Money.From(100, "en-US");
        money.Amount.ShouldBe(100m);
    }

    [Fact]
    public void From_Long_ShouldWork()
    {
        var money = Money.From(1000L, "en-US");
        money.Amount.ShouldBe(1000m);
    }

    [Fact]
    public void Denomination_ShouldCalculateUnitCount()
    {
        var money = new Money(100m, "en-US", 20m);

        money.Denomination.ShouldBe(20m);
        money.UnitCount.ShouldBe(5); // 100 / 20 = 5
        money.IsBillsOnly.ShouldBeTrue();
        money.IsCoinsOnly.ShouldBeFalse();
    }

    [Fact]
    public void Denomination_CoinDenomination_ShouldIdentifyAsCoins()
    {
        var money = new Money(2.50m, "en-US", 0.25m);

        money.Denomination.ShouldBe(0.25m);
        money.UnitCount.ShouldBe(10);
        money.IsCoinsOnly.ShouldBeTrue();
        money.IsBillsOnly.ShouldBeFalse();
    }

    [Fact]
    public void Denomination_ZeroDenomination_ShouldReturnZeroUnitCount()
    {
        var money = new Money(100m, "en-US", 0m);
        money.UnitCount.ShouldBe(0);
    }

    [Fact]
    public void ToString_ShouldFormatAsCurrency()
    {
        var money = Money.USD(1234.56m);
        var result = money.ToString();

        // Should contain the amount formatted as currency
        result.ShouldContain("1");
        result.ShouldContain("234");
    }

    [Fact]
    public void ToString_WithFormat_ShouldApplyFormat()
    {
        var money = Money.USD(1234.56m);
        var result = money.ToString("N2");

        result.ShouldContain("1");
        result.ShouldContain("234");
        result.ShouldContain("56");
    }

    [Fact]
    public void NullCultureName_ShouldDefaultToEnUS()
    {
        var money = new Money(50m, null);
        money.CultureName.ShouldBe("en-US");
        money.ISOCurrencyCode.ShouldBe("USD");
    }

    [Fact]
    public void StaticMethods_Add_ShouldWork()
    {
        var result = Money.Add(Money.USD(10m), Money.USD(20m));
        result.Amount.ShouldBe(30m);
    }

    [Fact]
    public void StaticMethods_Subtract_ShouldWork()
    {
        var result = Money.Subtract(Money.USD(50m), Money.USD(20m));
        result.Amount.ShouldBe(30m);
    }

    [Fact]
    public void StaticMethods_Multiply_ShouldWork()
    {
        var result = Money.Multiply(Money.USD(10m), 5m);
        result.Amount.ShouldBe(50m);
    }

    [Fact]
    public void StaticMethods_Divide_ShouldWork()
    {
        var result = Money.Divide(Money.USD(100m), 4m);
        result.Amount.ShouldBe(25m);
    }
}
