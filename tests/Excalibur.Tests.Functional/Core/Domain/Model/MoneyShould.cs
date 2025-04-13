using System.Globalization;

using Excalibur.Core;
using Excalibur.Core.Domain.Model.ValueObjects;
using Excalibur.Tests;

using Shouldly;

namespace Excalibur.Tests.Functional.Core.Domain.Model;

public class MoneyShould : SharedApplicationContextTestBase
{
	[Fact]
	public void SystemShouldProcessMoneyAndConfigurationCorrectly()
	{
		var context = new Dictionary<string, string?> { { "CurrencyCulture", "en-US" } };

		ApplicationContext.Init(context);

		var money = new Money(100m, ApplicationContext.Get("CurrencyCulture"));
		money.ToString().ShouldBe("$100.00");
	}

	[Fact]
	public void CorrectlyFormatMoneyBasedOnCulture()
	{
		var moneyUS = new Money(1000.50m, "en-US");
		var moneyFR = new Money(1000.50m, "fr-FR");

		moneyUS.ToString().ShouldBe(1000.50m.ToString("C", new CultureInfo("en-US")));
		moneyFR.ToString().ShouldBe(1000.50m.ToString("C", new CultureInfo("fr-FR")));
	}

	[Fact]
	public void RespectDenominationConstraints()
	{
		var money = new Money(500m, "en-US", 100m);

		money.Denomination.ShouldBe(100m);
		money.UnitCount.ShouldBe(5);
	}

	[Fact]
	public void ThrowIfAmountIsNotMultipleOfDenomination()
	{
		_ = Should.Throw<InvalidOperationException>(() => new Money(105, "en-US", 50));
	}
}
