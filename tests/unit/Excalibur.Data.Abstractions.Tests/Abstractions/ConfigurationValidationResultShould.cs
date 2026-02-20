// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Persistence;

namespace Excalibur.Data.Tests.Abstractions.Persistence;

/// <summary>
/// Unit tests for <see cref="ConfigurationValidationResult"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data")]
[Trait("Feature", "Abstractions")]
public sealed class ConfigurationValidationResultShould : UnitTestBase
{
	[Fact]
	public void InitializeWithRequiredParameters()
	{
		// Act
		var result = new ConfigurationValidationResult(
			true,
			"SqlServer",
			"Configuration is valid");

		// Assert
		result.IsValid.ShouldBeTrue();
		result.ProviderName.ShouldBe("SqlServer");
		result.Message.ShouldBe("Configuration is valid");
		result.Severity.ShouldBe(ValidationSeverity.Error); // Default
	}

	[Fact]
	public void InitializeWithAllParameters()
	{
		// Act
		var result = new ConfigurationValidationResult(
			false,
			"Postgres",
			"Connection string is missing",
			ValidationSeverity.Critical);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.ProviderName.ShouldBe("Postgres");
		result.Message.ShouldBe("Connection string is missing");
		result.Severity.ShouldBe(ValidationSeverity.Critical);
	}

	[Fact]
	public void CreateValidResult()
	{
		// Act
		var result = new ConfigurationValidationResult(
			true,
			"MongoDB",
			"All settings validated",
			ValidationSeverity.Info);

		// Assert
		result.IsValid.ShouldBeTrue();
		result.Severity.ShouldBe(ValidationSeverity.Info);
	}

	[Fact]
	public void CreateInvalidResult()
	{
		// Act
		var result = new ConfigurationValidationResult(
			false,
			"Redis",
			"Invalid connection port",
			ValidationSeverity.Error);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Severity.ShouldBe(ValidationSeverity.Error);
	}

	[Fact]
	public void AllowEmptyProviderName()
	{
		// Act
		var result = new ConfigurationValidationResult(
			false,
			string.Empty,
			"Provider not specified");

		// Assert
		result.ProviderName.ShouldBe(string.Empty);
	}

	[Fact]
	public void AllowEmptyMessage()
	{
		// Act
		var result = new ConfigurationValidationResult(
			true,
			"InMemory",
			string.Empty);

		// Assert
		result.Message.ShouldBe(string.Empty);
	}

	[Theory]
	[InlineData(ValidationSeverity.Info)]
	[InlineData(ValidationSeverity.Warning)]
	[InlineData(ValidationSeverity.Error)]
	[InlineData(ValidationSeverity.Critical)]
	public void AcceptAllSeverityLevels(ValidationSeverity severity)
	{
		// Act
		var result = new ConfigurationValidationResult(
			false,
			"Test",
			"Test message",
			severity);

		// Assert
		result.Severity.ShouldBe(severity);
	}
}
