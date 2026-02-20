// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Security.Tests.Compliance.Configuration;

/// <summary>
/// Unit tests for <see cref="EncryptionOptions"/> and related options classes.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class EncryptionOptionsShould
{
	#region EncryptionOptions Default Values Tests

	[Fact]
	public void EncryptionOptions_HaveDefaultPurposeOfDefault()
	{
		// Arrange & Act
		var options = new Dispatch.Compliance.EncryptionOptions();

		// Assert
		options.DefaultPurpose.ShouldBe("default");
	}

	[Fact]
	public void EncryptionOptions_DefaultRequireFipsComplianceToFalse()
	{
		// Arrange & Act
		var options = new Dispatch.Compliance.EncryptionOptions();

		// Assert
		options.RequireFipsCompliance.ShouldBeFalse();
	}

	[Fact]
	public void EncryptionOptions_DefaultTenantIdToNull()
	{
		// Arrange & Act
		var options = new Dispatch.Compliance.EncryptionOptions();

		// Assert
		options.DefaultTenantId.ShouldBeNull();
	}

	[Fact]
	public void EncryptionOptions_DefaultIncludeTimingMetadataToTrue()
	{
		// Arrange & Act
		var options = new Dispatch.Compliance.EncryptionOptions();

		// Assert
		options.IncludeTimingMetadata.ShouldBeTrue();
	}

	[Fact]
	public void EncryptionOptions_DefaultEncryptionAgeWarningThresholdToNull()
	{
		// Arrange & Act
		var options = new Dispatch.Compliance.EncryptionOptions();

		// Assert
		options.EncryptionAgeWarningThreshold.ShouldBeNull();
	}

	#endregion EncryptionOptions Default Values Tests

	#region EncryptionOptions Property Setters Tests

	[Fact]
	public void EncryptionOptions_AllowSettingCustomPurpose()
	{
		// Arrange
		var options = new Dispatch.Compliance.EncryptionOptions();

		// Act
		options.DefaultPurpose = "field-encryption";

		// Assert
		options.DefaultPurpose.ShouldBe("field-encryption");
	}

	[Fact]
	public void EncryptionOptions_AllowEnablingFipsCompliance()
	{
		// Arrange
		var options = new Dispatch.Compliance.EncryptionOptions();

		// Act
		options.RequireFipsCompliance = true;

		// Assert
		options.RequireFipsCompliance.ShouldBeTrue();
	}

	[Fact]
	public void EncryptionOptions_AllowSettingTenantId()
	{
		// Arrange
		var options = new Dispatch.Compliance.EncryptionOptions();

		// Act
		options.DefaultTenantId = "tenant-123";

		// Assert
		options.DefaultTenantId.ShouldBe("tenant-123");
	}

	[Fact]
	public void EncryptionOptions_AllowDisablingTimingMetadata()
	{
		// Arrange
		var options = new Dispatch.Compliance.EncryptionOptions();

		// Act
		options.IncludeTimingMetadata = false;

		// Assert
		options.IncludeTimingMetadata.ShouldBeFalse();
	}

	[Fact]
	public void EncryptionOptions_AllowSettingWarningThreshold()
	{
		// Arrange
		var options = new Dispatch.Compliance.EncryptionOptions();
		var threshold = TimeSpan.FromDays(90);

		// Act
		options.EncryptionAgeWarningThreshold = threshold;

		// Assert
		options.EncryptionAgeWarningThreshold.ShouldBe(threshold);
	}

	#endregion EncryptionOptions Property Setters Tests
}

