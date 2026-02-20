// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Caching;

namespace Excalibur.Dispatch.Caching.Tests;

/// <summary>
/// Unit tests for <see cref="MemoryCacheConfiguration"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Caching")]
public sealed class MemoryCacheConfigurationShould
{
	#region Default Value Tests

	[Fact]
	public void HaveNullSizeLimit_ByDefault()
	{
		// Arrange & Act
		var config = new MemoryCacheConfiguration();

		// Assert
		config.SizeLimit.ShouldBeNull();
	}

	[Fact]
	public void HaveDefaultCompactionPercentage()
	{
		// Arrange & Act
		var config = new MemoryCacheConfiguration();

		// Assert
		config.CompactionPercentage.ShouldBe(0.05);
	}

	[Fact]
	public void HaveDefaultExpirationScanFrequency()
	{
		// Arrange & Act
		var config = new MemoryCacheConfiguration();

		// Assert
		config.ExpirationScanFrequency.ShouldBe(TimeSpan.FromMinutes(1));
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void AllowSettingSizeLimit()
	{
		// Arrange
		var config = new MemoryCacheConfiguration();

		// Act
		config.SizeLimit = 1024 * 1024 * 100; // 100 MB

		// Assert
		config.SizeLimit.ShouldBe(1024 * 1024 * 100);
	}

	[Fact]
	public void AllowSettingCompactionPercentage()
	{
		// Arrange
		var config = new MemoryCacheConfiguration();

		// Act
		config.CompactionPercentage = 0.10;

		// Assert
		config.CompactionPercentage.ShouldBe(0.10);
	}

	[Fact]
	public void AllowSettingExpirationScanFrequency()
	{
		// Arrange
		var config = new MemoryCacheConfiguration();

		// Act
		config.ExpirationScanFrequency = TimeSpan.FromSeconds(30);

		// Assert
		config.ExpirationScanFrequency.ShouldBe(TimeSpan.FromSeconds(30));
	}

	#endregion

	#region Complete Configuration Tests

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var config = new MemoryCacheConfiguration
		{
			SizeLimit = 500_000,
			CompactionPercentage = 0.20,
			ExpirationScanFrequency = TimeSpan.FromMinutes(5),
		};

		// Assert
		config.SizeLimit.ShouldBe(500_000);
		config.CompactionPercentage.ShouldBe(0.20);
		config.ExpirationScanFrequency.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void AllowZeroSizeLimit()
	{
		// Arrange
		var config = new MemoryCacheConfiguration();

		// Act
		config.SizeLimit = 0;

		// Assert
		config.SizeLimit.ShouldBe(0);
	}

	[Fact]
	public void AllowZeroCompactionPercentage()
	{
		// Arrange
		var config = new MemoryCacheConfiguration();

		// Act
		config.CompactionPercentage = 0;

		// Assert
		config.CompactionPercentage.ShouldBe(0);
	}

	[Fact]
	public void AllowFullCompactionPercentage()
	{
		// Arrange
		var config = new MemoryCacheConfiguration();

		// Act
		config.CompactionPercentage = 1.0;

		// Assert
		config.CompactionPercentage.ShouldBe(1.0);
	}

	#endregion
}
