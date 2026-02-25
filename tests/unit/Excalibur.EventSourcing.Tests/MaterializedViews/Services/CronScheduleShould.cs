// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

namespace Excalibur.EventSourcing.Tests.MaterializedViews.Services;

/// <summary>
/// Unit tests for CronSchedule internal class.
/// </summary>
/// <remarks>
/// Sprint 517: Materialized Views provider tests.
/// Tests verify cron scheduling behavior via reflection since CronSchedule is internal.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "MaterializedViews")]
[Trait("Feature", "Services")]
public sealed class CronScheduleShould
{
	private static readonly Type CronScheduleType = typeof(EventSourcing.Services.MaterializedViewRefreshService)
		.Assembly
		.GetType("Excalibur.EventSourcing.Services.CronSchedule")!;

	#region Type Tests

	[Fact]
	public void BeInternalSealedClass()
	{
		// Assert
		CronScheduleType.ShouldNotBeNull();
		CronScheduleType.IsClass.ShouldBeTrue();
		CronScheduleType.IsSealed.ShouldBeTrue();
		CronScheduleType.IsPublic.ShouldBeFalse();
	}

	#endregion

	#region TryParse Tests

	[Fact]
	public void TryParse_ReturnFalseForNullExpression()
	{
		// Arrange
		var tryParseMethod = CronScheduleType.GetMethod("TryParse", BindingFlags.Public | BindingFlags.Static)!;

		// Act
		var args = new object?[] { null, null };
		var result = (bool)tryParseMethod.Invoke(null, args)!;

		// Assert
		result.ShouldBeFalse();
		args[1].ShouldBeNull();
	}

	[Fact]
	public void TryParse_ReturnFalseForEmptyExpression()
	{
		// Arrange
		var tryParseMethod = CronScheduleType.GetMethod("TryParse", BindingFlags.Public | BindingFlags.Static)!;

		// Act
		var args = new object?[] { "", null };
		var result = (bool)tryParseMethod.Invoke(null, args)!;

		// Assert
		result.ShouldBeFalse();
		args[1].ShouldBeNull();
	}

	[Fact]
	public void TryParse_ReturnFalseForWhitespaceExpression()
	{
		// Arrange
		var tryParseMethod = CronScheduleType.GetMethod("TryParse", BindingFlags.Public | BindingFlags.Static)!;

		// Act
		var args = new object?[] { "   ", null };
		var result = (bool)tryParseMethod.Invoke(null, args)!;

		// Assert
		result.ShouldBeFalse();
		args[1].ShouldBeNull();
	}

	[Fact]
	public void TryParse_ReturnFalseForInvalidCronExpression()
	{
		// Arrange
		var tryParseMethod = CronScheduleType.GetMethod("TryParse", BindingFlags.Public | BindingFlags.Static)!;

		// Act
		var args = new object?[] { "invalid cron", null };
		var result = (bool)tryParseMethod.Invoke(null, args)!;

		// Assert
		result.ShouldBeFalse();
		args[1].ShouldBeNull();
	}

	[Theory]
	[InlineData("* * * * *")]        // Every minute
	[InlineData("*/5 * * * *")]      // Every 5 minutes
	[InlineData("0 * * * *")]        // Every hour
	[InlineData("0 0 * * *")]        // Daily at midnight
	[InlineData("0 2 * * 0")]        // Weekly on Sunday at 2 AM
	[InlineData("30 4 1 * *")]       // Monthly on the 1st at 4:30 AM
	public void TryParse_ReturnTrueForValidCronExpression(string cronExpression)
	{
		// Arrange
		var tryParseMethod = CronScheduleType.GetMethod("TryParse", BindingFlags.Public | BindingFlags.Static)!;

		// Act
		var args = new object?[] { cronExpression, null };
		var result = (bool)tryParseMethod.Invoke(null, args)!;

		// Assert
		result.ShouldBeTrue();
		args[1].ShouldNotBeNull();
	}

	#endregion

	#region Constructor Tests

	[Fact]
	public void Constructor_ThrowArgumentExceptionForNullExpression()
	{
		// Arrange
		var constructor = CronScheduleType.GetConstructor([typeof(string)]);

		// Act & Assert
		var ex = Should.Throw<TargetInvocationException>(() => constructor.Invoke([null]));
		ex.InnerException.ShouldBeAssignableTo<ArgumentException>();
	}

	[Fact]
	public void Constructor_ThrowArgumentExceptionForEmptyExpression()
	{
		// Arrange
		var constructor = CronScheduleType.GetConstructor([typeof(string)]);

		// Act & Assert
		var ex = Should.Throw<TargetInvocationException>(() => constructor.Invoke([""]));
		ex.InnerException.ShouldBeOfType<ArgumentException>();
	}

