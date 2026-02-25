// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Domain.Extensions;

namespace Excalibur.Tests.Domain.Extensions;

/// <summary>
/// Unit tests for <see cref="ConfigurationExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Domain")]
public sealed class ConfigurationExtensionsShould
{
	[Fact]
	public void GetApplicationContextConfiguration_ReturnsEmptyDictionary_WhenSectionDoesNotExist()
	{
		// Arrange
		var configuration = new ConfigurationBuilder().Build();

		// Act
		var result = configuration.GetApplicationContextConfiguration();

		// Assert
		result.ShouldNotBeNull();
		result.ShouldBeEmpty();
	}

	[Fact]
	public void GetApplicationContextConfiguration_ReturnsSettings_WhenSectionExists()
	{
		// Arrange
		var configData = new Dictionary<string, string?>
		{
			["ApplicationContext:ApplicationName"] = "TestApp",
			["ApplicationContext:Environment"] = "Development",
			["ApplicationContext:Version"] = "1.0.0",
		};

		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(configData)
			.Build();

		// Act
		var result = configuration.GetApplicationContextConfiguration();

		// Assert
		result.ShouldNotBeNull();
		result.Count.ShouldBe(3);
		result["ApplicationName"].ShouldBe("TestApp");
		result["Environment"].ShouldBe("Development");
		result["Version"].ShouldBe("1.0.0");
	}

	[Fact]
	public void GetApplicationContextConfiguration_ThrowsArgumentNullException_WhenConfigurationIsNull()
	{
		// Arrange
		IConfiguration? configuration = null;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			configuration.GetApplicationContextConfiguration());
	}

	[Fact]
	public void GetApplicationContextConfiguration_HandlesNullValues()
	{
		// Arrange
		var configData = new Dictionary<string, string?>
		{
			["ApplicationContext:NullableKey"] = null,
			["ApplicationContext:NonNullKey"] = "value",
		};

		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(configData)
			.Build();

		// Act
		var result = configuration.GetApplicationContextConfiguration();

		// Assert
		result.ShouldNotBeNull();
		result.ContainsKey("NonNullKey").ShouldBeTrue();
		result["NonNullKey"].ShouldBe("value");
	}

	[Fact]
	public void GetApplicationContextConfiguration_IgnoresOtherSections()
	{
		// Arrange
		var configData = new Dictionary<string, string?>
		{
			["ApplicationContext:Key1"] = "Value1",
			["OtherSection:Key2"] = "Value2",
			["Logging:LogLevel:Default"] = "Information",
		};

		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(configData)
			.Build();

		// Act
		var result = configuration.GetApplicationContextConfiguration();

		// Assert
		result.Count.ShouldBe(1);
		result.ContainsKey("Key1").ShouldBeTrue();
		result.ContainsKey("Key2").ShouldBeFalse();
	}

	[Fact]
	public void GetApplicationContextConfiguration_HandlesNestedKeysAsFlat()
	{
		// Arrange
		var configData = new Dictionary<string, string?>
		{
			["ApplicationContext:Database:ConnectionString"] = "Server=localhost",
			["ApplicationContext:Database:Provider"] = "SqlServer",
		};

		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(configData)
			.Build();

		// Act
		var result = configuration.GetApplicationContextConfiguration();

		// Assert
		// The GetChildren only gets immediate children, so nested sections appear as "Database"
		result.ContainsKey("Database").ShouldBeTrue();
	}

	[Fact]
	public void GetApplicationContextConfiguration_WorksWithEmptySection()
	{
		// Arrange
		var configData = new Dictionary<string, string?>
		{
			["ApplicationContext"] = string.Empty, // Empty section value
		};

		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(configData)
			.Build();

		// Act
		var result = configuration.GetApplicationContextConfiguration();

		// Assert
		result.ShouldBeEmpty();
	}
}
