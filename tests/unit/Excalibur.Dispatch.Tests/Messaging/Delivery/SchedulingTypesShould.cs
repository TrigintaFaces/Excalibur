using Excalibur.Dispatch.Delivery;
using SchedulingTypes = Excalibur.Dispatch.Delivery;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
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
		history.Error!.ShouldBeNull();
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

}
