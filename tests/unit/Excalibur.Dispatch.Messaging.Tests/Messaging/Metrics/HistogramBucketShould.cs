// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Metrics;

namespace Excalibur.Dispatch.Tests.Messaging.Metrics;

/// <summary>
/// Unit tests for <see cref="HistogramBucket"/>.
/// </summary>
/// <remarks>
/// Tests the histogram bucket struct used for metrics collection.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Metrics")]
[Trait("Priority", "0")]
public sealed class HistogramBucketShould
{
	#region Constructor Tests

	[Fact]
	public void Constructor_SetsUpperBound()
	{
		// Arrange & Act
		var bucket = new HistogramBucket(100.0, 50);

		// Assert
		bucket.UpperBound.ShouldBe(100.0);
	}

	[Fact]
	public void Constructor_SetsCount()
	{
		// Arrange & Act
		var bucket = new HistogramBucket(100.0, 50);

		// Assert
		bucket.Count.ShouldBe(50);
	}

	[Fact]
	public void Constructor_WithZeroValues_Works()
	{
		// Arrange & Act
		var bucket = new HistogramBucket(0.0, 0);

		// Assert
		bucket.UpperBound.ShouldBe(0.0);
		bucket.Count.ShouldBe(0);
	}

	[Fact]
	public void Constructor_WithNegativeUpperBound_Works()
	{
		// Arrange & Act
		var bucket = new HistogramBucket(-50.0, 10);

		// Assert
		bucket.UpperBound.ShouldBe(-50.0);
	}

	[Fact]
	public void Constructor_WithInfinity_Works()
	{
		// Arrange & Act
		var bucket = new HistogramBucket(double.PositiveInfinity, 100);

		// Assert
		bucket.UpperBound.ShouldBe(double.PositiveInfinity);
	}

	[Fact]
	public void Constructor_WithMaxValues_Works()
	{
		// Arrange & Act
		var bucket = new HistogramBucket(double.MaxValue, long.MaxValue);

		// Assert
		bucket.UpperBound.ShouldBe(double.MaxValue);
		bucket.Count.ShouldBe(long.MaxValue);
	}

	#endregion

	#region Equality Tests

	[Fact]
	public void Equals_WithSameBucket_ReturnsTrue()
	{
		// Arrange
		var bucket1 = new HistogramBucket(100.0, 50);
		var bucket2 = new HistogramBucket(100.0, 50);

		// Act & Assert
		bucket1.Equals(bucket2).ShouldBeTrue();
	}

	[Fact]
	public void Equals_WithDifferentUpperBound_ReturnsFalse()
	{
		// Arrange
		var bucket1 = new HistogramBucket(100.0, 50);
		var bucket2 = new HistogramBucket(200.0, 50);

		// Act & Assert
		bucket1.Equals(bucket2).ShouldBeFalse();
	}

	[Fact]
	public void Equals_WithDifferentCount_ReturnsFalse()
	{
		// Arrange
		var bucket1 = new HistogramBucket(100.0, 50);
		var bucket2 = new HistogramBucket(100.0, 60);

		// Act & Assert
		bucket1.Equals(bucket2).ShouldBeFalse();
	}

	[Fact]
	public void Equals_WithObject_ReturnsTrue()
	{
		// Arrange
		var bucket1 = new HistogramBucket(100.0, 50);
		object bucket2 = new HistogramBucket(100.0, 50);

		// Act & Assert
		bucket1.Equals(bucket2).ShouldBeTrue();
	}

	[Fact]
	public void Equals_WithDifferentObject_ReturnsFalse()
	{
		// Arrange
		var bucket = new HistogramBucket(100.0, 50);

		// Act & Assert
		bucket.Equals("not a bucket").ShouldBeFalse();
	}

	[Fact]
	public void Equals_WithNull_ReturnsFalse()
	{
		// Arrange
		var bucket = new HistogramBucket(100.0, 50);

		// Act & Assert
		bucket.Equals(null).ShouldBeFalse();
	}

	#endregion

	#region Operator Tests

	[Fact]
	public void EqualityOperator_WithEqualBuckets_ReturnsTrue()
	{
		// Arrange
		var bucket1 = new HistogramBucket(100.0, 50);
		var bucket2 = new HistogramBucket(100.0, 50);

		// Act & Assert
		(bucket1 == bucket2).ShouldBeTrue();
	}

	[Fact]
	public void EqualityOperator_WithDifferentBuckets_ReturnsFalse()
	{
		// Arrange
		var bucket1 = new HistogramBucket(100.0, 50);
		var bucket2 = new HistogramBucket(200.0, 50);

		// Act & Assert
		(bucket1 == bucket2).ShouldBeFalse();
	}

	[Fact]
	public void InequalityOperator_WithDifferentBuckets_ReturnsTrue()
	{
		// Arrange
		var bucket1 = new HistogramBucket(100.0, 50);
		var bucket2 = new HistogramBucket(200.0, 50);

		// Act & Assert
		(bucket1 != bucket2).ShouldBeTrue();
	}

	[Fact]
	public void InequalityOperator_WithEqualBuckets_ReturnsFalse()
	{
		// Arrange
		var bucket1 = new HistogramBucket(100.0, 50);
		var bucket2 = new HistogramBucket(100.0, 50);

		// Act & Assert
		(bucket1 != bucket2).ShouldBeFalse();
	}

	#endregion

	#region GetHashCode Tests

	[Fact]
	public void GetHashCode_WithEqualBuckets_ReturnsSameHash()
	{
		// Arrange
		var bucket1 = new HistogramBucket(100.0, 50);
		var bucket2 = new HistogramBucket(100.0, 50);

		// Act & Assert
		bucket1.GetHashCode().ShouldBe(bucket2.GetHashCode());
	}

	[Fact]
	public void GetHashCode_WithDifferentBuckets_ReturnsDifferentHash()
	{
		// Arrange
		var bucket1 = new HistogramBucket(100.0, 50);
		var bucket2 = new HistogramBucket(200.0, 60);

		// Act & Assert - Different values should generally produce different hashes
		bucket1.GetHashCode().ShouldNotBe(bucket2.GetHashCode());
	}

	[Fact]
	public void GetHashCode_IsConsistent()
	{
		// Arrange
		var bucket = new HistogramBucket(100.0, 50);

		// Act
		var hash1 = bucket.GetHashCode();
		var hash2 = bucket.GetHashCode();

		// Assert
		hash1.ShouldBe(hash2);
	}

	#endregion

	#region Interface Tests

	[Fact]
	public void ImplementsIEquatable()
	{
		// Arrange
		var bucket = new HistogramBucket(100.0, 50);

		// Assert
		_ = bucket.ShouldBeAssignableTo<IEquatable<HistogramBucket>>();
	}

	#endregion
}
