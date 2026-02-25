using Excalibur.Dispatch.AuditLogging.Elasticsearch;

namespace Excalibur.Dispatch.AuditLogging.Elasticsearch.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ElasticsearchAuditLoggingEventIdShould
{
	[Fact]
	public void Have_unique_event_ids()
	{
		var ids = new[]
		{
			ElasticsearchAuditLoggingEventId.EventForwarded,
			ElasticsearchAuditLoggingEventId.BatchForwarded,
			ElasticsearchAuditLoggingEventId.ForwardFailedStatus,
			ElasticsearchAuditLoggingEventId.ForwardRetried,
			ElasticsearchAuditLoggingEventId.HealthCheckFailed,
			ElasticsearchAuditLoggingEventId.ForwardFailedHttpError,
			ElasticsearchAuditLoggingEventId.ForwardFailedTimeout,
			ElasticsearchAuditLoggingEventId.ForwardFailedBatchChunk
		};

		ids.ShouldBeUnique();
	}

	[Fact]
	public void Be_in_assigned_range()
	{
		ElasticsearchAuditLoggingEventId.EventForwarded.ShouldBeInRange(93460, 93479);
		ElasticsearchAuditLoggingEventId.BatchForwarded.ShouldBeInRange(93460, 93479);
		ElasticsearchAuditLoggingEventId.ForwardFailedStatus.ShouldBeInRange(93460, 93479);
		ElasticsearchAuditLoggingEventId.ForwardRetried.ShouldBeInRange(93460, 93479);
		ElasticsearchAuditLoggingEventId.HealthCheckFailed.ShouldBeInRange(93460, 93479);
		ElasticsearchAuditLoggingEventId.ForwardFailedHttpError.ShouldBeInRange(93460, 93479);
		ElasticsearchAuditLoggingEventId.ForwardFailedTimeout.ShouldBeInRange(93460, 93479);
		ElasticsearchAuditLoggingEventId.ForwardFailedBatchChunk.ShouldBeInRange(93460, 93479);
	}
}
