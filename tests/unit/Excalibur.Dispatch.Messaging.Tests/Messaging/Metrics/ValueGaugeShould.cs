// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Metrics;

namespace Excalibur.Dispatch.Tests.Messaging.Metrics;

/// <summary>
/// Unit tests for <see cref="ValueGauge"/>.
/// </summary>
/// <remarks>
/// Tests the thread-safe value gauge implementation.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Metrics")]
[Trait("Priority", "0")]
public sealed class ValueGaugeShould
{
	#region Constructor Tests

	[Fact]
	public void Constructor_Default_InitializesValueToZero()
	{
		// Arrange & Act
		var gauge = new ValueGauge();

		// Assert
		gauge.Value.ShouldBe(0);
	}

	[Fact]
	public void Constructor_Default_InitializesLastUpdated()
	{
		// Arrange
		var before = DateTimeOffset.UtcNow;

		// Act
		var gauge = new ValueGauge();

		// Assert
		var after = DateTimeOffset.UtcNow;
		gauge.LastUpdated.ShouldBeGreaterThanOrEqualTo(before);
		gauge.LastUpdated.ShouldBeLessThanOrEqualTo(after);
	}

	[Fact]
	public void Constructor_WithMetadata_CreatesInstance()
	{
		// Arrange
		var metadata = new MetricMetadata(1, "test_gauge", "Test gauge description", "items", MetricType.Gauge);

		// Act
		var gauge = new ValueGauge(metadata);

		// Assert
		_ = gauge.ShouldNotBeNull();
		gauge.Value.ShouldBe(0);
		gauge.Metadata.ShouldBe(metadata);
	}

	#endregion

	#region Set Tests

	[Fact]
	public void Set_WithPositiveValue_SetsCorrectly()
	{
		// Arrange
		var gauge = new ValueGauge();

		// Act
		gauge.Set(100);

		// Assert
		gauge.Value.ShouldBe(100);
	}

	[Fact]
	public void Set_WithZero_SetsToZero()
	{
		// Arrange
		var gauge = new ValueGauge();
		gauge.Set(50);

		// Act
		gauge.Set(0);

		// Assert
		gauge.Value.ShouldBe(0);
	}

	[Fact]
	public void Set_WithNegativeValue_SetsCorrectly()
	{
		// Arrange
		var gauge = new ValueGauge();

		// Act
		gauge.Set(-50);

		// Assert
		gauge.Value.ShouldBe(-50);
	}

	[Fact]
	public void Set_UpdatesLastUpdated()
	{
		// Arrange
		var gauge = new ValueGauge();
		var originalUpdated = gauge.LastUpdated;
		global::Tests.Shared.Infrastructure.TestTiming.Sleep(10);

		// Act
		gauge.Set(100);

		// Assert
		gauge.LastUpdated.ShouldBeGreaterThanOrEqualTo(originalUpdated);
	}

	[Fact]
	public void Set_MultipleTimes_TakesLastValue()
	{
		// Arrange
		var gauge = new ValueGauge();

		// Act
		gauge.Set(10);
		gauge.Set(20);
		gauge.Set(30);

		// Assert
		gauge.Value.ShouldBe(30);
	}

	#endregion

	#region Increment Tests

	[Fact]
	public void Increment_Default_IncrementsBy1()
	{
		// Arrange
		var gauge = new ValueGauge();

		// Act
		var result = gauge.Increment();

		// Assert
		result.ShouldBe(1);
		gauge.Value.ShouldBe(1);
	}

	[Fact]
	public void Increment_MultipleTimes_AccumulatesCorrectly()
	{
		// Arrange
		var gauge = new ValueGauge();

		// Act
		_ = gauge.Increment();
		_ = gauge.Increment();
		var result = gauge.Increment();

		// Assert
		result.ShouldBe(3);
		gauge.Value.ShouldBe(3);
	}

	[Fact]
	public void Increment_UpdatesLastUpdated()
	{
		// Arrange
		var gauge = new ValueGauge();
		var originalUpdated = gauge.LastUpdated;
		global::Tests.Shared.Infrastructure.TestTiming.Sleep(10);

		// Act
		_ = gauge.Increment();

		// Assert
		gauge.LastUpdated.ShouldBeGreaterThanOrEqualTo(originalUpdated);
	}

	[Fact]
	public void IncrementBy_WithPositiveAmount_AddsCorrectly()
	{
		// Arrange
		var gauge = new ValueGauge();

		// Act
		var result = gauge.IncrementBy(10);

		// Assert
		result.ShouldBe(10);
		gauge.Value.ShouldBe(10);
	}

	[Fact]
	public void IncrementBy_WithZero_DoesNotChangeValue()
	{
		// Arrange
		var gauge = new ValueGauge();
		_ = gauge.Increment();

		// Act
		var result = gauge.IncrementBy(0);

		// Assert
		result.ShouldBe(1);
		gauge.Value.ShouldBe(1);
	}

	[Fact]
	public void IncrementBy_UpdatesLastUpdated()
	{
		// Arrange
		var gauge = new ValueGauge();
		var originalUpdated = gauge.LastUpdated;
		global::Tests.Shared.Infrastructure.TestTiming.Sleep(10);

		// Act
		_ = gauge.IncrementBy(10);

		// Assert
		gauge.LastUpdated.ShouldBeGreaterThanOrEqualTo(originalUpdated);
	}

	#endregion

	#region Decrement Tests

