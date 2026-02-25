// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Hosting.Configuration;

namespace Excalibur.Hosting.Tests.Configuration;

/// <summary>
/// Unit tests for <see cref="ConfigurationValidationOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Hosting")]
[Trait("Feature", "Configuration")]
public sealed class ConfigurationValidationOptionsShould : UnitTestBase
{
	[Fact]
	public void HaveEnabledTrueByDefault()
	{
		// Act
		var options = new ConfigurationValidationOptions();

		// Assert
		options.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void HaveFailFastTrueByDefault()
	{
		// Act
		var options = new ConfigurationValidationOptions();

		// Assert
		options.FailFast.ShouldBeTrue();
	}

	[Fact]
	public void HaveTreatValidatorExceptionsAsErrorsTrueByDefault()
	{
		// Act
		var options = new ConfigurationValidationOptions();

		// Assert
		options.TreatValidatorExceptionsAsErrors.ShouldBeTrue();
	}

	[Fact]
	public void HaveValidationTimeoutOf30SecondsByDefault()
	{
		// Act
		var options = new ConfigurationValidationOptions();

		// Assert
		options.ValidationTimeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void AllowDisablingEnabled()
	{
		// Arrange
		var options = new ConfigurationValidationOptions();

		// Act
		options.Enabled = false;

		// Assert
		options.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void AllowDisablingFailFast()
	{
		// Arrange
		var options = new ConfigurationValidationOptions();

		// Act
		options.FailFast = false;

		// Assert
		options.FailFast.ShouldBeFalse();
	}

	[Fact]
	public void AllowDisablingTreatValidatorExceptionsAsErrors()
	{
		// Arrange
		var options = new ConfigurationValidationOptions();

		// Act
		options.TreatValidatorExceptionsAsErrors = false;

		// Assert
		options.TreatValidatorExceptionsAsErrors.ShouldBeFalse();
	}

	[Fact]
	public void AllowCustomValidationTimeout()
	{
		// Arrange
		var options = new ConfigurationValidationOptions();

		// Act
		options.ValidationTimeout = TimeSpan.FromMinutes(5);

		// Assert
		options.ValidationTimeout.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void AllowZeroValidationTimeout()
	{
		// Arrange
		var options = new ConfigurationValidationOptions();

		// Act
		options.ValidationTimeout = TimeSpan.Zero;

		// Assert
		options.ValidationTimeout.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void AllowInfiniteValidationTimeout()
	{
		// Arrange
		var options = new ConfigurationValidationOptions();

		// Act
		options.ValidationTimeout = Timeout.InfiniteTimeSpan;

		// Assert
		options.ValidationTimeout.ShouldBe(Timeout.InfiniteTimeSpan);
	}
}
