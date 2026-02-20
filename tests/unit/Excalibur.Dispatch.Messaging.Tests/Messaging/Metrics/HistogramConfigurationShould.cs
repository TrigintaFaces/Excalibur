// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Metrics;

namespace Excalibur.Dispatch.Tests.Messaging.Metrics;

/// <summary>
/// Unit tests for <see cref="HistogramConfiguration"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Metrics")]
public sealed class HistogramConfigurationShould : UnitTestBase
{
	#region Constructor Tests

	[Fact]
	public void CreateWithSingleBucket()
	{
		// Arrange & Act
		var config = new HistogramConfiguration(1.0);

		// Assert
		config.Buckets.Length.ShouldBe(1);
		config.Buckets[0].ShouldBe(1.0);
	}

	[Fact]
	public void CreateWithMultipleBuckets()
	{
		// Arrange & Act
		var config = new HistogramConfiguration(1.0, 5.0, 10.0, 25.0, 50.0);

		// Assert
		config.Buckets.Length.ShouldBe(5);
	}

	[Fact]
	public void SortBucketsAutomatically()
	{
		// Arrange - pass unsorted values
		var config = new HistogramConfiguration(10.0, 1.0, 5.0, 2.0);

		// Assert - should be sorted
		config.Buckets[0].ShouldBe(1.0);
		config.Buckets[1].ShouldBe(2.0);
		config.Buckets[2].ShouldBe(5.0);
		config.Buckets[3].ShouldBe(10.0);
	}

	[Fact]
	public void ThrowOnNullBuckets()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			new HistogramConfiguration(null!));
	}

	[Fact]
	public void ThrowOnEmptyBuckets()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			new HistogramConfiguration([]));
	}

	#endregion

	#region DefaultLatency Tests

	[Fact]
	public void DefaultLatencyHaveBuckets()
	{
		// Act
		var config = HistogramConfiguration.DefaultLatency;

		// Assert
		config.Buckets.Length.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void DefaultLatencyBeSorted()
	{
		// Act
		var config = HistogramConfiguration.DefaultLatency;

		// Assert
		for (int i = 1; i < config.Buckets.Length; i++)
		{
			config.Buckets[i].ShouldBeGreaterThan(config.Buckets[i - 1]);
		}
	}

	[Fact]
	public void DefaultLatencyStartSmall()
	{
		// Act
		var config = HistogramConfiguration.DefaultLatency;

		// Assert - first bucket should be small (5ms)
		config.Buckets[0].ShouldBe(0.005);
	}

	#endregion

	#region DefaultSize Tests

	[Fact]
	public void DefaultSizeHaveBuckets()
	{
		// Act
		var config = HistogramConfiguration.DefaultSize;

		// Assert
		config.Buckets.Length.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void DefaultSizeBeSorted()
	{
		// Act
		var config = HistogramConfiguration.DefaultSize;

		// Assert
		for (int i = 1; i < config.Buckets.Length; i++)
		{
			config.Buckets[i].ShouldBeGreaterThan(config.Buckets[i - 1]);
		}
	}

	#endregion

	#region Exponential Tests

	[Fact]
	public void ExponentialCreateCorrectNumberOfBuckets()
	{
		// Act
		var config = HistogramConfiguration.Exponential(100, 2, 5);

		// Assert
		config.Buckets.Length.ShouldBe(5);
	}

	[Fact]
	public void ExponentialStartWithCorrectValue()
	{
		// Act
		var config = HistogramConfiguration.Exponential(100, 2, 5);

		// Assert
		config.Buckets[0].ShouldBe(100);
	}

	[Fact]
	public void ExponentialApplyFactorCorrectly()
	{
		// Act
		var config = HistogramConfiguration.Exponential(100, 2, 5);

		// Assert
		config.Buckets[0].ShouldBe(100);   // 100 * 2^0
		config.Buckets[1].ShouldBe(200);   // 100 * 2^1
		config.Buckets[2].ShouldBe(400);   // 100 * 2^2
		config.Buckets[3].ShouldBe(800);   // 100 * 2^3
		config.Buckets[4].ShouldBe(1600);  // 100 * 2^4
	}

	[Fact]
	public void ExponentialThrowOnZeroStart()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			HistogramConfiguration.Exponential(0, 2, 5));
	}

	[Fact]
	public void ExponentialThrowOnNegativeStart()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			HistogramConfiguration.Exponential(-1, 2, 5));
	}

	[Fact]
	public void ExponentialThrowOnFactorEqualToOne()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			HistogramConfiguration.Exponential(100, 1, 5));
	}

	[Fact]
	public void ExponentialThrowOnFactorLessThanOne()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			HistogramConfiguration.Exponential(100, 0.5, 5));
	}

	[Fact]
	public void ExponentialThrowOnZeroCount()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			HistogramConfiguration.Exponential(100, 2, 0));
	}

	[Fact]
	public void ExponentialThrowOnNegativeCount()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			HistogramConfiguration.Exponential(100, 2, -1));
	}

	#endregion

	#region Linear Tests

	[Fact]
	public void LinearCreateCorrectNumberOfBuckets()
	{
		// Act
		var config = HistogramConfiguration.Linear(0, 10, 5);

		// Assert
		config.Buckets.Length.ShouldBe(5);
	}

	[Fact]
	public void LinearStartWithCorrectValue()
	{
		// Act
		var config = HistogramConfiguration.Linear(100, 10, 5);

		// Assert
		config.Buckets[0].ShouldBe(100);
	}

	[Fact]
	public void LinearApplyWidthCorrectly()
	{
		// Act
		var config = HistogramConfiguration.Linear(100, 25, 5);

		// Assert
		config.Buckets[0].ShouldBe(100);   // 100 + 25*0
		config.Buckets[1].ShouldBe(125);   // 100 + 25*1
		config.Buckets[2].ShouldBe(150);   // 100 + 25*2
		config.Buckets[3].ShouldBe(175);   // 100 + 25*3
		config.Buckets[4].ShouldBe(200);   // 100 + 25*4
	}

	[Fact]
	public void LinearStartFromZero()
	{
		// Act
		var config = HistogramConfiguration.Linear(0, 10, 3);

		// Assert
		config.Buckets[0].ShouldBe(0);
		config.Buckets[1].ShouldBe(10);
		config.Buckets[2].ShouldBe(20);
	}

	[Fact]
	public void LinearThrowOnZeroWidth()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			HistogramConfiguration.Linear(0, 0, 5));
	}

	[Fact]
	public void LinearThrowOnNegativeWidth()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			HistogramConfiguration.Linear(0, -10, 5));
	}

	[Fact]
	public void LinearThrowOnZeroCount()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			HistogramConfiguration.Linear(0, 10, 0));
	}

	[Fact]
	public void LinearThrowOnNegativeCount()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			HistogramConfiguration.Linear(0, 10, -1));
	}

	#endregion
}
