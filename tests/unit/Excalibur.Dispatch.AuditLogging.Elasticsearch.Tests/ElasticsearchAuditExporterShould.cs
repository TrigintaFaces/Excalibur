using System.Net;

using Excalibur.Dispatch.Compliance;
using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.AuditLogging.Elasticsearch.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ElasticsearchAuditExporterShould : IDisposable
{
	private readonly ElasticsearchExporterOptions _options = new()
	{
		ElasticsearchUrl = "https://es.example.com:9200",
		IndexPrefix = "test-audit",
		MaxRetryAttempts = 0,
		BulkBatchSize = 500
	};

	private readonly ILogger<ElasticsearchAuditExporter> _logger = EnabledTestLogger.Create<ElasticsearchAuditExporter>();
	private readonly FakeHttpMessageHandler _handler = new();

	[Fact]
	public void Have_name_elasticsearch()
	{
		var sut = CreateExporter();
		sut.Name.ShouldBe("Elasticsearch");
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
		_handler.SetResponse(HttpStatusCode.BadRequest, "Bad request");
		var sut = CreateExporter();
		var auditEvent = CreateAuditEvent();

		var result = await sut.ExportAsync(auditEvent, CancellationToken.None).ConfigureAwait(false);

		result.Success.ShouldBeFalse();
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
	public async Task Retry_transient_status_then_succeed()
	{
		_options.MaxRetryAttempts = 2;
		_options.RetryBaseDelay = TimeSpan.Zero;
		_handler.EnqueueResponse(HttpStatusCode.ServiceUnavailable, "busy");
		_handler.EnqueueResponse(HttpStatusCode.OK);
		var sut = CreateExporter();

		var result = await sut.ExportAsync(CreateAuditEvent(), CancellationToken.None).ConfigureAwait(false);

		result.Success.ShouldBeTrue();
		_handler.RequestCount.ShouldBe(2);
	}

	[Fact]
	public async Task Retry_http_request_exception_then_succeed()
	{
		_options.MaxRetryAttempts = 2;
		_options.RetryBaseDelay = TimeSpan.Zero;
		_handler.EnqueueException(new HttpRequestException("transient network"));
		_handler.EnqueueResponse(HttpStatusCode.OK);
		var sut = CreateExporter();

		var result = await sut.ExportAsync(CreateAuditEvent(), CancellationToken.None).ConfigureAwait(false);

		result.Success.ShouldBeTrue();
		_handler.RequestCount.ShouldBe(2);
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
	public async Task Propagate_operation_canceled_when_token_is_canceled()
	{
		_handler.SetException(new TaskCanceledException("Canceled"));
		var sut = CreateExporter();
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();

		await Should.ThrowAsync<OperationCanceledException>(
			() => sut.ExportAsync(CreateAuditEvent(), cts.Token)).ConfigureAwait(false);
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
	}

	[Fact]
	public async Task Return_empty_batch_result_for_empty_list()
	{
		var sut = CreateExporter();

		var result = await sut.ExportBatchAsync(Array.Empty<AuditEvent>(), CancellationToken.None).ConfigureAwait(false);

		result.TotalCount.ShouldBe(0);
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
	}

	[Fact]
	public async Task Throw_for_null_batch()
	{
		var sut = CreateExporter();

		await Should.ThrowAsync<ArgumentNullException>(
			() => sut.ExportBatchAsync(null!, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task Check_health_returns_healthy_on_success()
	{
		_handler.SetResponse(HttpStatusCode.OK);
		var sut = CreateExporter();

		var result = await sut.CheckHealthAsync(CancellationToken.None).ConfigureAwait(false);

		result.IsHealthy.ShouldBeTrue();
		result.ExporterName.ShouldBe("Elasticsearch");
		result.Endpoint.ShouldBe("https://es.example.com:9200");
		result.Diagnostics.ShouldNotBeNull();
		result.Diagnostics!["IndexPrefix"].ShouldBe("test-audit");
	}

	[Fact]
	public async Task Check_health_returns_unhealthy_on_failure()
	{
		_handler.SetResponse(HttpStatusCode.Unauthorized, "Unauthorized");
		var sut = CreateExporter();

		var result = await sut.CheckHealthAsync(CancellationToken.None).ConfigureAwait(false);

		result.IsHealthy.ShouldBeFalse();
		result.ErrorMessage.ShouldNotBeNull();
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
	public async Task Send_ndjson_content_type()
	{
		_handler.SetResponse(HttpStatusCode.OK);
		var sut = CreateExporter();
		var auditEvent = CreateAuditEvent();

		await sut.ExportAsync(auditEvent, CancellationToken.None).ConfigureAwait(false);

		_handler.LastRequest.ShouldNotBeNull();
		_handler.LastRequest!.Content!.Headers.ContentType!.MediaType.ShouldBe("application/x-ndjson");
	}

	[Fact]
	public async Task Include_api_key_header_when_configured()
	{
		_options.ApiKey = "test-api-key";
		_handler.SetResponse(HttpStatusCode.OK);
		var sut = CreateExporter();
		var auditEvent = CreateAuditEvent();

		await sut.ExportAsync(auditEvent, CancellationToken.None).ConfigureAwait(false);

		_handler.LastRequest.ShouldNotBeNull();
		_handler.LastRequest!.Headers.Authorization.ShouldNotBeNull();
		_handler.LastRequest.Headers.Authorization!.Scheme.ShouldBe("ApiKey");
		_handler.LastRequest.Headers.Authorization.Parameter.ShouldBe("test-api-key");
	}

	[Fact]
	public async Task Chunk_large_batches()
	{
		_options.BulkBatchSize = 2;
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
	public async Task Include_time_based_index_in_payload()
	{
		_handler.SetResponse(HttpStatusCode.OK);
		_handler.CaptureContent = true;
		var sut = CreateExporter();
		var auditEvent = CreateAuditEvent();

		await sut.ExportAsync(auditEvent, CancellationToken.None).ConfigureAwait(false);

		_handler.CapturedContent.ShouldNotBeNull();
		_handler.CapturedContent.ShouldContain("test-audit-");
	}

	[Fact]
	public void Throw_for_null_http_client()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ElasticsearchAuditExporter(
				null!,
				Microsoft.Extensions.Options.Options.Create(_options),
				_logger));
	}

	[Fact]
	public void Throw_for_null_options()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ElasticsearchAuditExporter(new HttpClient(_handler), null!, _logger));
	}

	[Fact]
	public void Throw_for_null_logger()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ElasticsearchAuditExporter(
				new HttpClient(_handler),
				Microsoft.Extensions.Options.Options.Create(_options),
				null!));
	}

	public void Dispose()
	{
		_handler.Dispose();
	}

	private ElasticsearchAuditExporter CreateExporter()
	{
		var httpClient = new HttpClient(_handler);
		return new ElasticsearchAuditExporter(
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
}