/// <summary>
/// Unit tests for <see cref="EncryptionMigrationOptions"/>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class EncryptionMigrationOptionsShould
{
	#region Default Values Tests

	[Fact]
	public void EncryptionMigrationOptions_DefaultBatchSizeTo100()
	{
		// Arrange & Act
		var options = new EncryptionMigrationOptions();

		// Assert
		options.BatchSize.ShouldBe(100);
	}

	[Fact]
	public void EncryptionMigrationOptions_DefaultMaxDegreeOfParallelismTo4()
	{
		// Arrange & Act
		var options = new EncryptionMigrationOptions();

		// Assert
		options.MaxDegreeOfParallelism.ShouldBe(4);
	}

	[Fact]
	public void EncryptionMigrationOptions_DefaultContinueOnErrorToFalse()
	{
		// Arrange & Act
		var options = new EncryptionMigrationOptions();

		// Assert
		options.ContinueOnError.ShouldBeFalse();
	}

	[Fact]
	public void EncryptionMigrationOptions_DefaultDelayBetweenBatchesToZero()
	{
		// Arrange & Act
		var options = new EncryptionMigrationOptions();

		// Assert
		options.DelayBetweenBatches.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void EncryptionMigrationOptions_DefaultSourceProviderIdToNull()
	{
		// Arrange & Act
		var options = new EncryptionMigrationOptions();

		// Assert
		options.SourceProviderId.ShouldBeNull();
	}

	[Fact]
	public void EncryptionMigrationOptions_DefaultTargetProviderIdToNull()
	{
		// Arrange & Act
		var options = new EncryptionMigrationOptions();

		// Assert
		options.TargetProviderId.ShouldBeNull();
	}

	[Fact]
	public void EncryptionMigrationOptions_DefaultVerifyBeforeReEncryptToTrue()
	{
		// Arrange & Act
		var options = new EncryptionMigrationOptions();

		// Assert
		options.VerifyBeforeReEncrypt.ShouldBeTrue();
	}

	[Fact]
	public void EncryptionMigrationOptions_DefaultOperationTimeoutTo30Seconds()
	{
		// Arrange & Act
		var options = new EncryptionMigrationOptions();

		// Assert
		options.OperationTimeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	#endregion Default Values Tests

	#region Property Setters Tests

	[Fact]
	public void EncryptionMigrationOptions_AllowSettingBatchSize()
	{
		// Arrange
		var options = new EncryptionMigrationOptions();

		// Act
		options.BatchSize = 500;

		// Assert
		options.BatchSize.ShouldBe(500);
	}

	[Fact]
	public void EncryptionMigrationOptions_AllowSettingMaxDegreeOfParallelism()
	{
		// Arrange
		var options = new EncryptionMigrationOptions();

		// Act
		options.MaxDegreeOfParallelism = 8;

		// Assert
		options.MaxDegreeOfParallelism.ShouldBe(8);
	}

	[Fact]
	public void EncryptionMigrationOptions_AllowEnablingContinueOnError()
	{
		// Arrange
		var options = new EncryptionMigrationOptions();

		// Act
		options.ContinueOnError = true;

		// Assert
		options.ContinueOnError.ShouldBeTrue();
	}

	[Fact]
	public void EncryptionMigrationOptions_AllowSettingDelayBetweenBatches()
	{
		// Arrange
		var options = new EncryptionMigrationOptions();
		var delay = TimeSpan.FromMilliseconds(100);

		// Act
		options.DelayBetweenBatches = delay;

		// Assert
		options.DelayBetweenBatches.ShouldBe(delay);
	}

	[Fact]
	public void EncryptionMigrationOptions_AllowSettingSourceProviderId()
	{
		// Arrange
		var options = new EncryptionMigrationOptions();

		// Act
		options.SourceProviderId = "old-provider";

		// Assert
		options.SourceProviderId.ShouldBe("old-provider");
	}

	[Fact]
	public void EncryptionMigrationOptions_AllowSettingTargetProviderId()
	{
		// Arrange
		var options = new EncryptionMigrationOptions();

		// Act
		options.TargetProviderId = "new-provider";

		// Assert
		options.TargetProviderId.ShouldBe("new-provider");
	}

	[Fact]
	public void EncryptionMigrationOptions_AllowDisablingVerifyBeforeReEncrypt()
	{
		// Arrange
		var options = new EncryptionMigrationOptions();

		// Act
		options.VerifyBeforeReEncrypt = false;

		// Assert
		options.VerifyBeforeReEncrypt.ShouldBeFalse();
	}

	[Fact]
	public void EncryptionMigrationOptions_AllowSettingOperationTimeout()
	{
		// Arrange
		var options = new EncryptionMigrationOptions();
		var timeout = TimeSpan.FromMinutes(2);

		// Act
		options.OperationTimeout = timeout;

		// Assert
		options.OperationTimeout.ShouldBe(timeout);
	}

	#endregion Property Setters Tests
}

/// <summary>
/// Unit tests for <see cref="EncryptionProviderRegistryOptions"/>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class EncryptionProviderRegistryOptionsShould
{
	#region Default Values Tests

	[Fact]
	public void EncryptionProviderRegistryOptions_DefaultPrimaryProviderIdToNull()
	{
		// Arrange & Act
		var options = new EncryptionProviderRegistryOptions();

		// Assert
		options.PrimaryProviderId.ShouldBeNull();
	}

	[Fact]
	public void EncryptionProviderRegistryOptions_DefaultLegacyProviderIdsToEmptyList()
	{
		// Arrange & Act
		var options = new EncryptionProviderRegistryOptions();

		// Assert
		_ = options.LegacyProviderIds.ShouldNotBeNull();
		options.LegacyProviderIds.ShouldBeEmpty();
	}

	[Fact]
	public void EncryptionProviderRegistryOptions_DefaultThrowOnDecryptionProviderNotFoundToFalse()
	{
		// Arrange & Act
		var options = new EncryptionProviderRegistryOptions();

		// Assert
		options.ThrowOnDecryptionProviderNotFound.ShouldBeFalse();
	}

	[Fact]
	public void EncryptionProviderRegistryOptions_DefaultValidateOnStartupToTrue()
	{
		// Arrange & Act
		var options = new EncryptionProviderRegistryOptions();

		// Assert
		options.ValidateOnStartup.ShouldBeTrue();
	}

	#endregion Default Values Tests

	#region Property Setters Tests

	[Fact]
	public void EncryptionProviderRegistryOptions_AllowSettingPrimaryProviderId()
	{
		// Arrange
		var options = new EncryptionProviderRegistryOptions();

		// Act
		options.PrimaryProviderId = "azure-kv-primary";

		// Assert
		options.PrimaryProviderId.ShouldBe("azure-kv-primary");
	}

	[Fact]
	public void EncryptionProviderRegistryOptions_AllowSettingLegacyProviderIds()
	{
		// Arrange
		var options = new EncryptionProviderRegistryOptions();

		// Act
		options.LegacyProviderIds = ["legacy-1", "legacy-2"];

		// Assert
		options.LegacyProviderIds.Count.ShouldBe(2);
		options.LegacyProviderIds.ShouldContain("legacy-1");
		options.LegacyProviderIds.ShouldContain("legacy-2");
	}

	[Fact]
	public void EncryptionProviderRegistryOptions_AllowEnablingThrowOnDecryptionProviderNotFound()
	{
		// Arrange
		var options = new EncryptionProviderRegistryOptions();

		// Act
		options.ThrowOnDecryptionProviderNotFound = true;

		// Assert
		options.ThrowOnDecryptionProviderNotFound.ShouldBeTrue();
	}

	[Fact]
	public void EncryptionProviderRegistryOptions_AllowDisablingValidateOnStartup()
	{
		// Arrange
		var options = new EncryptionProviderRegistryOptions();

		// Act
		options.ValidateOnStartup = false;

		// Assert
		options.ValidateOnStartup.ShouldBeFalse();
	}

	[Fact]
	public void EncryptionProviderRegistryOptions_SupportAddingToLegacyProviderIds()
	{
		// Arrange
		var options = new EncryptionProviderRegistryOptions();

		// Act
		options.LegacyProviderIds.Add("legacy-provider-1");
		options.LegacyProviderIds.Add("legacy-provider-2");

		// Assert
		options.LegacyProviderIds.Count.ShouldBe(2);
		options.LegacyProviderIds[0].ShouldBe("legacy-provider-1");
		options.LegacyProviderIds[1].ShouldBe("legacy-provider-2");
	}

	#endregion Property Setters Tests
}
