// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Messages;

namespace Excalibur.Dispatch.Tests.Messaging.Messages;

/// <summary>
/// Unit tests for <see cref="TimerInfo"/>.
/// </summary>
/// <remarks>
/// Tests the timer trigger information message class.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Messages")]
[Trait("Priority", "0")]
public sealed class TimerInfoShould
{
	#region Constructor Tests

	[Fact]
	public void Constructor_Default_InitializesWithDefaults()
	{
		// Arrange & Act
		var timerInfo = new TimerInfo();

		// Assert
		_ = timerInfo.ShouldNotBeNull();
		timerInfo.MessageId.ShouldNotBeNullOrEmpty();
		timerInfo.TimerName.ShouldBe(string.Empty);
		timerInfo.CronExpression.ShouldBe(string.Empty);
		timerInfo.IsPastDue.ShouldBeFalse();
		_ = timerInfo.Headers.ShouldNotBeNull();
		_ = timerInfo.Features.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_GeneratesUniqueMessageId()
	{
		// Arrange & Act
		var timerInfo1 = new TimerInfo();
		var timerInfo2 = new TimerInfo();

		// Assert
		timerInfo1.MessageId.ShouldNotBe(timerInfo2.MessageId);
	}

	[Fact]
	public void Constructor_SetsTimestampToNow()
	{
		// Arrange
		var before = DateTimeOffset.UtcNow;

		// Act
		var timerInfo = new TimerInfo();

		// Assert
		var after = DateTimeOffset.UtcNow;
		timerInfo.Timestamp.ShouldBeGreaterThanOrEqualTo(before);
		timerInfo.Timestamp.ShouldBeLessThanOrEqualTo(after);
	}

	[Fact]
	public void Constructor_SetsMessageType()
	{
		// Arrange & Act
		var timerInfo = new TimerInfo();

		// Assert
		timerInfo.MessageType.ShouldBe("TimerInfo");
	}

	#endregion

	#region MessageId Property Tests

	[Fact]
	public void MessageId_CanBeSet()
	{
		// Arrange
		var timerInfo = new TimerInfo();
		var newId = Guid.NewGuid().ToString();

		// Act
		timerInfo.MessageId = newId;

		// Assert
		timerInfo.MessageId.ShouldBe(newId);
	}

	[Fact]
	public void MessageId_IsValidGuidFormat()
	{
		// Arrange & Act
		var timerInfo = new TimerInfo();

		// Assert
		Guid.TryParse(timerInfo.MessageId, out _).ShouldBeTrue();
	}

	#endregion

	#region CorrelationId Property Tests

	[Fact]
	public void CorrelationId_DefaultsToNull()
	{
		// Arrange & Act
		var timerInfo = new TimerInfo();

		// Assert
		timerInfo.CorrelationId.ShouldBeNull();
	}

	[Fact]
	public void CorrelationId_CanBeSet()
	{
		// Arrange
		var timerInfo = new TimerInfo();

		// Act
		timerInfo.CorrelationId = "corr-12345";

		// Assert
		timerInfo.CorrelationId.ShouldBe("corr-12345");
	}

	[Fact]
	public void CorrelationId_CanBeCleared()
	{
		// Arrange
		var timerInfo = new TimerInfo();
		timerInfo.CorrelationId = "corr-12345";

		// Act
		timerInfo.CorrelationId = null;

		// Assert
		timerInfo.CorrelationId.ShouldBeNull();
	}

	[Fact]
	public void CorrelationId_AddsToHeaders()
	{
		// Arrange
		var timerInfo = new TimerInfo();

		// Act
		timerInfo.CorrelationId = "corr-12345";

		// Assert
		timerInfo.Headers.ShouldContainKey("CorrelationId");
		timerInfo.Headers["CorrelationId"].ShouldBe("corr-12345");
	}

	[Fact]
	public void CorrelationId_RemovesFromHeadersWhenSetToNull()
	{
		// Arrange
		var timerInfo = new TimerInfo();
		timerInfo.CorrelationId = "corr-12345";

		// Act
		timerInfo.CorrelationId = null;

		// Assert
		timerInfo.Headers.ShouldNotContainKey("CorrelationId");
	}

	#endregion

	#region Timestamp Property Tests

	[Fact]
	public void Timestamp_CanBeSet()
	{
		// Arrange
		var timerInfo = new TimerInfo();
		var newTimestamp = DateTimeOffset.UtcNow.AddHours(-1);

		// Act
		timerInfo.Timestamp = newTimestamp;

		// Assert
		timerInfo.Timestamp.ShouldBe(newTimestamp);
	}

	#endregion

	#region TimerName Property Tests

	[Fact]
	public void TimerName_CanBeSet()
	{
		// Arrange
		var timerInfo = new TimerInfo();

		// Act
		timerInfo.TimerName = "DailyCleanup";

		// Assert
		timerInfo.TimerName.ShouldBe("DailyCleanup");
	}

	[Theory]
	[InlineData("HourlySync")]
	[InlineData("WeeklyReport")]
	[InlineData("MonthlyBackup")]
	public void TimerName_WithVariousNames_Works(string name)
	{
		// Arrange
		var timerInfo = new TimerInfo();

		// Act
		timerInfo.TimerName = name;

		// Assert
		timerInfo.TimerName.ShouldBe(name);
	}

	#endregion

	#region CronExpression Property Tests

	[Fact]
	public void CronExpression_CanBeSet()
	{
		// Arrange
		var timerInfo = new TimerInfo();

		// Act
		timerInfo.CronExpression = "0 */5 * * * *";

		// Assert
		timerInfo.CronExpression.ShouldBe("0 */5 * * * *");
	}

	[Theory]
	[InlineData("0 0 * * * *")] // Every hour
	[InlineData("0 0 0 * * *")] // Every day at midnight
	[InlineData("0 0 0 * * MON")] // Every Monday at midnight
	[InlineData("0 */15 * * * *")] // Every 15 minutes
	public void CronExpression_WithVariousExpressions_Works(string expression)
	{
		// Arrange
		var timerInfo = new TimerInfo();

		// Act
		timerInfo.CronExpression = expression;

		// Assert
		timerInfo.CronExpression.ShouldBe(expression);
	}

	#endregion

	#region ScheduledTime Property Tests

	[Fact]
	public void ScheduledTime_CanBeSet()
	{
		// Arrange
		var timerInfo = new TimerInfo();
		var scheduledTime = DateTimeOffset.UtcNow.AddMinutes(30);

		// Act
		timerInfo.ScheduledTime = scheduledTime;

		// Assert
		timerInfo.ScheduledTime.ShouldBe(scheduledTime);
	}

	#endregion

	#region IsPastDue Property Tests

	[Fact]
	public void IsPastDue_DefaultsToFalse()
	{
		// Arrange & Act
		var timerInfo = new TimerInfo();

		// Assert
		timerInfo.IsPastDue.ShouldBeFalse();
	}

	[Fact]
	public void IsPastDue_CanBeSetToTrue()
	{
		// Arrange
		var timerInfo = new TimerInfo();

		// Act
		timerInfo.IsPastDue = true;

		// Assert
		timerInfo.IsPastDue.ShouldBeTrue();
	}

	#endregion

	#region Headers Property Tests

	[Fact]
	public void Headers_IsReadOnly()
	{
		// Arrange & Act
		var timerInfo = new TimerInfo();

		// Assert
		_ = timerInfo.Headers.ShouldBeAssignableTo<IReadOnlyDictionary<string, object>>();
	}

	#endregion

	#region Body Property Tests

	[Fact]
	public void Body_ReturnsSelf()
	{
		// Arrange & Act
		var timerInfo = new TimerInfo();

		// Assert
		timerInfo.Body.ShouldBe(timerInfo);
	}

	#endregion

	#region MessageType Property Tests

	[Fact]
	public void MessageType_ReturnsTypeName()
	{
		// Arrange & Act
		var timerInfo = new TimerInfo();

		// Assert
		timerInfo.MessageType.ShouldBe("TimerInfo");
	}

	#endregion

	#region Features Property Tests

	[Fact]
	public void Features_IsNotNull()
	{
		// Arrange & Act
		var timerInfo = new TimerInfo();

		// Assert
		_ = timerInfo.Features.ShouldNotBeNull();
		_ = timerInfo.Features.ShouldBeAssignableTo<IMessageFeatures>();
	}

	#endregion

	#region Id Property Tests

	[Fact]
	public void Id_ReturnsGuidFromMessageId()
	{
		// Arrange
		var timerInfo = new TimerInfo();
		var expectedGuid = Guid.Parse(timerInfo.MessageId);

		// Act
		var id = timerInfo.Id;

		// Assert
		id.ShouldBe(expectedGuid);
	}

	[Fact]
	public void Id_ReturnsEmptyGuidWhenMessageIdInvalid()
	{
		// Arrange
		var timerInfo = new TimerInfo();
		timerInfo.MessageId = "not-a-guid";

		// Act
		var id = timerInfo.Id;

		// Assert
		id.ShouldBe(Guid.Empty);
	}

	#endregion

	#region Kind Property Tests

	[Fact]
	public void Kind_ReturnsEvent()
	{
		// Arrange & Act
		var timerInfo = new TimerInfo();

		// Assert
		timerInfo.Kind.ShouldBe(MessageKinds.Event);
	}

	#endregion

	#region Interface Tests

	[Fact]
	public void ImplementsIDispatchMessage()
	{
		// Arrange
		var timerInfo = new TimerInfo();

		// Assert
		_ = timerInfo.ShouldBeAssignableTo<IDispatchMessage>();
	}

	#endregion

	#region Full Object Tests

	[Fact]
	public void AllProperties_CanBeSetViaObjectInitializer()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;
		var scheduledTime = now.AddMinutes(5);

		// Act
		var timerInfo = new TimerInfo
		{
			TimerName = "TestTimer",
			CronExpression = "0 */5 * * * *",
			ScheduledTime = scheduledTime,
			IsPastDue = true,
		};

		// Assert
		timerInfo.TimerName.ShouldBe("TestTimer");
		timerInfo.CronExpression.ShouldBe("0 */5 * * * *");
		timerInfo.ScheduledTime.ShouldBe(scheduledTime);
		timerInfo.IsPastDue.ShouldBeTrue();
	}

	#endregion
}
