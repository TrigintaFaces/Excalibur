using System.Globalization;

using Excalibur.Core;

using Shouldly;

namespace Excalibur.Tests.Integration.Core;

public class CulturesShould
{
	[Fact]
	public void ReturnSystemDefinedCultureInfo()
	{
		// Act
		var cultureInfo = Cultures.GetCultureInfo("en-US");

		// Assert
		_ = cultureInfo.ShouldNotBeNull();
		_ = cultureInfo.Calendar.ShouldBeOfType<GregorianCalendar>();
		cultureInfo.NumberFormat.CurrencySymbol.ShouldBe("$");
	}

	[Fact]
	public void MatchCultureInfoFromCultureInfoClass()
	{
		// Arrange
		var expectedCulture = new CultureInfo("fr-FR");

		// Act
		var actualCulture = Cultures.GetCultureInfo("fr-FR");

		// Assert
		actualCulture.ShouldBe(expectedCulture);
	}

	[Fact]
	public void ReturnValidCulturesFromSystem()
	{
		// Act
		var systemCultures = CultureInfo.GetCultures(CultureTypes.SpecificCultures);
		var customCultures = Cultures.Names.Select(CultureInfo.GetCultureInfo).ToList();

		// Assert
		customCultures.ShouldAllBe((CultureInfo c) => systemCultures.Any((CultureInfo sc) => sc.Name == c.Name));
	}

	[Fact]
	public void HandleInvalidCultureGracefully()
	{
		// Act & Assert
		var exception = Should.Throw<CultureNotFoundException>(() => Cultures.GetCultureInfo("invalid-culture"));
		exception.InvalidCultureName.ShouldBe("invalid-culture");
	}
}
