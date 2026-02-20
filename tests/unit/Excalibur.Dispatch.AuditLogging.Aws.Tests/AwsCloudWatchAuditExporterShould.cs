using System.Net;

using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.AuditLogging.Aws.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class AwsCloudWatchAuditExporterShould : IDisposable
{
	private readonly AwsAuditOptions _options = new()
	{
		LogGroupName = "test-log-group",
		Region = "us-east-1",
		StreamName = "test-stream",
		MaxRetryAttempts = 0,
		BatchSize = 500
	};

	private readonly ILogger<AwsCloudWatchAuditExporter> _logger = CreateEnabledLogger();
	private readonly FakeHttpMessageHandler _handler = new();

	[Fact]
	public void Have_name_aws_cloudwatch()
	{
		var sut = CreateExporter();
		sut.Name.ShouldBe("AwsCloudWatch");
	}

	[Fact]
	public async Task Export_single_event_successfully()
	{
		_handler.SetResponse(HttpStatusCode.OK);
		var sut = CreateExporter();
		var auditEvent = CreateAuditEvent();

		var result = await sut.ExportAsync(auditEvent, CancellationToken.None).ConfigureAwait(false);

		result.Success.ShouldBeTrue();
		result.EventId.ShouldBe(auditEvent.EventId);
		result.ExportedAt.ShouldNotBe(default);
	}

	[Fact]
	public async Task Return_failure_result_on_non_success_status()
	{
		_handler.SetResponse(HttpStatusCode.BadRequest, "Bad request body");
		var sut = CreateExporter();
		var auditEvent = CreateAuditEvent();

		var result = await sut.ExportAsync(auditEvent, CancellationToken.None).ConfigureAwait(false);

		result.Success.ShouldBeFalse();
		result.EventId.ShouldBe(auditEvent.EventId);
		result.ErrorMessage.ShouldNotBeNull();
		result.ErrorMessage.ShouldContain("400");
		result.IsTransientError.ShouldBeFalse();
	}

	[Theory]
	[InlineData(HttpStatusCode.RequestTimeout)]
	[InlineData(HttpStatusCode.TooManyRequests)]
	[InlineData(HttpStatusCode.InternalServerError)]
	[InlineData(HttpStatusCode.BadGateway)]
	[InlineData(HttpStatusCode.ServiceUnavailable)]
	[InlineData(HttpStatusCode.GatewayTimeout)]
	public async Task Mark_transient_errors_correctly(HttpStatusCode statusCode)
	{
		_handler.SetResponse(statusCode, "error");
		var sut = CreateExporter();
		var auditEvent = CreateAuditEvent();

		var result = await sut.ExportAsync(auditEvent, CancellationToken.None).ConfigureAwait(false);

		result.Success.ShouldBeFalse();
		result.IsTransientError.ShouldBeTrue();
	}

	[Fact]
	public async Task Return_transient_error_on_http_request_exception()
	{
		_handler.SetException(new HttpRequestException("Connection refused"));
		var sut = CreateExporter();
		var auditEvent = CreateAuditEvent();

		var result = await sut.ExportAsync(auditEvent, CancellationToken.None).ConfigureAwait(false);

		result.Success.ShouldBeFalse();
		result.IsTransientError.ShouldBeTrue();
		result.ErrorMessage.ShouldBe("Connection refused");
	}

	[Fact]
	public async Task Return_transient_error_on_timeout()
	{
		_handler.SetException(new TaskCanceledException("Timeout", new TimeoutException()));
		var sut = CreateExporter();
		var auditEvent = CreateAuditEvent();

		var result = await sut.ExportAsync(auditEvent, CancellationToken.None).ConfigureAwait(false);

		result.Success.ShouldBeFalse();
		result.IsTransientError.ShouldBeTrue();
	}

	[Fact]
	public async Task Throw_for_null_audit_event()
	{
		var sut = CreateExporter();

		await Should.ThrowAsync<ArgumentNullException>(
			() => sut.ExportAsync(null!, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task Export_batch_successfully()
	{
		_handler.SetResponse(HttpStatusCode.OK);
		var sut = CreateExporter();
		var events = new List<AuditEvent> { CreateAuditEvent("evt-1"), CreateAuditEvent("evt-2") };

		var result = await sut.ExportBatchAsync(events, CancellationToken.None).ConfigureAwait(false);

		result.TotalCount.ShouldBe(2);
		result.SuccessCount.ShouldBe(2);
		result.FailedCount.ShouldBe(0);
		result.AllSucceeded.ShouldBeTrue();
		result.FailedEventIds.ShouldBeNull();
		result.Errors.ShouldBeNull();
	}

	[Fact]
	public async Task Return_empty_batch_result_for_empty_list()
	{
		var sut = CreateExporter();

		var result = await sut.ExportBatchAsync(Array.Empty<AuditEvent>(), CancellationToken.None).ConfigureAwait(false);

		result.TotalCount.ShouldBe(0);
		result.SuccessCount.ShouldBe(0);
		result.FailedCount.ShouldBe(0);
	}

	[Fact]
	public async Task Track_failed_events_in_batch()
	{
		_handler.SetResponse(HttpStatusCode.BadRequest, "invalid");
		var sut = CreateExporter();
		var events = new List<AuditEvent> { CreateAuditEvent("evt-1"), CreateAuditEvent("evt-2") };

		var result = await sut.ExportBatchAsync(events, CancellationToken.None).ConfigureAwait(false);

		result.FailedCount.ShouldBe(2);
		result.SuccessCount.ShouldBe(0);
		result.FailedEventIds.ShouldNotBeNull();
		result.FailedEventIds!.Count.ShouldBe(2);
		result.Errors.ShouldNotBeNull();
		result.AllSucceeded.ShouldBeFalse();
	}

	[Fact]
	public async Task Throw_for_null_batch()
	{
		var sut = CreateExporter();

		await Should.ThrowAsync<ArgumentNullException>(
			() => sut.ExportBatchAsync(null!, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task Check_health_returns_healthy_on_reachable_endpoint()
	{
		_handler.SetResponse(HttpStatusCode.OK);
		var sut = CreateExporter();

		var result = await sut.CheckHealthAsync(CancellationToken.None).ConfigureAwait(false);

		result.IsHealthy.ShouldBeTrue();
		result.ExporterName.ShouldBe("AwsCloudWatch");
		result.LatencyMs.ShouldNotBeNull();
		result.Diagnostics.ShouldNotBeNull();
		result.Diagnostics!["Region"].ShouldBe("us-east-1");
		result.Diagnostics["LogGroupName"].ShouldBe("test-log-group");
	}

	[Fact]
	public async Task Check_health_returns_unhealthy_on_service_unavailable()
	{
		_handler.SetResponse(HttpStatusCode.ServiceUnavailable);
		var sut = CreateExporter();

		var result = await sut.CheckHealthAsync(CancellationToken.None).ConfigureAwait(false);

		result.IsHealthy.ShouldBeFalse();
		result.ErrorMessage.ShouldNotBeNull();
	}

	[Fact]
	public async Task Check_health_returns_unhealthy_on_gateway_timeout()
	{
		_handler.SetResponse(HttpStatusCode.GatewayTimeout);
		var sut = CreateExporter();

		var result = await sut.CheckHealthAsync(CancellationToken.None).ConfigureAwait(false);

		result.IsHealthy.ShouldBeFalse();
	}

	[Fact]
	public async Task Check_health_returns_unhealthy_on_exception()
	{
		_handler.SetException(new HttpRequestException("Connection refused"));
		var sut = CreateExporter();

		var result = await sut.CheckHealthAsync(CancellationToken.None).ConfigureAwait(false);

		result.IsHealthy.ShouldBeFalse();
		result.ErrorMessage.ShouldBe("Connection refused");
	}

	[Fact]
	public async Task Use_service_url_override_when_specified()
	{
		_options.ServiceUrl = "https://custom-endpoint.local";
		_handler.SetResponse(HttpStatusCode.OK);
		_handler.CaptureContent = true;
		var sut = CreateExporter();
		var auditEvent = CreateAuditEvent();

		await sut.ExportAsync(auditEvent, CancellationToken.None).ConfigureAwait(false);

		_handler.LastRequest.ShouldNotBeNull();
		_handler.LastRequest!.RequestUri!.ToString().ShouldStartWith("https://custom-endpoint.local");
	}

	[Fact]
	public async Task Include_amz_target_header()
	{
		_handler.SetResponse(HttpStatusCode.OK);
		var sut = CreateExporter();
		var auditEvent = CreateAuditEvent();

		await sut.ExportAsync(auditEvent, CancellationToken.None).ConfigureAwait(false);

		_handler.LastRequest.ShouldNotBeNull();
		_handler.LastRequest!.Headers.GetValues("X-Amz-Target")
			.ShouldContain("Logs_20140328.PutLogEvents");
	}

	[Fact]
	public async Task Send_json_content_type()
	{
		_handler.SetResponse(HttpStatusCode.OK);
		var sut = CreateExporter();
		var auditEvent = CreateAuditEvent();

		await sut.ExportAsync(auditEvent, CancellationToken.None).ConfigureAwait(false);

		_handler.LastRequest.ShouldNotBeNull();
		_handler.LastRequest!.Content!.Headers.ContentType!.MediaType.ShouldBe("application/x-amz-json-1.1");
	}

	[Fact]
	public async Task Chunk_large_batches()
	{
		_options.BatchSize = 2;
		_handler.SetResponse(HttpStatusCode.OK);
		var sut = CreateExporter();
		var events = new List<AuditEvent>
		{
			CreateAuditEvent("evt-1"),
			CreateAuditEvent("evt-2"),
			CreateAuditEvent("evt-3")
		};

		var result = await sut.ExportBatchAsync(events, CancellationToken.None).ConfigureAwait(false);

		result.TotalCount.ShouldBe(3);
		result.SuccessCount.ShouldBe(3);
		_handler.RequestCount.ShouldBe(2);
	}

	[Fact]
	public async Task Default_stream_name_uses_machine_name_when_null()
	{
		_options.StreamName = null;
		_handler.SetResponse(HttpStatusCode.OK);
		var sut = CreateExporter();

		var result = await sut.CheckHealthAsync(CancellationToken.None).ConfigureAwait(false);

		result.Diagnostics.ShouldNotBeNull();
		result.Diagnostics!["StreamName"].ShouldStartWith("dispatch-audit-");
	}

	[Fact]
	public void Throw_for_null_http_client()
	{
		Should.Throw<ArgumentNullException>(() =>
			new AwsCloudWatchAuditExporter(
				null!,
				Microsoft.Extensions.Options.Options.Create(_options),
				_logger));
	}

	[Fact]
	public void Throw_for_null_options()
	{
		Should.Throw<ArgumentNullException>(() =>
			new AwsCloudWatchAuditExporter(new HttpClient(_handler), null!, _logger));
	}

	[Fact]
	public void Throw_for_null_logger()
	{
		Should.Throw<ArgumentNullException>(() =>
			new AwsCloudWatchAuditExporter(
				new HttpClient(_handler),
				Microsoft.Extensions.Options.Options.Create(_options),
				null!));
	}

	public void Dispose()
	{
		_handler.Dispose();
	}

	private AwsCloudWatchAuditExporter CreateExporter()
	{
		var httpClient = new HttpClient(_handler);
		return new AwsCloudWatchAuditExporter(
			httpClient,
			Microsoft.Extensions.Options.Options.Create(_options),
			_logger);
	}

	private static AuditEvent CreateAuditEvent(string? eventId = null) => new()
	{
		EventId = eventId ?? "test-event-1",
		EventType = AuditEventType.DataAccess,
		Action = "Read",
		Outcome = AuditOutcome.Success,
		Timestamp = DateTimeOffset.UtcNow,
		ActorId = "user-1",
		ActorType = "User",
		ResourceId = "resource-1",
		ResourceType = "Customer",
		ResourceClassification = DataClassification.Confidential,
		CorrelationId = "corr-1",
		SessionId = "sess-1",
		IpAddress = "192.168.1.1",
		UserAgent = "TestAgent/1.0",
		Reason = "Testing",
		Metadata = new Dictionary<string, string> { ["key"] = "value" },
		EventHash = "hash-123"
	};

	private static ILogger<AwsCloudWatchAuditExporter> CreateEnabledLogger()
	{
		var logger = A.Fake<ILogger<AwsCloudWatchAuditExporter>>();
		A.CallTo(() => logger.IsEnabled(A<LogLevel>._)).Returns(true);
		return logger;
	}
}