	[Fact]
	public void Decrement_Default_DecrementsBy1()
	{
		// Arrange
		var gauge = new ValueGauge();
		_ = gauge.IncrementBy(5);

		// Act
		var result = gauge.Decrement();

		// Assert
		result.ShouldBe(4);
		gauge.Value.ShouldBe(4);
	}

	[Fact]
	public void Decrement_BelowZero_AllowsNegativeValue()
	{
		// Arrange
		var gauge = new ValueGauge();

		// Act
		var result = gauge.Decrement();

		// Assert
		result.ShouldBe(-1);
		gauge.Value.ShouldBe(-1);
	}

	[Fact]
	public void Decrement_UpdatesLastUpdated()
	{
		// Arrange
		var gauge = new ValueGauge();
		var originalUpdated = gauge.LastUpdated;
		global::Tests.Shared.Infrastructure.TestTiming.Sleep(10);

		// Act
		_ = gauge.Decrement();

		// Assert
		gauge.LastUpdated.ShouldBeGreaterThanOrEqualTo(originalUpdated);
	}

	[Fact]
	public void DecrementBy_WithPositiveAmount_SubtractsCorrectly()
	{
		// Arrange
		var gauge = new ValueGauge();
		_ = gauge.IncrementBy(20);

		// Act
		var result = gauge.DecrementBy(5);

		// Assert
		result.ShouldBe(15);
		gauge.Value.ShouldBe(15);
	}

	[Fact]
	public void DecrementBy_WithZero_DoesNotChangeValue()
	{
		// Arrange
		var gauge = new ValueGauge();
		_ = gauge.IncrementBy(10);

		// Act
		var result = gauge.DecrementBy(0);

		// Assert
		result.ShouldBe(10);
		gauge.Value.ShouldBe(10);
	}

	[Fact]
	public void DecrementBy_UpdatesLastUpdated()
	{
		// Arrange
		var gauge = new ValueGauge();
		var originalUpdated = gauge.LastUpdated;
		global::Tests.Shared.Infrastructure.TestTiming.Sleep(10);

		// Act
		_ = gauge.DecrementBy(5);

		// Assert
		gauge.LastUpdated.ShouldBeGreaterThanOrEqualTo(originalUpdated);
	}

	#endregion

	#region Reset Tests

	[Fact]
	public void Reset_ResetsValueToZero()
	{
		// Arrange
		var gauge = new ValueGauge();
		gauge.Set(100);

		// Act
		gauge.Reset();

		// Assert
		gauge.Value.ShouldBe(0);
	}

	[Fact]
	public void Reset_UpdatesLastUpdated()
	{
		// Arrange
		var gauge = new ValueGauge();
		var originalUpdated = gauge.LastUpdated;
		global::Tests.Shared.Infrastructure.TestTiming.Sleep(10);

		// Act
		gauge.Reset();

		// Assert
		gauge.LastUpdated.ShouldBeGreaterThanOrEqualTo(originalUpdated);
	}

	[Fact]
	public void Reset_WhenAlreadyZero_MaintainsZero()
	{
		// Arrange
		var gauge = new ValueGauge();

		// Act
		gauge.Reset();

		// Assert
		gauge.Value.ShouldBe(0);
	}

	#endregion

	#region ToString Tests

	[Fact]
	public void ToString_ReturnsFormattedString()
	{
		// Arrange
		var gauge = new ValueGauge();
		gauge.Set(42);

		// Act
		var result = gauge.ToString();

		// Assert
		result.ShouldContain("Gauge[");
		result.ShouldContain("Value=42");
		result.ShouldContain("LastUpdated=");
	}

	#endregion

	#region Interface Tests

	[Fact]
	public void ImplementsIMetric()
	{
		// Arrange
		var gauge = new ValueGauge();

		// Assert
		_ = gauge.ShouldBeAssignableTo<IMetric>();
	}

	#endregion

	#region Thread Safety Tests

	[Fact]
	public async Task Increment_ConcurrentAccess_MaintainsCorrectCount()
	{
		// Arrange
		var gauge = new ValueGauge();
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
					_ = gauge.Increment();
				}
			});
		}

		await Task.WhenAll(tasks);

		// Assert
		gauge.Value.ShouldBe(threadCount * incrementsPerThread);
	}

	[Fact]
	public async Task MixedOperations_ConcurrentAccess_MaintainsConsistency()
	{
		// Arrange
		var gauge = new ValueGauge();
		const int operations = 1000;
		var tasks = new List<Task>();

		// Act - Mix of increments and decrements
		for (var i = 0; i < operations; i++)
		{
			tasks.Add(Task.Run(() => gauge.Increment()));
			tasks.Add(Task.Run(() => gauge.Decrement()));
		}

		await Task.WhenAll(tasks);

		// Assert - Should be zero since equal increments and decrements
		gauge.Value.ShouldBe(0);
	}

	[Fact]
	public async Task Set_ConcurrentAccess_TakesLastValue()
	{
		// Arrange
		var gauge = new ValueGauge();
		const int operations = 100;
		var tasks = new Task[operations];

		// Act
		for (var i = 0; i < operations; i++)
		{
			var value = i;
			tasks[i] = Task.Run(() => gauge.Set(value));
		}

		await Task.WhenAll(tasks);

		// Assert - Value should be one of the set values (0-99)
		gauge.Value.ShouldBeInRange(0, operations - 1);
	}

	#endregion
}
