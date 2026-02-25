using Excalibur.Domain.Model.ValueObjects;

namespace Excalibur.Tests.Domain;

/// <summary>
/// Unit tests for Money value object.
/// </summary>
[Trait("Category", "Unit")]
public sealed class MoneyShould : UnitTestBase
{
	[Fact]
	public void Create_WithDefaults_UsesEnUsCulture()
	{
		// Arrange & Act
		var money = new Money(100m);

		// Assert
		money.Amount.ShouldBe(100m);
		money.CultureName.ShouldBe("en-US");
		money.CurrencySymbol.ShouldBe("$");
		money.ISOCurrencyCode.ShouldBe("USD");
	}

	[Fact]
	public void Create_WithGbpCulture_ReturnsCorrectSymbols()
	{
		// Arrange & Act
		var money = new Money(50m, "en-GB");

		// Assert
		money.Amount.ShouldBe(50m);
		money.CultureName.ShouldBe("en-GB");
		money.CurrencySymbol.ShouldBe("£");
		money.ISOCurrencyCode.ShouldBe("GBP");
	}

	[Fact]
	public void Create_WithDenomination_CalculatesUnitCount()
	{
		// Arrange & Act
		var money = new Money(100m, "en-US", 20m);

		// Assert
		money.Amount.ShouldBe(100m);
		money.Denomination.ShouldBe(20m);
		money.UnitCount.ShouldBe(5);
		money.IsBillsOnly.ShouldBeTrue();
		money.IsCoinsOnly.ShouldBeFalse();
	}

	[Fact]
	public void From_Decimal_CreatesCorrectMoney()
	{
		// Arrange & Act
		var money = Money.From(99.99m);

		// Assert
		money.Amount.ShouldBe(99.99m);
		money.CultureName.ShouldBe("en-US");
	}

	[Fact]
	public void From_Integer_CreatesCorrectMoney()
	{
		// Arrange & Act
		var money = Money.From(50);

		// Assert
		money.Amount.ShouldBe(50m);
	}

	[Fact]
	public void From_String_ParsesCorrectly()
	{
		// Arrange & Act
		var money = Money.From("$1,234.56", "en-US");

		// Assert
		money.Amount.ShouldBe(1234.56m);
	}

	[Fact]
	public void Addition_SameCurrency_ReturnsSummedValue()
	{
		// Arrange
		var left = new Money(100m);
		var right = new Money(50m);

		// Act
		var result = left + right;

		// Assert
		result.Amount.ShouldBe(150m);
	}

	[Fact]
	public void Subtraction_SameCurrency_ReturnsDifference()
	{
		// Arrange
		var left = new Money(100m);
		var right = new Money(30m);

		// Act
		var result = left - right;

		// Assert
		result.Amount.ShouldBe(70m);
	}

	[Fact]
	public void Multiplication_ByScalar_ReturnsProduct()
	{
		// Arrange
		var money = new Money(25m);

		// Act
		var result = money * 4;

		// Assert
		result.Amount.ShouldBe(100m);
	}

	[Fact]
	public void Division_ByScalar_ReturnsQuotient()
	{
		// Arrange
		var money = new Money(100m);

		// Act
		var result = money / 4;

		// Assert
		result.Amount.ShouldBe(25m);
	}

	[Fact]
	public void Comparison_GreaterThan_ReturnsCorrectResult()
	{
		// Arrange
		var left = new Money(100m);
		var right = new Money(50m);

		// Act & Assert
		(left > right).ShouldBeTrue();
		(right > left).ShouldBeFalse();
	}

	[Fact]
	public void CompareTo_ReturnsCorrectOrdering()
	{
		// Arrange
		var smaller = new Money(50m);
		var larger = new Money(100m);

		// Act & Assert
		smaller.CompareTo(larger).ShouldBeLessThan(0);
		larger.CompareTo(smaller).ShouldBeGreaterThan(0);
		smaller.CompareTo(new Money(50m)).ShouldBe(0);
	}

	[Fact]
	public void ToString_ReturnsFormattedCurrency()
	{
		// Arrange
		var money = new Money(1234.56m);

		// Act
		var result = money.ToString();

		// Assert
		result.ShouldContain("$");
		result.ShouldContain("1,234.56");
	}

	#region T419.10: Edge Cases

	[Fact]
	public void Division_ByZero_ThrowsDivideByZeroException()
	{
		// Arrange
		var money = new Money(100m);

		// Act & Assert
		_ = Should.Throw<DivideByZeroException>(() => money / 0);
	}

