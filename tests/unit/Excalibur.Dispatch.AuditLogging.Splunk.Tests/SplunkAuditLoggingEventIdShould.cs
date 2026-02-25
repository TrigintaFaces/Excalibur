using Excalibur.Dispatch.AuditLogging.Splunk;

namespace Excalibur.Dispatch.AuditLogging.Splunk.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class SplunkAuditLoggingEventIdShould
{
	[Fact]
	public void Have_unique_event_ids()
	{
		var ids = new[]
		{
			SplunkAuditLoggingEventId.EventForwarded,
			SplunkAuditLoggingEventId.BatchForwarded,
			SplunkAuditLoggingEventId.ForwardFailedStatus,
			SplunkAuditLoggingEventId.ForwardRetried,
			SplunkAuditLoggingEventId.HealthCheckFailed,
			SplunkAuditLoggingEventId.ForwardFailedHttpError,
			SplunkAuditLoggingEventId.ForwardFailedTimeout,
			SplunkAuditLoggingEventId.ForwardFailedBatchChunk
		};

		ids.ShouldBeUnique();
	}

	[Fact]
	public void Be_in_assigned_range()
	{
		SplunkAuditLoggingEventId.EventForwarded.ShouldBeInRange(93400, 93499);
		SplunkAuditLoggingEventId.BatchForwarded.ShouldBeInRange(93400, 93499);
		SplunkAuditLoggingEventId.ForwardFailedStatus.ShouldBeInRange(93400, 93499);
		SplunkAuditLoggingEventId.ForwardRetried.ShouldBeInRange(93400, 93499);
		SplunkAuditLoggingEventId.HealthCheckFailed.ShouldBeInRange(93400, 93499);
		SplunkAuditLoggingEventId.ForwardFailedHttpError.ShouldBeInRange(93400, 93499);
		SplunkAuditLoggingEventId.ForwardFailedTimeout.ShouldBeInRange(93400, 93499);
		SplunkAuditLoggingEventId.ForwardFailedBatchChunk.ShouldBeInRange(93400, 93499);
	}
}
