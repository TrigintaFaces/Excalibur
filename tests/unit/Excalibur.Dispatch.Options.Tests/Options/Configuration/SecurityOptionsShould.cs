// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Configuration;

namespace Excalibur.Dispatch.Tests.Options.Configuration;

/// <summary>
/// Unit tests for <see cref="SecurityOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Dispatch")]
public sealed class SecurityOptionsShould
{
	[Fact]
	public void HaveDefaultValues()
	{
		// Arrange & Act
		var options = new SecurityOptions();

		// Assert
		options.EnableEncryption.ShouldBeFalse();
		options.EnableSigning.ShouldBeFalse();
		options.EnableRateLimiting.ShouldBeFalse();
		options.EnableValidation.ShouldBeTrue(); // Only this is true by default
	}

	[Fact]
	public void AllowEnablingEncryption()
	{
		// Arrange
		var options = new SecurityOptions();

		// Act
		options.EnableEncryption = true;

		// Assert
		options.EnableEncryption.ShouldBeTrue();
	}

	[Fact]
	public void AllowEnablingSigning()
	{
		// Arrange
		var options = new SecurityOptions();

		// Act
		options.EnableSigning = true;

		// Assert
		options.EnableSigning.ShouldBeTrue();
	}

	[Fact]
	public void AllowEnablingRateLimiting()
	{
		// Arrange
		var options = new SecurityOptions();

		// Act
		options.EnableRateLimiting = true;

		// Assert
		options.EnableRateLimiting.ShouldBeTrue();
	}

	[Fact]
	public void AllowDisablingValidation()
	{
		// Arrange
		var options = new SecurityOptions();

		// Act
		options.EnableValidation = false;

		// Assert
		options.EnableValidation.ShouldBeFalse();
	}

	[Theory]
	[InlineData(true, true, true, true)]
	[InlineData(false, false, false, false)]
	[InlineData(true, false, true, false)]
	[InlineData(false, true, false, true)]
	public void SupportVariousConfigurations(bool encryption, bool signing, bool rateLimiting, bool validation)
	{
		// Arrange & Act
		var options = new SecurityOptions
		{
			EnableEncryption = encryption,
			EnableSigning = signing,
			EnableRateLimiting = rateLimiting,
			EnableValidation = validation
		};

		// Assert
		options.EnableEncryption.ShouldBe(encryption);
		options.EnableSigning.ShouldBe(signing);
		options.EnableRateLimiting.ShouldBe(rateLimiting);
		options.EnableValidation.ShouldBe(validation);
	}

	[Fact]
	public void AllowSelectiveFeatureEnabling()
	{
		// Arrange - Start with defaults
		var options = new SecurityOptions();

		// Act - Enable encryption and signing
		options.EnableEncryption = true;
		options.EnableSigning = true;

		// Assert
		options.EnableEncryption.ShouldBeTrue();
		options.EnableSigning.ShouldBeTrue();
		options.EnableRateLimiting.ShouldBeFalse();
		options.EnableValidation.ShouldBeTrue();
	}

	[Fact]
	public void AllowEnablingAllSecurityFeatures()
	{
		// Arrange & Act
		var options = new SecurityOptions
		{
			EnableEncryption = true,
			EnableSigning = true,
			EnableRateLimiting = true,
			EnableValidation = true
		};

		// Assert - All features enabled
		options.EnableEncryption.ShouldBeTrue();
		options.EnableSigning.ShouldBeTrue();
		options.EnableRateLimiting.ShouldBeTrue();
		options.EnableValidation.ShouldBeTrue();
	}
}
