// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Security;

namespace Excalibur.Dispatch.Security.Tests.Security.Signing;

/// <summary>
/// Unit tests for <see cref="SigningOptions"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
public sealed class SigningOptionsShould
{
	[Fact]
	public void HaveTrueEnabled_ByDefault()
	{
		// Arrange & Act
		var options = new SigningOptions();

		// Assert
		options.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void HaveHMACSHA256DefaultAlgorithm_ByDefault()
	{
		// Arrange & Act
		var options = new SigningOptions();

		// Assert
		options.DefaultAlgorithm.ShouldBe(SigningAlgorithm.HMACSHA256);
	}

	[Fact]
	public void HaveNullDefaultKeyId_ByDefault()
	{
		// Arrange & Act
		var options = new SigningOptions();

		// Assert
		options.DefaultKeyId.ShouldBeNull();
	}

	[Fact]
	public void Have5MaxSignatureAgeMinutes_ByDefault()
	{
		// Arrange & Act
		var options = new SigningOptions();

		// Assert
		options.MaxSignatureAgeMinutes.ShouldBe(5);
	}

	[Fact]
	public void HaveTrueIncludeTimestampByDefault_ByDefault()
	{
		// Arrange & Act
		var options = new SigningOptions();

		// Assert
		options.IncludeTimestampByDefault.ShouldBeTrue();
	}

	[Fact]
	public void Have30KeyRotationIntervalDays_ByDefault()
	{
		// Arrange & Act
		var options = new SigningOptions();

		// Assert
		options.KeyRotationIntervalDays.ShouldBe(30);
	}

	[Fact]
	public void AllowSettingEnabled()
	{
		// Arrange
		var options = new SigningOptions();

		// Act
		options.Enabled = false;

		// Assert
		options.Enabled.ShouldBeFalse();
	}

	[Theory]
	[InlineData(SigningAlgorithm.Unknown)]
	[InlineData(SigningAlgorithm.HMACSHA256)]
	[InlineData(SigningAlgorithm.HMACSHA512)]
	[InlineData(SigningAlgorithm.RSASHA256)]
	[InlineData(SigningAlgorithm.Ed25519)]
	public void AllowSettingDefaultAlgorithm(SigningAlgorithm algorithm)
	{
		// Arrange
		var options = new SigningOptions();

		// Act
		options.DefaultAlgorithm = algorithm;

		// Assert
		options.DefaultAlgorithm.ShouldBe(algorithm);
	}

	[Fact]
	public void AllowSettingDefaultKeyId()
	{
		// Arrange
		var options = new SigningOptions();

		// Act
		options.DefaultKeyId = "signing-key-v1";

		// Assert
		options.DefaultKeyId.ShouldBe("signing-key-v1");
	}

	[Fact]
	public void AllowSettingMaxSignatureAgeMinutes()
	{
		// Arrange
		var options = new SigningOptions();

		// Act
		options.MaxSignatureAgeMinutes = 15;

		// Assert
		options.MaxSignatureAgeMinutes.ShouldBe(15);
	}

	[Fact]
	public void AllowSettingIncludeTimestampByDefault()
	{
		// Arrange
		var options = new SigningOptions();

		// Act
		options.IncludeTimestampByDefault = false;

		// Assert
		options.IncludeTimestampByDefault.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingKeyRotationIntervalDays()
	{
		// Arrange
		var options = new SigningOptions();

		// Act
		options.KeyRotationIntervalDays = 90;

		// Assert
		options.KeyRotationIntervalDays.ShouldBe(90);
	}

	[Fact]
	public void AllowCreatingWithAllProperties()
	{
		// Arrange & Act
		var options = new SigningOptions
		{
			Enabled = true,
			DefaultAlgorithm = SigningAlgorithm.Ed25519,
			DefaultKeyId = "master-signing-key",
			MaxSignatureAgeMinutes = 10,
			IncludeTimestampByDefault = true,
			KeyRotationIntervalDays = 60,
		};

		// Assert
		options.Enabled.ShouldBeTrue();
		options.DefaultAlgorithm.ShouldBe(SigningAlgorithm.Ed25519);
		options.DefaultKeyId.ShouldBe("master-signing-key");
		options.MaxSignatureAgeMinutes.ShouldBe(10);
		options.IncludeTimestampByDefault.ShouldBeTrue();
		options.KeyRotationIntervalDays.ShouldBe(60);
	}

	[Fact]
	public void BeSealed()
	{
		// Assert
		typeof(SigningOptions).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void BePartialClass()
	{
		// SigningOptions is a partial class (has middleware extension in separate file)
		// Just verify it compiles and creates correctly
		var options = new SigningOptions();
		options.ShouldNotBeNull();
	}
}
