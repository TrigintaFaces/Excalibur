using Excalibur.Compliance.Configuration;
// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Security.Tests.Compliance.Configuration;

/// <summary>
/// Unit tests for <see cref="EncryptionOptions"/> and related options classes.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Security)]
public sealed class EncryptionOptionsShould
{
	#region EncryptionOptions Default Values Tests

	[Fact]
	public void EncryptionOptions_HaveDefaultPurposeOfDefault()
	{
		// Arrange & Act
		var options = new global::Excalibur.Compliance.Configuration.EncryptionOptions();

		// Assert
		options.DefaultPurpose.ShouldBe("default");
	}

	[Fact]
	public void EncryptionOptions_DefaultRequireFipsComplianceToFalse()
	{
		// Arrange & Act
		var options = new global::Excalibur.Compliance.Configuration.EncryptionOptions();

		// Assert
		options.RequireFipsCompliance.ShouldBeFalse();
	}

	[Fact]
	public void EncryptionOptions_DefaultTenantIdToNull()
	{
		// Arrange & Act
		var options = new global::Excalibur.Compliance.Configuration.EncryptionOptions();

		// Assert
		options.DefaultTenantId.ShouldBeNull();
	}

	[Fact]
	public void EncryptionOptions_DefaultIncludeTimingMetadataToTrue()
	{
		// Arrange & Act
		var options = new global::Excalibur.Compliance.Configuration.EncryptionOptions();

		// Assert
		options.IncludeTimingMetadata.ShouldBeTrue();
	}

	[Fact]
	public void EncryptionOptions_DefaultEncryptionAgeWarningThresholdToNull()
	{
		// Arrange & Act
		var options = new global::Excalibur.Compliance.Configuration.EncryptionOptions();

		// Assert
		options.EncryptionAgeWarningThreshold.ShouldBeNull();
	}

	#endregion EncryptionOptions Default Values Tests

	#region EncryptionOptions Property Setters Tests

	[Fact]
	public void EncryptionOptions_AllowSettingCustomPurpose()
	{
		// Arrange
		var options = new global::Excalibur.Compliance.Configuration.EncryptionOptions();

		// Act
		options.DefaultPurpose = "field-encryption";

		// Assert
		options.DefaultPurpose.ShouldBe("field-encryption");
	}

	[Fact]
	public void EncryptionOptions_AllowEnablingFipsCompliance()
	{
		// Arrange
		var options = new global::Excalibur.Compliance.Configuration.EncryptionOptions();

		// Act
		options.RequireFipsCompliance = true;

		// Assert
		options.RequireFipsCompliance.ShouldBeTrue();
	}

	[Fact]
	public void EncryptionOptions_AllowSettingTenantId()
	{
		// Arrange
		var options = new global::Excalibur.Compliance.Configuration.EncryptionOptions();

		// Act
		options.DefaultTenantId = "tenant-123";

		// Assert
		options.DefaultTenantId.ShouldBe("tenant-123");
	}

	[Fact]
	public void EncryptionOptions_AllowDisablingTimingMetadata()
	{
		// Arrange
		var options = new global::Excalibur.Compliance.Configuration.EncryptionOptions();

		// Act
		options.IncludeTimingMetadata = false;

		// Assert
		options.IncludeTimingMetadata.ShouldBeFalse();
	}

	[Fact]
	public void EncryptionOptions_AllowSettingWarningThreshold()
	{
		// Arrange
		var options = new global::Excalibur.Compliance.Configuration.EncryptionOptions();
		var threshold = TimeSpan.FromDays(90);

		// Act
		options.EncryptionAgeWarningThreshold = threshold;

		// Assert
		options.EncryptionAgeWarningThreshold.ShouldBe(threshold);
	}

	#endregion EncryptionOptions Property Setters Tests
}
