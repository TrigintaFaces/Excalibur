// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Metrics;

namespace Excalibur.Dispatch.Tests.Messaging.Metrics;

/// <summary>
/// Unit tests for <see cref="RateCounter"/>.
/// </summary>
/// <remarks>
/// Tests the thread-safe rate counter implementation.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Metrics")]
[Trait("Priority", "0")]
public sealed class RateCounterShould
{
	#region Constructor Tests

	[Fact]
	public void Constructor_Default_InitializesValueToZero()
	{
		// Arrange & Act
		var counter = new RateCounter();

		// Assert
		counter.Value.ShouldBe(0);
	}

	[Fact]
	public void Constructor_Default_InitializesLastReset()
	{
		// Arrange
		var before = DateTimeOffset.UtcNow;

		// Act
		var counter = new RateCounter();

		// Assert
		var after = DateTimeOffset.UtcNow;
		counter.LastReset.ShouldBeGreaterThanOrEqualTo(before);
		counter.LastReset.ShouldBeLessThanOrEqualTo(after);
	}

	[Fact]
	public void Constructor_WithParameters_CreatesInstance()
	{
		// Arrange & Act
		var counter = new RateCounter(null, "test_counter", "items", "Test counter description");

		// Assert
		_ = counter.ShouldNotBeNull();
		counter.Value.ShouldBe(0);
	}

	[Fact]
	public void Constructor_WithNullParameters_CreatesInstance()
	{
		// Arrange & Act
		var counter = new RateCounter(null, "test", null, null);

		// Assert
		_ = counter.ShouldNotBeNull();
	}

	#endregion

	#region Increment Tests

	[Fact]
	public void Increment_Default_IncrementsBy1()
	{
		// Arrange
		var counter = new RateCounter();

		// Act
		var result = counter.Increment();

		// Assert
		result.ShouldBe(1);
		counter.Value.ShouldBe(1);
	}

	[Fact]
	public void Increment_MultipleTimes_AccumulatesCorrectly()
	{
		// Arrange
		var counter = new RateCounter();

		// Act
		_ = counter.Increment();
		_ = counter.Increment();
		var result = counter.Increment();

		// Assert
		result.ShouldBe(3);
		counter.Value.ShouldBe(3);
	}

	[Fact]
	public void IncrementBy_WithPositiveAmount_AddsCorrectly()
	{
		// Arrange
		var counter = new RateCounter();

		// Act
		var result = counter.IncrementBy(10);

		// Assert
		result.ShouldBe(10);
		counter.Value.ShouldBe(10);
	}

	[Fact]
	public void IncrementBy_WithZero_DoesNotChangeValue()
	{
		// Arrange
		var counter = new RateCounter();
		_ = counter.Increment();

		// Act
		var result = counter.IncrementBy(0);

		// Assert
		result.ShouldBe(1);
		counter.Value.ShouldBe(1);
	}

	[Fact]
	public void IncrementBy_WithLargeAmount_Works()
	{
		// Arrange
		var counter = new RateCounter();

		// Act
		var result = counter.IncrementBy(1_000_000);

		// Assert
		result.ShouldBe(1_000_000);
		counter.Value.ShouldBe(1_000_000);
	}

	#endregion

	#region Decrement Tests

	[Fact]
	public void Decrement_Default_DecrementsBy1()
	{
		// Arrange
		var counter = new RateCounter();
		_ = counter.IncrementBy(5);

		// Act
		var result = counter.Decrement();

		// Assert
		result.ShouldBe(4);
		counter.Value.ShouldBe(4);
	}

	[Fact]
	public void Decrement_BelowZero_AllowsNegativeValue()
	{
		// Arrange
		var counter = new RateCounter();

		// Act
		var result = counter.Decrement();

		// Assert
		result.ShouldBe(-1);
		counter.Value.ShouldBe(-1);
	}

	[Fact]
	public void DecrementBy_WithPositiveAmount_SubtractsCorrectly()
	{
		// Arrange
		var counter = new RateCounter();
		_ = counter.IncrementBy(20);

		// Act
		var result = counter.DecrementBy(5);

		// Assert
		result.ShouldBe(15);
		counter.Value.ShouldBe(15);
	}

