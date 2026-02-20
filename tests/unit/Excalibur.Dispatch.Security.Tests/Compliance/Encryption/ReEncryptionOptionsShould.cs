// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Security.Tests.Compliance.Encryption;

/// <summary>
/// Unit tests for <see cref="ReEncryptionOptions"/> class.
/// </summary>
/// <remarks>
/// Per AD-256-1, these tests verify the batch re-encryption options configuration.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Compliance")]
public sealed class ReEncryptionOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void HaveNullSourceProviderIdByDefault()
	{
		// Arrange & Act
		var options = new ReEncryptionOptions();

		// Assert
		options.SourceProviderId.ShouldBeNull();
	}

	[Fact]
	public void HaveNullTargetProviderIdByDefault()
	{
		// Arrange & Act
		var options = new ReEncryptionOptions();

		// Assert
		options.TargetProviderId.ShouldBeNull();
	}

	[Fact]
	public void HaveDefaultBatchSizeOf100()
	{
		// Arrange & Act
		var options = new ReEncryptionOptions();

		// Assert
		options.BatchSize.ShouldBe(100);
	}

	[Fact]
	public void HaveDefaultMaxDegreeOfParallelismOf4()
	{
		// Arrange & Act
		var options = new ReEncryptionOptions();

		// Assert
		options.MaxDegreeOfParallelism.ShouldBe(4);
	}

	[Fact]
	public void HaveContinueOnErrorFalseByDefault()
	{
		// Arrange & Act
		var options = new ReEncryptionOptions();

		// Assert
		options.ContinueOnError.ShouldBeFalse();
	}

	[Fact]
	public void HaveVerifyBeforeReEncryptTrueByDefault()
	{
		// Arrange & Act
		var options = new ReEncryptionOptions();

		// Assert
		options.VerifyBeforeReEncrypt.ShouldBeTrue();
	}

	[Fact]
	public void HaveNullContextByDefault()
	{
		// Arrange & Act
		var options = new ReEncryptionOptions();

		// Assert
		options.Context.ShouldBeNull();
	}

	[Fact]
	public void HaveDefaultItemTimeoutOf30Seconds()
	{
		// Arrange & Act
		var options = new ReEncryptionOptions();

		// Assert
		options.ItemTimeout.ShouldBe(TimeSpan.FromSeconds(30));
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
		var options = new ReEncryptionOptions { SourceProviderId = sourceProviderId };

		// Assert
		options.SourceProviderId.ShouldBe(sourceProviderId);
	}

	[Theory]
	[InlineData("provider-new")]
	[InlineData("aws-kms-2024")]
	[InlineData("azure-keyvault-v2")]
	public void AllowTargetProviderIdConfiguration(string targetProviderId)
	{
		// Arrange & Act
		var options = new ReEncryptionOptions { TargetProviderId = targetProviderId };

		// Assert
		options.TargetProviderId.ShouldBe(targetProviderId);
	}

	[Theory]
	[InlineData(1)]
	[InlineData(50)]
	[InlineData(100)]
	[InlineData(500)]
	[InlineData(1000)]
	public void AllowBatchSizeConfiguration(int batchSize)
	{
		// Arrange & Act
		var options = new ReEncryptionOptions { BatchSize = batchSize };

		// Assert
		options.BatchSize.ShouldBe(batchSize);
	}

	[Theory]
	[InlineData(1)]
	[InlineData(2)]
	[InlineData(4)]
	[InlineData(8)]
	[InlineData(16)]
	public void AllowMaxDegreeOfParallelismConfiguration(int maxDop)
	{
		// Arrange & Act
		var options = new ReEncryptionOptions { MaxDegreeOfParallelism = maxDop };

		// Assert
		options.MaxDegreeOfParallelism.ShouldBe(maxDop);
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void AllowContinueOnErrorConfiguration(bool continueOnError)
	{
		// Arrange & Act
		var options = new ReEncryptionOptions { ContinueOnError = continueOnError };

		// Assert
		options.ContinueOnError.ShouldBe(continueOnError);
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void AllowVerifyBeforeReEncryptConfiguration(bool verify)
	{
		// Arrange & Act
		var options = new ReEncryptionOptions { VerifyBeforeReEncrypt = verify };

		// Assert
		options.VerifyBeforeReEncrypt.ShouldBe(verify);
	}

	[Fact]
	public void AllowContextConfiguration()
	{
		// Arrange
		var context = new EncryptionContext { TenantId = "tenant-1", Purpose = "key-rotation" };

		// Act
		var options = new ReEncryptionOptions { Context = context };

		// Assert
		_ = options.Context.ShouldNotBeNull();
		options.Context.TenantId.ShouldBe("tenant-1");
		options.Context.Purpose.ShouldBe("key-rotation");
	}

	[Theory]
	[InlineData(5)]
	[InlineData(30)]
	[InlineData(60)]
	[InlineData(120)]
	public void AllowItemTimeoutConfiguration(int seconds)
	{
		// Arrange & Act
		var options = new ReEncryptionOptions { ItemTimeout = TimeSpan.FromSeconds(seconds) };

		// Assert
		options.ItemTimeout.ShouldBe(TimeSpan.FromSeconds(seconds));
	}

	#endregion Property Assignment Tests

	#region Semantic Tests

	[Fact]
	public void SupportAutoDetectSourceProvider_WhenSourceProviderIdIsNull()
	{
		// Per AD-256-1: When null, provider is detected from encrypted data envelope
		var options = new ReEncryptionOptions { SourceProviderId = null };

		options.SourceProviderId.ShouldBeNull();
	}

	[Fact]
	public void SupportPrimaryTargetProvider_WhenTargetProviderIdIsNull()
	{
		// Per AD-256-1: When null, GetPrimary() is used
		var options = new ReEncryptionOptions { TargetProviderId = null };

		options.TargetProviderId.ShouldBeNull();
	}

	[Fact]
	public void SupportSequentialProcessing_WhenMaxDegreeOfParallelismIsOne()
	{
		// Per AD-256-1: MaxDegreeOfParallelism = 1 means sequential processing
		var options = new ReEncryptionOptions { MaxDegreeOfParallelism = 1 };

		options.MaxDegreeOfParallelism.ShouldBe(1);
	}

	[Fact]
	public void BeFullyConfigurable()
	{
		// Arrange
		var context = new EncryptionContext { TenantId = "tenant-1", Purpose = "migration" };

		// Act
		var options = new ReEncryptionOptions
		{
			SourceProviderId = "old-provider",
			TargetProviderId = "new-provider",
			BatchSize = 500,
			MaxDegreeOfParallelism = 8,
			ContinueOnError = true,
			VerifyBeforeReEncrypt = false,
			Context = context,
			ItemTimeout = TimeSpan.FromMinutes(2)
		};

		// Assert
		options.SourceProviderId.ShouldBe("old-provider");
		options.TargetProviderId.ShouldBe("new-provider");
		options.BatchSize.ShouldBe(500);
		options.MaxDegreeOfParallelism.ShouldBe(8);
		options.ContinueOnError.ShouldBeTrue();
		options.VerifyBeforeReEncrypt.ShouldBeFalse();
		options.Context.ShouldBe(context);
		options.ItemTimeout.ShouldBe(TimeSpan.FromMinutes(2));
	}

	[Fact]
	public void SupportKeyRotationUseCase()
	{
		// Per AD-256-1: Key rotation with batch processing
		var options = new ReEncryptionOptions
		{
			SourceProviderId = "aws-kms-key-2023",
			TargetProviderId = "aws-kms-key-2024",
			BatchSize = 100,
			VerifyBeforeReEncrypt = true
		};

		options.SourceProviderId.ShouldNotBe(options.TargetProviderId);
		options.VerifyBeforeReEncrypt.ShouldBeTrue();
	}

	[Fact]
	public void SupportHighThroughputUseCase()
	{
		// Per AD-256-1: High throughput with parallel processing
		var options = new ReEncryptionOptions
		{
			BatchSize = 1000,
			MaxDegreeOfParallelism = 16,
			ContinueOnError = true
		};

		options.BatchSize.ShouldBe(1000);
		options.MaxDegreeOfParallelism.ShouldBe(16);
		options.ContinueOnError.ShouldBeTrue();
	}

	[Fact]
	public void SupportLowMemoryUseCase()
	{
		// Per AD-256-1: Low memory with small batches
		var options = new ReEncryptionOptions
		{
			BatchSize = 10,
			MaxDegreeOfParallelism = 1
		};

		options.BatchSize.ShouldBe(10);
		options.MaxDegreeOfParallelism.ShouldBe(1);
	}

	#endregion Semantic Tests
}