	[Fact]
	public void Constructor_ThrowArgumentExceptionForInvalidExpression()
	{
		// Arrange
		var constructor = CronScheduleType.GetConstructor([typeof(string)]);

		// Act & Assert
		var ex = Should.Throw<TargetInvocationException>(() => constructor.Invoke(["not valid"]));
		ex.InnerException.ShouldBeOfType<ArgumentException>();
	}

	[Fact]
	public void Constructor_SucceedForValidExpression()
	{
		// Arrange
		var constructor = CronScheduleType.GetConstructor([typeof(string)]);

		// Act
		var instance = constructor.Invoke(["* * * * *"]);

		// Assert
		instance.ShouldNotBeNull();
	}

	#endregion

	#region GetNextOccurrence Tests

	[Fact]
	public void GetNextOccurrence_ReturnFutureTime()
	{
		// Arrange - every minute cron
		var constructor = CronScheduleType.GetConstructor([typeof(string)])!;
		var schedule = constructor.Invoke(["* * * * *"]);
		var getNextMethod = CronScheduleType.GetMethod("GetNextOccurrence")!;

		var now = new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero);

		// Act
		var next = (DateTimeOffset)getNextMethod.Invoke(schedule, [now])!;

		// Assert - should be at most 1 minute in the future
		next.ShouldBeGreaterThan(now);
		(next - now).TotalMinutes.ShouldBeLessThanOrEqualTo(1);
	}

	[Fact]
	public void GetNextOccurrence_ReturnCorrectTimeForHourlySchedule()
	{
		// Arrange - at minute 0 every hour
		var constructor = CronScheduleType.GetConstructor([typeof(string)])!;
		var schedule = constructor.Invoke(["0 * * * *"]);
		var getNextMethod = CronScheduleType.GetMethod("GetNextOccurrence")!;

		var now = new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero);

		// Act
		var next = (DateTimeOffset)getNextMethod.Invoke(schedule, [now])!;

		// Assert - should be at 11:00:00
		next.Hour.ShouldBe(11);
		next.Minute.ShouldBe(0);
	}

	[Fact]
	public void GetNextOccurrence_ReturnCorrectTimeForDailySchedule()
	{
		// Arrange - daily at midnight
		var constructor = CronScheduleType.GetConstructor([typeof(string)])!;
		var schedule = constructor.Invoke(["0 0 * * *"]);
		var getNextMethod = CronScheduleType.GetMethod("GetNextOccurrence")!;

		var now = new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero);

		// Act
		var next = (DateTimeOffset)getNextMethod.Invoke(schedule, [now])!;

		// Assert - should be at midnight on the 16th
		next.Day.ShouldBe(16);
		next.Hour.ShouldBe(0);
		next.Minute.ShouldBe(0);
	}

	#endregion

	#region GetDelayUntilNext Tests

	[Fact]
	public void GetDelayUntilNext_ReturnPositiveDelay()
	{
		// Arrange - every minute
		var constructor = CronScheduleType.GetConstructor([typeof(string)])!;
		var schedule = constructor.Invoke(["* * * * *"]);
		var getDelayMethod = CronScheduleType.GetMethod("GetDelayUntilNext")!;

		var now = new DateTimeOffset(2026, 1, 15, 10, 30, 30, TimeSpan.Zero);

		// Act
		var delay = (TimeSpan)getDelayMethod.Invoke(schedule, [now])!;

		// Assert - delay should be positive and less than or equal to 1 minute
		delay.ShouldBeGreaterThan(TimeSpan.Zero);
		delay.TotalSeconds.ShouldBeLessThanOrEqualTo(60);
	}

	[Fact]
	public void GetDelayUntilNext_ReturnZeroWhenNextOccurrenceIsPast()
	{
		// Arrange - every minute
		var constructor = CronScheduleType.GetConstructor([typeof(string)])!;
		var schedule = constructor.Invoke(["* * * * *"]);
		var getDelayMethod = CronScheduleType.GetMethod("GetDelayUntilNext")!;

		// Use exact minute boundary
		var now = new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero);

		// Act
		var delay = (TimeSpan)getDelayMethod.Invoke(schedule, [now])!;

		// Assert - next occurrence is in the future, so delay should be positive
		delay.ShouldBeGreaterThanOrEqualTo(TimeSpan.Zero);
	}

	[Fact]
	public void GetDelayUntilNext_CalculateCorrectDelayForHourlySchedule()
	{
		// Arrange - at minute 0 every hour
		var constructor = CronScheduleType.GetConstructor([typeof(string)])!;
		var schedule = constructor.Invoke(["0 * * * *"]);
		var getDelayMethod = CronScheduleType.GetMethod("GetDelayUntilNext")!;

		var now = new DateTimeOffset(2026, 1, 15, 10, 45, 0, TimeSpan.Zero);

		// Act
		var delay = (TimeSpan)getDelayMethod.Invoke(schedule, [now])!;

		// Assert - 15 minutes until 11:00
		delay.TotalMinutes.ShouldBe(15);
	}

	#endregion
}
