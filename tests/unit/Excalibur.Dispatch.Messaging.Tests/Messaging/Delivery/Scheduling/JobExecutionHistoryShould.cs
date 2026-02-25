// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Delivery;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery.Scheduling;

/// <summary>
/// Unit tests for <see cref="JobExecutionHistory"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class JobExecutionHistoryShould
{
	[Fact]
	public void HaveNullJobIdByDefault()
	{
		// Arrange & Act
		var history = new JobExecutionHistory();

		// Assert
		history.JobId.ShouldBeNull();
	}

	[Fact]
	public void HaveDefaultStartedUtc()
	{
		// Arrange & Act
		var history = new JobExecutionHistory();

		// Assert
		history.StartedUtc.ShouldBe(default);
	}

	[Fact]
	public void HaveNullCompletedUtcByDefault()
	{
		// Arrange & Act
		var history = new JobExecutionHistory();

		// Assert
		history.CompletedUtc.ShouldBeNull();
	}

	[Fact]
	public void HaveFalseSuccessByDefault()
	{
		// Arrange & Act
		var history = new JobExecutionHistory();

		// Assert
		history.Success.ShouldBeFalse();
	}

	[Fact]
	public void HaveNullErrorByDefault()
	{
		// Arrange & Act
		var history = new JobExecutionHistory();

		// Assert
		history.Error.ShouldBeNull();
	}

	[Fact]
	public void HaveNullDurationWhenNotCompleted()
	{
		// Arrange
		var history = new JobExecutionHistory
		{
			StartedUtc = DateTimeOffset.UtcNow,
			CompletedUtc = null,
		};

		// Act & Assert
		history.Duration.ShouldBeNull();
	}

	[Fact]
	public void CalculateDurationWhenCompleted()
	{
		// Arrange
		var startTime = new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);
		var endTime = new DateTimeOffset(2026, 1, 15, 10, 5, 30, TimeSpan.Zero);
		var history = new JobExecutionHistory
		{
			StartedUtc = startTime,
			CompletedUtc = endTime,
		};

		// Act
		var duration = history.Duration;

		// Assert
		duration.ShouldNotBeNull();
		duration.Value.TotalMinutes.ShouldBe(5.5);
	}

	[Fact]
	public void AllowSettingJobId()
	{
		// Arrange
		var history = new JobExecutionHistory();

		// Act
		history.JobId = "job-123";

		// Assert
		history.JobId.ShouldBe("job-123");
	}

	[Fact]
	public void AllowSettingStartedUtc()
	{
		// Arrange
		var history = new JobExecutionHistory();
		var startTime = new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);

		// Act
		history.StartedUtc = startTime;

		// Assert
		history.StartedUtc.ShouldBe(startTime);
	}

	[Fact]
	public void AllowSettingCompletedUtc()
	{
		// Arrange
		var history = new JobExecutionHistory();
		var endTime = new DateTimeOffset(2026, 1, 15, 10, 5, 0, TimeSpan.Zero);

		// Act
		history.CompletedUtc = endTime;

		// Assert
		history.CompletedUtc.ShouldBe(endTime);
	}

	[Fact]
	public void AllowSettingSuccess()
	{
		// Arrange
		var history = new JobExecutionHistory();

		// Act
		history.Success = true;

		// Assert
		history.Success.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingError()
	{
		// Arrange
		var history = new JobExecutionHistory();

		// Act
		history.Error = "Connection failed";

		// Assert
		history.Error.ShouldBe("Connection failed");
	}

	[Fact]
	public void SupportObjectInitializer()
	{
		// Arrange & Act
		var startTime = new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);
		var endTime = new DateTimeOffset(2026, 1, 15, 10, 2, 30, TimeSpan.Zero);
		var history = new JobExecutionHistory
		{
			JobId = "daily-backup",
			StartedUtc = startTime,
			CompletedUtc = endTime,
			Success = true,
			Error = null,
		};

		// Assert
		history.JobId.ShouldBe("daily-backup");
		history.StartedUtc.ShouldBe(startTime);
		history.CompletedUtc.ShouldBe(endTime);
		history.Success.ShouldBeTrue();
		history.Error.ShouldBeNull();
		history.Duration!.Value.TotalMinutes.ShouldBe(2.5);
	}

	[Fact]
	public void TrackFailedExecution()
	{
		// Arrange & Act
		var startTime = new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);
		var endTime = new DateTimeOffset(2026, 1, 15, 10, 0, 5, TimeSpan.Zero);
		var history = new JobExecutionHistory
		{
			JobId = "email-processor",
			StartedUtc = startTime,
			CompletedUtc = endTime,
			Success = false,
			Error = "SMTP server unavailable",
		};

		// Assert
		history.Success.ShouldBeFalse();
		history.Error.ShouldBe("SMTP server unavailable");
		history.Duration!.Value.TotalSeconds.ShouldBe(5);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(60)]
	[InlineData(3600)]
	public void CalculateVariousDurations(int seconds)
	{
		// Arrange
		var startTime = new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);
		var endTime = startTime.AddSeconds(seconds);
		var history = new JobExecutionHistory
		{
			StartedUtc = startTime,
			CompletedUtc = endTime,
		};

		// Act
		var duration = history.Duration;

		// Assert
		duration.ShouldNotBeNull();
		duration.Value.TotalSeconds.ShouldBe(seconds);
	}

	[Fact]
	public void AllowNegativeDurationWhenEndBeforeStart()
	{
		// Arrange - Edge case where CompletedUtc is before StartedUtc
		var startTime = new DateTimeOffset(2026, 1, 15, 10, 5, 0, TimeSpan.Zero);
		var endTime = new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);
		var history = new JobExecutionHistory
		{
			StartedUtc = startTime,
			CompletedUtc = endTime,
		};

		// Act
		var duration = history.Duration;

		// Assert - Duration calculation still works (negative value)
		duration.ShouldNotBeNull();
		duration.Value.TotalMinutes.ShouldBe(-5);
	}

	[Fact]
	public void HandleZeroDuration()
	{
		// Arrange
		var time = new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);
		var history = new JobExecutionHistory
		{
			StartedUtc = time,
			CompletedUtc = time,
		};

		// Act
		var duration = history.Duration;

		// Assert
		duration.ShouldNotBeNull();
		duration.Value.ShouldBe(TimeSpan.Zero);
	}
}