	[Fact]
	public void DecrementBy_WithZero_DoesNotChangeValue()
	{
		// Arrange
		var counter = new RateCounter();
		_ = counter.IncrementBy(10);

		// Act
		var result = counter.DecrementBy(0);

		// Assert
		result.ShouldBe(10);
		counter.Value.ShouldBe(10);
	}

	#endregion

	#region Set Tests

	[Fact]
	public void Set_WithPositiveValue_SetsCorrectly()
	{
		// Arrange
		var counter = new RateCounter();

		// Act
		var previousValue = counter.Set(100);

		// Assert
		previousValue.ShouldBe(0);
		counter.Value.ShouldBe(100);
	}

	[Fact]
	public void Set_WithZero_SetsToZero()
	{
		// Arrange
		var counter = new RateCounter();
		_ = counter.IncrementBy(50);

		// Act
		var previousValue = counter.Set(0);

		// Assert
		previousValue.ShouldBe(50);
		counter.Value.ShouldBe(0);
	}

	[Fact]
	public void Set_WithNegativeValue_AllowsNegative()
	{
		// Arrange
		var counter = new RateCounter();

		// Act
		var previousValue = counter.Set(-50);

		// Assert
		previousValue.ShouldBe(0);
		counter.Value.ShouldBe(-50);
	}

	#endregion

	#region Reset Tests

	[Fact]
	public void Reset_ResetsValueToZero()
	{
		// Arrange
		var counter = new RateCounter();
		_ = counter.IncrementBy(100);

		// Act
		var previousValue = counter.Reset();

		// Assert
		previousValue.ShouldBe(100);
		counter.Value.ShouldBe(0);
	}

	[Fact]
	public void Reset_UpdatesLastReset()
	{
		// Arrange
		var counter = new RateCounter();
		var originalReset = counter.LastReset;
		global::Tests.Shared.Infrastructure.TestTiming.Sleep(10);

		// Act
		_ = counter.Reset();

		// Assert
		counter.LastReset.ShouldBeGreaterThan(originalReset);
	}

	[Fact]
	public void Reset_WhenAlreadyZero_ReturnsZero()
	{
		// Arrange
		var counter = new RateCounter();

		// Act
		var previousValue = counter.Reset();

		// Assert
		previousValue.ShouldBe(0);
		counter.Value.ShouldBe(0);
	}

	#endregion

	#region GetRate Tests

	[Fact]
	public void GetRate_WithNoTimePassed_ReturnsZero()
	{
		// Arrange
		var counter = new RateCounter();

		// Act
		var rate = counter.GetRate();

		// Assert
		rate.ShouldBe(0.0);
	}

	[Fact]
	public async Task GetRate_WithTimeAndIncrements_ReturnsRate()
	{
		// Arrange
		var counter = new RateCounter();
		_ = counter.IncrementBy(100);

		// Act - Wait and increment more
		await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(100);
		_ = counter.IncrementBy(100);
		var rate = counter.GetRate();

		// Assert - Rate should be positive
		rate.ShouldBeGreaterThan(0.0);
	}

	#endregion

	#region GetAverageRate Tests

	[Fact]
	public async Task GetAverageRate_WithTimeAndIncrements_ReturnsRate()
	{
		// Arrange
		var counter = new RateCounter();
		_ = counter.IncrementBy(100);

		// Act
		await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(100);
		var rate = counter.GetAverageRate();

		// Assert - Average rate should be positive
		rate.ShouldBeGreaterThan(0.0);
	}

	[Fact]
	public void GetAverageRate_WithNoTimePassed_ReturnsZero()
	{
		// Arrange
		var counter = new RateCounter();

		// Act
		var rate = counter.GetAverageRate();

		// Assert
		rate.ShouldBe(0.0);
	}

	#endregion

	#region GetSnapshot Tests

	[Fact]
	public void GetSnapshot_ReturnsCorrectValue()
	{
		// Arrange
		var counter = new RateCounter();
		_ = counter.IncrementBy(42);

		// Act
		var snapshot = counter.GetSnapshot();

		// Assert
		_ = snapshot.ShouldNotBeNull();
		snapshot.Value.ShouldBe(42);
	}

