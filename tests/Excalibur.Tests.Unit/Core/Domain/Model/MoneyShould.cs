using System.Globalization;

using Excalibur.Core.Domain.Model.ValueObjects;

using Shouldly;

namespace Excalibur.Tests.Unit.Core.Domain.Model;

public class MoneyShould
{
	private const string UsCulture = "en-US";
	private const string FrCulture = "fr-FR";

	#region Construction

	[Fact]
	public void ConstructWithAmountAndCulture()
	{
		var money = new Money(100.50m, UsCulture);

		money.Amount.ShouldBe(100.50m);
		money.CultureName.ShouldBe(UsCulture);
		money.CurrencySymbol.ShouldBe(CultureInfo.GetCultureInfo(UsCulture).NumberFormat.CurrencySymbol);
	}

	[Fact]
	public void ConstructWithDefaultCulture()
	{
		var money = new Money(99m);

		money.Amount.ShouldBe(99m);
		money.CultureName.ShouldBe(UsCulture);
	}

	[Fact]
	public void ConstructWithCultureInfo()
	{
		var culture = new CultureInfo(FrCulture);
		var money = new Money(50m, culture);

		money.CultureName.ShouldBe(FrCulture);
		money.CurrencySymbol.ShouldBe(culture.NumberFormat.CurrencySymbol);
	}

	[Fact]
	public void ConstructWithValidDenomination()
	{
		var money = new Money(20m, UsCulture, 10);

		money.Denomination.ShouldBe(10);
		money.UnitCount.ShouldBe(2);
		money.IsBillsOnly.ShouldBe(true);
		money.IsCoinsOnly.ShouldBe(false);
	}

	[Fact]
	public void ConstructorWithValidConstraintsDoesNotThrow()
	{
		var m = new Money(100m, UsCulture, 20m);
		m.Amount.ShouldBe(100m);
	}

	#endregion Construction

	#region DenominationValidation

	[Theory]
	[InlineData(105.5, 5)]
	[InlineData(5.5, 1)]
	[InlineData(5.5, 0)]
	[InlineData(5.5, -2)]
	public void ThrowForInvalidDenomination(decimal amount, decimal denomination)
	{
		_ = Should.Throw<InvalidOperationException>(() => new Money(amount, UsCulture, denomination));
	}

	[Fact]
	public void ThrowForZeroOrNegativeDenomination()
	{
		_ = Should.Throw<InvalidOperationException>(() => new Money(100m, UsCulture, 0));
		_ = Should.Throw<InvalidOperationException>(() => new Money(100m, UsCulture, -5));
	}

	[Fact]
	public void AddMoneyWithMatchingDenominationUsesIt()
	{
		var m1 = new Money(20m, UsCulture, 10m);
		var m2 = new Money(30m, UsCulture, 10m);

		var result = m1 + m2;

		result.Denomination.ShouldBe(10m);
	}

	[Fact]
	public void AddMoneyWithMismatchedDenominationDropsIt()
	{
		var m1 = new Money(20m, UsCulture, 10m);
		var m2 = new Money(30m, UsCulture, 5m);

		var result = m1 + m2;

		result.Denomination.ShouldBeNull();
	}

	[Fact]
	public void DenominationWithWholeNumberResultsInIsBillsOnlyTrue()
	{
		var m = new Money(100m, UsCulture, 20m);
		m.IsBillsOnly.ShouldBe(true);
		m.IsCoinsOnly.ShouldBe(false);
	}

	#endregion DenominationValidation

	#region FactoryMethods

	[Fact]
	public void ZeroShouldReturnMoneyWithZeroAmountAndDefaultCulture()
	{
#pragma warning disable CA1304 // Specify CultureInfo
		var zero = Money.Zero();
#pragma warning restore CA1304 // Specify CultureInfo

		zero.Amount.ShouldBe(0);
		zero.CultureName.ShouldBe(UsCulture);
	}

	[Fact]
	public void ZeroWithCultureInfoCreatesMoneyWithZeroAmount()
	{
		var culture = new CultureInfo(FrCulture);
		var money = Money.Zero(culture);

		money.Amount.ShouldBe(0m);
		money.CultureName.ShouldBe(FrCulture);
	}

	[Fact]
	public void ZeroShouldReturnMoneyWithSpecifiedCulture()
	{
		var zero = Money.Zero(FrCulture);

		zero.Amount.ShouldBe(0);
		zero.CultureName.ShouldBe(FrCulture);
	}

	[Fact]
	public void FromDecimalShouldReturnMoney()
	{
		var money = Money.From(10m, UsCulture);

		money.Amount.ShouldBe(10m);
		money.CultureName.ShouldBe(UsCulture);
	}

