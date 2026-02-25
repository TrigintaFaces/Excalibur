// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Metrics;

namespace Excalibur.Dispatch.Tests.Messaging.Metrics;

/// <summary>
/// Unit tests for <see cref="CacheAlignedCounter"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Metrics")]
public sealed class CacheAlignedCounterShould : UnitTestBase
{
	#region Create Tests

	[Fact]
	public void CreateWithDefaultValue()
	{
		// Act
		var counter = CacheAlignedCounter.Create();

		// Assert
		counter.Value.ShouldBe(0);
	}

	[Fact]
	public void CreateWithInitialValue()
	{
		// Act
		var counter = CacheAlignedCounter.Create(100);

		// Assert
		counter.Value.ShouldBe(100);
	}

	[Fact]
	public void CreateWithNegativeValue()
	{
		// Act
		var counter = CacheAlignedCounter.Create(-50);

		// Assert
		counter.Value.ShouldBe(-50);
	}

	#endregion

	#region Increment Tests

	[Fact]
	public void IncrementByOne()
	{
		// Arrange
		var counter = CacheAlignedCounter.Create(0);

		// Act
		var result = counter.Increment();

		// Assert
		result.ShouldBe(1);
		counter.Value.ShouldBe(1);
	}

	[Fact]
	public void IncrementMultipleTimes()
	{
		// Arrange
		var counter = CacheAlignedCounter.Create(0);

		// Act
		_ = counter.Increment();
		_ = counter.Increment();
		var result = counter.Increment();

		// Assert
		result.ShouldBe(3);
		counter.Value.ShouldBe(3);
	}

	#endregion

	#region Decrement Tests

	[Fact]
	public void DecrementByOne()
	{
		// Arrange
		var counter = CacheAlignedCounter.Create(10);

		// Act
		var result = counter.Decrement();

		// Assert
		result.ShouldBe(9);
		counter.Value.ShouldBe(9);
	}

	[Fact]
	public void DecrementBelowZero()
	{
		// Arrange
		var counter = CacheAlignedCounter.Create(0);

		// Act
		var result = counter.Decrement();

		// Assert
		result.ShouldBe(-1);
		counter.Value.ShouldBe(-1);
	}

	#endregion

	#region Add Tests

	[Fact]
	public void AddPositiveValue()
	{
		// Arrange
		var counter = CacheAlignedCounter.Create(10);

		// Act
		var result = counter.Add(5);

		// Assert
		result.ShouldBe(15);
		counter.Value.ShouldBe(15);
	}

	[Fact]
	public void AddNegativeValue()
	{
		// Arrange
		var counter = CacheAlignedCounter.Create(10);

		// Act
		var result = counter.Add(-3);

		// Assert
		result.ShouldBe(7);
		counter.Value.ShouldBe(7);
	}

	[Fact]
	public void AddZero()
	{
		// Arrange
		var counter = CacheAlignedCounter.Create(10);

		// Act
		var result = counter.Add(0);

		// Assert
		result.ShouldBe(10);
		counter.Value.ShouldBe(10);
	}

	#endregion

	#region Exchange Tests

	[Fact]
	public void ExchangeValue()
	{
		// Arrange
		var counter = CacheAlignedCounter.Create(100);

		// Act
		var previousValue = counter.Exchange(200);

		// Assert
		previousValue.ShouldBe(100);
		counter.Value.ShouldBe(200);
	}

	[Fact]
	public void ExchangeToZero()
	{
		// Arrange
		var counter = CacheAlignedCounter.Create(50);

		// Act
		var previousValue = counter.Exchange(0);

		// Assert
		previousValue.ShouldBe(50);
		counter.Value.ShouldBe(0);
	}

	#endregion

	#region CompareExchange Tests

	[Fact]
	public void CompareExchangeWhenEqual()
	{
		// Arrange
		var counter = CacheAlignedCounter.Create(100);

		// Act
		var original = counter.CompareExchange(200, 100);

		// Assert
		original.ShouldBe(100);
		counter.Value.ShouldBe(200);
	}

