// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.IO.Compression;

using Excalibur.Dispatch.Patterns.ClaimCheck;

namespace Excalibur.Dispatch.Patterns.Tests.ClaimCheck;

/// <summary>
/// Unit tests for <see cref="ClaimCheckOptions"/> class and its sub-options.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Patterns")]
public sealed class ClaimCheckOptionsShould
{
	// ============================================================================
	// Core options defaults
	// ============================================================================

	[Fact]
	public void Have256KBPayloadThreshold_ByDefault()
	{
		var options = new ClaimCheckOptions();
		options.PayloadThreshold.ShouldBe(256 * 1024);
	}

	[Fact]
	public void HaveCcIdPrefix_ByDefault()
	{
		var options = new ClaimCheckOptions();
		options.IdPrefix.ShouldBe("cc-");
	}

	[Fact]
	public void HaveTrueValidateChecksum_ByDefault()
	{
		var options = new ClaimCheckOptions();
		options.ValidateChecksum.ShouldBeTrue();
	}

	[Fact]
	public void HaveTrueEnableMetrics_ByDefault()
	{
		var options = new ClaimCheckOptions();
		options.EnableMetrics.ShouldBeTrue();
	}

	// ============================================================================
	// Backward-compatible delegating property defaults
	// ============================================================================

	[Fact]
	public void HaveEmptyConnectionString_ByDefault()
	{
		var options = new ClaimCheckOptions();
		options.ConnectionString.ShouldBe(string.Empty);
	}

	[Fact]
	public void HaveClaimChecksContainerName_ByDefault()
	{
		var options = new ClaimCheckOptions();
		options.ContainerName.ShouldBe("claim-checks");
	}

	[Fact]
	public void HaveTrueEnableCompression_ByDefault()
	{
		var options = new ClaimCheckOptions();
		options.EnableCompression.ShouldBeTrue();
	}

	[Fact]
	public void Have1KBCompressionThreshold_ByDefault()
	{
		var options = new ClaimCheckOptions();
		options.CompressionThreshold.ShouldBe(1024);
	}

	[Fact]
	public void HaveTrueEnableCleanup_ByDefault()
	{
		var options = new ClaimCheckOptions();
		options.EnableCleanup.ShouldBeTrue();
	}

	[Fact]
	public void HaveOneHourCleanupInterval_ByDefault()
	{
		var options = new ClaimCheckOptions();
		options.CleanupInterval.ShouldBe(TimeSpan.FromHours(1));
	}

	[Fact]
	public void HaveSevenDaysRetentionPeriod_ByDefault()
	{
		var options = new ClaimCheckOptions();
		options.RetentionPeriod.ShouldBe(TimeSpan.FromDays(7));
	}

	[Fact]
	public void HaveSevenDaysDefaultTtl_ByDefault()
	{
		var options = new ClaimCheckOptions();
		options.DefaultTtl.ShouldBe(TimeSpan.FromDays(7));
	}

	[Fact]
	public void Have0Point8MinCompressionRatio_ByDefault()
	{
		var options = new ClaimCheckOptions();
		options.MinCompressionRatio.ShouldBe(0.8);
	}

	[Fact]
	public void HaveOptimalCompressionLevel_ByDefault()
	{
		var options = new ClaimCheckOptions();
		options.CompressionLevel.ShouldBe(CompressionLevel.Optimal);
	}

	[Fact]
	public void HaveClaimsBlobNamePrefix_ByDefault()
	{
		var options = new ClaimCheckOptions();
		options.BlobNamePrefix.ShouldBe("claims");
	}

	// ============================================================================
	// Storage sub-options defaults
	// ============================================================================

	[Fact]
	public void HaveStorageSubOptions_WithDefaults()
	{
		var options = new ClaimCheckOptions();
		options.Storage.ShouldNotBeNull();
		options.Storage.ConnectionString.ShouldBe(string.Empty);
		options.Storage.ContainerName.ShouldBe("claim-checks");
		options.Storage.BlobNamePrefix.ShouldBe("claims");
		options.Storage.UseHierarchicalStorage.ShouldBeFalse();
		options.Storage.ColdStorageThreshold.ShouldBe(TimeSpan.FromDays(30));
		options.Storage.EnableEncryption.ShouldBeFalse();
		options.Storage.ChunkSize.ShouldBe(1024 * 1024);
		options.Storage.MaxConcurrency.ShouldBe(Environment.ProcessorCount);
		options.Storage.BufferPoolSize.ShouldBe(100);
		options.Storage.OperationTimeout.ShouldBe(TimeSpan.FromSeconds(30));
		options.Storage.MaxRetries.ShouldBe(3);
		options.Storage.RetryDelay.ShouldBe(TimeSpan.FromSeconds(1));
	}

	// ============================================================================
	// Compression sub-options defaults
	// ============================================================================

