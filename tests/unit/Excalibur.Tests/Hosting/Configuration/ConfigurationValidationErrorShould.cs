// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using ExcaliburConfigurationValidationError = Excalibur.Hosting.Configuration.ConfigurationValidationError;

namespace Excalibur.Tests.Hosting.Configuration;

[Trait("Category", "Unit")]
public sealed class ExcaliburConfigurationValidationErrorShould
{
	[Fact]
	public void InitializeWithAllProperties()
	{
		// Arrange & Act
		var error = new ExcaliburConfigurationValidationError(
			"Error message",
			"Config:Path",
			"invalid-value",
			"Fix recommendation");

		// Assert
		error.Message.ShouldBe("Error message");
		error.ConfigurationPath.ShouldBe("Config:Path");
		error.Value.ShouldBe("invalid-value");
		error.Recommendation.ShouldBe("Fix recommendation");
	}

	[Fact]
	public void InitializeWithMinimalProperties()
	{
		// Arrange & Act
		var error = new ExcaliburConfigurationValidationError("Error message");

		// Assert
		error.Message.ShouldBe("Error message");
		error.ConfigurationPath.ShouldBeNull();
		error.Value.ShouldBeNull();
		error.Recommendation.ShouldBeNull();
	}

	[Fact]
	public void ThrowWhenMessageIsEmpty()
	{
		// Arrange, Act & Assert
		_ = Should.Throw<ArgumentException>(static () => new ExcaliburConfigurationValidationError(""));
		_ = Should.Throw<ArgumentException>(static () => new ExcaliburConfigurationValidationError(" "));
	}

	[Fact]
	public void FormatToStringCorrectly()
	{
		// Arrange
		var error = new ExcaliburConfigurationValidationError(
			"Error message",
			"Config:Path",
			"invalid-value",
			"Fix recommendation");

		// Act
		var str = error.ToString();

		// Assert
		str.ShouldContain("[Config:Path]");
		str.ShouldContain("Error message");
		str.ShouldContain("Value: 'invalid-value'");
		str.ShouldContain("Recommendation: Fix recommendation");
	}

	[Fact]
	public void FormatToStringWithMinimalProperties()
	{
		// Arrange
		var error = new ExcaliburConfigurationValidationError("Error message");

		// Act
		var str = error.ToString();

		// Assert
		str.ShouldBe("Error message");
	}
}
