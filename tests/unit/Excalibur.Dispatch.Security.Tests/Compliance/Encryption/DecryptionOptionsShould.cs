// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Security.Tests.Compliance.Encryption;

/// <summary>
/// Unit tests for <see cref="DecryptionOptions"/> class.
/// </summary>
/// <remarks>
/// Per AD-255-3, these tests verify the decryption options configuration.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Compliance")]
public sealed class DecryptionOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void HaveDefaultBatchSizeOf100()
	{
		// Arrange & Act
		var options = new DecryptionOptions();

		// Assert
		options.BatchSize.ShouldBe(100);
	}

	[Fact]
	public void HaveIncludeUnencryptedFieldsTrueByDefault()
	{
		// Arrange & Act
		var options = new DecryptionOptions();

		// Assert
		options.IncludeUnencryptedFields.ShouldBeTrue();
	}

	[Fact]
	public void HaveNullProviderIdByDefault()
	{
		// Arrange & Act
		var options = new DecryptionOptions();

		// Assert
		options.ProviderId.ShouldBeNull();
	}

	[Fact]
	public void HaveContinueOnErrorFalseByDefault()
	{
		// Arrange & Act
		var options = new DecryptionOptions();

		// Assert
		options.ContinueOnError.ShouldBeFalse();
	}

	[Fact]
	public void HaveNullContextByDefault()
	{
		// Arrange & Act
		var options = new DecryptionOptions();

		// Assert
		options.Context.ShouldBeNull();
	}

	#endregion Default Value Tests

	#region Property Assignment Tests

	[Theory]
	[InlineData(1)]
	[InlineData(50)]
	[InlineData(100)]
	[InlineData(500)]
	[InlineData(1000)]
	public void AllowBatchSizeConfiguration(int batchSize)
	{
		// Arrange & Act
		var options = new DecryptionOptions { BatchSize = batchSize };

		// Assert
		options.BatchSize.ShouldBe(batchSize);
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void AllowIncludeUnencryptedFieldsConfiguration(bool include)
	{
		// Arrange & Act
		var options = new DecryptionOptions { IncludeUnencryptedFields = include };

		// Assert
		options.IncludeUnencryptedFields.ShouldBe(include);
	}

	[Theory]
	[InlineData("provider-1")]
	[InlineData("aws-kms")]
	[InlineData("azure-keyvault")]
	public void AllowProviderIdConfiguration(string providerId)
	{
		// Arrange & Act
		var options = new DecryptionOptions { ProviderId = providerId };

		// Assert
		options.ProviderId.ShouldBe(providerId);
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void AllowContinueOnErrorConfiguration(bool continueOnError)
	{
		// Arrange & Act
		var options = new DecryptionOptions { ContinueOnError = continueOnError };

		// Assert
		options.ContinueOnError.ShouldBe(continueOnError);
	}

	[Fact]
	public void AllowContextConfiguration()
	{
		// Arrange
		var context = new EncryptionContext { TenantId = "tenant-1", Purpose = "test" };

		// Act
		var options = new DecryptionOptions { Context = context };

		// Assert
		_ = options.Context.ShouldNotBeNull();
		options.Context.TenantId.ShouldBe("tenant-1");
		options.Context.Purpose.ShouldBe("test");
	}

	#endregion Property Assignment Tests

	#region Semantic Tests

	[Fact]
	public void SupportAutoDetectProvider_WhenProviderIdIsNull()
	{
		// Per AD-255-3: When null, provider is detected from encrypted data envelope
		var options = new DecryptionOptions { ProviderId = null };

		options.ProviderId.ShouldBeNull();
	}

	[Fact]
	public void SupportSpecificProvider_WhenProviderIdIsSet()
	{
		// Per AD-255-3: Can specify a specific provider ID
		var options = new DecryptionOptions { ProviderId = "specific-provider" };

		_ = options.ProviderId.ShouldNotBeNull();
	}

	[Fact]
	public void BeFullyConfigurable()
	{
		// Arrange & Act
		var context = new EncryptionContext { TenantId = "tenant-1", Purpose = "export" };
		var options = new DecryptionOptions
		{
			BatchSize = 250,
			IncludeUnencryptedFields = false,
			ProviderId = "custom-provider",
			ContinueOnError = true,
			Context = context
		};

		// Assert
		options.BatchSize.ShouldBe(250);
		options.IncludeUnencryptedFields.ShouldBeFalse();
		options.ProviderId.ShouldBe("custom-provider");
		options.ContinueOnError.ShouldBeTrue();
		options.Context.ShouldBe(context);
	}

	#endregion Semantic Tests
}