	[Fact]
	public void FromStringShouldParseAndReturnMoney()
	{
		var money = Money.From("123.45", UsCulture);

		money.Amount.ShouldBe(123.45m);
		money.CultureName.ShouldBe(UsCulture);
	}

	[Fact]
	public void FromStringShouldThrowForInvalidFormat()
	{
		_ = Should.Throw<FormatException>(() => Money.From("not-a-number", UsCulture));
	}

	[Fact]
	public void FromStringShouldThrowForInvalidCulture()
	{
		_ = Should.Throw<CultureNotFoundException>(() => Money.From("123.45", "bad-culture"));
	}

	[Fact]
	public void FromIntCreatesExpectedMoney()
	{
		var money = Money.From(500, UsCulture);
		money.Amount.ShouldBe(500m);
		money.CultureName.ShouldBe(UsCulture);
	}

	[Fact]
	public void FromLongCreatesExpectedMoney()
	{
		var money = Money.From(1_000_000L, UsCulture);
		money.Amount.ShouldBe(1_000_000m);
		money.CultureName.ShouldBe(UsCulture);
	}

	[Fact]
	public void FromFloatCreatesExpectedMoney()
	{
		var money = Money.From(10.5f, UsCulture);
		decimal.Round(money.Amount, 2).ShouldBe(decimal.Round((decimal)10.5f, 2));
		money.CultureName.ShouldBe(UsCulture);
	}

	[Fact]
	public void FromDoubleCreatesExpectedMoney()
	{
		var money = Money.From(1234.5678d, UsCulture);
		decimal.Round(money.Amount, 2).ShouldBe(decimal.Round((decimal)1234.5678d, 2));
		money.CultureName.ShouldBe(UsCulture);
	}

	#endregion FactoryMethods

	#region ArithmeticOperators

	[Fact]
	public void OperatorsBehaveCorrectly()
	{
		var a = new Money(100m, UsCulture);
		var b = new Money(200m, UsCulture);
		var c = new Money(100m, UsCulture);

		(a == c).ShouldBeTrue();
		(a != b).ShouldBeTrue();
		(a < b).ShouldBeTrue();
		(b > a).ShouldBeTrue();
		(a <= c).ShouldBeTrue();
		(a >= c).ShouldBeTrue();
		(a + c).Amount.ShouldBe(200m);
		(b - a).Amount.ShouldBe(100m);
		(a * 2).Amount.ShouldBe(200m);
		(b / 2).Amount.ShouldBe(100m);
	}

	[Fact]
	public void AddMoneyInstances()
	{
		var result = new Money(10m, UsCulture) + new Money(15m, UsCulture);
		result.Amount.ShouldBe(25m);
	}

	[Fact]
	public void AddMoneyAndDecimal()
	{
		var result = new Money(10m, UsCulture) + 5m;
		result.Amount.ShouldBe(15m);
	}

	[Fact]
	public void AddDecimalAndMoney()
	{
		var result = 5m + new Money(10m, UsCulture);
		result.Amount.ShouldBe(15m);
	}

	[Fact]
	public void SubtractMoneyInstances()
	{
		var result = new Money(20m, UsCulture) - new Money(5m, UsCulture);
		result.Amount.ShouldBe(15m);
	}

	[Fact]
	public void SubtractMoneyAndDecimal()
	{
		var result = new Money(20m, UsCulture) - 5m;
		result.Amount.ShouldBe(15m);
	}

	[Fact]
	public void SubtractDecimalAndMoney()
	{
		var result = 25m - new Money(10m, UsCulture);
		result.Amount.ShouldBe(15m);
	}

	[Fact]
	public void MultiplyMoneyByDecimal()
	{
		var result = new Money(10m, UsCulture) * 3;
		result.Amount.ShouldBe(30m);
	}

	[Fact]
	public void MultiplyDecimalByMoney()
	{
		var result = 4m * new Money(2m, UsCulture);
		result.Amount.ShouldBe(8m);
	}

	[Fact]
	public void DivideMoneyByDecimal()
	{
		var result = new Money(20m, UsCulture) / 2;
		result.Amount.ShouldBe(10m);
	}

	[Fact]
	public void ThrowWhenDividingByZero()
	{
		_ = Should.Throw<DivideByZeroException>(() => _ = new Money(10m, UsCulture) / 0);
	}

	#endregion ArithmeticOperators

	#region ComparisonOperators

