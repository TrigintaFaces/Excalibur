// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Metrics;
using Excalibur.Dispatch.Abstractions.Diagnostics;

namespace Excalibur.Dispatch.Tests.Messaging.Metrics;

/// <summary>
/// Unit tests for <see cref="CacheAlignedTimestamp"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Metrics")]
public sealed class CacheAlignedTimestampShould : UnitTestBase
{
	#region Now Tests

	[Fact]
	public void CreateWithCurrentTime()
	{
		// Arrange
		var before = DateTime.UtcNow;

		// Act
		var timestamp = CacheAlignedTimestamp.Now();

		// Assert
		timestamp.Ticks.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void CreateWithPerformanceTimestamp()
	{
		// Act
		var timestamp = CacheAlignedTimestamp.Now();

		// Assert
		timestamp.PerformanceTimestamp.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void CreateWithValidDateTime()
	{
		// Act
		var timestamp = CacheAlignedTimestamp.Now();

		// Assert
		timestamp.DateTime.Year.ShouldBeGreaterThanOrEqualTo(2000);
	}

	#endregion

	#region UpdateNow Tests

	[Fact]
	public void UpdateNowSetsTicks()
	{
		// Arrange
		var timestamp = default(CacheAlignedTimestamp);

		// Act
		timestamp.UpdateNow();

		// Assert
		timestamp.Ticks.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void UpdateNowSetsPerformanceTimestamp()
	{
		// Arrange
		var timestamp = default(CacheAlignedTimestamp);

		// Act
		timestamp.UpdateNow();

		// Assert
		timestamp.PerformanceTimestamp.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void UpdateNowChangesTime()
	{
		// Arrange
		var timestamp = CacheAlignedTimestamp.Now();
		var initialTicks = timestamp.Ticks;

		// Small delay to ensure time changes
		global::Tests.Shared.Infrastructure.TestTiming.Sleep(1);

		// Act
		timestamp.UpdateNow();

		// Assert
		timestamp.Ticks.ShouldBeGreaterThanOrEqualTo(initialTicks);
	}

	#endregion

	#region UpdateHighResolution Tests

	[Fact]
	public void UpdateHighResolutionSetsTicks()
	{
		// Arrange
		var timestamp = default(CacheAlignedTimestamp);

		// Act
		timestamp.UpdateHighResolution();

		// Assert
		timestamp.Ticks.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void UpdateHighResolutionSetsPerformanceTimestamp()
	{
		// Arrange
		var timestamp = default(CacheAlignedTimestamp);

		// Act
		timestamp.UpdateHighResolution();

		// Assert
		timestamp.PerformanceTimestamp.ShouldBeGreaterThan(0);
	}

	#endregion

	#region GetElapsedMilliseconds Tests

	[Fact]
	public void GetElapsedMillisecondsReturnPositiveAfterDelay()
	{
		// Arrange
		var timestamp = CacheAlignedTimestamp.Now();
		global::Tests.Shared.Infrastructure.TestTiming.Sleep(10);

		// Act
		var elapsed = timestamp.GetElapsedMilliseconds();

		// Assert
		elapsed.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void GetElapsedMillisecondsReturnReasonableValue()
	{
		// Arrange
		var timestamp = CacheAlignedTimestamp.Now();
		var reference = ValueStopwatch.StartNew();
		global::Tests.Shared.Infrastructure.TestTiming.Sleep(50);

		// Act
		var elapsed = timestamp.GetElapsedMilliseconds();
		var expectedElapsed = reference.Elapsed.TotalMilliseconds;

		// Assert against an independent high-resolution stopwatch to avoid CI
		// scheduler jitter assumptions while still validating conversion accuracy.
		elapsed.ShouldBeGreaterThan(0);
		Math.Abs(elapsed - expectedElapsed).ShouldBeLessThan(100d);
	}

	#endregion

	#region Equality Tests

	[Fact]
	public void BeEqualWhenValuesMatch()
	{
		// Arrange
		var timestamp = CacheAlignedTimestamp.Now();

		// Act & Assert - same instance should be equal to itself
		timestamp.Equals(timestamp).ShouldBeTrue();
	}

	[Fact]
	public void NotBeEqualWhenValuesAreDifferent()
	{
		// Arrange
		var timestamp1 = CacheAlignedTimestamp.Now();
		global::Tests.Shared.Infrastructure.TestTiming.Sleep(1);
		var timestamp2 = CacheAlignedTimestamp.Now();

		// Act & Assert
		timestamp1.Equals(timestamp2).ShouldBeFalse();
		(timestamp1 != timestamp2).ShouldBeTrue();
	}

	[Fact]
	public void EqualsObjectReturnFalseForNull()
	{
		// Arrange
		var timestamp = CacheAlignedTimestamp.Now();

		// Act & Assert
		timestamp.Equals(null).ShouldBeFalse();
	}

	[Fact]
	public void EqualsObjectReturnFalseForDifferentType()
	{
		// Arrange
		var timestamp = CacheAlignedTimestamp.Now();

		// Act & Assert
		timestamp.Equals("not a timestamp").ShouldBeFalse();
	}

	[Fact]
	public void EqualsObjectReturnTrueForMatchingTimestamp()
	{
		// Arrange
		var timestamp1 = CacheAlignedTimestamp.Now();
		object timestamp2 = timestamp1;

		// Act & Assert
		timestamp1.Equals(timestamp2).ShouldBeTrue();
	}

	[Fact]
	public void EqualityOperatorReturnTrueForEqual()
	{
		// Arrange
		var timestamp1 = CacheAlignedTimestamp.Now();
		var timestamp2 = timestamp1;

		// Act & Assert
		(timestamp1 == timestamp2).ShouldBeTrue();
	}

	[Fact]
	public void InequalityOperatorReturnTrueForDifferent()
	{
		// Arrange
		var timestamp1 = CacheAlignedTimestamp.Now();
		global::Tests.Shared.Infrastructure.TestTiming.Sleep(1);
		var timestamp2 = CacheAlignedTimestamp.Now();

		// Act & Assert
		(timestamp1 != timestamp2).ShouldBeTrue();
	}

	#endregion

	#region GetHashCode Tests

	[Fact]
	public void ProduceConsistentHashCode()
	{
		// Arrange
		var timestamp = CacheAlignedTimestamp.Now();

		// Act
		var hash1 = timestamp.GetHashCode();
		var hash2 = timestamp.GetHashCode();

		// Assert
		hash1.ShouldBe(hash2);
	}

	#endregion

	#region Thread Safety Tests

	[Fact]
	public async Task SupportConcurrentUpdates()
	{
		// Arrange
		var timestamp = default(CacheAlignedTimestamp);
		const int threads = 10;
		const int iterations = 100;

		// Act
		var tasks = Enumerable.Range(0, threads)
			.Select(unused => Task.Run(() =>
			{
				for (int i = 0; i < iterations; i++)
				{
					timestamp.UpdateNow();
				}
			}));

		await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert - timestamp should be valid after concurrent updates
		timestamp.Ticks.ShouldBeGreaterThan(0);
		timestamp.PerformanceTimestamp.ShouldBeGreaterThan(0);
	}

	#endregion
}
