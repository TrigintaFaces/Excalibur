// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Caching;

namespace Excalibur.Dispatch.Caching.Tests;

/// <summary>
/// Unit tests for <see cref="CacheOptions"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Caching")]
[Trait("Feature", "Configuration")]
public sealed class CacheOptionsShould : UnitTestBase
{
	[Fact]
	public void HaveEnabledFalse_ByDefault()
	{
		// Arrange & Act
		var options = new CacheOptions();

		// Assert
		options.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void HaveHybridCacheMode_ByDefault()
	{
		// Arrange & Act
		var options = new CacheOptions();

		// Assert
		options.CacheMode.ShouldBe(CacheMode.Hybrid);
	}

	[Fact]
	public void HaveTenMinuteDefaultExpiration()
	{
		// Arrange & Act
		var options = new CacheOptions();

		// Assert
		options.Behavior.DefaultExpiration.ShouldBe(TimeSpan.FromMinutes(10));
	}

	[Fact]
	public void HaveSlidingExpirationEnabled_ByDefault()
	{
		// Arrange & Act
		var options = new CacheOptions();

		// Assert
		options.Behavior.UseSlidingExpiration.ShouldBeTrue();
	}

	[Fact]
	public void HaveEmptyDefaultTags_ByDefault()
	{
		// Arrange & Act
		var options = new CacheOptions();

		// Assert
		options.DefaultTags.ShouldNotBeNull();
		options.DefaultTags.ShouldBeEmpty();
	}

	[Fact]
	public void Have200MsCacheTimeout_ByDefault()
	{
		// Arrange & Act
		var options = new CacheOptions();

		// Assert
		options.Behavior.CacheTimeout.ShouldBe(TimeSpan.FromMilliseconds(200));
	}

	[Fact]
	public void HaveTenPercentJitterRatio_ByDefault()
	{
		// Arrange & Act
		var options = new CacheOptions();

		// Assert
		options.Behavior.JitterRatio.ShouldBe(0.10);
	}

	[Fact]
	public void HaveNullGlobalPolicy_ByDefault()
	{
		// Arrange & Act
		var options = new CacheOptions();

		// Assert
		options.GlobalPolicy.ShouldBeNull();
	}

	[Fact]
	public void HaveNullCacheKeyBuilder_ByDefault()
	{
		// Arrange & Act
		var options = new CacheOptions();

		// Assert
		options.CacheKeyBuilder.ShouldBeNull();
	}

	[Fact]
	public void HaveMemoryConfiguration_ByDefault()
	{
		// Arrange & Act
		var options = new CacheOptions();

		// Assert
		options.Memory.ShouldNotBeNull();
	}

	[Fact]
	public void HaveDistributedConfiguration_ByDefault()
	{
		// Arrange & Act
		var options = new CacheOptions();

		// Assert
		options.Distributed.ShouldNotBeNull();
	}

	[Fact]
	public void HaveStatisticsDisabled_ByDefault()
	{
		// Arrange & Act
		var options = new CacheOptions();

		// Assert
		options.Behavior.EnableStatistics.ShouldBeFalse();
	}

	[Fact]
	public void HaveCompressionDisabled_ByDefault()
	{
		// Arrange & Act
		var options = new CacheOptions();

		// Assert
		options.Behavior.EnableCompression.ShouldBeFalse();
	}

	[Fact]
	public void HaveResilienceConfiguration_ByDefault()
	{
		// Arrange & Act
		var options = new CacheOptions();

		// Assert
		options.Resilience.ShouldNotBeNull();
	}

	[Fact]
	public void UseDistributedCache_ReturnsTrueForDistributedMode()
	{
		// Arrange
		var options = new CacheOptions
		{
			CacheMode = CacheMode.Distributed
		};

		// Assert
		options.UseDistributedCache.ShouldBeTrue();
	}

	[Fact]
	public void UseDistributedCache_ReturnsTrueForHybridMode()
	{
		// Arrange
		var options = new CacheOptions
		{
			CacheMode = CacheMode.Hybrid
		};

		// Assert
		options.UseDistributedCache.ShouldBeTrue();
	}

	[Fact]
	public void UseDistributedCache_ReturnsFalseForMemoryMode()
	{
		// Arrange
		var options = new CacheOptions
		{
			CacheMode = CacheMode.Memory
		};

		// Assert
		options.UseDistributedCache.ShouldBeFalse();
	}

	[Fact]
	public void SettingUseDistributedCacheTrue_SetsCacheModeToDistributed()
	{
		// Arrange
		var options = new CacheOptions();

		// Act
		options.UseDistributedCache = true;

		// Assert
		options.CacheMode.ShouldBe(CacheMode.Distributed);
	}

	[Fact]
	public void SettingUseDistributedCacheTrue_PreservesExplicitHybridMode()
	{
		// Arrange
		var options = new CacheOptions
		{
			CacheMode = CacheMode.Hybrid,
		};

		// Act
		options.UseDistributedCache = true;

		// Assert
		options.CacheMode.ShouldBe(CacheMode.Hybrid);
	}

	[Fact]
	public void SettingUseDistributedCacheFalse_SetsCacheModeToMemory()
	{
		// Arrange
		var options = new CacheOptions();

		// Act
		options.UseDistributedCache = false;

		// Assert
		options.CacheMode.ShouldBe(CacheMode.Memory);
	}

	[Fact]
	public void AllowSettingEnabled()
	{
		// Act
		var options = new CacheOptions
		{
			Enabled = true
		};

		// Assert
		options.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingDefaultExpiration()
	{
		// Arrange
		var expiration = TimeSpan.FromHours(1);

		// Act
		var options = new CacheOptions();
		options.Behavior.DefaultExpiration = expiration;

		// Assert
		options.Behavior.DefaultExpiration.ShouldBe(expiration);
	}

	[Fact]
	public void AllowSettingDefaultTags()
	{
		// Arrange
		var tags = new[] { "tag1", "tag2" };

		// Act
		var options = new CacheOptions
		{
			DefaultTags = tags
		};

		// Assert
		options.DefaultTags.ShouldBe(tags);
	}
}
