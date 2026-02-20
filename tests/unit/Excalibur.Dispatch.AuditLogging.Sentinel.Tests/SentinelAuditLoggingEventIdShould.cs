using Excalibur.Dispatch.AuditLogging.Sentinel;

namespace Excalibur.Dispatch.AuditLogging.Sentinel.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class SentinelAuditLoggingEventIdShould
{
	[Fact]
	public void Have_event_ids_in_assigned_range()
	{
		// Assert — Event IDs should be in range 93440-93459
		SentinelAuditLoggingEventId.EventForwarded.ShouldBeInRange(93440, 93459);
		SentinelAuditLoggingEventId.BatchForwarded.ShouldBeInRange(93440, 93459);
		SentinelAuditLoggingEventId.ForwardFailedStatus.ShouldBeInRange(93440, 93459);
		SentinelAuditLoggingEventId.ForwardRetried.ShouldBeInRange(93440, 93459);
		SentinelAuditLoggingEventId.HealthCheckFailed.ShouldBeInRange(93440, 93459);
		SentinelAuditLoggingEventId.ForwardFailedHttpError.ShouldBeInRange(93440, 93459);
		SentinelAuditLoggingEventId.ForwardFailedTimeout.ShouldBeInRange(93440, 93459);
		SentinelAuditLoggingEventId.ForwardFailedBatchChunk.ShouldBeInRange(93440, 93459);
	}

	[Fact]
	public void Have_unique_event_ids()
	{
		// Arrange
		var ids = new[]
		{
			SentinelAuditLoggingEventId.EventForwarded,
			SentinelAuditLoggingEventId.BatchForwarded,
			SentinelAuditLoggingEventId.ForwardFailedStatus,
			SentinelAuditLoggingEventId.ForwardRetried,
			SentinelAuditLoggingEventId.HealthCheckFailed,
			SentinelAuditLoggingEventId.ForwardFailedHttpError,
			SentinelAuditLoggingEventId.ForwardFailedTimeout,
			SentinelAuditLoggingEventId.ForwardFailedBatchChunk
		};

		// Assert
		ids.Distinct().Count().ShouldBe(ids.Length);
	}

	[Fact]
	public void Have_expected_specific_values()
	{
		// Assert
		SentinelAuditLoggingEventId.EventForwarded.ShouldBe(93440);
		SentinelAuditLoggingEventId.BatchForwarded.ShouldBe(93441);
		SentinelAuditLoggingEventId.ForwardFailedStatus.ShouldBe(93445);
		SentinelAuditLoggingEventId.ForwardRetried.ShouldBe(93446);
		SentinelAuditLoggingEventId.HealthCheckFailed.ShouldBe(93447);
		SentinelAuditLoggingEventId.ForwardFailedHttpError.ShouldBe(93448);
		SentinelAuditLoggingEventId.ForwardFailedTimeout.ShouldBe(93449);
		SentinelAuditLoggingEventId.ForwardFailedBatchChunk.ShouldBe(93450);
	}
}
