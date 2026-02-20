using Excalibur.Dispatch.AuditLogging.GoogleCloud;

namespace Excalibur.Dispatch.AuditLogging.GoogleCloud.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class GoogleCloudAuditLoggingEventIdShould
{
	[Fact]
	public void Have_unique_event_ids()
	{
		var ids = new[]
		{
			GoogleCloudAuditLoggingEventId.EventForwarded,
			GoogleCloudAuditLoggingEventId.BatchForwarded,
			GoogleCloudAuditLoggingEventId.ForwardFailedStatus,
			GoogleCloudAuditLoggingEventId.ForwardRetried,
			GoogleCloudAuditLoggingEventId.HealthCheckFailed,
			GoogleCloudAuditLoggingEventId.ForwardFailedHttpError,
			GoogleCloudAuditLoggingEventId.ForwardFailedTimeout,
			GoogleCloudAuditLoggingEventId.ForwardFailedBatchChunk
		};

		ids.ShouldBeUnique();
	}
}