	[Fact]
	public void Addition_DifferentCurrencies_ThrowsInvalidOperationException()
	{
		// Arrange
		var usd = new Money(100m, "en-US");
		var gbp = new Money(50m, "en-GB");

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => usd + gbp);
		exception.Message.ShouldContain("USD");
		exception.Message.ShouldContain("GBP");
	}

	[Fact]
	public void Subtraction_DifferentCurrencies_ThrowsInvalidOperationException()
	{
		// Arrange
		var usd = new Money(100m, "en-US");
		var eur = new Money(50m, "de-DE");

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() => usd - eur);
	}

	[Fact]
	public void GreaterThan_DifferentCurrencies_ThrowsInvalidOperationException()
	{
		// Arrange
		var usd = new Money(100m, "en-US");
		var gbp = new Money(50m, "en-GB");

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() => _ = usd > gbp);
	}

	[Fact]
	public void LessThan_DifferentCurrencies_ThrowsInvalidOperationException()
	{
		// Arrange
		var usd = new Money(100m, "en-US");
		var gbp = new Money(50m, "en-GB");

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() => _ = usd < gbp);
	}

	[Fact]
	public void CompareTo_DifferentCurrencies_ThrowsInvalidOperationException()
	{
		// Arrange
		var usd = new Money(100m, "en-US");
		var gbp = new Money(50m, "en-GB");

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() => usd.CompareTo(gbp));
	}

	[Fact]
	public void CompareTo_Null_ReturnsPositive()
	{
		// Arrange
		var money = new Money(100m);

		// Act
		var result = money.CompareTo(null);

		// Assert
		result.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void From_InvalidString_ThrowsFormatException()
	{
		// Arrange & Act & Assert
		_ = Should.Throw<FormatException>(() => Money.From("not-a-number", "en-US"));
	}

	[Fact]
	public void Create_WithZeroDenomination_SetsUnitCountToZero()
	{
		// Arrange & Act
		var money = new Money(100m, "en-US", 0m);

		// Assert
		money.UnitCount.ShouldBe(0);
	}

	[Fact]
	public void IsCoinsOnly_WithCoinDenomination_ReturnsTrue()
	{
		// Arrange
		var money = new Money(5m, "en-US", 0.25m);

		// Assert
		money.IsCoinsOnly.ShouldBeTrue();
		money.IsBillsOnly.ShouldBeFalse();
		money.UnitCount.ShouldBe(20);
	}

	[Fact]
	public void Multiplication_ScalarOnLeft_ReturnsProduct()
	{
		// Arrange
		var money = new Money(25m);

		// Act
		var result = 4m * money;

		// Assert
		result.Amount.ShouldBe(100m);
	}

	[Fact]
	public void ToString_WithFormat_ReturnsFormattedValue()
	{
		// Arrange
		var money = new Money(1234.567m);

		// Act
		var result = money.ToString("N2");

		// Assert
		result.ShouldContain("1,234.57");
	}

	[Fact]
	public void GetEqualityComponents_IncludesAllComponents()
	{
		// Arrange
		var money = new Money(100m, "en-US", 20m);

		// Act
		var components = money.GetEqualityComponents().ToList();

		// Assert
		components.Count.ShouldBe(3);
		components.ShouldContain(100m);
		components.ShouldContain("USD");
		components.ShouldContain(20m);
	}

	[Fact]
	public void GreaterThanOrEqual_SameCurrency_ReturnsCorrectResult()
	{
		// Arrange
		var left = new Money(100m);
		var right = new Money(100m);

		// Act & Assert
		(left >= right).ShouldBeTrue();
	}

	[Fact]
	public void LessThanOrEqual_SameCurrency_ReturnsCorrectResult()
	{
		// Arrange
		var left = new Money(50m);
		var right = new Money(100m);

		// Act & Assert
		(left <= right).ShouldBeTrue();
	}

	[Fact]
	public void StaticAdd_ReturnsSum()
	{
		// Arrange
		var left = new Money(100m);
		var right = new Money(50m);

		// Act
		var result = Money.Add(left, right);

		// Assert
		result.Amount.ShouldBe(150m);
	}

	[Fact]
	public void StaticSubtract_ReturnsDifference()
	{
		// Arrange
		var left = new Money(100m);
		var right = new Money(30m);

		// Act
		var result = Money.Subtract(left, right);

		// Assert
		result.Amount.ShouldBe(70m);
	}

	[Fact]
	public void StaticMultiply_ReturnsProduct()
	{
		// Arrange
		var money = new Money(25m);

		// Act
		var result = Money.Multiply(money, 4m);

		// Assert
		result.Amount.ShouldBe(100m);
	}

	[Fact]
	public void StaticDivide_ReturnsQuotient()
	{
		// Arrange
		var money = new Money(100m);

		// Act
		var result = Money.Divide(money, 4m);

		// Assert
		result.Amount.ShouldBe(25m);
	}

	[Fact]
	public void From_Double_CreatesCorrectMoney()
	{
		// Arrange & Act
		var money = Money.From(99.99);

		// Assert
		money.Amount.ShouldBe(99.99m);
	}

	[Fact]
	public void From_Float_CreatesCorrectMoney()
	{
		// Arrange & Act
		var money = Money.From(99.99f);

		// Assert
		money.Amount.ShouldBeInRange(99.98m, 100.00m); // Float precision
	}

	[Fact]
	public void From_Long_CreatesCorrectMoney()
	{
		// Arrange & Act
		var money = Money.From(1000000L);

		// Assert
		money.Amount.ShouldBe(1000000m);
	}

	[Fact]
	public void Create_WithNullCultureName_UsesDefault()
	{
		// Arrange & Act
		var money = new Money(100m, null);

		// Assert
		money.CultureName.ShouldBe("en-US");
	}

	#endregion T419.10: Edge Cases
}
