// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Security.Tests.Compliance.Encryption;

/// <summary>
/// Unit tests for <see cref="ReEncryptionContext"/> class.
/// </summary>
/// <remarks>
/// Per AD-256-1, these tests verify the re-encryption context configuration.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Compliance")]
public sealed class ReEncryptionContextShould
{
	#region Default Value Tests

	[Fact]
	public void HaveNullSourceProviderIdByDefault()
	{
		// Arrange & Act
		var context = new ReEncryptionContext();

		// Assert
		context.SourceProviderId.ShouldBeNull();
	}

	[Fact]
	public void HaveNullTargetProviderIdByDefault()
	{
		// Arrange & Act
		var context = new ReEncryptionContext();

		// Assert
		context.TargetProviderId.ShouldBeNull();
	}

	[Fact]
	public void HaveNullEncryptionContextByDefault()
	{
		// Arrange & Act
		var context = new ReEncryptionContext();

		// Assert
		context.EncryptionContext.ShouldBeNull();
	}

	[Fact]
	public void HaveVerifyBeforeReEncryptTrueByDefault()
	{
		// Arrange & Act
		var context = new ReEncryptionContext();

		// Assert
		context.VerifyBeforeReEncrypt.ShouldBeTrue();
	}

	#endregion Default Value Tests

	#region Property Assignment Tests

	[Theory]
	[InlineData("provider-old")]
	[InlineData("aws-kms-2023")]
	[InlineData("azure-keyvault-v1")]
	public void AllowSourceProviderIdConfiguration(string sourceProviderId)
	{
		// Arrange & Act
		var context = new ReEncryptionContext { SourceProviderId = sourceProviderId };

		// Assert
		context.SourceProviderId.ShouldBe(sourceProviderId);
	}

	[Theory]
	[InlineData("provider-new")]
	[InlineData("aws-kms-2024")]
	[InlineData("azure-keyvault-v2")]
	public void AllowTargetProviderIdConfiguration(string targetProviderId)
	{
		// Arrange & Act
		var context = new ReEncryptionContext { TargetProviderId = targetProviderId };

		// Assert
		context.TargetProviderId.ShouldBe(targetProviderId);
	}

	[Fact]
	public void AllowEncryptionContextConfiguration()
	{
		// Arrange
		var encContext = new EncryptionContext { TenantId = "tenant-1", Purpose = "key-rotation" };

		// Act
		var context = new ReEncryptionContext { EncryptionContext = encContext };

		// Assert
		_ = context.EncryptionContext.ShouldNotBeNull();
		context.EncryptionContext.TenantId.ShouldBe("tenant-1");
		context.EncryptionContext.Purpose.ShouldBe("key-rotation");
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void AllowVerifyBeforeReEncryptConfiguration(bool verify)
	{
		// Arrange & Act
		var context = new ReEncryptionContext { VerifyBeforeReEncrypt = verify };

		// Assert
		context.VerifyBeforeReEncrypt.ShouldBe(verify);
	}

	#endregion Property Assignment Tests

	#region Semantic Tests

	[Fact]
	public void SupportAutoDetectSourceProvider_WhenSourceProviderIdIsNull()
	{
		// Per AD-256-1: When null, provider is detected from encrypted data envelope
		var context = new ReEncryptionContext { SourceProviderId = null };

		context.SourceProviderId.ShouldBeNull();
	}

	[Fact]
	public void SupportPrimaryTargetProvider_WhenTargetProviderIdIsNull()
	{
		// Per AD-256-1: When null, GetPrimary() is used
		var context = new ReEncryptionContext { TargetProviderId = null };

		context.TargetProviderId.ShouldBeNull();
	}

	[Fact]
	public void BeFullyConfigurable()
	{
		// Arrange
		var encContext = new EncryptionContext { TenantId = "tenant-1", Purpose = "migration" };

		// Act
		var context = new ReEncryptionContext
		{
			SourceProviderId = "old-provider",
			TargetProviderId = "new-provider",
			EncryptionContext = encContext,
			VerifyBeforeReEncrypt = false
		};

		// Assert
		context.SourceProviderId.ShouldBe("old-provider");
		context.TargetProviderId.ShouldBe("new-provider");
		context.EncryptionContext.ShouldBe(encContext);
		context.VerifyBeforeReEncrypt.ShouldBeFalse();
	}

	[Fact]
	public void SupportKeyRotationUseCase()
	{
		// Per AD-256-1: Key rotation uses same provider type, different keys
		var context = new ReEncryptionContext
		{
			SourceProviderId = "aws-kms-key-2023",
			TargetProviderId = "aws-kms-key-2024",
			VerifyBeforeReEncrypt = true
		};

		context.SourceProviderId.ShouldNotBe(context.TargetProviderId);
		context.VerifyBeforeReEncrypt.ShouldBeTrue();
	}

	[Fact]
	public void SupportAlgorithmMigrationUseCase()
	{
		// Per AD-256-1: Algorithm migration uses different provider types
		var context = new ReEncryptionContext
		{
			SourceProviderId = "aes-128-provider",
			TargetProviderId = "aes-256-gcm-provider"
		};

		context.SourceProviderId.ShouldNotBe(context.TargetProviderId);
	}

	#endregion Semantic Tests
}
