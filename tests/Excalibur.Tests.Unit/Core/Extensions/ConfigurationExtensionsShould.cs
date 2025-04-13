using Excalibur.Core.Extensions;
using Excalibur.Tests.Shared;

using Microsoft.Extensions.Configuration;

using Shouldly;

namespace Excalibur.Tests.Unit.Core.Extensions;

public class ConfigurationExtensionsShould
{
	[Fact]
	public void ThrowArgumentNullExceptionIfConfigurationIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
				Excalibur.Core.Extensions.ConfigurationExtensions.GetApplicationContextConfiguration(null!))
			.ParamName.ShouldBe("configuration");
	}

	[Fact]
	public void ReturnEmptyDictionaryIfSectionIsMissing()
	{
		// Arrange
		var configuration = new ConfigurationBuilder().Build();

		// Act
		var result = configuration.GetApplicationContextConfiguration();

		// Assert
		_ = result.ShouldNotBeNull();
		result.ShouldBeEmpty();
	}

	[Fact]
	public void ReturnCorrectDictionaryIfSectionExists()
	{
		// Arrange
		var configuration = ConfigurationTestHelper.BuildConfiguration(new Dictionary<string, string?>
		{
			{ "ApplicationContext:Key1", "Value1" }, { "ApplicationContext:Key2", "Value2" }
		});

		// Act
		var result = configuration.GetApplicationContextConfiguration();

		// Assert
		_ = result.ShouldNotBeNull();
		result.Count.ShouldBe(2);
		result["Key1"].ShouldBe("Value1");
		result["Key2"].ShouldBe("Value2");
	}

	[Fact]
	public void HandleNullValuesInConfiguration()
	{
		// Arrange
		var configuration = ConfigurationTestHelper.BuildConfiguration(new Dictionary<string, string?>
		{
			{ "ApplicationContext:Key1", null }, { "ApplicationContext:Key2", "Value2" }
		});

		// Act
		var result = configuration.GetApplicationContextConfiguration();

		// Assert
		_ = result.ShouldNotBeNull();
		result.Count.ShouldBe(2);
		result["Key1"].ShouldBeNull();
		result["Key2"].ShouldBe("Value2");
	}

	[Fact]
	public void ReturnEmptyDictionaryIfNoApplicationContextKeys()
	{
		// Arrange
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?> { { "OtherSection:Key1", "Value1" } }).Build();

		// Act
		var result = configuration.GetApplicationContextConfiguration();

		// Assert
		_ = result.ShouldNotBeNull();
		result.ShouldBeEmpty();
	}
}