	[Fact]
	public void CompareMoneyValues()
	{
		var low = new Money(10m, UsCulture);
		var high = new Money(20m, UsCulture);
		var equal = new Money(10m, UsCulture);

		(low < high).ShouldBeTrue();
		(high > low).ShouldBeTrue();
		(low <= equal).ShouldBeTrue();
		(low >= equal).ShouldBeTrue();
	}

	[Fact]
	public void DecimalBasedComparisonsShouldWork()
	{
		var money = new Money(100m, UsCulture);

		(money < 200m).ShouldBeTrue();
		(money <= 100m).ShouldBeTrue();
		(money > 50m).ShouldBeTrue();
		(money >= 100m).ShouldBeTrue();
	}

	[Fact]
	public void CompareToDecimal()
	{
		var money = new Money(100m, UsCulture);

		money.CompareTo(200m).ShouldBeLessThan(0);
		money.CompareTo(100m).ShouldBe(0);
		money.CompareTo(50m).ShouldBeGreaterThan(0);
	}

	#endregion ComparisonOperators

	#region Equality

	[Fact]
	public void EqualOperatorShouldCompareAmountAndCulture()
	{
		var a = new Money(10m, UsCulture);
		var b = new Money(10m, UsCulture);

		(a == b).ShouldBeTrue();
		(a != new Money(20m, UsCulture)).ShouldBeTrue();
	}

	[Fact]
	public void EqualsShouldMatchDecimal()
	{
		var money = new Money(100m, UsCulture);

		money.Equals(100m).ShouldBeTrue();
		money.Equals(99m).ShouldBeFalse();
	}

	[Fact]
	public void EqualsShouldCompareObject()
	{
		var a = new Money(100m, UsCulture);
		var b = new Money(100m, UsCulture);

#pragma warning disable IDE0004 // Remove Unnecessary Cast
		a.Equals((object)b).ShouldBeTrue();
#pragma warning restore IDE0004 // Remove Unnecessary Cast
#pragma warning disable CA1508 // Avoid dead conditional code
		a.Equals((object?)null).ShouldBeFalse();
#pragma warning restore CA1508 // Avoid dead conditional code
	}

	[Fact]
	public void EqualsWithObjectCastsCorrectly()
	{
		var a = new Money(200m, UsCulture);
		object b = new Money(200m, UsCulture);

		a.Equals(b).ShouldBeTrue();
	}

	[Fact]
	public void EqualsInternalIsUsedForEquality()
	{
		// Direct usage of ValueObjectBase.Equals
		IValueObject a = new Money(100m, UsCulture);
		IValueObject b = new Money(100m, UsCulture);

		a.Equals(b).ShouldBeTrue();
	}

	[Fact]
	public void GetHashCodeInternalIsUsed()
	{
		var m1 = new Money(123m, UsCulture);
		var m2 = new Money(123m, UsCulture);

		m1.GetHashCode().ShouldBe(m2.GetHashCode());
	}

	[Fact]
	public void GetHashCodeInternalShouldReturnConsistentValue()
	{
		var money1 = Money.From(123.45m, UsCulture);
		var money2 = Money.From(123.45m, UsCulture);

		money1.GetHashCode().ShouldBe(money2.GetHashCode());
	}

	[Fact]
	public void EqualsWithDecimalObjectShouldReturnTrue()
	{
		var money = Money.From(123.45m, UsCulture);
		object input = 123.45m;
		money.Equals(input).ShouldBeTrue();
	}

	[Fact]
	public void EqualsWithNullObjectShouldReturnFalse()
	{
		var money = Money.From(123.45m, UsCulture);
		money.Equals(null).ShouldBeFalse();
	}

	[Fact]
	public void EqualsInternalWithDifferentValueObjectShouldReturnFalse()
	{
		var money = Money.From(100, UsCulture);
		var dummy = new DummyValueObject();

		money.Equals(dummy).ShouldBeFalse();
	}

	#endregion Equality

	#region Formatting

