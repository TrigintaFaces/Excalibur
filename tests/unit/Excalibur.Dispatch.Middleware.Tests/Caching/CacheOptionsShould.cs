// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Caching;

namespace Excalibur.Dispatch.Middleware.Tests.Caching;

/// <summary>
/// Unit tests for <see cref="CacheOptions"/>.
/// Covers default values, property setters, and the UseDistributedCache convenience property.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Caching")]
public sealed class CacheOptionsShould : UnitTestBase
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new CacheOptions();

		// Assert
		options.Enabled.ShouldBeFalse();
		options.CacheMode.ShouldBe(CacheMode.Hybrid);
		options.Behavior.DefaultExpiration.ShouldBe(TimeSpan.FromMinutes(10));
		options.Behavior.UseSlidingExpiration.ShouldBeTrue();
		options.DefaultTags.ShouldBeEmpty();
		options.Behavior.CacheTimeout.ShouldBe(TimeSpan.FromMilliseconds(200));
		options.Behavior.JitterRatio.ShouldBe(0.10);
		options.GlobalPolicy.ShouldBeNull();
		options.CacheKeyBuilder.ShouldBeNull();
		options.Behavior.EnableStatistics.ShouldBeFalse();
		options.Behavior.EnableCompression.ShouldBeFalse();
	}

	[Fact]
	public void HaveDefaultMemoryConfiguration()
	{
		// Arrange & Act
		var options = new CacheOptions();

		// Assert
		options.Memory.ShouldNotBeNull();
	}

	[Fact]
	public void HaveDefaultDistributedConfiguration()
	{
		// Arrange & Act
		var options = new CacheOptions();

		// Assert
		options.Distributed.ShouldNotBeNull();
	}

	[Fact]
	public void HaveDefaultResilienceConfiguration()
	{
		// Arrange & Act
		var options = new CacheOptions();

		// Assert
		options.Resilience.ShouldNotBeNull();
	}

	[Fact]
	public void UseDistributedCache_WhenSetToTrue_SetsCacheModeToDistributed()
	{
		// Arrange
		var options = new CacheOptions();

		// Act
		options.UseDistributedCache = true;

		// Assert
		options.CacheMode.ShouldBe(CacheMode.Distributed);
	}

	[Fact]
	public void UseDistributedCache_WhenSetToTrue_PreservesExplicitHybridMode()
	{
		// Arrange
		var options = new CacheOptions { CacheMode = CacheMode.Hybrid };

		// Act
		options.UseDistributedCache = true;

		// Assert
		options.CacheMode.ShouldBe(CacheMode.Hybrid);
	}

	[Fact]
	public void UseDistributedCache_WhenSetToFalse_SetsCacheModeToMemory()
	{
		// Arrange
		var options = new CacheOptions();

		// Act
		options.UseDistributedCache = false;

		// Assert
		options.CacheMode.ShouldBe(CacheMode.Memory);
	}

	[Fact]
	public void UseDistributedCache_WhenCacheModeIsDistributed_ReturnsTrue()
	{
		// Arrange
		var options = new CacheOptions { CacheMode = CacheMode.Distributed };

		// Act & Assert
		options.UseDistributedCache.ShouldBeTrue();
	}

	[Fact]
	public void UseDistributedCache_WhenCacheModeIsHybrid_ReturnsTrue()
	{
		// Arrange
		var options = new CacheOptions { CacheMode = CacheMode.Hybrid };

		// Act & Assert
		options.UseDistributedCache.ShouldBeTrue();
	}

	[Fact]
	public void UseDistributedCache_WhenCacheModeIsMemory_ReturnsFalse()
	{
		// Arrange
		var options = new CacheOptions { CacheMode = CacheMode.Memory };

		// Act & Assert
		options.UseDistributedCache.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingEnabled()
	{
		// Arrange
		var options = new CacheOptions();

		// Act
		options.Enabled = true;

		// Assert
		options.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingDefaultExpiration()
	{
		// Arrange
		var options = new CacheOptions();
		var expiration = TimeSpan.FromMinutes(30);

		// Act
		options.Behavior.DefaultExpiration = expiration;

		// Assert
		options.Behavior.DefaultExpiration.ShouldBe(expiration);
	}

	[Fact]
	public void AllowSettingCacheTimeout()
	{
		// Arrange
		var options = new CacheOptions();
		var timeout = TimeSpan.FromSeconds(5);

		// Act
		options.Behavior.CacheTimeout = timeout;

		// Assert
		options.Behavior.CacheTimeout.ShouldBe(timeout);
	}

	[Fact]
	public void AllowSettingJitterRatio()
	{
		// Arrange
		var options = new CacheOptions();

		// Act
		options.Behavior.JitterRatio = 0.25;

		// Assert
		options.Behavior.JitterRatio.ShouldBe(0.25);
	}

	[Fact]
	public void AllowSettingDefaultTags()
	{
		// Arrange
		var options = new CacheOptions();
		var tags = new[] { "tag1", "tag2" };

		// Act
		options.DefaultTags = tags;

		// Assert
		options.DefaultTags.ShouldBe(tags);
	}

	[Fact]
	public void AllowSettingGlobalPolicy()
	{
		// Arrange
		var options = new CacheOptions();
		var policy = A.Fake<IResultCachePolicy>();

		// Act
		options.GlobalPolicy = policy;

		// Assert
		options.GlobalPolicy.ShouldBe(policy);
	}

	[Fact]
	public void AllowSettingCacheKeyBuilder()
	{
		// Arrange
		var options = new CacheOptions();
		var builder = A.Fake<ICacheKeyBuilder>();

		// Act
		options.CacheKeyBuilder = builder;

		// Assert
		options.CacheKeyBuilder.ShouldBe(builder);
	}

	[Fact]
	public void AllowSettingEnableStatistics()
	{
		// Arrange
		var options = new CacheOptions();

		// Act
		options.Behavior.EnableStatistics = true;

		// Assert
		options.Behavior.EnableStatistics.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingEnableCompression()
	{
		// Arrange
		var options = new CacheOptions();

		// Act
		options.Behavior.EnableCompression = true;

		// Assert
		options.Behavior.EnableCompression.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingUseSlidingExpiration()
	{
		// Arrange
		var options = new CacheOptions();

		// Act
		options.Behavior.UseSlidingExpiration = false;

		// Assert
		options.Behavior.UseSlidingExpiration.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingMemoryConfiguration()
	{
		// Arrange
		var options = new CacheOptions();
		var memoryConfig = new MemoryCacheConfiguration();

		// Act
		options.Memory = memoryConfig;

		// Assert
		options.Memory.ShouldBe(memoryConfig);
	}

	[Fact]
	public void AllowSettingDistributedConfiguration()
	{
		// Arrange
		var options = new CacheOptions();
		var distConfig = new DistributedCacheConfiguration();

		// Act
		options.Distributed = distConfig;

		// Assert
		options.Distributed.ShouldBe(distConfig);
	}

	[Fact]
	public void AllowSettingResilienceConfiguration()
	{
		// Arrange
		var options = new CacheOptions();
		var resilience = new CacheResilienceOptions();

		// Act
		options.Resilience = resilience;

		// Assert
		options.Resilience.ShouldBe(resilience);
	}
}
