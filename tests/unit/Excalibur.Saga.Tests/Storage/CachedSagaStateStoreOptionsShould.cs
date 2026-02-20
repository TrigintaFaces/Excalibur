// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Storage;

namespace Excalibur.Saga.Tests.Storage;

/// <summary>
/// Unit tests for <see cref="CachedSagaStateStoreOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class CachedSagaStateStoreOptionsShould
{
	#region Default Values Tests

	[Fact]
	public void HaveDefaultEnableCaching()
	{
		// Arrange & Act
		var options = new CachedSagaStateStoreOptions();

		// Assert
		options.EnableCaching.ShouldBeTrue();
	}

	[Fact]
	public void HaveDefaultCacheTtl()
	{
		// Arrange & Act
		var options = new CachedSagaStateStoreOptions();

		// Assert
		options.DefaultCacheTtl.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void HaveDefaultActiveSagaCacheTtl()
	{
		// Arrange & Act
		var options = new CachedSagaStateStoreOptions();

		// Assert
		options.ActiveSagaCacheTtl.ShouldBe(TimeSpan.FromMinutes(1));
	}

	[Fact]
	public void HaveDefaultCompletedSagaCacheTtl()
	{
		// Arrange & Act
		var options = new CachedSagaStateStoreOptions();

		// Assert
		options.CompletedSagaCacheTtl.ShouldBe(TimeSpan.FromHours(1));
	}

	[Fact]
	public void HaveDefaultInvalidateCacheOnUpdate()
	{
		// Arrange & Act
		var options = new CachedSagaStateStoreOptions();

		// Assert
		options.InvalidateCacheOnUpdate.ShouldBeFalse();
	}

	[Fact]
	public void HaveDefaultUseLocalCache()
	{
		// Arrange & Act
		var options = new CachedSagaStateStoreOptions();

		// Assert
		options.UseLocalCache.ShouldBeTrue();
	}

	[Fact]
	public void HaveDefaultLocalCacheSizeLimit()
	{
		// Arrange & Act
		var options = new CachedSagaStateStoreOptions();

		// Assert
		options.LocalCacheSizeLimit.ShouldBe(1000);
	}

	#endregion Default Values Tests

	#region Property Setting Tests

	[Fact]
	public void AllowEnableCachingToBeSet()
	{
		// Arrange & Act
		var options = new CachedSagaStateStoreOptions { EnableCaching = false };

		// Assert
		options.EnableCaching.ShouldBeFalse();
	}

	[Fact]
	public void AllowDefaultCacheTtlToBeSet()
	{
		// Arrange & Act
		var options = new CachedSagaStateStoreOptions { DefaultCacheTtl = TimeSpan.FromMinutes(10) };

		// Assert
		options.DefaultCacheTtl.ShouldBe(TimeSpan.FromMinutes(10));
	}

	[Fact]
	public void AllowActiveSagaCacheTtlToBeSet()
	{
		// Arrange & Act
		var options = new CachedSagaStateStoreOptions { ActiveSagaCacheTtl = TimeSpan.FromSeconds(30) };

		// Assert
		options.ActiveSagaCacheTtl.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void AllowCompletedSagaCacheTtlToBeSet()
	{
		// Arrange & Act
		var options = new CachedSagaStateStoreOptions { CompletedSagaCacheTtl = TimeSpan.FromHours(24) };

		// Assert
		options.CompletedSagaCacheTtl.ShouldBe(TimeSpan.FromHours(24));
	}

	[Fact]
	public void AllowInvalidateCacheOnUpdateToBeSet()
	{
		// Arrange & Act
		var options = new CachedSagaStateStoreOptions { InvalidateCacheOnUpdate = true };

		// Assert
		options.InvalidateCacheOnUpdate.ShouldBeTrue();
	}

	[Fact]
	public void AllowUseLocalCacheToBeSet()
	{
		// Arrange & Act
		var options = new CachedSagaStateStoreOptions { UseLocalCache = false };

		// Assert
		options.UseLocalCache.ShouldBeFalse();
	}

	[Fact]
	public void AllowLocalCacheSizeLimitToBeSet()
	{
		// Arrange & Act
		var options = new CachedSagaStateStoreOptions { LocalCacheSizeLimit = 5000 };

		// Assert
		options.LocalCacheSizeLimit.ShouldBe(5000);
	}

	#endregion Property Setting Tests

	#region Configuration Scenario Tests

	[Fact]
	public void CreateAggressiveCachingConfiguration()
	{
		// Arrange & Act
		var options = new CachedSagaStateStoreOptions
		{
			EnableCaching = true,
			DefaultCacheTtl = TimeSpan.FromMinutes(30),
			ActiveSagaCacheTtl = TimeSpan.FromMinutes(5),
			CompletedSagaCacheTtl = TimeSpan.FromHours(24),
			UseLocalCache = true,
			LocalCacheSizeLimit = 5000,
		};

		// Assert
		options.EnableCaching.ShouldBeTrue();
		options.DefaultCacheTtl.ShouldBeGreaterThan(TimeSpan.FromMinutes(5));
		options.LocalCacheSizeLimit.ShouldBe(5000);
	}

	[Fact]
	public void CreateMinimalCachingConfiguration()
	{
		// Arrange & Act
		var options = new CachedSagaStateStoreOptions
		{
			EnableCaching = true,
			DefaultCacheTtl = TimeSpan.FromSeconds(30),
			ActiveSagaCacheTtl = TimeSpan.FromSeconds(10),
			CompletedSagaCacheTtl = TimeSpan.FromMinutes(5),
			LocalCacheSizeLimit = 100,
		};

		// Assert
		options.DefaultCacheTtl.ShouldBeLessThan(TimeSpan.FromMinutes(1));
		options.LocalCacheSizeLimit.ShouldBe(100);
	}

	[Fact]
	public void CreateDisabledCachingConfiguration()
	{
		// Arrange & Act
		var options = new CachedSagaStateStoreOptions
		{
			EnableCaching = false,
		};

		// Assert
		options.EnableCaching.ShouldBeFalse();
	}

	[Fact]
	public void CreateConsistencyFocusedConfiguration()
	{
		// Arrange & Act
		var options = new CachedSagaStateStoreOptions
		{
			EnableCaching = true,
			InvalidateCacheOnUpdate = true,
			ActiveSagaCacheTtl = TimeSpan.FromSeconds(5),
		};

		// Assert
		options.InvalidateCacheOnUpdate.ShouldBeTrue();
		options.ActiveSagaCacheTtl.ShouldBeLessThanOrEqualTo(TimeSpan.FromSeconds(5));
	}

	[Fact]
	public void CreateDistributedCacheOnlyConfiguration()
	{
		// Arrange & Act
		var options = new CachedSagaStateStoreOptions
		{
			EnableCaching = true,
			UseLocalCache = false,
		};

		// Assert
		options.EnableCaching.ShouldBeTrue();
		options.UseLocalCache.ShouldBeFalse();
	}

	#endregion Configuration Scenario Tests
}
