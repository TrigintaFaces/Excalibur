using Excalibur.Dispatch.Compliance;

namespace Excalibur.Dispatch.Compliance.Tests.Audit;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class MigrationProgressAuditEventsShould
{
	[Fact]
	public void Create_plaintext_data_detected_event_with_high_severity()
	{
		var evt = MigrationProgressAuditEvents.PlaintextDataDetected(
			"Customer", 50, PlaintextSeverity.High, "scan", "corr-1");

		evt.EventType.ShouldBe(AuditEventType.Compliance);
		evt.Action.ShouldBe("PlaintextDataDetected");
		evt.Outcome.ShouldBe(AuditOutcome.Error);
		evt.ActorId.ShouldBe("System");
		evt.ResourceType.ShouldBe("Customer");
		evt.CorrelationId.ShouldBe("corr-1");
		evt.Metadata!["count"].ShouldBe("50");
		evt.Metadata["severity"].ShouldBe("High");
		evt.Metadata["detectionMethod"].ShouldBe("scan");
	}

	[Fact]
	public void Create_plaintext_data_detected_event_with_low_severity()
	{
		var evt = MigrationProgressAuditEvents.PlaintextDataDetected(
			"Config", 5, PlaintextSeverity.Low, "audit");

		evt.Outcome.ShouldBe(AuditOutcome.Pending);
		evt.CorrelationId.ShouldBeNull();
	}

	[Fact]
	public void Create_data_migration_queued_event()
	{
		var evt = MigrationProgressAuditEvents.DataMigrationQueued(
			"batch-1", "Order", 1000, 5242880, "provA", "provB", "corr-2");

		evt.EventType.ShouldBe(AuditEventType.Security);
		evt.Action.ShouldBe("DataMigrationQueued");
		evt.Outcome.ShouldBe(AuditOutcome.Pending);
		evt.ResourceId.ShouldBe("batch-1");
		evt.Metadata!["recordCount"].ShouldBe("1000");
		evt.Metadata["sourceProvider"].ShouldBe("provA");
		evt.Metadata["targetProvider"].ShouldBe("provB");
	}

	[Fact]
	public void Create_data_migration_completed_event_with_no_failures()
	{
		var evt = MigrationProgressAuditEvents.DataMigrationCompleted(
			"batch-1", "Order", 100, 100, 0, 0, TimeSpan.FromSeconds(30));

		evt.Outcome.ShouldBe(AuditOutcome.Success);
		evt.Metadata!["successRate"].ShouldContain("%");
	}

	[Fact]
	public void Create_data_migration_completed_event_with_partial_failures()
	{
		var evt = MigrationProgressAuditEvents.DataMigrationCompleted(
			"batch-1", "Order", 100, 90, 10, 0, TimeSpan.FromSeconds(60));

		evt.Outcome.ShouldBe(AuditOutcome.Error);
		evt.Metadata!["failedCount"].ShouldBe("10");
	}

	[Fact]
	public void Create_data_migration_completed_event_with_all_failures()
	{
		var evt = MigrationProgressAuditEvents.DataMigrationCompleted(
			"batch-1", "Order", 100, 0, 100, 0, TimeSpan.FromSeconds(10));

		evt.Outcome.ShouldBe(AuditOutcome.Failure);
	}

	[Fact]
	public void Create_migration_batch_progress_event()
	{
		var evt = MigrationProgressAuditEvents.MigrationBatchProgress(
			"batch-1", 3, 10, 300, 1000, TimeSpan.FromMinutes(5),
			TimeSpan.FromMinutes(12), "corr-3");

		evt.EventType.ShouldBe(AuditEventType.System);
		evt.Action.ShouldBe("MigrationBatchProgress");
		evt.Metadata!["currentBatch"].ShouldBe("3");
		evt.Metadata["totalBatches"].ShouldBe("10");
		evt.Metadata["progressPercentage"].ShouldBe("30.00");
	}
}

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class PlaintextSeverityShould
{
	[Theory]
	[InlineData(PlaintextSeverity.Low, 0)]
	[InlineData(PlaintextSeverity.Medium, 1)]
	[InlineData(PlaintextSeverity.High, 2)]
	[InlineData(PlaintextSeverity.Critical, 3)]
	public void Have_expected_integer_values(PlaintextSeverity severity, int expectedValue)
	{
		((int)severity).ShouldBe(expectedValue);
	}

	[Fact]
	public void Have_exactly_four_values()
	{
		Enum.GetValues<PlaintextSeverity>().Length.ShouldBe(4);
	}
}
