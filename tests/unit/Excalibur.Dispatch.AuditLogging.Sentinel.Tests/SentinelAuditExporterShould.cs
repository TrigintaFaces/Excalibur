using System.Net;

using Excalibur.Dispatch.AuditLogging.Sentinel;
using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.AuditLogging.Sentinel.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class SentinelAuditExporterShould : IDisposable
{
	// Valid base64 key for HMACSHA256 signature generation
	private static readonly string TestSharedKey = Convert.ToBase64String(new byte[32]);

	private readonly SentinelExporterOptions _options = new()
	{
		WorkspaceId = "test-workspace-id",
		SharedKey = TestSharedKey,
		LogType = "TestAudit",
		MaxRetryAttempts = 0
	};

	private readonly ILogger<SentinelAuditExporter> _logger = NullLogger<SentinelAuditExporter>.Instance;
	private readonly FakeSentinelHttpHandler _handler = new();

	[Fact]
	public void Have_name_azure_sentinel()
	{
		// Arrange
		var sut = CreateExporter();

		// Act & Assert
		sut.Name.ShouldBe("AzureSentinel");
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
		_handler.SetResponse(HttpStatusCode.Forbidden, "Forbidden");
		var sut = CreateExporter();
		var auditEvent = CreateAuditEvent();

		// Act
		var result = await sut.ExportAsync(auditEvent, CancellationToken.None);

		// Assert
		result.Success.ShouldBeFalse();
		result.ErrorMessage.ShouldNotBeNull();
		result.ErrorMessage.ShouldContain("403");
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
		result.ExporterName.ShouldBe("AzureSentinel");
		result.Endpoint.ShouldContain("opinsights.azure.com");
		result.Diagnostics.ShouldNotBeNull();
		result.Diagnostics!["LogType"].ShouldBe("TestAudit");
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
	public async Task Include_authorization_header_with_shared_key()
	{
		// Arrange
		_handler.SetResponse(HttpStatusCode.OK);
		var sut = CreateExporter();
		var auditEvent = CreateAuditEvent();

		// Act
		await sut.ExportAsync(auditEvent, CancellationToken.None);

		// Assert
		_handler.LastRequest.ShouldNotBeNull();
		_handler.LastRequest!.Headers.GetValues("Authorization")
			.ShouldHaveSingleItem()
			.ShouldStartWith("SharedKey test-workspace-id:");
	}

	[Fact]
	public async Task Include_log_type_header()
	{
		// Arrange
		_handler.SetResponse(HttpStatusCode.OK);
		var sut = CreateExporter();
		var auditEvent = CreateAuditEvent();

		// Act
		await sut.ExportAsync(auditEvent, CancellationToken.None);

		// Assert
		_handler.LastRequest.ShouldNotBeNull();
		_handler.LastRequest!.Headers.GetValues("Log-Type").ShouldContain("TestAudit");
	}

	[Fact]
	public async Task Include_x_ms_date_header()
	{
		// Arrange
		_handler.SetResponse(HttpStatusCode.OK);
		var sut = CreateExporter();
		var auditEvent = CreateAuditEvent();

		// Act
		await sut.ExportAsync(auditEvent, CancellationToken.None);

		// Assert
		_handler.LastRequest.ShouldNotBeNull();
		_handler.LastRequest!.Headers.Contains("x-ms-date").ShouldBeTrue();
	}

	[Fact]
	public async Task Include_time_generated_field_header_when_set()
	{
		// Arrange
		_options.TimeGeneratedField = "timestamp";
		_handler.SetResponse(HttpStatusCode.OK);
		var sut = CreateExporter();
		var auditEvent = CreateAuditEvent();

		// Act
		await sut.ExportAsync(auditEvent, CancellationToken.None);

		// Assert
		_handler.LastRequest.ShouldNotBeNull();
		_handler.LastRequest!.Headers.GetValues("time-generated-field").ShouldContain("timestamp");
	}

	[Fact]
	public async Task Include_azure_resource_id_header_when_set()
	{
		// Arrange
		_options.AzureResourceId = "/subscriptions/test-sub/resourceGroups/test-rg/providers/test";
		_handler.SetResponse(HttpStatusCode.OK);
		var sut = CreateExporter();
		var auditEvent = CreateAuditEvent();

		// Act
		await sut.ExportAsync(auditEvent, CancellationToken.None);

		// Assert
		_handler.LastRequest.ShouldNotBeNull();
		_handler.LastRequest!.Headers.Contains("x-ms-AzureResourceId").ShouldBeTrue();
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
		_handler.RequestCount.ShouldBe(2);
	}

	[Fact]
	public void Throw_for_null_http_client()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new SentinelAuditExporter(
				null!,
				Microsoft.Extensions.Options.Options.Create(_options),
				_logger));
	}

	[Fact]
	public void Throw_for_null_options()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new SentinelAuditExporter(new HttpClient(_handler), null!, _logger));
	}

	[Fact]
	public void Throw_for_null_logger()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new SentinelAuditExporter(
				new HttpClient(_handler),
				Microsoft.Extensions.Options.Options.Create(_options),
				null!));
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
	public async Task Mask_workspace_id_in_health_diagnostics()
	{
		// Arrange
		_handler.SetResponse(HttpStatusCode.OK);
		var sut = CreateExporter();

		// Act
		var result = await sut.CheckHealthAsync(CancellationToken.None);

		// Assert
		result.Diagnostics.ShouldNotBeNull();
		result.Diagnostics!["WorkspaceId"].ShouldEndWith("...");
		result.Diagnostics["WorkspaceId"].Length.ShouldBeLessThan(_options.WorkspaceId.Length);
	}

	public void Dispose()
	{
		_handler.Dispose();
	}

	private SentinelAuditExporter CreateExporter()
	{
		var httpClient = new HttpClient(_handler);
		return new SentinelAuditExporter(
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

/// <summary>
/// Fake HTTP message handler for Sentinel exporter tests.
/// </summary>
internal sealed class FakeSentinelHttpHandler : HttpMessageHandler
{
	private HttpStatusCode _statusCode = HttpStatusCode.OK;
	private string _responseBody = string.Empty;
	private Exception? _exception;

	public HttpRequestMessage? LastRequest { get; private set; }
	public int RequestCount { get; private set; }

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

	protected override Task<HttpResponseMessage> SendAsync(
		HttpRequestMessage request,
		CancellationToken cancellationToken)
	{
		RequestCount++;
		LastRequest = request;

		if (_exception != null)
		{
			throw _exception;
		}

		return Task.FromResult(new HttpResponseMessage(_statusCode)
		{
			Content = new StringContent(_responseBody)
		});
	}
}
