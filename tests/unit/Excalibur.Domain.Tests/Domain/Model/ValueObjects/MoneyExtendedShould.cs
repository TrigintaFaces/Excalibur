using Excalibur.Domain.Model.ValueObjects;

namespace Excalibur.Tests.Domain.Model.ValueObjects;

[Trait("Category", "Unit")]
[Trait("Component", "Domain")]
public sealed class MoneyExtendedShould
{
	[Fact]
	public void CreateFromDecimal()
	{
		// Act
		var money = Money.From(100.50m);

		// Assert
		money.Amount.ShouldBe(100.50m);
		money.ISOCurrencyCode.ShouldBe("USD");
	}

	[Fact]
	public void CreateFromDouble()
	{
		// Act
		var money = Money.From(99.99);

		// Assert
		money.Amount.ShouldBe(99.99m);
	}

	[Fact]
	public void CreateFromFloat()
	{
		// Act
		var money = Money.From(42.5f);

		// Assert
		money.Amount.ShouldBe(42.5m);
	}

	[Fact]
	public void CreateFromInt()
	{
		// Act
		var money = Money.From(100);

		// Assert
		money.Amount.ShouldBe(100m);
	}

	[Fact]
	public void CreateFromLong()
	{
		// Act
		var money = Money.From(1000L);

		// Assert
		money.Amount.ShouldBe(1000m);
	}

	[Fact]
	public void CreateFromString()
	{
		// Act
		var money = Money.From("$99.99", "en-US");

		// Assert
		money.Amount.ShouldBe(99.99m);
	}

	[Fact]
	public void ThrowFormatException_WhenStringIsInvalid()
	{
		// Act & Assert
		Should.Throw<FormatException>(() => Money.From("not-a-number", "en-US"));
	}

	[Fact]
	public void CreateUSD()
	{
		// Act
		var money = Money.USD(50m);

		// Assert
		money.ISOCurrencyCode.ShouldBe("USD");
		money.Amount.ShouldBe(50m);
	}

	[Fact]
	public void CreateEUR()
	{
		// Act
		var money = Money.EUR(50m);

		// Assert
		money.ISOCurrencyCode.ShouldBe("EUR");
	}

	[Fact]
	public void CreateGBP()
	{
		// Act
		var money = Money.GBP(50m);

		// Assert
		money.ISOCurrencyCode.ShouldBe("GBP");
	}

	[Fact]
	public void CreateJPY()
	{
		// Act
		var money = Money.JPY(1000m);

		// Assert
		money.ISOCurrencyCode.ShouldBe("JPY");
	}

	[Fact]
	public void CreateCHF()
	{
		// Act
		var money = Money.CHF(50m);

		// Assert
		money.ISOCurrencyCode.ShouldBe("CHF");
	}

	[Fact]
	public void CreateCAD()
	{
		// Act
		var money = Money.CAD(50m);

		// Assert
		money.ISOCurrencyCode.ShouldBe("CAD");
	}

	[Fact]
	public void CreateAUD()
	{
		// Act
		var money = Money.AUD(50m);

		// Assert
		money.ISOCurrencyCode.ShouldBe("AUD");
	}

	[Fact]
	public void AddMoneyOfSameCurrency()
	{
		// Arrange
		var a = Money.USD(10m);
		var b = Money.USD(20m);

		// Act
		var result = a + b;

		// Assert
		result.Amount.ShouldBe(30m);
	}

	[Fact]
	public void SubtractMoneyOfSameCurrency()
	{
		// Arrange
		var a = Money.USD(30m);
		var b = Money.USD(10m);

		// Act
		var result = a - b;

		// Assert
		result.Amount.ShouldBe(20m);
	}

	[Fact]
	public void MultiplyByScalar()
	{
		// Arrange
		var money = Money.USD(10m);

		// Act
		var result = money * 3m;

		// Assert
		result.Amount.ShouldBe(30m);
	}

	[Fact]
	public void MultiplyScalarByMoney()
	{
		// Arrange
		var money = Money.USD(10m);

		// Act
		var result = 3m * money;

		// Assert
		result.Amount.ShouldBe(30m);
	}

	[Fact]
	public void DivideByScalar()
	{
		// Arrange
		var money = Money.USD(30m);

		// Act
		var result = money / 3m;

		// Assert
		result.Amount.ShouldBe(10m);
	}

	[Fact]
	public void ThrowDivideByZero()
	{
		// Arrange
		var money = Money.USD(30m);

		// Act & Assert
		Should.Throw<DivideByZeroException>(() => money / 0m);
	}

	[Fact]
	public void CompareGreaterThan()
	{
		// Arrange
		var a = Money.USD(20m);
		var b = Money.USD(10m);

		// Assert
		(a > b).ShouldBeTrue();
		(b > a).ShouldBeFalse();
	}

	[Fact]
	public void CompareLessThan()
	{
		// Arrange
		var a = Money.USD(10m);
		var b = Money.USD(20m);

		// Assert
		(a < b).ShouldBeTrue();
		(b < a).ShouldBeFalse();
	}

	[Fact]
	public void CompareGreaterThanOrEqual()
	{
		// Arrange
		var a = Money.USD(20m);
		var b = Money.USD(20m);

		// Assert
		(a >= b).ShouldBeTrue();
	}

