using Excalibur.Dispatch.Compliance;

namespace Excalibur.Dispatch.Compliance.Tests.Audit;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class MultiRegionAuditEventsShould
{
	[Fact]
	public void Create_failover_initiated_event_automatic()
	{
		var evt = MultiRegionAuditEvents.FailoverInitiated(
			"us-east-1", "eu-west-1", "Primary unhealthy", true, "System", "corr-1");

		evt.EventType.ShouldBe(AuditEventType.Security);
		evt.Action.ShouldBe("FailoverInitiated");
		evt.Outcome.ShouldBe(AuditOutcome.Success);
		evt.ActorType.ShouldBe("System");
		evt.ResourceId.ShouldBe("us-east-1->eu-west-1");
		evt.Metadata!["isAutomatic"].ShouldBe("True");
	}

	[Fact]
	public void Create_failover_initiated_event_manual()
	{
		var evt = MultiRegionAuditEvents.FailoverInitiated(
			"us-east-1", "eu-west-1", "Planned maintenance", false, "admin-1");

		evt.ActorType.ShouldBe("User");
		evt.ActorId.ShouldBe("admin-1");
	}

	[Fact]
	public void Create_failback_completed_event()
	{
		var evt = MultiRegionAuditEvents.FailbackCompleted(
			"eu-west-1", "us-east-1", "Primary recovered", "admin-1", 42, "corr-2");

		evt.Action.ShouldBe("FailbackCompleted");
		evt.Outcome.ShouldBe(AuditOutcome.Success);
		evt.Metadata!["keysSynchronized"].ShouldBe("42");
	}

	[Fact]
	public void Create_rpo_threshold_breached_event()
	{
		var evt = MultiRegionAuditEvents.RpoThresholdBreached(
			TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(5), "us-east-1", 10);

		evt.EventType.ShouldBe(AuditEventType.Compliance);
		evt.Action.ShouldBe("RpoThresholdBreached");
		evt.Outcome.ShouldBe(AuditOutcome.Pending);
		evt.Reason.ShouldContain("exceeded RPO target");
		evt.Metadata!["pendingKeys"].ShouldBe("10");
	}

	[Fact]
	public void Create_replication_sync_completed_event()
	{
		var evt = MultiRegionAuditEvents.ReplicationSyncCompleted(
			"us-east-1", "eu-west-1", 100, TimeSpan.FromSeconds(5));

		evt.Action.ShouldBe("ReplicationSyncCompleted");
		evt.Outcome.ShouldBe(AuditOutcome.Success);
		evt.Metadata!["keyCount"].ShouldBe("100");
	}

	[Fact]
	public void Create_region_health_changed_event_unhealthy()
	{
		var evt = MultiRegionAuditEvents.RegionHealthChanged(
			"us-east-1", true, false, 3, "Connection timeout");

		evt.Action.ShouldBe("RegionHealthChanged");
		evt.Outcome.ShouldBe(AuditOutcome.Error);
		evt.Reason.ShouldBe("Healthy->Unhealthy");
		evt.Metadata!["consecutiveFailures"].ShouldBe("3");
		evt.Metadata["errorMessage"].ShouldBe("Connection timeout");
	}

	[Fact]
	public void Create_region_health_changed_event_recovered()
	{
		var evt = MultiRegionAuditEvents.RegionHealthChanged(
			"us-east-1", false, true, 0);

		evt.Outcome.ShouldBe(AuditOutcome.Success);
		evt.Reason.ShouldBe("Unhealthy->Healthy");
		evt.Metadata!["errorMessage"].ShouldBe(string.Empty);
	}
}