	[Fact]
	public void CompareExchangeWhenNotEqual()
	{
		// Arrange
		var counter = CacheAlignedCounter.Create(100);

		// Act
		var original = counter.CompareExchange(200, 50); // Comparand doesn't match

		// Assert
		original.ShouldBe(100); // Returns original value
		counter.Value.ShouldBe(100); // Value unchanged
	}

	#endregion

	#region Reset Tests

	[Fact]
	public void ResetToZero()
	{
		// Arrange
		var counter = CacheAlignedCounter.Create(100);

		// Act
		counter.Reset();

		// Assert
		counter.Value.ShouldBe(0);
	}

	[Fact]
	public void ResetAlreadyZeroCounter()
	{
		// Arrange
		var counter = CacheAlignedCounter.Create(0);

		// Act
		counter.Reset();

		// Assert
		counter.Value.ShouldBe(0);
	}

	#endregion

	#region Equality Tests

	[Fact]
	public void BeEqualWhenValuesMatch()
	{
		// Arrange
		var counter1 = CacheAlignedCounter.Create(100);
		var counter2 = CacheAlignedCounter.Create(100);

		// Act & Assert
		counter1.Equals(counter2).ShouldBeTrue();
		(counter1 == counter2).ShouldBeTrue();
		(counter1 != counter2).ShouldBeFalse();
	}

	[Fact]
	public void NotBeEqualWhenValuesDiffer()
	{
		// Arrange
		var counter1 = CacheAlignedCounter.Create(100);
		var counter2 = CacheAlignedCounter.Create(200);

		// Act & Assert
		counter1.Equals(counter2).ShouldBeFalse();
		(counter1 != counter2).ShouldBeTrue();
	}

	[Fact]
	public void EqualsObjectReturnFalseForNull()
	{
		// Arrange
		var counter = CacheAlignedCounter.Create(100);

		// Act & Assert
		counter.Equals(null).ShouldBeFalse();
	}

	[Fact]
	public void EqualsObjectReturnFalseForDifferentType()
	{
		// Arrange
		var counter = CacheAlignedCounter.Create(100);

		// Act & Assert
		counter.Equals("not a counter").ShouldBeFalse();
	}

	[Fact]
	public void EqualsObjectReturnTrueForMatchingCounter()
	{
		// Arrange
		var counter1 = CacheAlignedCounter.Create(100);
		object counter2 = CacheAlignedCounter.Create(100);

		// Act & Assert
		counter1.Equals(counter2).ShouldBeTrue();
	}

	#endregion

	#region GetHashCode Tests

	[Fact]
	public void ProduceConsistentHashCode()
	{
		// Arrange
		var counter = CacheAlignedCounter.Create(100);

		// Act
		var hash1 = counter.GetHashCode();
		var hash2 = counter.GetHashCode();

		// Assert
		hash1.ShouldBe(hash2);
	}

	[Fact]
	public void ProduceSameHashCodeForEqualCounters()
	{
		// Arrange
		var counter1 = CacheAlignedCounter.Create(100);
		var counter2 = CacheAlignedCounter.Create(100);

		// Act & Assert
		counter1.GetHashCode().ShouldBe(counter2.GetHashCode());
	}

	#endregion

	#region Thread Safety Tests

	[Fact]
	public async Task SupportConcurrentIncrements()
	{
		// Arrange
		var counter = CacheAlignedCounter.Create(0);
		const long iterations = 1000L;
		const long threads = 10L;

		// Act
		var tasks = Enumerable.Range(0, (int)threads)
			.Select(unused => Task.Run(() =>
			{
				for (long i = 0; i < iterations; i++)
				{
					counter.Increment();
				}
			}));

		await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert
		counter.Value.ShouldBe(iterations * threads);
	}

	[Fact]
	public async Task SupportConcurrentAdds()
	{
		// Arrange
		var counter = CacheAlignedCounter.Create(0);
		const long iterations = 1000L;
		const long threads = 10L;

		// Act
		var tasks = Enumerable.Range(0, (int)threads)
			.Select(unused => Task.Run(() =>
			{
				for (long i = 0; i < iterations; i++)
				{
					counter.Add(1L);
				}
			}));

		await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert
		counter.Value.ShouldBe(iterations * threads);
	}

	#endregion
}
