// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;
using Excalibur.Dispatch.Security;

namespace Excalibur.Dispatch.Security.Tests.Security.Encryption;

/// <summary>
/// Unit tests for <see cref="EncryptionOptions"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
public sealed class EncryptionOptionsShould
{
	[Fact]
	public void HaveTrueEnabled_ByDefault()
	{
		// Arrange & Act
		var options = new EncryptionOptions();

		// Assert
		options.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void HaveNullCurrentKeyId_ByDefault()
	{
		// Arrange & Act
		var options = new EncryptionOptions();

		// Assert
		options.CurrentKeyId.ShouldBeNull();
	}

	[Fact]
	public void HaveTrueIncludeMetadataHeader_ByDefault()
	{
		// Arrange & Act
		var options = new EncryptionOptions();

		// Assert
		options.IncludeMetadataHeader.ShouldBeTrue();
	}

	[Fact]
	public void HaveAes256GcmDefaultAlgorithm_ByDefault()
	{
		// Arrange & Act
		var options = new EncryptionOptions();

		// Assert
		options.DefaultAlgorithm.ShouldBe(EncryptionAlgorithm.Aes256Gcm);
	}

	[Fact]
	public void HaveFalseEnableCompressionByDefault_ByDefault()
	{
		// Arrange & Act
		var options = new EncryptionOptions();

		// Assert
		options.EnableCompressionByDefault.ShouldBeFalse();
	}

	[Fact]
	public void Have90KeyRotationIntervalDays_ByDefault()
	{
		// Arrange & Act
		var options = new EncryptionOptions();

		// Assert
		options.KeyRotationIntervalDays.ShouldBe(90);
	}

	[Fact]
	public void HaveNullAzureKeyVaultUrl_ByDefault()
	{
		// Arrange & Act
		var options = new EncryptionOptions();

		// Assert
		options.AzureKeyVaultUrl.ShouldBeNull();
	}

	[Fact]
	public void HaveNullAwsKmsKeyArn_ByDefault()
	{
		// Arrange & Act
		var options = new EncryptionOptions();

		// Assert
		options.AwsKmsKeyArn.ShouldBeNull();
	}

	[Fact]
	public void HaveTrueEncryptByDefault_ByDefault()
	{
		// Arrange & Act
		var options = new EncryptionOptions();

		// Assert
		options.EncryptByDefault.ShouldBeTrue();
	}

	[Fact]
	public void HaveNullExcludedMessageTypes_ByDefault()
	{
		// Arrange & Act
		var options = new EncryptionOptions();

		// Assert
		options.ExcludedMessageTypes.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingEnabled()
	{
		// Arrange
		var options = new EncryptionOptions();

		// Act
		options.Enabled = false;

		// Assert
		options.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingCurrentKeyId()
	{
		// Arrange
		var options = new EncryptionOptions();

		// Act
		options.CurrentKeyId = "key-2024-01";

		// Assert
		options.CurrentKeyId.ShouldBe("key-2024-01");
	}

	[Fact]
	public void AllowSettingIncludeMetadataHeader()
	{
		// Arrange
		var options = new EncryptionOptions();

		// Act
		options.IncludeMetadataHeader = false;

		// Assert
		options.IncludeMetadataHeader.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingDefaultAlgorithm()
	{
		// Arrange
		var options = new EncryptionOptions();

		// Act
		options.DefaultAlgorithm = EncryptionAlgorithm.Aes256CbcHmac;

		// Assert
		options.DefaultAlgorithm.ShouldBe(EncryptionAlgorithm.Aes256CbcHmac);
	}

	[Fact]
	public void AllowSettingEnableCompressionByDefault()
	{
		// Arrange
		var options = new EncryptionOptions();

		// Act
		options.EnableCompressionByDefault = true;

		// Assert
		options.EnableCompressionByDefault.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingKeyRotationIntervalDays()
	{
		// Arrange
		var options = new EncryptionOptions();

		// Act
		options.KeyRotationIntervalDays = 30;

		// Assert
		options.KeyRotationIntervalDays.ShouldBe(30);
	}

	[Fact]
	public void AllowSettingAzureKeyVaultUrl()
	{
		// Arrange
		var options = new EncryptionOptions();

		// Act
		options.AzureKeyVaultUrl = new Uri("https://my-vault.vault.azure.net");

		// Assert
		options.AzureKeyVaultUrl.ShouldNotBeNull();
		options.AzureKeyVaultUrl.AbsoluteUri.ShouldBe("https://my-vault.vault.azure.net/");
	}

	[Fact]
	public void AllowSettingAwsKmsKeyArn()
	{
		// Arrange
		var options = new EncryptionOptions();

		// Act
		options.AwsKmsKeyArn = "arn:aws:kms:us-east-1:123456789012:key/12345678-1234-1234-1234-123456789012";

		// Assert
		options.AwsKmsKeyArn.ShouldBe("arn:aws:kms:us-east-1:123456789012:key/12345678-1234-1234-1234-123456789012");
	}

	[Fact]
	public void AllowSettingEncryptByDefault()
	{
		// Arrange
		var options = new EncryptionOptions();

		// Act
		options.EncryptByDefault = false;

		// Assert
		options.EncryptByDefault.ShouldBeFalse();
	}

	[Fact]
	public void AllowInitializingExcludedMessageTypes()
	{
		// Arrange & Act
		var options = new EncryptionOptions
		{
			ExcludedMessageTypes = new HashSet<string> { "HealthCheck", "Ping" },
		};

		// Assert
		options.ExcludedMessageTypes.ShouldNotBeNull();
		options.ExcludedMessageTypes.Count.ShouldBe(2);
		options.ExcludedMessageTypes.ShouldContain("HealthCheck");
		options.ExcludedMessageTypes.ShouldContain("Ping");
	}

	[Fact]
	public void AllowCreatingWithAllProperties()
	{
		// Arrange & Act
		var options = new EncryptionOptions
		{
			Enabled = true,
			CurrentKeyId = "master-key-v2",
			IncludeMetadataHeader = true,
			DefaultAlgorithm = EncryptionAlgorithm.Aes256Gcm,
			EnableCompressionByDefault = true,
			KeyRotationIntervalDays = 60,
			AzureKeyVaultUrl = new Uri("https://vault.azure.net"),
			AwsKmsKeyArn = "arn:aws:kms:us-west-2:111122223333:key/abcd1234",
			EncryptByDefault = true,
			ExcludedMessageTypes = new HashSet<string> { "LogEvent" },
		};

		// Assert
		options.Enabled.ShouldBeTrue();
		options.CurrentKeyId.ShouldBe("master-key-v2");
		options.IncludeMetadataHeader.ShouldBeTrue();
		options.DefaultAlgorithm.ShouldBe(EncryptionAlgorithm.Aes256Gcm);
		options.EnableCompressionByDefault.ShouldBeTrue();
		options.KeyRotationIntervalDays.ShouldBe(60);
		options.AzureKeyVaultUrl.ShouldNotBeNull();
		options.AwsKmsKeyArn.ShouldBe("arn:aws:kms:us-west-2:111122223333:key/abcd1234");
		options.EncryptByDefault.ShouldBeTrue();
		options.ExcludedMessageTypes.ShouldNotBeNull();
		options.ExcludedMessageTypes.ShouldContain("LogEvent");
	}

	[Fact]
	public void BeSealed()
	{
		// Assert
		typeof(EncryptionOptions).IsSealed.ShouldBeTrue();
	}
}
