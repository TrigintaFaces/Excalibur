using System.Globalization;

using Excalibur.Core;

using Shouldly;

namespace Excalibur.Tests.Unit.Core;

public class CulturesShould
{
	[Fact]
	public void HaveDefaultCultureNameAsEnUS()
	{
		// Act & Assert
		Cultures.DefaultCultureName.ShouldBe("en-US");
	}

	[Fact]
	public void ReturnValidCultureNames()
	{
		// Act
		var cultureNames = Cultures.Names.ToList();

		// Assert
		cultureNames.ShouldNotBeEmpty();
		cultureNames.ShouldContain("en-US");
		cultureNames.ShouldContain("fr-FR");
	}

	[Fact]
	public void ValidateValidCultureNameCorrectly()
	{
		// Act & Assert
		Cultures.IsValidCultureName("en-US").ShouldBeTrue();
		Cultures.IsValidCultureName("fr-FR").ShouldBeTrue();
		Cultures.IsValidCultureName("invalid-culture").ShouldBeFalse();
		Cultures.IsValidCultureName(string.Empty).ShouldBeFalse();
		Cultures.IsValidCultureName(null).ShouldBeFalse();
	}

	[Fact]
	public void GetCultureInfoForValidCultureName()
	{
		// Act
		var cultureInfo = Cultures.GetCultureInfo("en-US");

		// Assert
		_ = cultureInfo.ShouldNotBeNull();
		cultureInfo.Name.ShouldBe("en-US");
	}

	[Fact]
	public void ThrowCultureNotFoundExceptionForInvalidCultureName()
	{
		// Act & Assert
		var exception = Should.Throw<CultureNotFoundException>(() => Cultures.GetCultureInfo("invalid-culture"));
		exception.Message.ShouldContain("The requested culture invalid-culture is not available.");
	}

	[Fact]
	public void ThrowCultureNotFoundExceptionForNullOrEmptyCultureName()
	{
		// Act & Assert
		_ = Should.Throw<CultureNotFoundException>(() => Cultures.GetCultureInfo(null!));
		_ = Should.Throw<CultureNotFoundException>(() => Cultures.GetCultureInfo(string.Empty));
	}
}