	[Fact]
	public void GetSnapshot_ReturnsCorrectTimestamp()
	{
		// Arrange
		var before = DateTimeOffset.UtcNow;
		var counter = new RateCounter();

		// Act
		var snapshot = counter.GetSnapshot();

		// Assert
		var after = DateTimeOffset.UtcNow;
		snapshot.Timestamp.ShouldBeGreaterThanOrEqualTo(before);
		snapshot.Timestamp.ShouldBeLessThanOrEqualTo(after);
	}

	[Fact]
	public void GetSnapshot_ReturnsLastReset()
	{
		// Arrange
		var counter = new RateCounter();

		// Act
		var snapshot = counter.GetSnapshot();

		// Assert
		snapshot.LastReset.ShouldBe(counter.LastReset);
	}

	[Fact]
	public void GetSnapshot_ReturnsPositiveTimeSinceReset()
	{
		// Arrange
		var counter = new RateCounter();

		// Act
		var snapshot = counter.GetSnapshot();

		// Assert
		snapshot.TimeSinceReset.ShouldBeGreaterThanOrEqualTo(TimeSpan.Zero);
	}

	#endregion

	#region Add Tests

	[Fact]
	public void Add_WithOtherCounter_AddsValue()
	{
		// Arrange
		var counter1 = new RateCounter();
		_ = counter1.IncrementBy(50);
		var counter2 = new RateCounter();
		_ = counter2.IncrementBy(30);

		// Act
		counter1.Add(counter2);

		// Assert
		counter1.Value.ShouldBe(80);
	}

	[Fact]
	public void Add_WithNullCounter_ThrowsArgumentNullException()
	{
		// Arrange
		var counter = new RateCounter();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => counter.Add(null!));
	}

	[Fact]
	public void Add_WithZeroValueCounter_DoesNotChangeValue()
	{
		// Arrange
		var counter1 = new RateCounter();
		_ = counter1.IncrementBy(100);
		var counter2 = new RateCounter();

		// Act
		counter1.Add(counter2);

		// Assert
		counter1.Value.ShouldBe(100);
	}

	#endregion

	#region ToString Tests

	[Fact]
	public void ToString_ReturnsFormattedString()
	{
		// Arrange
		var counter = new RateCounter();
		_ = counter.IncrementBy(42);

		// Act
		var result = counter.ToString();

		// Assert
		result.ShouldContain("Counter[");
		result.ShouldContain("Value=42");
		result.ShouldContain("Rate=");
		result.ShouldContain("AvgRate=");
	}

	#endregion

	#region Interface Tests

	[Fact]
	public void ImplementsIMetric()
	{
		// Arrange
		var counter = new RateCounter();

		// Assert
		_ = counter.ShouldBeAssignableTo<IMetric>();
	}

	#endregion

	#region Thread Safety Tests

	[Fact]
	public async Task Increment_ConcurrentAccess_MaintainsCorrectCount()
	{
		// Arrange
		var counter = new RateCounter();
		const int threadCount = 10;
		const int incrementsPerThread = 1000;
		var tasks = new Task[threadCount];

		// Act
		for (var i = 0; i < threadCount; i++)
		{
			tasks[i] = Task.Run(() =>
			{
				for (var j = 0; j < incrementsPerThread; j++)
				{
					_ = counter.Increment();
				}
			});
		}

		await Task.WhenAll(tasks);

		// Assert
		counter.Value.ShouldBe(threadCount * incrementsPerThread);
	}

	[Fact]
	public async Task MixedOperations_ConcurrentAccess_MaintainsConsistency()
	{
		// Arrange
		var counter = new RateCounter();
		const int operations = 1000;
		var tasks = new List<Task>();

		// Act - Mix of increments and decrements
		for (var i = 0; i < operations; i++)
		{
			tasks.Add(Task.Run(() => counter.Increment()));
			tasks.Add(Task.Run(() => counter.Decrement()));
		}

		await Task.WhenAll(tasks);

		// Assert - Should be zero since equal increments and decrements
		counter.Value.ShouldBe(0);
	}

	#endregion
}
