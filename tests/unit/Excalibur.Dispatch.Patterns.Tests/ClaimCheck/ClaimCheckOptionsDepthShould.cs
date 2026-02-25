// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.IO.Compression;

namespace Excalibur.Dispatch.Patterns.Tests.ClaimCheck;

/// <summary>
/// Depth coverage tests for <see cref="ClaimCheckOptions"/> delegating properties.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ClaimCheckOptionsDepthShould
{
	[Fact]
	public void Defaults_AreReasonable()
	{
		var options = new ClaimCheckOptions();
		options.PayloadThreshold.ShouldBe(256 * 1024);
		options.IdPrefix.ShouldBe("cc-");
		options.ValidateChecksum.ShouldBeTrue();
		options.EnableMetrics.ShouldBeTrue();
	}

	[Fact]
	public void ConnectionString_DelegatesToStorage()
	{
		var options = new ClaimCheckOptions { ConnectionString = "connstr" };
		options.Storage.ConnectionString.ShouldBe("connstr");
		options.ConnectionString.ShouldBe("connstr");
	}

	[Fact]
	public void ContainerName_DelegatesToStorage()
	{
		var options = new ClaimCheckOptions { ContainerName = "my-container" };
		options.Storage.ContainerName.ShouldBe("my-container");
		options.ContainerName.ShouldBe("my-container");
	}

	[Fact]
	public void BlobNamePrefix_DelegatesToStorage()
	{
		var options = new ClaimCheckOptions { BlobNamePrefix = "prefix/" };
		options.Storage.BlobNamePrefix.ShouldBe("prefix/");
		options.BlobNamePrefix.ShouldBe("prefix/");
	}

	[Fact]
	public void EnableCompression_DelegatesToCompression()
	{
		var options = new ClaimCheckOptions { EnableCompression = true };
		options.Compression.EnableCompression.ShouldBeTrue();
		options.EnableCompression.ShouldBeTrue();
	}

	[Fact]
	public void CompressionThreshold_DelegatesToCompression()
	{
		var options = new ClaimCheckOptions { CompressionThreshold = 512 };
		options.Compression.CompressionThreshold.ShouldBe(512);
		options.CompressionThreshold.ShouldBe(512);
	}

	[Fact]
	public void MinCompressionRatio_DelegatesToCompression()
	{
		var options = new ClaimCheckOptions { MinCompressionRatio = 0.5 };
		options.Compression.MinCompressionRatio.ShouldBe(0.5);
	}

	[Fact]
	public void CompressionLevel_DelegatesToCompression()
	{
		var options = new ClaimCheckOptions { CompressionLevel = CompressionLevel.Fastest };
		options.Compression.CompressionLevel.ShouldBe(CompressionLevel.Fastest);
	}

	[Fact]
	public void EnableCleanup_DelegatesToCleanup()
	{
		var options = new ClaimCheckOptions { EnableCleanup = false };
		options.Cleanup.EnableCleanup.ShouldBeFalse();
		options.EnableCleanup.ShouldBeFalse();
	}

	[Fact]
	public void CleanupInterval_DelegatesToCleanup()
	{
		var interval = TimeSpan.FromMinutes(30);
		var options = new ClaimCheckOptions { CleanupInterval = interval };
		options.Cleanup.CleanupInterval.ShouldBe(interval);
		options.CleanupInterval.ShouldBe(interval);
	}

	[Fact]
	public void RetentionPeriod_DelegatesToDefaultTtl()
	{
		var ttl = TimeSpan.FromDays(7);
		var options = new ClaimCheckOptions { RetentionPeriod = ttl };
		options.Cleanup.DefaultTtl.ShouldBe(ttl);
		options.RetentionPeriod.ShouldBe(ttl);
	}

	[Fact]
	public void DefaultTtl_DelegatesToCleanup()
	{
		var ttl = TimeSpan.FromHours(48);
		var options = new ClaimCheckOptions { DefaultTtl = ttl };
		options.Cleanup.DefaultTtl.ShouldBe(ttl);
		options.DefaultTtl.ShouldBe(ttl);
	}
}
