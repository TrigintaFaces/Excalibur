using System.Net;

using Excalibur.Dispatch.AuditLogging.Datadog;
using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.AuditLogging.Datadog.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class DatadogAuditExporterShould : IDisposable
{
	private readonly DatadogExporterOptions _options = new()
	{
		ApiKey = "test-api-key",
		Site = "datadoghq.com",
		Service = "test-service",
		Source = "test-source",
		MaxRetryAttempts = 0,
		UseCompression = false
	};

	private readonly ILogger<DatadogAuditExporter> _logger = NullLogger<DatadogAuditExporter>.Instance;
	private readonly FakeHttpMessageHandler _handler = new();

	[Fact]
	public void Have_name_datadog()
	{
		// Arrange
		var sut = CreateExporter();

		// Act & Assert
		sut.Name.ShouldBe("Datadog");
	}

	[Fact]
	public async Task Export_single_event_successfully()
	{
		// Arrange
		_handler.SetResponse(HttpStatusCode.OK);
		var sut = CreateExporter();
		var auditEvent = CreateAuditEvent();

		// Act
		var result = await sut.ExportAsync(auditEvent, CancellationToken.None);

		// Assert
		result.Success.ShouldBeTrue();
		result.EventId.ShouldBe(auditEvent.EventId);
		result.ExportedAt.ShouldNotBe(default);
	}

	[Fact]
	public async Task Return_failure_result_on_non_success_status()
	{
		// Arrange
		_handler.SetResponse(HttpStatusCode.BadRequest, "Bad request body");
		var sut = CreateExporter();
		var auditEvent = CreateAuditEvent();

		// Act
		var result = await sut.ExportAsync(auditEvent, CancellationToken.None);

		// Assert
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
		// Arrange
		_handler.SetResponse(statusCode, "error");
		var sut = CreateExporter();
		var auditEvent = CreateAuditEvent();

		// Act
		var result = await sut.ExportAsync(auditEvent, CancellationToken.None);

		// Assert
		result.Success.ShouldBeFalse();
		result.IsTransientError.ShouldBeTrue();
	}

	[Fact]
	public async Task Return_transient_error_on_http_request_exception()
	{
		// Arrange
		_handler.SetException(new HttpRequestException("Connection refused"));
		var sut = CreateExporter();
		var auditEvent = CreateAuditEvent();

		// Act
		var result = await sut.ExportAsync(auditEvent, CancellationToken.None);

		// Assert
		result.Success.ShouldBeFalse();
		result.IsTransientError.ShouldBeTrue();
		result.ErrorMessage.ShouldBe("Connection refused");
	}

	[Fact]
	public async Task Return_transient_error_on_timeout()
	{
		// Arrange
		_handler.SetException(new TaskCanceledException("Timeout", new TimeoutException()));
		var sut = CreateExporter();
		var auditEvent = CreateAuditEvent();

		// Act
		var result = await sut.ExportAsync(auditEvent, CancellationToken.None);

		// Assert
		result.Success.ShouldBeFalse();
		result.IsTransientError.ShouldBeTrue();
	}

	[Fact]
	public async Task Throw_null_for_null_audit_event()
	{
		// Arrange
		var sut = CreateExporter();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => sut.ExportAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task Export_batch_successfully()
	{
		// Arrange
		_handler.SetResponse(HttpStatusCode.OK);
		var sut = CreateExporter();
		var events = new List<AuditEvent> { CreateAuditEvent("evt-1"), CreateAuditEvent("evt-2") };

		// Act
		var result = await sut.ExportBatchAsync(events, CancellationToken.None);

		// Assert
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
		// Arrange
		var sut = CreateExporter();

		// Act
		var result = await sut.ExportBatchAsync(Array.Empty<AuditEvent>(), CancellationToken.None);

		// Assert
		result.TotalCount.ShouldBe(0);
		result.SuccessCount.ShouldBe(0);
		result.FailedCount.ShouldBe(0);
	}

	[Fact]
	public async Task Track_failed_events_in_batch()
	{
		// Arrange
		_handler.SetResponse(HttpStatusCode.BadRequest, "invalid");
		var sut = CreateExporter();
		var events = new List<AuditEvent> { CreateAuditEvent("evt-1"), CreateAuditEvent("evt-2") };

		// Act
		var result = await sut.ExportBatchAsync(events, CancellationToken.None);

		// Assert
		result.FailedCount.ShouldBe(2);
		result.SuccessCount.ShouldBe(0);
		result.FailedEventIds.ShouldNotBeNull();
		result.FailedEventIds.Count.ShouldBe(2);
		result.Errors.ShouldNotBeNull();
		result.AllSucceeded.ShouldBeFalse();
	}

	[Fact]
	public async Task Throw_null_for_null_batch()
	{
		// Arrange
		var sut = CreateExporter();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => sut.ExportBatchAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task Check_health_returns_healthy_on_success()
	{
		// Arrange
		_handler.SetResponse(HttpStatusCode.OK);
		var sut = CreateExporter();

		// Act
		var result = await sut.CheckHealthAsync(CancellationToken.None);

		// Assert
		result.IsHealthy.ShouldBeTrue();
		result.ExporterName.ShouldBe("Datadog");
		result.Endpoint.ShouldContain("datadoghq.com");
		result.LatencyMs.ShouldNotBeNull();
		result.Diagnostics.ShouldNotBeNull();
		result.Diagnostics!["Site"].ShouldBe("datadoghq.com");
		result.Diagnostics["Service"].ShouldBe("test-service");
	}

	[Fact]
	public async Task Check_health_returns_unhealthy_on_failure()
	{
		// Arrange
		_handler.SetResponse(HttpStatusCode.Unauthorized, "Unauthorized");
		var sut = CreateExporter();

		// Act
		var result = await sut.CheckHealthAsync(CancellationToken.None);

		// Assert
		result.IsHealthy.ShouldBeFalse();
		result.ErrorMessage.ShouldNotBeNull();
	}

	[Fact]
	public async Task Check_health_returns_unhealthy_on_exception()
	{
		// Arrange
		_handler.SetException(new HttpRequestException("Connection refused"));
		var sut = CreateExporter();

		// Act
		var result = await sut.CheckHealthAsync(CancellationToken.None);

		// Assert
		result.IsHealthy.ShouldBeFalse();
		result.ErrorMessage.ShouldBe("Connection refused");
	}

	[Fact]
	public async Task Include_dd_api_key_header_in_request()
	{
		// Arrange
		_handler.SetResponse(HttpStatusCode.OK);
		var sut = CreateExporter();
		var auditEvent = CreateAuditEvent();

		// Act
		await sut.ExportAsync(auditEvent, CancellationToken.None);

		// Assert
		_handler.LastRequest.ShouldNotBeNull();
		_handler.LastRequest!.Headers.GetValues("DD-API-KEY").ShouldContain("test-api-key");
	}

	[Fact]
	public async Task Send_json_content_type()
	{
		// Arrange
		_handler.SetResponse(HttpStatusCode.OK);
		var sut = CreateExporter();
		var auditEvent = CreateAuditEvent();

		// Act
		await sut.ExportAsync(auditEvent, CancellationToken.None);

		// Assert
		_handler.LastRequest.ShouldNotBeNull();
		_handler.LastRequest!.Content!.Headers.ContentType!.MediaType.ShouldBe("application/json");
	}

	[Fact]
	public async Task Use_gzip_compression_when_enabled()
	{
		// Arrange
		_options.UseCompression = true;
		_handler.SetResponse(HttpStatusCode.OK);
		var sut = CreateExporter();
		var auditEvent = CreateAuditEvent();

		// Act
		await sut.ExportAsync(auditEvent, CancellationToken.None);

		// Assert
		_handler.LastRequest.ShouldNotBeNull();
		_handler.LastRequest!.Content!.Headers.ContentEncoding.ShouldContain("gzip");
	}

	[Fact]
	public async Task Include_tags_in_log_entry()
	{
		// Arrange
		_options.Tags = "env:test,team:platform";
		_handler.SetResponse(HttpStatusCode.OK);
		_handler.CaptureContent = true;
		var sut = CreateExporter();
		var auditEvent = CreateAuditEvent();

		// Act
		await sut.ExportAsync(auditEvent, CancellationToken.None);

		// Assert
		_handler.CapturedContent.ShouldNotBeNull();
		_handler.CapturedContent.ShouldContain("env:test,team:platform");
	}

	[Fact]
	public async Task Include_tenant_tag_when_present()
	{
		// Arrange
		_handler.SetResponse(HttpStatusCode.OK);
		_handler.CaptureContent = true;
		var sut = CreateExporter();
		var auditEvent = CreateAuditEvent() with { TenantId = "tenant-123" };

		// Act
		await sut.ExportAsync(auditEvent, CancellationToken.None);

		// Assert
		_handler.CapturedContent.ShouldNotBeNull();
		_handler.CapturedContent.ShouldContain("tenant:tenant-123");
	}

	[Fact]
	public async Task Chunk_large_batches()
	{
		// Arrange
		_options.MaxBatchSize = 2;
		_handler.SetResponse(HttpStatusCode.OK);
		var sut = CreateExporter();
		var events = new List<AuditEvent>
		{
			CreateAuditEvent("evt-1"),
			CreateAuditEvent("evt-2"),
			CreateAuditEvent("evt-3")
		};

		// Act
		var result = await sut.ExportBatchAsync(events, CancellationToken.None);

		// Assert
		result.TotalCount.ShouldBe(3);
		result.SuccessCount.ShouldBe(3);
		_handler.RequestCount.ShouldBe(2); // 2 chunks: [evt-1, evt-2] and [evt-3]
	}

	[Fact]
	public void Throw_for_null_http_client()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new DatadogAuditExporter(
				null!,
				Microsoft.Extensions.Options.Options.Create(_options),
				_logger));
	}

	[Fact]
	public void Throw_for_null_options()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new DatadogAuditExporter(new HttpClient(_handler), null!, _logger));
	}

	[Fact]
	public void Throw_for_null_logger()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new DatadogAuditExporter(
				new HttpClient(_handler),
				Microsoft.Extensions.Options.Options.Create(_options),
				null!));
	}

	public void Dispose()
	{
		_handler.Dispose();
	}

	private DatadogAuditExporter CreateExporter()
	{
		var httpClient = new HttpClient(_handler);
		return new DatadogAuditExporter(
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
		TenantId = null,
		CorrelationId = "corr-1",
		SessionId = "sess-1",
		IpAddress = "192.168.1.1",
		UserAgent = "TestAgent/1.0",
		Reason = "Testing",
		Metadata = new Dictionary<string, string> { ["key"] = "value" },
		EventHash = "hash-123"
	};
}

/// <summary>
/// Fake HTTP message handler for testing HTTP-based exporters.
/// </summary>
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
	private HttpStatusCode _statusCode = HttpStatusCode.OK;
	private string _responseBody = string.Empty;
	private Exception? _exception;

	public HttpRequestMessage? LastRequest { get; private set; }
	public int RequestCount { get; private set; }
	public bool CaptureContent { get; set; }
	public string? CapturedContent { get; private set; }

	public void SetResponse(HttpStatusCode statusCode, string body = "")
	{
		_statusCode = statusCode;
		_responseBody = body;
		_exception = null;
	}

	public void SetException(Exception exception)
	{
		_exception = exception;
	}

	protected override async Task<HttpResponseMessage> SendAsync(
		HttpRequestMessage request,
		CancellationToken cancellationToken)
	{
		RequestCount++;
		LastRequest = request;

		if (CaptureContent && request.Content != null)
		{
			CapturedContent = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
		}

		if (_exception != null)
		{
			throw _exception;
		}

		return new HttpResponseMessage(_statusCode)
		{
			Content = new StringContent(_responseBody)
		};
	}
}
