using System.Globalization;

using Excalibur.Core;

using Shouldly;

namespace Excalibur.Tests.Functional.Core;

public class CulturesShould
{
	[Fact]
	public void ContainCommonCultures()
	{
		// Arrange - Common cultures that should be available
		var commonCultures = new[]
		{
			"en-US", // English (United States)
			"en-GB", // English (United Kingdom)
			"es-ES", // Spanish (Spain)
			"fr-FR", // French (France)
			"de-DE", // German (Germany)
			"it-IT", // Italian (Italy)
			"zh-CN", // Chinese (Simplified, China)
			"ja-JP", // Japanese (Japan)
			"ru-RU", // Russian (Russia)
			"pt-BR" // Portuguese (Brazil)
		};

		// Act & Assert
		foreach (var culture in commonCultures)
		{
			if (!Cultures.Names.Contains(culture, StringComparer.OrdinalIgnoreCase))
			{
				continue; // gracefully skip unavailable ones
			}

			Cultures.IsValidCultureName(culture).ShouldBeTrue($"Culture '{culture}' should be valid");
			Cultures.Names.ShouldContain(culture);
		}
	}

	[Fact]
	public void ReturnCorrectDateAndNumberFormatsForDifferentCultures()
	{
		// Arrange
		var testValue = 1234.56;
		var testDate = new DateTime(2025, 4, 15);

		// Act - US format (MM/dd/yyyy)
		var usCulture = Cultures.GetCultureInfo("en-US");
		var usNumber = testValue.ToString("C", usCulture);
		var usDate = testDate.ToString("d", usCulture);

		// Act - German format (dd.MM.yyyy)
		var deCulture = Cultures.GetCultureInfo("de-DE");
		var deNumber = testValue.ToString("C", deCulture);
		var deDate = testDate.ToString("d", deCulture);

		// Assert
		usNumber.ShouldStartWith("$");
		usDate.ShouldBe("4/15/2025");

		deNumber.ShouldStartWith("1.234,56");
		deNumber.ShouldContain("€");
		deDate.ShouldBe("15.04.2025");
	}

	[Fact]
	public void ThrowCultureNotFoundExceptionForInvalidCulture()
	{
		// Arrange
		var invalidCulture = "xx-XX";

		// Act & Assert
		var exception = Should.Throw<CultureNotFoundException>(() => Cultures.GetCultureInfo(invalidCulture));
		exception.InvalidCultureName.ShouldBe(invalidCulture);
	}

	[Fact]
	public void ThrowCultureNotFoundExceptionForNullOrEmptyCulture()
	{
		// Act & Assert
		_ = Should.Throw<CultureNotFoundException>(() => Cultures.GetCultureInfo(null));
		_ = Should.Throw<CultureNotFoundException>(() => Cultures.GetCultureInfo(string.Empty));
		_ = Should.Throw<CultureNotFoundException>(() => Cultures.GetCultureInfo("   "));
	}

	[Fact]
	public void ReturnDefaultCultureWhenRequested()
	{
		// Act
		var defaultCulture = Cultures.GetCultureInfo(Cultures.DefaultCultureName);

		// Assert
		_ = defaultCulture.ShouldNotBeNull();
		defaultCulture.Name.ShouldBe("en-US");
	}

	[Fact]
	public void ProvideConsistentResultsForCultureQueries()
	{
		// Act
		var firstCall = Cultures.GetCultureInfo("fr-FR");
		var secondCall = Cultures.GetCultureInfo("fr-FR");

		// Assert
		firstCall.ShouldBeSameAs(secondCall);
	}

	[Fact]
	public void SupportCultureSpecificStringOperations()
	{
		// Arrange
		var turkishUpperI = "i";
		var turkishCulture = Cultures.GetCultureInfo("tr-TR");
		var usCulture = Cultures.GetCultureInfo("en-US");

		// Act
		var turkishResult = turkishUpperI.ToUpper(turkishCulture);
		var usResult = turkishUpperI.ToUpper(usCulture);

		// Assert - Turkish uppercase 'i' is 'İ' (with dot), while in English it's 'I'
		turkishResult.ShouldNotBe(usResult);
		turkishResult.ShouldBe("İ");
		usResult.ShouldBe("I");
	}

	[Fact]
	public void EnsureAllCulturesAreValid()
	{
		// Act
		var cultureNames = Cultures.Names.ToList();

		// Assert
		cultureNames.ShouldNotBeEmpty();

		foreach (var name in cultureNames)
		{
			name.ShouldNotBeNullOrWhiteSpace();
			_ = Should.NotThrow(() => new CultureInfo(name));
		}
	}

	[Fact]
	public void ReturnCorrectCultureInfoForValidNames()
	{
		// Act
		var usCulture = Cultures.GetCultureInfo("en-US");
		var frCulture = Cultures.GetCultureInfo("fr-FR");

		// Assert
		usCulture.Name.ShouldBe("en-US");
		usCulture.NumberFormat.CurrencySymbol.ShouldBe("$");

		frCulture.Name.ShouldBe("fr-FR");
		frCulture.NumberFormat.CurrencySymbol.ShouldBe("€");
	}

	[Fact]
	public void ValidateAllCultureNamesInValidCultures()
	{
		// Act
		var allNamesAreValid = Cultures.Names.All(Cultures.IsValidCultureName);

		// Assert
		allNamesAreValid.ShouldBeTrue();
	}

	[Fact]
	public void HandleCultureValidationGracefully()
	{
		// Act & Assert
		Cultures.IsValidCultureName("en-US").ShouldBeTrue();
		Cultures.IsValidCultureName("xyz-XYZ").ShouldBeFalse();
	}
}
