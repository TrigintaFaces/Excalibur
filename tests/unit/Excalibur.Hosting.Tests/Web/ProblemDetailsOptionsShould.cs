using Excalibur.Hosting.Web.Diagnostics;

namespace Excalibur.Hosting.Tests.Web;

/// <summary>
/// Unit tests for ProblemDetailsOptions configuration.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ProblemDetailsOptionsShould : UnitTestBase
{
	[Fact]
	public void Create_WithDefaults_HasExpectedDefaultValues()
	{
		// Arrange & Act
		var options = new ProblemDetailsOptions();

		// Assert
		options.StatusTypeBaseUrl.ShouldBe("https://developer.mozilla.org");
		_ = options.SupportedLocales.ShouldNotBeNull();
		options.SupportedLocales.ShouldNotBeEmpty();
	}

	[Fact]
	public void SupportedLocales_ContainsEnglishUS()
	{
		// Arrange & Act
		var options = new ProblemDetailsOptions();

		// Assert
		options.SupportedLocales.ShouldContain("en-US");
	}

	[Fact]
	public void SupportedLocales_ContainsMultipleLanguages()
	{
		// Arrange & Act
		var options = new ProblemDetailsOptions();

		// Assert
		options.SupportedLocales.ShouldContain("fr");
		options.SupportedLocales.ShouldContain("de");
		options.SupportedLocales.ShouldContain("es");
		options.SupportedLocales.ShouldContain("ja");
		options.SupportedLocales.ShouldContain("zh-CN");
	}

	[Fact]
	public void SupportedLocales_IsCaseInsensitive()
	{
		// Arrange & Act
		var options = new ProblemDetailsOptions();

		// Assert
		options.SupportedLocales.Contains("EN-US").ShouldBeTrue();
		options.SupportedLocales.Contains("en-us").ShouldBeTrue();
	}

	[Fact]
	public void StatusTypeBaseUrl_CanBeCustomized()
	{
		// Arrange
		var options = new ProblemDetailsOptions();

		// Act
		options.StatusTypeBaseUrl = "https://api.example.com/errors";

		// Assert
		options.StatusTypeBaseUrl.ShouldBe("https://api.example.com/errors");
	}

	[Fact]
	public void SupportedLocales_CanAddNewLocale()
	{
		// Arrange
		var options = new ProblemDetailsOptions();

		// Act
		_ = options.SupportedLocales.Add("cy"); // Welsh

		// Assert
		options.SupportedLocales.ShouldContain("cy");
	}

	[Fact]
	public void SupportedLocales_HasExpectedCount()
	{
		// Arrange & Act
		var options = new ProblemDetailsOptions();

		// Assert - At least 30 locales expected (allowing for additions)
		options.SupportedLocales.Count.ShouldBeGreaterThanOrEqualTo(30);
	}
}
