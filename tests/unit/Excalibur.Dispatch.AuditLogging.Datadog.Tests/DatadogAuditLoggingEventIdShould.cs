using Excalibur.Dispatch.AuditLogging.Datadog;

namespace Excalibur.Dispatch.AuditLogging.Datadog.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class DatadogAuditLoggingEventIdShould
{
	[Fact]
	public void Have_event_ids_in_assigned_range()
	{
		// Assert — Event IDs should be in range 93420-93439
		DatadogAuditLoggingEventId.EventForwarded.ShouldBeInRange(93420, 93439);
		DatadogAuditLoggingEventId.BatchForwarded.ShouldBeInRange(93420, 93439);
		DatadogAuditLoggingEventId.ForwardFailedStatus.ShouldBeInRange(93420, 93439);
		DatadogAuditLoggingEventId.ForwardRetried.ShouldBeInRange(93420, 93439);
		DatadogAuditLoggingEventId.HealthCheckFailed.ShouldBeInRange(93420, 93439);
		DatadogAuditLoggingEventId.ForwardFailedHttpError.ShouldBeInRange(93420, 93439);
		DatadogAuditLoggingEventId.ForwardFailedTimeout.ShouldBeInRange(93420, 93439);
		DatadogAuditLoggingEventId.ForwardFailedBatchChunk.ShouldBeInRange(93420, 93439);
	}

	[Fact]
	public void Have_unique_event_ids()
	{
		// Arrange
		var ids = new[]
		{
			DatadogAuditLoggingEventId.EventForwarded,
			DatadogAuditLoggingEventId.BatchForwarded,
			DatadogAuditLoggingEventId.ForwardFailedStatus,
			DatadogAuditLoggingEventId.ForwardRetried,
			DatadogAuditLoggingEventId.HealthCheckFailed,
			DatadogAuditLoggingEventId.ForwardFailedHttpError,
			DatadogAuditLoggingEventId.ForwardFailedTimeout,
			DatadogAuditLoggingEventId.ForwardFailedBatchChunk
		};

		// Assert
		ids.Distinct().Count().ShouldBe(ids.Length);
	}

	[Fact]
	public void Have_expected_specific_values()
	{
		// Assert
		DatadogAuditLoggingEventId.EventForwarded.ShouldBe(93420);
		DatadogAuditLoggingEventId.BatchForwarded.ShouldBe(93421);
		DatadogAuditLoggingEventId.ForwardFailedStatus.ShouldBe(93425);
		DatadogAuditLoggingEventId.ForwardRetried.ShouldBe(93426);
		DatadogAuditLoggingEventId.HealthCheckFailed.ShouldBe(93427);
		DatadogAuditLoggingEventId.ForwardFailedHttpError.ShouldBe(93428);
		DatadogAuditLoggingEventId.ForwardFailedTimeout.ShouldBe(93429);
		DatadogAuditLoggingEventId.ForwardFailedBatchChunk.ShouldBe(93430);
	}
}
