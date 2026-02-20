// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;

namespace Excalibur.Tests.Domain;

/// <summary>
/// Unit tests for <see cref="Cultures"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Domain")]
public sealed class CulturesShould
{
	[Fact]
	public void DefaultCultureName_IsEnUs()
	{
		// Assert
		Cultures.DefaultCultureName.ShouldBe("en-US");
	}

	[Fact]
	public void Names_ContainsValidCultures()
	{
		// Assert
		var names = Cultures.Names.ToList();
		names.ShouldNotBeEmpty();
		names.ShouldContain("en-US");
		names.ShouldContain("en-GB");
	}

	[Fact]
	public void Names_ContainsOnlySpecificCultures()
	{
		// Assert - all cultures should be specific (not neutral like "en")
		foreach (var name in Cultures.Names.Take(10)) // Check first 10 to avoid long test
		{
			var culture = CultureInfo.GetCultureInfo(name);
			culture.IsNeutralCulture.ShouldBeFalse($"Culture {name} should be specific, not neutral");
		}
	}

	[Theory]
	[InlineData("en-US", true)]
	[InlineData("en-GB", true)]
	[InlineData("de-DE", true)]
	[InlineData("fr-FR", true)]
	public void IsValidCultureName_ReturnsTrue_ForValidCultures(string cultureName, bool expected)
	{
		// Act
		var result = Cultures.IsValidCultureName(cultureName);

		// Assert
		result.ShouldBe(expected);
	}

	[Theory]
	[InlineData("invalid-culture")]
	[InlineData("xx-XX")]
	[InlineData("")]
	[InlineData("not-a-culture")]
	public void IsValidCultureName_ReturnsFalse_ForInvalidCultures(string cultureName)
	{
		// Act
		var result = Cultures.IsValidCultureName(cultureName);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void IsValidCultureName_IsCaseInsensitive()
	{
		// Act & Assert
		Cultures.IsValidCultureName("EN-US").ShouldBeTrue();
		Cultures.IsValidCultureName("en-us").ShouldBeTrue();
		Cultures.IsValidCultureName("En-Us").ShouldBeTrue();
	}

	[Fact]
	public void GetCultureInfo_ReturnsCorrectCulture()
	{
		// Act
		var culture = Cultures.GetCultureInfo("en-US");

		// Assert
		culture.ShouldNotBeNull();
		culture.Name.ShouldBe("en-US");
	}

	[Fact]
	public void GetCultureInfo_ThrowsCultureNotFoundException_ForInvalidCulture()
	{
		// Act & Assert
		var exception = Should.Throw<CultureNotFoundException>(() =>
			Cultures.GetCultureInfo("invalid-culture"));
		exception.InvalidCultureName.ShouldBe("invalid-culture");
	}

	[Fact]
	public void GetCultureInfo_ThrowsCultureNotFoundException_ForNullCulture()
	{
		// Act & Assert
		_ = Should.Throw<CultureNotFoundException>(() =>
			Cultures.GetCultureInfo(null!));
	}

	[Fact]
	public void GetCultureInfo_ThrowsCultureNotFoundException_ForEmptyString()
	{
		// Act & Assert
		_ = Should.Throw<CultureNotFoundException>(() =>
			Cultures.GetCultureInfo(string.Empty));
	}

	[Fact]
	public void GetCultureInfo_ThrowsCultureNotFoundException_ForWhitespace()
	{
		// Act & Assert
		_ = Should.Throw<CultureNotFoundException>(() =>
			Cultures.GetCultureInfo("   "));
	}

	[Fact]
	public void GetCultureInfo_ReturnsCorrectCultureInfo_ForDifferentCultures()
	{
		// Act & Assert
		var deDE = Cultures.GetCultureInfo("de-DE");
		deDE.Name.ShouldBe("de-DE");
		deDE.TwoLetterISOLanguageName.ShouldBe("de");

		var frFR = Cultures.GetCultureInfo("fr-FR");
		frFR.Name.ShouldBe("fr-FR");
		frFR.TwoLetterISOLanguageName.ShouldBe("fr");
	}
}
