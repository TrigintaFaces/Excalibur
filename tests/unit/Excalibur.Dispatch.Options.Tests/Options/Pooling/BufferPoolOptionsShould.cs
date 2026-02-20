// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Pooling.Configuration;
using Excalibur.Dispatch.Options.Pooling;

namespace Excalibur.Dispatch.Tests.Options.Pooling;

/// <summary>
/// Unit tests for <see cref="BufferPoolOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class BufferPoolOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_Enabled_IsTrue()
	{
		// Arrange & Act
		var options = new BufferPoolOptions();

		// Assert
		options.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void Default_SizeBuckets_IsNotNull()
	{
		// Arrange & Act
		var options = new BufferPoolOptions();

		// Assert
		_ = options.SizeBuckets.ShouldNotBeNull();
	}

	[Fact]
	public void Default_MaxBuffersPerBucket_IsProcessorCountTimesFour()
	{
		// Arrange & Act
		var options = new BufferPoolOptions();

		// Assert
		options.MaxBuffersPerBucket.ShouldBe(Environment.ProcessorCount * 4);
	}

	[Fact]
	public void Default_ClearOnReturn_IsFalse()
	{
		// Arrange & Act
		var options = new BufferPoolOptions();

		// Assert
		options.ClearOnReturn.ShouldBeFalse();
	}

	[Fact]
	public void Default_EnableThreadLocalCache_IsTrue()
	{
		// Arrange & Act
		var options = new BufferPoolOptions();

		// Assert
		options.EnableThreadLocalCache.ShouldBeTrue();
	}

	[Fact]
	public void Default_ThreadLocalCacheSize_IsTwo()
	{
		// Arrange & Act
		var options = new BufferPoolOptions();

		// Assert
		options.ThreadLocalCacheSize.ShouldBe(2);
	}

	[Fact]
	public void Default_TrimBehavior_IsAdaptive()
	{
		// Arrange & Act
		var options = new BufferPoolOptions();

		// Assert
		options.TrimBehavior.ShouldBe(TrimBehavior.Adaptive);
	}

	[Fact]
	public void Default_TrimPercentage_IsFifty()
	{
		// Arrange & Act
		var options = new BufferPoolOptions();

		// Assert
		options.TrimPercentage.ShouldBe(50);
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void Enabled_CanBeSet()
	{
		// Arrange
		var options = new BufferPoolOptions();

		// Act
		options.Enabled = false;

		// Assert
		options.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void SizeBuckets_CanBeSet()
	{
		// Arrange
		var options = new BufferPoolOptions();
		var sizeBuckets = new SizeBucketOptions();

		// Act
		options.SizeBuckets = sizeBuckets;

		// Assert
		options.SizeBuckets.ShouldBe(sizeBuckets);
	}

	[Fact]
	public void MaxBuffersPerBucket_CanBeSet()
	{
		// Arrange
		var options = new BufferPoolOptions();

		// Act
		options.MaxBuffersPerBucket = 100;

		// Assert
		options.MaxBuffersPerBucket.ShouldBe(100);
	}

	[Fact]
	public void ClearOnReturn_CanBeSet()
	{
		// Arrange
		var options = new BufferPoolOptions();

		// Act
		options.ClearOnReturn = true;

		// Assert
		options.ClearOnReturn.ShouldBeTrue();
	}

	[Fact]
	public void EnableThreadLocalCache_CanBeSet()
	{
		// Arrange
		var options = new BufferPoolOptions();

		// Act
		options.EnableThreadLocalCache = false;

		// Assert
		options.EnableThreadLocalCache.ShouldBeFalse();
	}

	[Fact]
	public void ThreadLocalCacheSize_CanBeSet()
	{
		// Arrange
		var options = new BufferPoolOptions();

		// Act
		options.ThreadLocalCacheSize = 8;

		// Assert
		options.ThreadLocalCacheSize.ShouldBe(8);
	}

	[Fact]
	public void TrimBehavior_CanBeSet()
	{
		// Arrange
		var options = new BufferPoolOptions();

		// Act
		options.TrimBehavior = TrimBehavior.Aggressive;

		// Assert
		options.TrimBehavior.ShouldBe(TrimBehavior.Aggressive);
	}

	[Fact]
	public void TrimPercentage_CanBeSet()
	{
		// Arrange
		var options = new BufferPoolOptions();

		// Act
		options.TrimPercentage = 75;

		// Assert
		options.TrimPercentage.ShouldBe(75);
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Arrange
		var sizeBuckets = new SizeBucketOptions();

		// Act
		var options = new BufferPoolOptions
		{
			Enabled = false,
			SizeBuckets = sizeBuckets,
			MaxBuffersPerBucket = 50,
			ClearOnReturn = true,
			EnableThreadLocalCache = false,
			ThreadLocalCacheSize = 4,
			TrimBehavior = TrimBehavior.Fixed,
			TrimPercentage = 25,
		};

		// Assert
		options.Enabled.ShouldBeFalse();
		options.SizeBuckets.ShouldBe(sizeBuckets);
		options.MaxBuffersPerBucket.ShouldBe(50);
		options.ClearOnReturn.ShouldBeTrue();
		options.EnableThreadLocalCache.ShouldBeFalse();
		options.ThreadLocalCacheSize.ShouldBe(4);
		options.TrimBehavior.ShouldBe(TrimBehavior.Fixed);
		options.TrimPercentage.ShouldBe(25);
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Options_ForSecuritySensitive_ClearsOnReturn()
	{
		// Act
		var options = new BufferPoolOptions
		{
			ClearOnReturn = true,
		};

		// Assert
		options.ClearOnReturn.ShouldBeTrue();
	}

	[Fact]
	public void Options_ForMemoryConstrained_HasAggressiveTrim()
	{
		// Act
		var options = new BufferPoolOptions
		{
			TrimBehavior = TrimBehavior.Aggressive,
			TrimPercentage = 90,
			MaxBuffersPerBucket = 4,
		};

		// Assert
		options.TrimBehavior.ShouldBe(TrimBehavior.Aggressive);
		options.TrimPercentage.ShouldBeGreaterThan(80);
	}

	#endregion
}
