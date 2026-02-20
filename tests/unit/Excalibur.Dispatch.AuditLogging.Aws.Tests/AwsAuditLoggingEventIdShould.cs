using Excalibur.Dispatch.AuditLogging.Aws;

namespace Excalibur.Dispatch.AuditLogging.Aws.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class AwsAuditLoggingEventIdShould
{
	[Fact]
	public void Have_unique_event_ids()
	{
		var ids = new[]
		{
			AwsAuditLoggingEventId.EventForwarded,
			AwsAuditLoggingEventId.BatchForwarded,
			AwsAuditLoggingEventId.ForwardFailedStatus,
			AwsAuditLoggingEventId.ForwardRetried,
			AwsAuditLoggingEventId.HealthCheckFailed,
			AwsAuditLoggingEventId.ForwardFailedHttpError,
			AwsAuditLoggingEventId.ForwardFailedTimeout,
			AwsAuditLoggingEventId.ForwardFailedBatchChunk
		};

		ids.ShouldBeUnique();
	}

	[Fact]
	public void Be_in_assigned_range()
	{
		AwsAuditLoggingEventId.EventForwarded.ShouldBeInRange(93480, 93499);
		AwsAuditLoggingEventId.BatchForwarded.ShouldBeInRange(93480, 93499);
		AwsAuditLoggingEventId.ForwardFailedStatus.ShouldBeInRange(93480, 93499);
		AwsAuditLoggingEventId.ForwardRetried.ShouldBeInRange(93480, 93499);
		AwsAuditLoggingEventId.HealthCheckFailed.ShouldBeInRange(93480, 93499);
		AwsAuditLoggingEventId.ForwardFailedHttpError.ShouldBeInRange(93480, 93499);
		AwsAuditLoggingEventId.ForwardFailedTimeout.ShouldBeInRange(93480, 93499);
		AwsAuditLoggingEventId.ForwardFailedBatchChunk.ShouldBeInRange(93480, 93499);
	}
}
