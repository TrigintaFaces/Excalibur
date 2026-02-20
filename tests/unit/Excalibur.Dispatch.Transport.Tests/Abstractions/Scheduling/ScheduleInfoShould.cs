// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Scheduling;

/// <summary>
/// Unit tests for <see cref="ScheduleInfo"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport.Abstractions")]
public sealed class ScheduleInfoShould
{
	[Fact]
	public void HaveEmptyScheduleId_ByDefault()
	{
		// Arrange & Act
		var info = new ScheduleInfo();

		// Assert
		info.ScheduleId.ShouldBe(string.Empty);
	}

	[Fact]
	public void HaveNullMessage_ByDefault()
	{
		// Arrange & Act
		var info = new ScheduleInfo();

		// Assert
		info.Message.ShouldBeNull();
	}

	[Fact]
	public void HaveDefaultScheduledTime_ByDefault()
	{
		// Arrange & Act
		var info = new ScheduleInfo();

		// Assert
		info.ScheduledTime.ShouldBe(default(DateTimeOffset));
	}

	[Fact]
	public void HaveDefaultCreatedTime_ByDefault()
	{
		// Arrange & Act
		var info = new ScheduleInfo();

		// Assert
		info.CreatedTime.ShouldBe(default(DateTimeOffset));
	}

	[Fact]
	public void HaveScheduledStatus_ByDefault()
	{
		// Arrange & Act
		var info = new ScheduleInfo();

		// Assert
		info.Status.ShouldBe(ScheduleStatus.Scheduled);
	}

	[Fact]
	public void HaveNullLastError_ByDefault()
	{
		// Arrange & Act
		var info = new ScheduleInfo();

		// Assert
		info.LastError.ShouldBeNull();
	}

	[Fact]
	public void HaveZeroDeliveryAttempts_ByDefault()
	{
		// Arrange & Act
		var info = new ScheduleInfo();

		// Assert
		info.DeliveryAttempts.ShouldBe(0);
	}

	[Fact]
	public void AllowSettingScheduleId()
	{
		// Arrange
		var info = new ScheduleInfo();

		// Act
		info.ScheduleId = "schedule-123";

		// Assert
		info.ScheduleId.ShouldBe("schedule-123");
	}

	[Fact]
	public void AllowSettingScheduledTime()
	{
		// Arrange
		var info = new ScheduleInfo();
		var scheduledTime = DateTimeOffset.UtcNow.AddHours(1);

		// Act
		info.ScheduledTime = scheduledTime;

		// Assert
		info.ScheduledTime.ShouldBe(scheduledTime);
	}

	[Fact]
	public void AllowSettingCreatedTime()
	{
		// Arrange
		var info = new ScheduleInfo();
		var createdTime = DateTimeOffset.UtcNow;

		// Act
		info.CreatedTime = createdTime;

		// Assert
		info.CreatedTime.ShouldBe(createdTime);
	}

	[Fact]
	public void AllowSettingStatus()
	{
		// Arrange
		var info = new ScheduleInfo();

		// Act
		info.Status = ScheduleStatus.Completed;

		// Assert
		info.Status.ShouldBe(ScheduleStatus.Completed);
	}

	[Theory]
	[InlineData(ScheduleStatus.Scheduled)]
	[InlineData(ScheduleStatus.InProgress)]
	[InlineData(ScheduleStatus.Completed)]
	[InlineData(ScheduleStatus.Failed)]
	[InlineData(ScheduleStatus.Cancelled)]
	public void AllowSettingAnyStatus(ScheduleStatus status)
	{
		// Arrange
		var info = new ScheduleInfo();

		// Act
		info.Status = status;

		// Assert
		info.Status.ShouldBe(status);
	}

	[Fact]
	public void AllowSettingLastError()
	{
		// Arrange
		var info = new ScheduleInfo();

		// Act
		info.LastError = "Connection timeout";

		// Assert
		info.LastError.ShouldBe("Connection timeout");
	}

	[Fact]
	public void AllowSettingDeliveryAttempts()
	{
		// Arrange
		var info = new ScheduleInfo();

		// Act
		info.DeliveryAttempts = 3;

		// Assert
		info.DeliveryAttempts.ShouldBe(3);
	}

	[Fact]
	public void AllowCreatingFailedScheduleInfo()
	{
		// Arrange
		var createdTime = DateTimeOffset.UtcNow.AddMinutes(-30);
		var scheduledTime = DateTimeOffset.UtcNow.AddMinutes(-10);

		// Act
		var info = new ScheduleInfo
		{
			ScheduleId = "schedule-failed-123",
			ScheduledTime = scheduledTime,
			CreatedTime = createdTime,
			Status = ScheduleStatus.Failed,
			LastError = "Message too large",
			DeliveryAttempts = 5,
		};

		// Assert
		info.ScheduleId.ShouldBe("schedule-failed-123");
		info.ScheduledTime.ShouldBe(scheduledTime);
		info.CreatedTime.ShouldBe(createdTime);
		info.Status.ShouldBe(ScheduleStatus.Failed);
		info.LastError.ShouldBe("Message too large");
		info.DeliveryAttempts.ShouldBe(5);
	}

	[Fact]
	public void AllowCreatingCompletedScheduleInfo()
	{
		// Arrange
		var createdTime = DateTimeOffset.UtcNow.AddMinutes(-30);
		var scheduledTime = DateTimeOffset.UtcNow.AddMinutes(-10);

		// Act
		var info = new ScheduleInfo
		{
			ScheduleId = "schedule-success-456",
			ScheduledTime = scheduledTime,
			CreatedTime = createdTime,
			Status = ScheduleStatus.Completed,
			DeliveryAttempts = 1,
		};

		// Assert
		info.ScheduleId.ShouldBe("schedule-success-456");
		info.Status.ShouldBe(ScheduleStatus.Completed);
		info.LastError.ShouldBeNull();
		info.DeliveryAttempts.ShouldBe(1);
	}
}
