// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Hosting.Configuration;

namespace Excalibur.Hosting.Tests.Configuration;

/// <summary>
/// Unit tests for <see cref="ConfigurationValidationError"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Hosting")]
[Trait("Feature", "Configuration")]
public sealed class ConfigurationValidationErrorShould : UnitTestBase
{
	[Fact]
	public void RequireMessage()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => new ConfigurationValidationError(null!));
	}

	[Fact]
	public void RequireNonEmptyMessage()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => new ConfigurationValidationError(string.Empty));
	}

	[Fact]
	public void RequireNonWhitespaceMessage()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => new ConfigurationValidationError("   "));
	}

	[Fact]
	public void StoreMessageProperty()
	{
		// Act
		var error = new ConfigurationValidationError("Test error message");

		// Assert
		error.Message.ShouldBe("Test error message");
	}

	[Fact]
	public void HaveNullConfigurationPathByDefault()
	{
		// Act
		var error = new ConfigurationValidationError("Test error");

		// Assert
		error.ConfigurationPath.ShouldBeNull();
	}

	[Fact]
	public void StoreConfigurationPath()
	{
		// Act
		var error = new ConfigurationValidationError("Test error", "ConnectionStrings:DefaultConnection");

		// Assert
		error.ConfigurationPath.ShouldBe("ConnectionStrings:DefaultConnection");
	}

	[Fact]
	public void HaveNullValueByDefault()
	{
		// Act
		var error = new ConfigurationValidationError("Test error");

		// Assert
		error.Value.ShouldBeNull();
	}

	[Fact]
	public void StoreValue()
	{
		// Act
		var error = new ConfigurationValidationError("Test error", "Path", "invalid-value");

		// Assert
		error.Value.ShouldBe("invalid-value");
	}

	[Fact]
	public void HaveNullRecommendationByDefault()
	{
		// Act
		var error = new ConfigurationValidationError("Test error");

		// Assert
		error.Recommendation.ShouldBeNull();
	}

	[Fact]
	public void StoreRecommendation()
	{
		// Act
		var error = new ConfigurationValidationError(
			"Test error",
			"Path",
			"value",
			"Use a valid connection string");

		// Assert
		error.Recommendation.ShouldBe("Use a valid connection string");
	}

	[Fact]
	public void ToStringWithMessageOnly()
	{
		// Arrange
		var error = new ConfigurationValidationError("Test error message");

		// Act
		var result = error.ToString();

		// Assert
		result.ShouldBe("Test error message");
	}

	[Fact]
	public void ToStringWithConfigurationPath()
	{
		// Arrange
		var error = new ConfigurationValidationError("Test error message", "Database:ConnectionString");

		// Act
		var result = error.ToString();

		// Assert
		result.ShouldContain("[Database:ConnectionString]");
		result.ShouldContain("Test error message");
	}

	[Fact]
	public void ToStringWithValue()
	{
		// Arrange
		var error = new ConfigurationValidationError("Invalid value", null, "bad-value");

		// Act
		var result = error.ToString();

		// Assert
		result.ShouldContain("Value: 'bad-value'");
	}

	[Fact]
	public void ToStringWithRecommendation()
	{
		// Arrange
		var error = new ConfigurationValidationError(
			"Invalid configuration",
			null,
			null,
			"Check documentation");

		// Act
		var result = error.ToString();

		// Assert
		result.ShouldContain("Recommendation: Check documentation");
	}

	[Fact]
	public void ToStringWithAllProperties()
	{
		// Arrange
		var error = new ConfigurationValidationError(
			"Connection string is invalid",
			"Database:ConnectionString",
			"not-a-connection-string",
			"Provide a valid SQL Server connection string");

		// Act
		var result = error.ToString();

		// Assert
		result.ShouldContain("[Database:ConnectionString]");
		result.ShouldContain("Connection string is invalid");
		result.ShouldContain("Value: 'not-a-connection-string'");
		result.ShouldContain("Recommendation: Provide a valid SQL Server connection string");
	}

	[Fact]
	public void ToStringSkipsEmptyConfigurationPath()
	{
		// Arrange
		var error = new ConfigurationValidationError("Test error", "");

		// Act
		var result = error.ToString();

		// Assert
		result.ShouldNotContain("[");
		result.ShouldNotContain("]");
	}

	[Fact]
	public void ToStringSkipsWhitespaceRecommendation()
	{
		// Arrange
		var error = new ConfigurationValidationError("Test error", null, null, "   ");

		// Act
		var result = error.ToString();

		// Assert
		result.ShouldNotContain("Recommendation:");
	}

	[Fact]
	public void AcceptNumericValue()
	{
		// Act
		var error = new ConfigurationValidationError("Port out of range", "Server:Port", 99999);

		// Assert
		error.Value.ShouldBe(99999);
	}

	[Fact]
	public void AcceptBooleanValue()
	{
		// Act
		var error = new ConfigurationValidationError("Invalid setting", "Feature:Enabled", false);

		// Assert
		error.Value.ShouldBe(false);
	}
}
