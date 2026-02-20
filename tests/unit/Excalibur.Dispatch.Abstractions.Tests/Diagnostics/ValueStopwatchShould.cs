// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Diagnostics;

namespace Excalibur.Dispatch.Abstractions.Tests.Diagnostics;

/// <summary>
/// Unit tests for <see cref="ValueStopwatch"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Diagnostics")]
[Trait("Priority", "0")]
public sealed class ValueStopwatchShould
{
	#region Empty Tests

	[Fact]
	public void Empty_IsNotActive()
	{
		// Act
		var stopwatch = ValueStopwatch.Empty;

		// Assert
		stopwatch.IsActive.ShouldBeFalse();
	}

	[Fact]
	public void Empty_ToString_ReturnsNotStarted()
	{
		// Act
		var result = ValueStopwatch.Empty.ToString();

		// Assert
		result.ShouldBe("[Not Started]");
	}

	[Fact]
	public void Empty_Elapsed_ThrowsInvalidOperationException()
	{
		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() => _ = ValueStopwatch.Empty.Elapsed);
	}

	[Fact]
	public void Empty_ElapsedTicks_ThrowsInvalidOperationException()
	{
		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() => _ = ValueStopwatch.Empty.ElapsedTicks);
	}

	[Fact]
	public void Empty_ElapsedMilliseconds_ThrowsInvalidOperationException()
	{
		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() => _ = ValueStopwatch.Empty.ElapsedMilliseconds);
	}

	[Fact]
	public void Empty_ElapsedMicroseconds_ThrowsInvalidOperationException()
	{
		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() => _ = ValueStopwatch.Empty.ElapsedMicroseconds);
	}

	[Fact]
	public void Empty_GetElapsedTime_ThrowsInvalidOperationException()
	{
		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() => _ = ValueStopwatch.Empty.GetElapsedTime());
	}

	#endregion

	#region StartNew Tests

	[Fact]
	public void StartNew_IsActive()
	{
		// Act
		var stopwatch = ValueStopwatch.StartNew();

		// Assert
		stopwatch.IsActive.ShouldBeTrue();
	}

	[Fact]
	public void StartNew_Elapsed_ReturnsPositiveOrZeroTimeSpan()
	{
		// Act
		var stopwatch = ValueStopwatch.StartNew();
		var elapsed = stopwatch.Elapsed;

		// Assert
		elapsed.ShouldBeGreaterThanOrEqualTo(TimeSpan.Zero);
	}

	[Fact]
	public void StartNew_ElapsedTicks_ReturnsNonNegativeValue()
	{
		// Act
		var stopwatch = ValueStopwatch.StartNew();
		var ticks = stopwatch.ElapsedTicks;

		// Assert
		ticks.ShouldBeGreaterThanOrEqualTo(0);
	}

	[Fact]
	public void StartNew_ElapsedMilliseconds_ReturnsNonNegativeValue()
	{
		// Act
		var stopwatch = ValueStopwatch.StartNew();
		var ms = stopwatch.ElapsedMilliseconds;

		// Assert
		ms.ShouldBeGreaterThanOrEqualTo(0);
	}

	[Fact]
	public void StartNew_ElapsedMicroseconds_ReturnsNonNegativeValue()
	{
		// Act
		var stopwatch = ValueStopwatch.StartNew();
		var us = stopwatch.ElapsedMicroseconds;

		// Assert
		us.ShouldBeGreaterThanOrEqualTo(0);
	}

	#endregion

	#region Restart Tests

	[Fact]
	public void Restart_IsActive()
	{
		// Act
		var stopwatch = ValueStopwatch.Restart();

		// Assert
		stopwatch.IsActive.ShouldBeTrue();
	}

	#endregion

	#region FromTimestamp Tests

	[Fact]
	public void FromTimestamp_WithValidTimestamp_IsActive()
	{
		// Act
		var stopwatch = ValueStopwatch.FromTimestamp(ValueStopwatch.GetTimestamp());

		// Assert
		stopwatch.IsActive.ShouldBeTrue();
	}

	[Fact]
	public void FromTimestamp_WithZero_IsActive()
	{
		// Act
		var stopwatch = ValueStopwatch.FromTimestamp(0);

		// Assert
		stopwatch.IsActive.ShouldBeTrue();
	}

	[Fact]
	public void FromTimestamp_WithNegativeValue_ThrowsArgumentOutOfRangeException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() => ValueStopwatch.FromTimestamp(-1));
	}

	[Fact]
	public void FromTimestamp_WithLongMinValue_IsNotActive()
	{
		// Act
		var stopwatch = ValueStopwatch.FromTimestamp(long.MinValue);

		// Assert
		stopwatch.IsActive.ShouldBeFalse();
	}

	#endregion

	#region GetTimestamp Tests

	[Fact]
	public void GetTimestamp_ReturnsPositiveValue()
	{
		// Act
		var timestamp = ValueStopwatch.GetTimestamp();

		// Assert
		timestamp.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void GetTimestamp_ReturnsIncreasingValues()
	{
		// Act
		var timestamp1 = ValueStopwatch.GetTimestamp();
		var timestamp2 = ValueStopwatch.GetTimestamp();

		// Assert
		timestamp2.ShouldBeGreaterThanOrEqualTo(timestamp1);
	}

	#endregion

	#region GetFrequency Tests

	[Fact]
	public void GetFrequency_ReturnsPositiveValue()
	{
		// Act
		var frequency = ValueStopwatch.GetFrequency();

		// Assert
		frequency.ShouldBeGreaterThan(0);
	}

	#endregion

	#region Time Action Tests

	[Fact]
	public void Time_WithNullAction_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => ValueStopwatch.Time(null!));
	}

	[Fact]
	public void Time_ExecutesAction()
	{
		// Arrange
		var executed = false;

		// Act
		_ = ValueStopwatch.Time(() => executed = true);

		// Assert
		executed.ShouldBeTrue();
	}

	[Fact]
	public void Time_ReturnsNonNegativeTimeSpan()
	{
		// Act
		var elapsed = ValueStopwatch.Time(() => { });

		// Assert
		elapsed.ShouldBeGreaterThanOrEqualTo(TimeSpan.Zero);
	}

	#endregion

	#region TimeAsync Tests

	[Fact]
	public async Task TimeAsync_WithNullOperation_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(() => ValueStopwatch.TimeAsync(null!));
	}

	[Fact]
	public async Task TimeAsync_ExecutesOperation()
	{
		// Arrange
		var executed = false;

		// Act
		_ = await ValueStopwatch.TimeAsync(async () =>
		{
			await Task.Yield();
			executed = true;
		});

		// Assert
		executed.ShouldBeTrue();
	}

	[Fact]
	public async Task TimeAsync_ReturnsNonNegativeTimeSpan()
	{
		// Act
		var elapsed = await ValueStopwatch.TimeAsync(() => Task.CompletedTask);

		// Assert
		elapsed.ShouldBeGreaterThanOrEqualTo(TimeSpan.Zero);
	}

	#endregion

	#region ToString Tests

	[Fact]
	public void ToString_WhenActive_ReturnsFormattedString()
	{
		// Arrange
		var stopwatch = ValueStopwatch.StartNew();

		// Act
		var result = stopwatch.ToString();

		// Assert
		result.ShouldNotBeNullOrEmpty();
		result.ShouldNotBe("[Not Started]");
		result.ShouldNotBe("[Invalid]");
	}

	#endregion

	#region Equality Tests

	[Fact]
	public void Equals_WithSameStopwatch_ReturnsTrue()
	{
		// Arrange
		var stopwatch = ValueStopwatch.StartNew();

		// Act & Assert
		stopwatch.Equals(stopwatch).ShouldBeTrue();
	}

	[Fact]
	public void Equals_WithDifferentStopwatches_ReturnsFalse()
	{
		// Arrange
		var stopwatch1 = ValueStopwatch.StartNew();
		Thread.Sleep(1);
		var stopwatch2 = ValueStopwatch.StartNew();

		// Act & Assert
		stopwatch1.Equals(stopwatch2).ShouldBeFalse();
	}

	[Fact]
	public void Equals_WithEmptyStopwatches_ReturnsTrue()
	{
		// Arrange
		var stopwatch1 = ValueStopwatch.Empty;
		var stopwatch2 = ValueStopwatch.Empty;

		// Act & Assert
		stopwatch1.Equals(stopwatch2).ShouldBeTrue();
	}

	[Fact]
	public void Equals_WithNonValueStopwatch_ReturnsFalse()
	{
		// Arrange
		var stopwatch = ValueStopwatch.StartNew();

		// Act & Assert
		stopwatch.Equals("not a stopwatch").ShouldBeFalse();
	}

	[Fact]
	public void Equals_WithNull_ReturnsFalse()
	{
		// Arrange
		var stopwatch = ValueStopwatch.StartNew();

		// Act & Assert
		stopwatch.Equals(null).ShouldBeFalse();
	}

	[Fact]
	public void ObjectEquals_WithBoxedValueStopwatch_ReturnsTrue()
	{
		// Arrange
		var stopwatch = ValueStopwatch.StartNew();
		object boxed = stopwatch;

		// Act & Assert
		stopwatch.Equals(boxed).ShouldBeTrue();
	}

	#endregion

	#region GetHashCode Tests

	[Fact]
	public void GetHashCode_WithSameTimestamp_ReturnsSameHash()
	{
		// Arrange
		var timestamp = ValueStopwatch.GetTimestamp();
		var stopwatch1 = ValueStopwatch.FromTimestamp(timestamp);
		var stopwatch2 = ValueStopwatch.FromTimestamp(timestamp);

		// Act & Assert
		stopwatch1.GetHashCode().ShouldBe(stopwatch2.GetHashCode());
	}

	[Fact]
	public void GetHashCode_WithEmpty_ReturnsConsistentHash()
	{
		// Arrange
		var stopwatch1 = ValueStopwatch.Empty;
		var stopwatch2 = ValueStopwatch.Empty;

		// Act & Assert
		stopwatch1.GetHashCode().ShouldBe(stopwatch2.GetHashCode());
	}

	#endregion

	#region CompareTo Tests

	[Fact]
	public void CompareTo_WithSameStopwatch_ReturnsZero()
	{
		// Arrange
		var stopwatch = ValueStopwatch.StartNew();

		// Act & Assert
		stopwatch.CompareTo(stopwatch).ShouldBe(0);
	}

	[Fact]
	public void CompareTo_WithEarlierStopwatch_ReturnsPositive()
	{
		// Arrange
		var earlier = ValueStopwatch.StartNew();
		Thread.Sleep(1);
		var later = ValueStopwatch.StartNew();

		// Act & Assert
		later.CompareTo(earlier).ShouldBeGreaterThan(0);
	}

	[Fact]
	public void CompareTo_WithLaterStopwatch_ReturnsNegative()
	{
		// Arrange
		var earlier = ValueStopwatch.StartNew();
		Thread.Sleep(1);
		var later = ValueStopwatch.StartNew();

		// Act & Assert
		earlier.CompareTo(later).ShouldBeLessThan(0);
	}

	[Fact]
	public void CompareTo_WithNull_ReturnsPositive()
	{
		// Arrange
		var stopwatch = ValueStopwatch.StartNew();

		// Act & Assert
		stopwatch.CompareTo(null).ShouldBeGreaterThan(0);
	}

	[Fact]
	public void CompareTo_WithNonValueStopwatch_ThrowsArgumentException()
	{
		// Arrange
		var stopwatch = ValueStopwatch.StartNew();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => stopwatch.CompareTo("not a stopwatch"));
	}

	#endregion

	#region Operator Tests

	[Fact]
	public void OperatorEquals_WithSameTimestamp_ReturnsTrue()
	{
		// Arrange
		var timestamp = ValueStopwatch.GetTimestamp();
		var stopwatch1 = ValueStopwatch.FromTimestamp(timestamp);
		var stopwatch2 = ValueStopwatch.FromTimestamp(timestamp);

		// Act & Assert
		(stopwatch1 == stopwatch2).ShouldBeTrue();
	}

	[Fact]
	public void OperatorNotEquals_WithDifferentStopwatches_ReturnsTrue()
	{
		// Arrange
		var stopwatch1 = ValueStopwatch.StartNew();
		Thread.Sleep(1);
		var stopwatch2 = ValueStopwatch.StartNew();

		// Act & Assert
		(stopwatch1 != stopwatch2).ShouldBeTrue();
	}

	[Fact]
	public void OperatorLessThan_WithEarlierFirst_ReturnsTrue()
	{
		// Arrange
		var earlier = ValueStopwatch.StartNew();
		Thread.Sleep(1);
		var later = ValueStopwatch.StartNew();

		// Act & Assert
		(earlier < later).ShouldBeTrue();
	}

	[Fact]
	public void OperatorGreaterThan_WithLaterFirst_ReturnsTrue()
	{
		// Arrange
		var earlier = ValueStopwatch.StartNew();
		Thread.Sleep(1);
		var later = ValueStopwatch.StartNew();

		// Act & Assert
		(later > earlier).ShouldBeTrue();
	}

	[Fact]
	public void OperatorLessThanOrEqual_WithSameTimestamp_ReturnsTrue()
	{
		// Arrange
		var timestamp = ValueStopwatch.GetTimestamp();
		var stopwatch1 = ValueStopwatch.FromTimestamp(timestamp);
		var stopwatch2 = ValueStopwatch.FromTimestamp(timestamp);

		// Act & Assert
		(stopwatch1 <= stopwatch2).ShouldBeTrue();
	}

	[Fact]
	public void OperatorGreaterThanOrEqual_WithSameTimestamp_ReturnsTrue()
	{
		// Arrange
		var timestamp = ValueStopwatch.GetTimestamp();
		var stopwatch1 = ValueStopwatch.FromTimestamp(timestamp);
		var stopwatch2 = ValueStopwatch.FromTimestamp(timestamp);

		// Act & Assert
		(stopwatch1 >= stopwatch2).ShouldBeTrue();
	}

	#endregion

	#region GetElapsedTime Tests

	[Fact]
	public void GetElapsedTime_WhenActive_ReturnsNonNegativeTimeSpan()
	{
		// Arrange
		var stopwatch = ValueStopwatch.StartNew();

		// Act
		var elapsed = stopwatch.GetElapsedTime();

		// Assert - GetElapsedTime and Elapsed both compute elapsed time, values may differ slightly
		elapsed.ShouldBeGreaterThanOrEqualTo(TimeSpan.Zero);
	}

	#endregion
}
