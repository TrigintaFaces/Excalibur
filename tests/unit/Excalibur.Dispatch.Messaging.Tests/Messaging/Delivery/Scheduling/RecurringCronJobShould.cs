// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Delivery;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery.Scheduling;

/// <summary>
/// Unit tests for <see cref="RecurringCronJob"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class RecurringCronJobShould
{
	[Fact]
	public void HaveGeneratedIdByDefault()
	{
		// Arrange & Act
		var job = new RecurringCronJob();

		// Assert
		job.Id.ShouldNotBeNullOrEmpty();
		Guid.TryParse(job.Id, out _).ShouldBeTrue();
	}

	[Fact]
	public void HaveEmptyNameByDefault()
	{
		// Arrange & Act
		var job = new RecurringCronJob();

		// Assert
		job.Name.ShouldBe(string.Empty);
	}

	[Fact]
	public void HaveNullDescriptionByDefault()
	{
		// Arrange & Act
		var job = new RecurringCronJob();

		// Assert
		job.Description.ShouldBeNull();
	}

	[Fact]
	public void HaveUtcTimeZoneByDefault()
	{
		// Arrange & Act
		var job = new RecurringCronJob();

		// Assert
		job.TimeZoneId.ShouldBe(TimeZoneInfo.Utc.Id);
	}

	[Fact]
	public void HaveEmptyMetadataByDefault()
	{
		// Arrange & Act
		var job = new RecurringCronJob();

		// Assert
		job.Metadata.ShouldNotBeNull();
		job.Metadata.ShouldBeEmpty();
	}

	[Fact]
	public void BeEnabledByDefault()
	{
		// Arrange & Act
		var job = new RecurringCronJob();

		// Assert
		job.IsEnabled.ShouldBeTrue();
	}

	[Fact]
	public void HaveRecentCreatedUtcByDefault()
	{
		// Arrange & Act
		var beforeCreate = DateTimeOffset.UtcNow;
		var job = new RecurringCronJob();
		var afterCreate = DateTimeOffset.UtcNow;

		// Assert
		job.CreatedUtc.ShouldBeGreaterThanOrEqualTo(beforeCreate);
		job.CreatedUtc.ShouldBeLessThanOrEqualTo(afterCreate);
	}

	[Fact]
	public void HaveNullLastModifiedUtcByDefault()
	{
		// Arrange & Act
		var job = new RecurringCronJob();

		// Assert
		job.LastModifiedUtc.ShouldBeNull();
	}

	[Fact]
	public void HaveNullLastRunUtcByDefault()
	{
		// Arrange & Act
		var job = new RecurringCronJob();

		// Assert
		job.LastRunUtc.ShouldBeNull();
	}

	[Fact]
	public void HaveNullNextRunUtcByDefault()
	{
		// Arrange & Act
		var job = new RecurringCronJob();

		// Assert
		job.NextRunUtc.ShouldBeNull();
	}

	[Fact]
	public void HaveZeroRunCountByDefault()
	{
		// Arrange & Act
		var job = new RecurringCronJob();

		// Assert
		job.RunCount.ShouldBe(0);
	}

	[Fact]
	public void HaveZeroFailureCountByDefault()
	{
		// Arrange & Act
		var job = new RecurringCronJob();

		// Assert
		job.FailureCount.ShouldBe(0);
	}

	[Fact]
	public void HaveNullLastErrorByDefault()
	{
		// Arrange & Act
		var job = new RecurringCronJob();

		// Assert
		job.LastError.ShouldBeNull();
	}

	[Fact]
	public void HaveEmptyTagsByDefault()
	{
		// Arrange & Act
		var job = new RecurringCronJob();

		// Assert
		job.Tags.ShouldNotBeNull();
		job.Tags.ShouldBeEmpty();
	}

	[Fact]
	public void HaveZeroPriorityByDefault()
	{
		// Arrange & Act
		var job = new RecurringCronJob();

		// Assert
		job.Priority.ShouldBe(0);
	}

	[Fact]
	public void HaveNullMaxRuntimeByDefault()
	{
		// Arrange & Act
		var job = new RecurringCronJob();

		// Assert
		job.MaxRuntime.ShouldBeNull();
	}

	[Fact]
	public void HaveRetryOnFailureEnabledByDefault()
	{
		// Arrange & Act
		var job = new RecurringCronJob();

		// Assert
		job.RetryOnFailure.ShouldBeTrue();
	}

	[Fact]
	public void HaveThreeMaxRetryAttemptsByDefault()
	{
		// Arrange & Act
		var job = new RecurringCronJob();

		// Assert
		job.MaxRetryAttempts.ShouldBe(3);
	}

	[Fact]
	public void HaveNullStartDateByDefault()
	{
		// Arrange & Act
		var job = new RecurringCronJob();

		// Assert
		job.StartDate.ShouldBeNull();
	}

	[Fact]
	public void HaveNullEndDateByDefault()
	{
		// Arrange & Act
		var job = new RecurringCronJob();

		// Assert
		job.EndDate.ShouldBeNull();
	}

	// ShouldRunAt tests
	[Fact]
	public void ReturnTrueForEnabledJobWithoutDateConstraints()
	{
		// Arrange
		var job = new RecurringCronJob { IsEnabled = true };

		// Act
		var result = job.ShouldRunAt(DateTimeOffset.UtcNow);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ReturnFalseForDisabledJob()
	{
		// Arrange
		var job = new RecurringCronJob { IsEnabled = false };

		// Act
		var result = job.ShouldRunAt(DateTimeOffset.UtcNow);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ReturnFalseWhenBeforeStartDate()
	{
		// Arrange
		var job = new RecurringCronJob
		{
			IsEnabled = true,
			StartDate = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
		};
		var checkTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

		// Act
		var result = job.ShouldRunAt(checkTime);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ReturnTrueWhenAtStartDate()
	{
		// Arrange
		var startDate = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
		var job = new RecurringCronJob
		{
			IsEnabled = true,
			StartDate = startDate,
		};

		// Act
		var result = job.ShouldRunAt(startDate);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ReturnTrueWhenAfterStartDate()
	{
		// Arrange
		var job = new RecurringCronJob
		{
			IsEnabled = true,
			StartDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
		};
		var checkTime = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

		// Act
		var result = job.ShouldRunAt(checkTime);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ReturnFalseWhenAfterEndDate()
	{
		// Arrange
		var job = new RecurringCronJob
		{
			IsEnabled = true,
			EndDate = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
		};
		var checkTime = new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero);

		// Act
		var result = job.ShouldRunAt(checkTime);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ReturnTrueWhenAtEndDate()
	{
		// Arrange
		var endDate = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
		var job = new RecurringCronJob
		{
			IsEnabled = true,
			EndDate = endDate,
		};

		// Act
		var result = job.ShouldRunAt(endDate);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ReturnTrueWhenBeforeEndDate()
	{
		// Arrange
		var job = new RecurringCronJob
		{
			IsEnabled = true,
			EndDate = new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero),
		};
		var checkTime = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

		// Act
		var result = job.ShouldRunAt(checkTime);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ReturnTrueWhenWithinDateRange()
	{
		// Arrange
		var job = new RecurringCronJob
		{
			IsEnabled = true,
			StartDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
			EndDate = new DateTimeOffset(2026, 12, 31, 23, 59, 59, TimeSpan.Zero),
		};
		var checkTime = new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

		// Act
		var result = job.ShouldRunAt(checkTime);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ReturnFalseWhenDisabledEvenWithinDateRange()
	{
		// Arrange
		var job = new RecurringCronJob
		{
			IsEnabled = false,
			StartDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
			EndDate = new DateTimeOffset(2026, 12, 31, 23, 59, 59, TimeSpan.Zero),
		};
		var checkTime = new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

		// Act
		var result = job.ShouldRunAt(checkTime);

		// Assert
		result.ShouldBeFalse();
	}

	// UpdateRunStatistics tests
	[Fact]
	public void IncrementRunCountOnSuccessfulRun()
	{
		// Arrange
		var job = new RecurringCronJob { RunCount = 5 };

		// Act
		job.UpdateRunStatistics(success: true);

		// Assert
		job.RunCount.ShouldBe(6);
	}

	[Fact]
	public void IncrementRunCountOnFailedRun()
	{
		// Arrange
		var job = new RecurringCronJob { RunCount = 5 };

		// Act
		job.UpdateRunStatistics(success: false);

		// Assert
		job.RunCount.ShouldBe(6);
	}

	[Fact]
	public void UpdateLastRunUtcOnRun()
	{
		// Arrange
		var job = new RecurringCronJob();
		var beforeUpdate = DateTimeOffset.UtcNow;

		// Act
		job.UpdateRunStatistics(success: true);
		var afterUpdate = DateTimeOffset.UtcNow;

		// Assert
		job.LastRunUtc.ShouldNotBeNull();
		job.LastRunUtc.Value.ShouldBeGreaterThanOrEqualTo(beforeUpdate);
		job.LastRunUtc.Value.ShouldBeLessThanOrEqualTo(afterUpdate);
	}

	[Fact]
	public void ClearLastErrorOnSuccessfulRun()
	{
		// Arrange
		var job = new RecurringCronJob { LastError = "Previous error" };

		// Act
		job.UpdateRunStatistics(success: true);

		// Assert
		job.LastError.ShouldBeNull();
	}

	[Fact]
	public void NotIncrementFailureCountOnSuccessfulRun()
	{
		// Arrange
		var job = new RecurringCronJob { FailureCount = 2 };

		// Act
		job.UpdateRunStatistics(success: true);

		// Assert
		job.FailureCount.ShouldBe(2);
	}

	[Fact]
	public void IncrementFailureCountOnFailedRun()
	{
		// Arrange
		var job = new RecurringCronJob { FailureCount = 2 };

		// Act
		job.UpdateRunStatistics(success: false);

		// Assert
		job.FailureCount.ShouldBe(3);
	}

	[Fact]
	public void SetLastErrorOnFailedRun()
	{
		// Arrange
		var job = new RecurringCronJob();

		// Act
		job.UpdateRunStatistics(success: false, error: "Connection timeout");

		// Assert
		job.LastError.ShouldBe("Connection timeout");
	}

	[Fact]
	public void OverwriteLastErrorOnSubsequentFailure()
	{
		// Arrange
		var job = new RecurringCronJob { LastError = "Old error" };

		// Act
		job.UpdateRunStatistics(success: false, error: "New error");

		// Assert
		job.LastError.ShouldBe("New error");
	}

	[Fact]
	public void AcceptNullErrorOnFailedRun()
	{
		// Arrange
		var job = new RecurringCronJob { LastError = "Old error" };

		// Act
		job.UpdateRunStatistics(success: false, error: null);

		// Assert
		job.LastError.ShouldBeNull();
	}

	// Property setter tests
	[Fact]
	public void AllowSettingId()
	{
		// Arrange
		var job = new RecurringCronJob();

		// Act
		job.Id = "custom-job-id";

		// Assert
		job.Id.ShouldBe("custom-job-id");
	}

	[Fact]
	public void AllowSettingName()
	{
		// Arrange
		var job = new RecurringCronJob();

		// Act
		job.Name = "Daily Backup";

		// Assert
		job.Name.ShouldBe("Daily Backup");
	}

	[Fact]
	public void AllowSettingCronExpression()
	{
		// Arrange
		var job = new RecurringCronJob();

		// Act
		job.CronExpression = "0 0 * * *";

		// Assert
		job.CronExpression.ShouldBe("0 0 * * *");
	}

	[Fact]
	public void AllowSettingTimeZoneId()
	{
		// Arrange
		var job = new RecurringCronJob();

		// Act
		job.TimeZoneId = "America/New_York";

		// Assert
		job.TimeZoneId.ShouldBe("America/New_York");
	}

	[Fact]
	public void AllowSettingMessageTypeName()
	{
		// Arrange
		var job = new RecurringCronJob();

		// Act
		job.MessageTypeName = "MyApp.Commands.BackupCommand";

		// Assert
		job.MessageTypeName.ShouldBe("MyApp.Commands.BackupCommand");
	}

	[Fact]
	public void AllowSettingMessagePayload()
	{
		// Arrange
		var job = new RecurringCronJob();

		// Act
		job.MessagePayload = "{\"target\":\"database\"}";

		// Assert
		job.MessagePayload.ShouldBe("{\"target\":\"database\"}");
	}

	[Fact]
	public void AllowAddingMetadata()
	{
		// Arrange
		var job = new RecurringCronJob();

		// Act
		job.Metadata["env"] = "production";
		job.Metadata["region"] = "us-east-1";

		// Assert
		job.Metadata.Count.ShouldBe(2);
		job.Metadata["env"].ShouldBe("production");
		job.Metadata["region"].ShouldBe("us-east-1");
	}

	[Fact]
	public void AllowAddingTags()
	{
		// Arrange
		var job = new RecurringCronJob();

		// Act
		job.Tags.Add("critical");
		job.Tags.Add("backup");

		// Assert
		job.Tags.Count.ShouldBe(2);
		job.Tags.ShouldContain("critical");
		job.Tags.ShouldContain("backup");
	}

	[Fact]
	public void AllowSettingPriority()
	{
		// Arrange
		var job = new RecurringCronJob();

		// Act
		job.Priority = 10;

		// Assert
		job.Priority.ShouldBe(10);
	}

	[Fact]
	public void AllowSettingMaxRuntime()
	{
		// Arrange
		var job = new RecurringCronJob();

		// Act
		job.MaxRuntime = TimeSpan.FromMinutes(30);

		// Assert
		job.MaxRuntime.ShouldBe(TimeSpan.FromMinutes(30));
	}

	[Fact]
	public void AllowSettingMaxRetryAttempts()
	{
		// Arrange
		var job = new RecurringCronJob();

		// Act
		job.MaxRetryAttempts = 5;

		// Assert
		job.MaxRetryAttempts.ShouldBe(5);
	}

	[Fact]
	public void SupportObjectInitializer()
	{
		// Arrange & Act
		var job = new RecurringCronJob
		{
			Id = "backup-job",
			Name = "Daily Backup",
			Description = "Backs up the database daily",
			CronExpression = "0 2 * * *",
			TimeZoneId = "Europe/London",
			MessageTypeName = "BackupCommand",
			MessagePayload = "{}",
			IsEnabled = true,
			Priority = 5,
			MaxRetryAttempts = 5,
			RetryOnFailure = true,
		};

		// Assert
		job.Id.ShouldBe("backup-job");
		job.Name.ShouldBe("Daily Backup");
		job.Description.ShouldBe("Backs up the database daily");
		job.CronExpression.ShouldBe("0 2 * * *");
		job.TimeZoneId.ShouldBe("Europe/London");
		job.MessageTypeName.ShouldBe("BackupCommand");
		job.MessagePayload.ShouldBe("{}");
		job.IsEnabled.ShouldBeTrue();
		job.Priority.ShouldBe(5);
		job.MaxRetryAttempts.ShouldBe(5);
		job.RetryOnFailure.ShouldBeTrue();
	}

	[Fact]
	public void TrackMultipleRunsCorrectly()
	{
		// Arrange
		var job = new RecurringCronJob();

		// Act - Simulate multiple runs
		job.UpdateRunStatistics(success: true);
		job.UpdateRunStatistics(success: true);
		job.UpdateRunStatistics(success: false, error: "Error 1");
		job.UpdateRunStatistics(success: true);
		job.UpdateRunStatistics(success: false, error: "Error 2");

		// Assert
		job.RunCount.ShouldBe(5);
		job.FailureCount.ShouldBe(2);
		job.LastError.ShouldBe("Error 2"); // Last error persists
	}

	[Fact]
	public void GenerateUniqueIdsForDifferentInstances()
	{
		// Arrange & Act
		var job1 = new RecurringCronJob();
		var job2 = new RecurringCronJob();

		// Assert
		job1.Id.ShouldNotBe(job2.Id);
	}
}