	[Theory]
	[InlineData(1234.56, false)]
	[InlineData(1234.56, true)]
	public void ToStringWithExcludeSymbolCoversBothPaths(decimal amount, bool exclude)
	{
		var money = new Money(amount, UsCulture);
		var formatted = money.ToString(exclude);
		formatted.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void FormatToString()
	{
		var money = new Money(123.45m, UsCulture);
		money.ToString().ShouldContain("$");
		money.ToString(true).ShouldNotContain("$");
	}

	[Fact]
	public void FormatToStringForISOCurrency()
	{
		var money = new Money(100m, UsCulture);
		money.ISOCurrencyCode.ShouldBe("USD");
	}

	[Fact]
	public void ISOCurrencyShouldThrowForInvariant()
	{
		var money = new Money(100m, CultureInfo.InvariantCulture);
		_ = Should.Throw<InvalidOperationException>(() => _ = money.ISOCurrencyCode);
	}

	#endregion Formatting

	#region Metadata

	[Fact]
	public void WithMetadataShouldReturnUpdatedMoney()
	{
		var money = new Money(100m, UsCulture);
		var result = money.WithMetadata(FrCulture, 5);

		result.CultureName.ShouldBe(FrCulture);
		result.Denomination.ShouldBe(5);
	}

	[Fact]
	public void AddMetadataShouldReturnNewInstance()
	{
		var money = new Money(100m, UsCulture);
		var result = Money.AddMetadata(money, FrCulture, 10);

		result.CultureName.ShouldBe(FrCulture);
		result.Denomination.ShouldBe(10);
	}

	#endregion Metadata

	[Fact]
	public void GetResultDenominationMoneyMoneyReturnsCommonDenomination()
	{
		var m1 = new Money(100m, UsCulture, 10);
		var m2 = new Money(50m, UsCulture, 10);

		var result = typeof(Money)
			.GetMethod("GetResultDenomination", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
				null, [typeof(Money), typeof(Money)], null)!
			.Invoke(null, [m1, m2]);

		result.ShouldBe(10m);
	}

	[Fact]
	public void GetResultDenominationMoneyMoneyReturnsNullForDifferentDenominations()
	{
		var m1 = new Money(100m, UsCulture, 10);
		var m2 = new Money(50m, UsCulture, 5);

		var result = typeof(Money)
			.GetMethod("GetResultDenomination", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
				null, [typeof(Money), typeof(Money)], null)!
			.Invoke(null, [m1, m2]);

		result.ShouldBeNull();
	}

	[Fact]
	public void GetResultDenominationMoneyDecimalReturnsDenominationWhenDivisible()
	{
		var m1 = new Money(100m, UsCulture, 10);
		var method = typeof(Money).GetMethod("GetResultDenomination",
			System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
			null, [typeof(Money), typeof(decimal)], null);

		var result = method!.Invoke(null, [m1, 20m]);
		result.ShouldBe(10m);
	}

	[Fact]
	public void GetResultDenominationMoneyDecimalReturnsNullWhenNotDivisible()
	{
		// Arrange
		var m1 = new Money(100m, UsCulture, 10);
		var method = typeof(Money).GetMethod("GetResultDenomination",
			System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
			null, [typeof(Money), typeof(decimal)], null);

		// Act
		var result = method!.Invoke(null, [m1, 3m]);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void GetResultDenominationDecimalMoneyReturnsDenominationWhenDivisible()
	{
		var m2 = new Money(100m, UsCulture, 5);
		var method = typeof(Money).GetMethod("GetResultDenomination",
			System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
			null, [typeof(decimal), typeof(Money)], null);

		var result = method!.Invoke(null, [25m, m2]);
		result.ShouldBe(5m);
	}

	[Fact]
	public void IsWholeNumberWorksCorrectly()
	{
		var method = typeof(Money).GetMethod("IsWholeNumber",
			System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

		((bool)method!.Invoke(null, [5m])!).ShouldBeTrue();
		((bool)method!.Invoke(null, [5.5m])!).ShouldBeFalse();
	}

	[Fact]
	public void EqualsObjectHandlesNullAndWrongType()
	{
		var m1 = new Money(100m, UsCulture);
#pragma warning disable CA1508 // Avoid dead conditional code
		m1.Equals(null).ShouldBeFalse();
#pragma warning restore CA1508 // Avoid dead conditional code
		m1.Equals("not money").ShouldBeFalse();
	}

	[Fact]
	public void EqualsInternalReturnsTrueForEqualMoney()
	{
		var m1 = new Money(100m, UsCulture);
		var m2 = new Money(100m, UsCulture);
		m1.Equals(m2).ShouldBeTrue();
	}

	[Fact]
	public void CompareToThrowsIfCulturesDiffer()
	{
		var m1 = new Money(100m, UsCulture);
		var m2 = new Money(100m, FrCulture);

		Should.Throw<InvalidOperationException>(() => m1.CompareTo(m2))
			.Message.ShouldContain("same culture");
	}

	[Fact]
	public void GetHashCodeInternalMatchesEqualObjects()
	{
		var m1 = new Money(100m, UsCulture);
		var m2 = new Money(100m, UsCulture);

		m1.GetHashCode().ShouldBe(m2.GetHashCode());
	}
}

internal sealed class DummyValueObject : ValueObjectBase
{
	protected override bool EqualsInternal(IValueObject? other) => false;

	protected override int GetHashCodeInternal() => 0;
}