	[Fact]
	public void HaveCompressionSubOptions_WithDefaults()
	{
		var options = new ClaimCheckOptions();
		options.Compression.ShouldNotBeNull();
		options.Compression.EnableCompression.ShouldBeTrue();
		options.Compression.CompressionThreshold.ShouldBe(1024);
		options.Compression.MinCompressionRatio.ShouldBe(0.8);
		options.Compression.CompressionLevel.ShouldBe(CompressionLevel.Optimal);
	}

	// ============================================================================
	// Cleanup sub-options defaults
	// ============================================================================

	[Fact]
	public void HaveCleanupSubOptions_WithDefaults()
	{
		var options = new ClaimCheckOptions();
		options.Cleanup.ShouldNotBeNull();
		options.Cleanup.EnableCleanup.ShouldBeTrue();
		options.Cleanup.CleanupInterval.ShouldBe(TimeSpan.FromHours(1));
		options.Cleanup.DefaultTtl.ShouldBe(TimeSpan.FromDays(7));
		options.Cleanup.CleanupBatchSize.ShouldBe(1000);
	}

	// ============================================================================
	// Delegating property round-trip tests
	// ============================================================================

	[Fact]
	public void DelegateConnectionStringToStorage()
	{
		var options = new ClaimCheckOptions { ConnectionString = "Server=test" };
		options.Storage.ConnectionString.ShouldBe("Server=test");
	}

	[Fact]
	public void DelegateEnableCompressionToCompression()
	{
		var options = new ClaimCheckOptions { EnableCompression = false };
		options.Compression.EnableCompression.ShouldBeFalse();
	}

	[Fact]
	public void DelegateEnableCleanupToCleanup()
	{
		var options = new ClaimCheckOptions { EnableCleanup = false };
		options.Cleanup.EnableCleanup.ShouldBeFalse();
	}

	[Fact]
	public void DelegateDefaultTtlToCleanup()
	{
		var options = new ClaimCheckOptions { DefaultTtl = TimeSpan.FromDays(3) };
		options.Cleanup.DefaultTtl.ShouldBe(TimeSpan.FromDays(3));
	}

	// ============================================================================
	// Full creation test
	// ============================================================================

	[Fact]
	public void AllowCreatingWithAllProperties()
	{
		var options = new ClaimCheckOptions
		{
			ConnectionString = "DefaultEndpointsProtocol=https;AccountName=test",
			ContainerName = "my-claims",
			PayloadThreshold = 512 * 1024,
			EnableCompression = true,
			CompressionThreshold = 2048,
			EnableCleanup = true,
			CleanupInterval = TimeSpan.FromMinutes(30),
			RetentionPeriod = TimeSpan.FromDays(14),
			DefaultTtl = TimeSpan.FromDays(10),
			ValidateChecksum = true,
			IdPrefix = "cc_",
			MinCompressionRatio = 0.7,
			CompressionLevel = CompressionLevel.Fastest,
			BlobNamePrefix = "payloads",
			EnableMetrics = true,
		};

		options.ConnectionString.ShouldContain("AccountName=test");
		options.ContainerName.ShouldBe("my-claims");
		options.PayloadThreshold.ShouldBe(512 * 1024);
		options.EnableCompression.ShouldBeTrue();
		options.CompressionLevel.ShouldBe(CompressionLevel.Fastest);
		options.EnableMetrics.ShouldBeTrue();

		// Verify delegation to sub-options
		options.Storage.ConnectionString.ShouldContain("AccountName=test");
		options.Compression.CompressionThreshold.ShouldBe(2048);
		options.Cleanup.EnableCleanup.ShouldBeTrue();
		options.Cleanup.DefaultTtl.ShouldBe(TimeSpan.FromDays(10));
	}

	[Fact]
	public void AllowCreatingWithSubOptionsDirectly()
	{
		var options = new ClaimCheckOptions
		{
			PayloadThreshold = 128 * 1024,
			Storage =
			{
				ConnectionString = "Server=test",
				UseHierarchicalStorage = true,
				ColdStorageThreshold = TimeSpan.FromDays(60),
				EnableEncryption = true,
				MaxRetries = 5,
			},
			Compression =
			{
				EnableCompression = true,
				CompressionLevel = CompressionLevel.Fastest,
				MinCompressionRatio = 0.6,
			},
			Cleanup =
			{
				EnableCleanup = true,
				CleanupBatchSize = 500,
				DefaultTtl = TimeSpan.FromDays(3),
			},
		};

		options.PayloadThreshold.ShouldBe(128 * 1024);
		options.Storage.UseHierarchicalStorage.ShouldBeTrue();
		options.Storage.EnableEncryption.ShouldBeTrue();
		options.Compression.CompressionLevel.ShouldBe(CompressionLevel.Fastest);
		options.Cleanup.CleanupBatchSize.ShouldBe(500);
	}
}
