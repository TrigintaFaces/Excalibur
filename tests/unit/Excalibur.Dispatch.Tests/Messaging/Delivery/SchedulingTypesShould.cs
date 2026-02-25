using Excalibur.Dispatch.Delivery;
using SchedulingTypes = Excalibur.Dispatch.Delivery;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SchedulingTypesShould
{
	// --- ScheduledMessage ---

	[Fact]
	public void ScheduledMessage_HaveDefaults()
	{
		var msg = new ScheduledMessage();

		msg.Id.ShouldNotBe(Guid.Empty);
		msg.CronExpression.ShouldBeNull();
		msg.TimeZoneId.ShouldBeNull();
		msg.Interval.ShouldBeNull();
		msg.MessageName.ShouldBeNull();
		msg.MessageBody.ShouldBeNull();
		msg.CorrelationId.ShouldBeNull();
		msg.TraceParent.ShouldBeNull();
		msg.TenantId.ShouldBeNull();
		msg.UserId.ShouldBeNull();
		msg.NextExecutionUtc.ShouldBeNull();
		msg.LastExecutionUtc.ShouldBeNull();
		msg.Enabled.ShouldBeTrue();
		msg.MissedExecutionBehavior.ShouldBeNull();
	}

	[Fact]
	public void ScheduledMessage_SetAllProperties()
	{
		var id = Guid.NewGuid();
		var next = DateTimeOffset.UtcNow.AddHours(1);
		var last = DateTimeOffset.UtcNow.AddHours(-1);
		var msg = new ScheduledMessage
		{
			Id = id,
			CronExpression = "0 * * * *",
			TimeZoneId = "UTC",
			Interval = TimeSpan.FromMinutes(30),
			MessageName = "TestMessage",
			MessageBody = "{\"key\":1}",
			CorrelationId = "corr-1",
			TraceParent = "trace-1",
			TenantId = "tenant-1",
			UserId = "user-1",
			NextExecutionUtc = next,
			LastExecutionUtc = last,
			Enabled = false,
			MissedExecutionBehavior = SchedulingTypes.MissedExecutionBehavior.SkipMissed,
		};

		msg.Id.ShouldBe(id);
		msg.CronExpression.ShouldBe("0 * * * *");
		msg.TimeZoneId.ShouldBe("UTC");
		msg.Interval.ShouldBe(TimeSpan.FromMinutes(30));
		msg.MessageName.ShouldBe("TestMessage");
		msg.MessageBody.ShouldBe("{\"key\":1}");
		msg.CorrelationId.ShouldBe("corr-1");
		msg.TraceParent.ShouldBe("trace-1");
		msg.TenantId.ShouldBe("tenant-1");
		msg.UserId.ShouldBe("user-1");
		msg.NextExecutionUtc.ShouldBe(next);
		msg.LastExecutionUtc.ShouldBe(last);
		msg.Enabled.ShouldBeFalse();
		msg.MissedExecutionBehavior.ShouldBe(SchedulingTypes.MissedExecutionBehavior.SkipMissed);
	}

	// --- JobExecutionHistory ---

	[Fact]
	public void JobExecutionHistory_HaveDefaults()
	{
		var history = new JobExecutionHistory();

		history.JobId.ShouldBeNull();
		history.StartedUtc.ShouldBe(default);
		history.CompletedUtc.ShouldBeNull();
		history.Success.ShouldBeFalse();
		history.Error.ShouldBeNull();
		history.Duration.ShouldBeNull();
	}

	[Fact]
	public void JobExecutionHistory_CalculateDuration()
	{
		var start = DateTimeOffset.UtcNow;
		var end = start.AddSeconds(30);
		var history = new JobExecutionHistory
		{
			JobId = "job-1",
			StartedUtc = start,
			CompletedUtc = end,
			Success = true,
		};

		history.Duration.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void JobExecutionHistory_DurationNullWhenNotCompleted()
	{
		var history = new JobExecutionHistory
		{
			JobId = "job-1",
			StartedUtc = DateTimeOffset.UtcNow,
		};

		history.Duration.ShouldBeNull();
	}

	// --- RecurringCronJob ---

	[Fact]
	public void RecurringCronJob_HaveDefaults()
	{
		var job = new RecurringCronJob();

		job.Id.ShouldNotBeNullOrWhiteSpace();
		job.Name.ShouldBe(string.Empty);
		job.Description.ShouldBeNull();
		job.CronExpression.ShouldBeNull();
		job.TimeZoneId.ShouldBe(TimeZoneInfo.Utc.Id);
		job.MessageTypeName.ShouldBeNull();
		job.MessagePayload.ShouldBeNull();
		job.Metadata.ShouldNotBeNull();
		job.Metadata.ShouldBeEmpty();
		job.IsEnabled.ShouldBeTrue();
		job.CreatedUtc.ShouldNotBe(default);
		job.LastModifiedUtc.ShouldBeNull();
		job.LastRunUtc.ShouldBeNull();
		job.NextRunUtc.ShouldBeNull();
		job.RunCount.ShouldBe(0);
		job.FailureCount.ShouldBe(0);
		job.LastError.ShouldBeNull();
		job.Tags.ShouldNotBeNull();
		job.Tags.ShouldBeEmpty();
		job.Priority.ShouldBe(0);
		job.MaxRuntime.ShouldBeNull();
		job.RetryOnFailure.ShouldBeTrue();
		job.MaxRetryAttempts.ShouldBe(3);
		job.StartDate.ShouldBeNull();
		job.EndDate.ShouldBeNull();
	}

	[Fact]
	public void RecurringCronJob_ShouldRunAt_ReturnsFalseWhenDisabled()
	{
		var job = new RecurringCronJob { IsEnabled = false };

		job.ShouldRunAt(DateTimeOffset.UtcNow).ShouldBeFalse();
	}

	[Fact]
	public void RecurringCronJob_ShouldRunAt_ReturnsFalseBeforeStartDate()
	{
		var job = new RecurringCronJob { StartDate = DateTimeOffset.UtcNow.AddDays(1) };

		job.ShouldRunAt(DateTimeOffset.UtcNow).ShouldBeFalse();
	}

	[Fact]
	public void RecurringCronJob_ShouldRunAt_ReturnsFalseAfterEndDate()
	{
		var job = new RecurringCronJob { EndDate = DateTimeOffset.UtcNow.AddDays(-1) };

		job.ShouldRunAt(DateTimeOffset.UtcNow).ShouldBeFalse();
	}

	[Fact]
	public void RecurringCronJob_ShouldRunAt_ReturnsTrueWhenInRange()
	{
		var now = DateTimeOffset.UtcNow;
		var job = new RecurringCronJob
		{
			StartDate = now.AddDays(-1),
			EndDate = now.AddDays(1),
		};

		job.ShouldRunAt(now).ShouldBeTrue();
	}

	[Fact]
	public void RecurringCronJob_UpdateRunStatistics_Success()
	{
		var job = new RecurringCronJob();

		job.UpdateRunStatistics(true);

		job.RunCount.ShouldBe(1);
		job.FailureCount.ShouldBe(0);
		job.LastRunUtc.ShouldNotBeNull();
		job.LastError.ShouldBeNull();
	}

	[Fact]
	public void RecurringCronJob_UpdateRunStatistics_Failure()
	{
		var job = new RecurringCronJob();

		job.UpdateRunStatistics(false, "Connection refused");

		job.RunCount.ShouldBe(1);
		job.FailureCount.ShouldBe(1);
		job.LastError.ShouldBe("Connection refused");
	}

	[Fact]
	public void RecurringCronJob_UpdateRunStatistics_MultipleCalls()
	{
		var job = new RecurringCronJob();

		job.UpdateRunStatistics(true);
		job.UpdateRunStatistics(true);
		job.UpdateRunStatistics(false, "timeout");

		job.RunCount.ShouldBe(3);
		job.FailureCount.ShouldBe(1);
		job.LastError.ShouldBe("timeout");
	}

	[Fact]
	public void RecurringCronJob_UpdateRunStatistics_SuccessClearsError()
	{
		var job = new RecurringCronJob();
		job.UpdateRunStatistics(false, "error");

		job.UpdateRunStatistics(true);

		job.LastError.ShouldBeNull();
	}
}