	[Fact]
	public void CompareLessThanOrEqual()
	{
		// Arrange
		var a = Money.USD(20m);
		var b = Money.USD(20m);

		// Assert
		(a <= b).ShouldBeTrue();
	}

	[Fact]
	public void ThrowOnDifferentCurrencyAddition()
	{
		// Arrange
		var usd = Money.USD(10m);
		var eur = Money.EUR(10m);

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => usd + eur);
	}

	[Fact]
	public void ThrowOnDifferentCurrencySubtraction()
	{
		// Arrange
		var usd = Money.USD(10m);
		var eur = Money.EUR(10m);

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => usd - eur);
	}

	[Fact]
	public void ThrowOnDifferentCurrencyComparison()
	{
		// Arrange
		var usd = Money.USD(10m);
		var eur = Money.EUR(10m);

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => _ = usd > eur);
	}

	[Fact]
	public void CompareTo_ReturnsPositive_WhenOtherIsNull()
	{
		// Arrange
		var money = Money.USD(10m);

		// Act
		var result = money.CompareTo(null);

		// Assert
		result.ShouldBe(1);
	}

	[Fact]
	public void CompareTo_ReturnsZero_WhenEqual()
	{
		// Arrange
		var a = Money.USD(10m);
		var b = Money.USD(10m);

		// Act & Assert
		a.CompareTo(b).ShouldBe(0);
	}

	[Fact]
	public void StaticAdd_DelegatesToOperator()
	{
		// Arrange
		var a = Money.USD(10m);
		var b = Money.USD(20m);

		// Act
		var result = Money.Add(a, b);

		// Assert
		result.Amount.ShouldBe(30m);
	}

	[Fact]
	public void StaticSubtract_DelegatesToOperator()
	{
		// Arrange
		var a = Money.USD(30m);
		var b = Money.USD(10m);

		// Act
		var result = Money.Subtract(a, b);

		// Assert
		result.Amount.ShouldBe(20m);
	}

	[Fact]
	public void StaticMultiply_DelegatesToOperator()
	{
		// Act
		var result = Money.Multiply(Money.USD(10m), 5m);

		// Assert
		result.Amount.ShouldBe(50m);
	}

	[Fact]
	public void StaticDivide_DelegatesToOperator()
	{
		// Act
		var result = Money.Divide(Money.USD(50m), 5m);

		// Assert
		result.Amount.ShouldBe(10m);
	}

	[Fact]
	public void Denomination_WhenSet_CalculatesUnitCount()
	{
		// Arrange & Act
		var money = new Money(100m, "en-US", 20m);

		// Assert
		money.Denomination.ShouldBe(20m);
		money.UnitCount.ShouldBe(5);
		money.IsBillsOnly.ShouldBeTrue();
		money.IsCoinsOnly.ShouldBeFalse();
	}

	[Fact]
	public void Denomination_WhenCoin_IsCoinsOnly()
	{
		// Arrange & Act
		var money = new Money(0.50m, "en-US", 0.25m);

		// Assert
		money.Denomination.ShouldBe(0.25m);
		money.UnitCount.ShouldBe(2);
		money.IsBillsOnly.ShouldBeFalse();
		money.IsCoinsOnly.ShouldBeTrue();
	}

	[Fact]
	public void Denomination_Zero_UnitCountIsZero()
	{
		// Arrange & Act
		var money = new Money(100m, "en-US", 0m);

		// Assert
		money.UnitCount.ShouldBe(0);
	}

	[Fact]
	public void NullCultureName_DefaultsToEnUS()
	{
		// Act
		var money = new Money(10m, null);

		// Assert
		money.CultureName.ShouldBe("en-US");
		money.ISOCurrencyCode.ShouldBe("USD");
	}

	[Fact]
	public void ToString_ReturnsFormattedAmount()
	{
		// Arrange
		var money = Money.USD(1234.56m);

		// Act
		var result = money.ToString();

		// Assert
		result.ShouldNotBeNullOrEmpty();
		result.ShouldContain("1");
	}

	[Fact]
	public void ToString_WithFormat_ReturnsFormattedAmount()
	{
		// Arrange
		var money = Money.USD(1234.56m);

		// Act
		var result = money.ToString("N2");

		// Assert
		result.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public void GetEqualityComponents_IncludesAmountCurrencyAndDenomination()
	{
		// Arrange
		var money = Money.USD(100m);

		// Act
		var components = money.GetEqualityComponents().ToList();

		// Assert
		components.ShouldContain(100m);
		components.ShouldContain("USD");
	}

	[Fact]
	public void EqualMoney_HasSameHashCode()
	{
		// Arrange
		var a = Money.USD(100m);
		var b = Money.USD(100m);

		// Assert
		a.GetHashCode().ShouldBe(b.GetHashCode());
	}

	[Fact]
	public void DifferentMoney_HasDifferentHashCode()
	{
		// Arrange
		var a = Money.USD(100m);
		var b = Money.EUR(100m);

		// Assert
		a.GetHashCode().ShouldNotBe(b.GetHashCode());
	}
}
