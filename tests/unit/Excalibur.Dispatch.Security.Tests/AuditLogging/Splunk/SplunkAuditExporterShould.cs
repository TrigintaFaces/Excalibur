// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Net;
using System.Text.Json;

using Excalibur.Dispatch.AuditLogging.Splunk;

namespace Excalibur.Dispatch.Security.Tests.AuditLogging.Splunk;

/// <summary>
/// Unit tests for <see cref="SplunkAuditExporter"/>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class SplunkAuditExporterShould : IDisposable
{
	private readonly MockHttpMessageHandler _mockHandler;
	private readonly HttpClient _httpClient;
	private readonly SplunkAuditExporter _sut;
	private readonly SplunkExporterOptions _options;

	public SplunkAuditExporterShould()
	{
		_options = new SplunkExporterOptions
		{
			HecEndpoint = new Uri("https://splunk.example.com:8088/services/collector"),
			HecToken = "test-token-123",
			Index = "audit",
			SourceType = "audit:dispatch",
			Source = "test-app",
			Host = "test-host",
			MaxRetryAttempts = 1,
			RetryBaseDelay = TimeSpan.FromMilliseconds(10)
		};

		_mockHandler = new MockHttpMessageHandler();
		_httpClient = new HttpClient(_mockHandler);

		_sut = new SplunkAuditExporter(
			_httpClient,
			Microsoft.Extensions.Options.Options.Create(_options),
			CreateEnabledLogger());
	}

	public void Dispose()
	{
		_httpClient.Dispose();
		_mockHandler.Dispose();
	}

	#region Constructor Tests

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenHttpClientIsNull()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(_options);
		var logger = CreateEnabledLogger();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new SplunkAuditExporter(null!, options, logger));
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenOptionsIsNull()
	{
		// Arrange
		var handler = new MockHttpMessageHandler();
		using var client = new HttpClient(handler);
		var logger = CreateEnabledLogger();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new SplunkAuditExporter(client, null!, logger));
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenLoggerIsNull()
	{
		// Arrange
		var handler = new MockHttpMessageHandler();
		using var client = new HttpClient(handler);
		var options = Microsoft.Extensions.Options.Options.Create(_options);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new SplunkAuditExporter(client, options, null!));
	}

	#endregion Constructor Tests

	#region ExportAsync Tests

	[Fact]
	public async Task ExportAsync_ReturnsSuccess_WhenHecAcceptsEvent()
	{
		// Arrange
		var auditEvent = CreateTestAuditEvent();
		_mockHandler.SetResponse(HttpStatusCode.OK, "{\"text\":\"Success\",\"code\":0}");

		// Act
		var result = await _sut.ExportAsync(auditEvent, CancellationToken.None);

		// Assert
		result.Success.ShouldBeTrue();
		result.EventId.ShouldBe(auditEvent.EventId);
		result.ExportedAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddMinutes(-1));
	}

	[Fact]
	public async Task ExportAsync_ReturnsFailure_WhenHecRejectsEvent()
	{
		// Arrange
		var auditEvent = CreateTestAuditEvent();
		_mockHandler.SetResponse(HttpStatusCode.BadRequest, "{\"text\":\"Invalid token\",\"code\":4}");

		// Act
		var result = await _sut.ExportAsync(auditEvent, CancellationToken.None);

		// Assert
		result.Success.ShouldBeFalse();
		result.EventId.ShouldBe(auditEvent.EventId);
		result.ErrorMessage.ShouldNotBeNullOrEmpty();
		result.IsTransientError.ShouldBeFalse();
	}

	[Fact]
	public async Task ExportAsync_ReturnsTransientError_WhenServerError()
	{
		// Arrange
		var auditEvent = CreateTestAuditEvent();
		_mockHandler.SetResponse(HttpStatusCode.ServiceUnavailable, "Service unavailable");

		// Act
		var result = await _sut.ExportAsync(auditEvent, CancellationToken.None);

		// Assert
		result.Success.ShouldBeFalse();
		result.IsTransientError.ShouldBeTrue();
	}

	[Fact]
	public async Task ExportAsync_ThrowsException_WhenAuditEventIsNull()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(() =>
			_sut.ExportAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task ExportAsync_IncludesAuthorizationHeader()
	{
		// Arrange
		var auditEvent = CreateTestAuditEvent();
		_mockHandler.SetResponse(HttpStatusCode.OK, "{\"text\":\"Success\"}");

		// Act
		_ = await _sut.ExportAsync(auditEvent, CancellationToken.None);

		// Assert
		_ = _mockHandler.LastRequest.ShouldNotBeNull();
		_ = _mockHandler.LastRequest.Headers.Authorization.ShouldNotBeNull();
		_mockHandler.LastRequest.Headers.Authorization.Scheme.ShouldBe("Splunk");
		_mockHandler.LastRequest.Headers.Authorization.Parameter.ShouldBe("test-token-123");
	}

	[Fact]
	public async Task ExportAsync_SendsCorrectJsonPayload()
	{
		// Arrange
		var auditEvent = CreateTestAuditEvent();
		_mockHandler.SetResponse(HttpStatusCode.OK, "{\"text\":\"Success\"}");

		// Act
		_ = await _sut.ExportAsync(auditEvent, CancellationToken.None);

		// Assert
		_ = _mockHandler.LastRequestContent.ShouldNotBeNull();
		var json = JsonDocument.Parse(_mockHandler.LastRequestContent);
		json.RootElement.GetProperty("host").GetString().ShouldBe("test-host");
		json.RootElement.GetProperty("source").GetString().ShouldBe("test-app");
		json.RootElement.GetProperty("sourcetype").GetString().ShouldBe("audit:dispatch");
		json.RootElement.GetProperty("index").GetString().ShouldBe("audit");
		json.RootElement.GetProperty("event").GetProperty("event_id").GetString().ShouldBe(auditEvent.EventId);
	}

	#endregion ExportAsync Tests

	#region ExportBatchAsync Tests

	[Fact]
	public async Task ExportBatchAsync_ReturnsAllSuccess_WhenHecAcceptsBatch()
	{
		// Arrange
		var events = new List<AuditEvent>
		{
			CreateTestAuditEvent("event-1"),
			CreateTestAuditEvent("event-2"),
			CreateTestAuditEvent("event-3")
		};
		_mockHandler.SetResponse(HttpStatusCode.OK, "{\"text\":\"Success\"}");

		// Act
		var result = await _sut.ExportBatchAsync(events, CancellationToken.None);

		// Assert
		result.TotalCount.ShouldBe(3);
		result.SuccessCount.ShouldBe(3);
		result.FailedCount.ShouldBe(0);
		result.AllSucceeded.ShouldBeTrue();
	}

	[Fact]
	public async Task ExportBatchAsync_ReturnsEmptyResult_WhenNoEvents()
	{
		// Arrange
		var events = new List<AuditEvent>();

		// Act
		var result = await _sut.ExportBatchAsync(events, CancellationToken.None);

		// Assert
		result.TotalCount.ShouldBe(0);
		result.SuccessCount.ShouldBe(0);
		result.FailedCount.ShouldBe(0);
	}

	[Fact]
	public async Task ExportBatchAsync_ThrowsException_WhenEventsIsNull()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(() =>
			_sut.ExportBatchAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task ExportBatchAsync_SendsNewlineDelimitedJson()
	{
		// Arrange
		var events = new List<AuditEvent>
		{
			CreateTestAuditEvent("event-1"),
			CreateTestAuditEvent("event-2")
		};
		_mockHandler.SetResponse(HttpStatusCode.OK, "{\"text\":\"Success\"}");

		// Act
		_ = await _sut.ExportBatchAsync(events, CancellationToken.None);

		// Assert
		_ = _mockHandler.LastRequestContent.ShouldNotBeNull();
		var lines = _mockHandler.LastRequestContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
		lines.Length.ShouldBe(2);
	}

	#endregion ExportBatchAsync Tests

	#region CheckHealthAsync Tests

	[Fact]
	public async Task CheckHealthAsync_ReturnsHealthy_WhenEndpointResponds()
	{
		// Arrange
		_mockHandler.SetResponse(HttpStatusCode.OK, "");

		// Act
		var result = await _sut.CheckHealthAsync(CancellationToken.None);

		// Assert
		result.IsHealthy.ShouldBeTrue();
		result.ExporterName.ShouldBe("Splunk");
		result.Endpoint.ShouldBe("https://splunk.example.com:8088/services/collector");
		_ = result.LatencyMs.ShouldNotBeNull();
	}

	[Fact]
	public async Task CheckHealthAsync_ReturnsHealthy_WhenEndpointReturnsBadRequest()
	{
		// Arrange - HEC returns 400 for GET requests but endpoint is reachable
		_mockHandler.SetResponse(HttpStatusCode.BadRequest, "");

		// Act
		var result = await _sut.CheckHealthAsync(CancellationToken.None);

		// Assert
		result.IsHealthy.ShouldBeTrue();
	}

	[Fact]
	public async Task CheckHealthAsync_ReturnsUnhealthy_WhenConnectionFails()
	{
		// Arrange
		_mockHandler.SetException(new HttpRequestException("Connection refused"));

		// Act
		var result = await _sut.CheckHealthAsync(CancellationToken.None);

		// Assert
		result.IsHealthy.ShouldBeFalse();
		result.ErrorMessage.ShouldContain("Connection refused");
	}

	[Fact]
	public async Task CheckHealthAsync_IncludesDiagnostics()
	{
		// Arrange
		_mockHandler.SetResponse(HttpStatusCode.OK, "");

		// Act
		var result = await _sut.CheckHealthAsync(CancellationToken.None);

		// Assert
		_ = result.Diagnostics.ShouldNotBeNull();
		result.Diagnostics["Index"].ShouldBe("audit");
		result.Diagnostics["SourceType"].ShouldBe("audit:dispatch");
	}

	#endregion CheckHealthAsync Tests

	#region HttpRequestException and Timeout Tests

	[Fact]
	public async Task ExportAsync_ReturnsTransientError_WhenHttpRequestExceptionThrown()
	{
		// Arrange
		var auditEvent = CreateTestAuditEvent();
		_mockHandler.SetException(new HttpRequestException("Connection refused"));

		// Act
		var result = await _sut.ExportAsync(auditEvent, CancellationToken.None);

		// Assert
		result.Success.ShouldBeFalse();
		result.IsTransientError.ShouldBeTrue();
		result.ErrorMessage.ShouldContain("Connection refused");
	}

	[Fact]
	public async Task ExportAsync_ReturnsTransientError_WhenTimeoutOccurs()
	{
		// Arrange
		var auditEvent = CreateTestAuditEvent();
		_mockHandler.SetException(new TaskCanceledException("The request timed out", new TimeoutException()));

		// Act
		var result = await _sut.ExportAsync(auditEvent, CancellationToken.None);

		// Assert
		result.Success.ShouldBeFalse();
		result.IsTransientError.ShouldBeTrue();
		result.ErrorMessage.ShouldNotBeNullOrEmpty();
	}

	#endregion HttpRequestException and Timeout Tests

	#region Batch Failure Tests

	[Fact]
	public async Task ExportBatchAsync_ReportsFailures_WhenChunkFails()
	{
		// Arrange
		var events = new List<AuditEvent>
		{
			CreateTestAuditEvent("event-1"),
			CreateTestAuditEvent("event-2")
		};
		_mockHandler.SetResponse(HttpStatusCode.Forbidden, "Invalid token");

		// Act
		var result = await _sut.ExportBatchAsync(events, CancellationToken.None);

		// Assert
		result.TotalCount.ShouldBe(2);
		result.SuccessCount.ShouldBe(0);
		result.FailedCount.ShouldBe(2);
		result.AllSucceeded.ShouldBeFalse();
		result.FailedEventIds.ShouldNotBeNull();
		result.FailedEventIds.Count.ShouldBe(2);
		result.Errors.ShouldNotBeNull();
	}

	[Fact]
	public async Task ExportBatchAsync_ReportsFailures_WhenExceptionThrown()
	{
		// Arrange
		var events = new List<AuditEvent>
		{
			CreateTestAuditEvent("event-1"),
			CreateTestAuditEvent("event-2")
		};
		_mockHandler.SetException(new HttpRequestException("Connection refused"));

		// Act
		var result = await _sut.ExportBatchAsync(events, CancellationToken.None);

		// Assert
		result.TotalCount.ShouldBe(2);
		result.FailedCount.ShouldBe(2);
		result.AllSucceeded.ShouldBeFalse();
	}

	#endregion Batch Failure Tests

	#region Splunk-Specific Feature Tests

	[Fact]
	public async Task ExportAsync_IncludesChannelHeader_WhenUseAckEnabled()
	{
		// Arrange
		var ackOptions = new SplunkExporterOptions
		{
			HecEndpoint = new Uri("https://splunk.example.com:8088/services/collector"),
			HecToken = "test-token-123",
			UseAck = true,
			Channel = "test-channel-abc",
			MaxRetryAttempts = 0
		};

		var handler = new MockHttpMessageHandler();
		using var client = new HttpClient(handler);
		var exporter = new SplunkAuditExporter(
			client,
			Microsoft.Extensions.Options.Options.Create(ackOptions),
			CreateEnabledLogger());

		var auditEvent = CreateTestAuditEvent();
		handler.SetResponse(HttpStatusCode.OK, "{\"text\":\"Success\"}");

		// Act
		_ = await exporter.ExportAsync(auditEvent, CancellationToken.None);

		// Assert
		_ = handler.LastRequest.ShouldNotBeNull();
		handler.LastRequest.Headers.Contains("X-Splunk-Request-Channel").ShouldBeTrue();
		handler.LastRequest.Headers.GetValues("X-Splunk-Request-Channel").First().ShouldBe("test-channel-abc");
	}

	[Fact]
	public async Task ExportAsync_UsesEnvironmentMachineName_WhenHostIsNull()
	{
		// Arrange
		var noHostOptions = new SplunkExporterOptions
		{
			HecEndpoint = new Uri("https://splunk.example.com:8088/services/collector"),
			HecToken = "test-token-123",
			Host = null,
			MaxRetryAttempts = 0
		};

		var handler = new MockHttpMessageHandler();
		using var client = new HttpClient(handler);
		var exporter = new SplunkAuditExporter(
			client,
			Microsoft.Extensions.Options.Options.Create(noHostOptions),
			CreateEnabledLogger());

		var auditEvent = CreateTestAuditEvent();
		handler.SetResponse(HttpStatusCode.OK, "{\"text\":\"Success\"}");

		// Act
		_ = await exporter.ExportAsync(auditEvent, CancellationToken.None);

		// Assert
		_ = handler.LastRequestContent.ShouldNotBeNull();
		var json = JsonDocument.Parse(handler.LastRequestContent);
		var host = json.RootElement.GetProperty("host").GetString();
		host.ShouldBe(Environment.MachineName);
	}

	[Fact]
	public async Task ExportAsync_UsesDefaultSource_WhenSourceIsNull()
	{
		// Arrange
		var noSourceOptions = new SplunkExporterOptions
		{
			HecEndpoint = new Uri("https://splunk.example.com:8088/services/collector"),
			HecToken = "test-token-123",
			Source = null,
			MaxRetryAttempts = 0
		};

		var handler = new MockHttpMessageHandler();
		using var client = new HttpClient(handler);
		var exporter = new SplunkAuditExporter(
			client,
			Microsoft.Extensions.Options.Options.Create(noSourceOptions),
			CreateEnabledLogger());

		var auditEvent = CreateTestAuditEvent();
		handler.SetResponse(HttpStatusCode.OK, "{\"text\":\"Success\"}");

		// Act
		_ = await exporter.ExportAsync(auditEvent, CancellationToken.None);

		// Assert
		_ = handler.LastRequestContent.ShouldNotBeNull();
		var json = JsonDocument.Parse(handler.LastRequestContent);
		json.RootElement.GetProperty("source").GetString().ShouldBe("dispatch");
	}

	#endregion Splunk-Specific Feature Tests

	#region Health Check Edge Cases

	[Fact]
	public async Task CheckHealthAsync_ReturnsUnhealthy_WhenUnexpectedStatusCode()
	{
		// Arrange
		_mockHandler.SetResponse(HttpStatusCode.Forbidden, "Forbidden");

		// Act
		var result = await _sut.CheckHealthAsync(CancellationToken.None);

		// Assert
		result.IsHealthy.ShouldBeFalse();
		result.ErrorMessage.ShouldNotBeNullOrEmpty();
		result.ErrorMessage.ShouldContain("Unexpected status code");
	}

	[Fact]
	public async Task CheckHealthAsync_ReturnsHealthy_WhenMethodNotAllowed()
	{
		// Arrange - HEC may return 405 for GET
		_mockHandler.SetResponse(HttpStatusCode.MethodNotAllowed, "");

		// Act
		var result = await _sut.CheckHealthAsync(CancellationToken.None);

		// Assert
		result.IsHealthy.ShouldBeTrue();
	}

	[Fact]
	public async Task CheckHealthAsync_IncludesDefaultIndex_WhenIndexIsNull()
	{
		// Arrange
		var noIndexOptions = new SplunkExporterOptions
		{
			HecEndpoint = new Uri("https://splunk.example.com:8088/services/collector"),
			HecToken = "test-token-123",
			Index = null,
			MaxRetryAttempts = 0
		};

		var handler = new MockHttpMessageHandler();
		using var client = new HttpClient(handler);
		var exporter = new SplunkAuditExporter(
			client,
			Microsoft.Extensions.Options.Options.Create(noIndexOptions),
			CreateEnabledLogger());

		handler.SetResponse(HttpStatusCode.OK, "");

		// Act
		var result = await exporter.CheckHealthAsync(CancellationToken.None);

		// Assert
		result.Diagnostics.ShouldNotBeNull();
		result.Diagnostics["Index"].ShouldBe("(default)");
	}

	#endregion Health Check Edge Cases

	#region Name Property Tests

	[Fact]
	public void Name_ReturnsSplunk()
	{
		// Assert
		_sut.Name.ShouldBe("Splunk");
	}

	#endregion Name Property Tests

	#region Retry Logic Tests

	[Fact]
	public async Task ExportAsync_RetriesOnTransientFailure_ThenSucceeds()
	{
		// Arrange
		var retryHandler = new RetryMockHttpMessageHandler(
			new[] { HttpStatusCode.ServiceUnavailable },
			HttpStatusCode.OK);
		using var client = new HttpClient(retryHandler);

		var retryOptions = new SplunkExporterOptions
		{
			HecEndpoint = new Uri("https://splunk.example.com:8088/services/collector"),
			HecToken = "test-token",
			MaxRetryAttempts = 2,
			RetryBaseDelay = TimeSpan.FromMilliseconds(10)
		};

		var exporter = new SplunkAuditExporter(
			client,
			Microsoft.Extensions.Options.Options.Create(retryOptions),
			CreateEnabledLogger());

		var auditEvent = CreateTestAuditEvent();

		// Act
		var result = await exporter.ExportAsync(auditEvent, CancellationToken.None);

		// Assert
		result.Success.ShouldBeTrue();
		retryHandler.RequestCount.ShouldBe(2);
	}

	[Fact]
	public async Task ExportAsync_ReturnsFailure_WhenAllRetriesExhausted()
	{
		// Arrange
		var retryHandler = new RetryMockHttpMessageHandler(
			new[] { HttpStatusCode.ServiceUnavailable, HttpStatusCode.ServiceUnavailable, HttpStatusCode.ServiceUnavailable },
			HttpStatusCode.ServiceUnavailable);
		using var client = new HttpClient(retryHandler);

		var retryOptions = new SplunkExporterOptions
		{
			HecEndpoint = new Uri("https://splunk.example.com:8088/services/collector"),
			HecToken = "test-token",
			MaxRetryAttempts = 2,
			RetryBaseDelay = TimeSpan.FromMilliseconds(1)
		};

		var exporter = new SplunkAuditExporter(
			client,
			Microsoft.Extensions.Options.Options.Create(retryOptions),
			CreateEnabledLogger());

		var auditEvent = CreateTestAuditEvent();

		// Act
		var result = await exporter.ExportAsync(auditEvent, CancellationToken.None);

		// Assert
		result.Success.ShouldBeFalse();
		result.IsTransientError.ShouldBeTrue();
	}

	[Fact]
	public async Task ExportAsync_RetriesOnHttpRequestException_ThenSucceeds()
	{
		// Arrange
		var handler = new ExceptionThenSuccessHandler(
			exceptionsToThrow: 1,
			successCode: HttpStatusCode.OK);
		using var client = new HttpClient(handler);

		var retryOptions = new SplunkExporterOptions
		{
			HecEndpoint = new Uri("https://splunk.example.com:8088/services/collector"),
			HecToken = "test-token",
			MaxRetryAttempts = 2,
			RetryBaseDelay = TimeSpan.FromMilliseconds(1)
		};

		var exporter = new SplunkAuditExporter(
			client,
			Microsoft.Extensions.Options.Options.Create(retryOptions),
			CreateEnabledLogger());

		var auditEvent = CreateTestAuditEvent();

		// Act
		var result = await exporter.ExportAsync(auditEvent, CancellationToken.None);

		// Assert
		result.Success.ShouldBeTrue();
		handler.RequestCount.ShouldBe(2);
	}

	[Theory]
	[InlineData(HttpStatusCode.RequestTimeout)]
	[InlineData(HttpStatusCode.TooManyRequests)]
	[InlineData(HttpStatusCode.InternalServerError)]
	[InlineData(HttpStatusCode.BadGateway)]
	[InlineData(HttpStatusCode.GatewayTimeout)]
	public async Task ExportAsync_IdentifiesTransientStatusCode(HttpStatusCode statusCode)
	{
		// Arrange
		var auditEvent = CreateTestAuditEvent();
		_mockHandler.SetResponse(statusCode, "error");

		// Act
		var result = await _sut.ExportAsync(auditEvent, CancellationToken.None);

		// Assert
		result.Success.ShouldBeFalse();
		result.IsTransientError.ShouldBeTrue();
	}

	[Theory]
	[InlineData(HttpStatusCode.BadRequest)]
	[InlineData(HttpStatusCode.Unauthorized)]
	[InlineData(HttpStatusCode.NotFound)]
	public async Task ExportAsync_IdentifiesNonTransientStatusCode(HttpStatusCode statusCode)
	{
		// Arrange
		var auditEvent = CreateTestAuditEvent();
		_mockHandler.SetResponse(statusCode, "error");

		// Act
		var result = await _sut.ExportAsync(auditEvent, CancellationToken.None);

		// Assert
		result.Success.ShouldBeFalse();
		result.IsTransientError.ShouldBeFalse();
	}

	#endregion Retry Logic Tests

	#region Full Property Coverage Tests

	[Fact]
	public async Task ExportAsync_SerializesAllOptionalProperties()
	{
		// Arrange
		var auditEvent = new AuditEvent
		{
			EventId = "full-test",
			EventType = AuditEventType.Security,
			Action = "Login",
			Outcome = AuditOutcome.Failure,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "user-999",
			ActorType = "Service",
			ResourceId = "resource-abc",
			ResourceType = "Configuration",
			ResourceClassification = DataClassification.Restricted,
			TenantId = "tenant-full",
			CorrelationId = "corr-full",
			SessionId = "session-full",
			IpAddress = "192.168.1.1",
			UserAgent = "TestAgent/1.0",
			Reason = "Suspicious activity",
			Metadata = new Dictionary<string, string> { ["key1"] = "val1" },
			EventHash = "abc123hash"
		};
		_mockHandler.SetResponse(HttpStatusCode.OK, "{\"text\":\"Success\"}");

		// Act
		_ = await _sut.ExportAsync(auditEvent, CancellationToken.None);

		// Assert
		_ = _mockHandler.LastRequestContent.ShouldNotBeNull();
		var json = JsonDocument.Parse(_mockHandler.LastRequestContent);
		var evt = json.RootElement.GetProperty("event");
		evt.GetProperty("session_id").GetString().ShouldBe("session-full");
		evt.GetProperty("ip_address").GetString().ShouldBe("192.168.1.1");
		evt.GetProperty("user_agent").GetString().ShouldBe("TestAgent/1.0");
		evt.GetProperty("reason").GetString().ShouldBe("Suspicious activity");
		evt.GetProperty("event_hash").GetString().ShouldBe("abc123hash");
		evt.GetProperty("resource_classification").GetString().ShouldBe("Restricted");
	}

	#endregion Full Property Coverage Tests

	private static ILogger<SplunkAuditExporter> CreateEnabledLogger()
	{
		var factory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Debug));
		return factory.CreateLogger<SplunkAuditExporter>();
	}

	private static AuditEvent CreateTestAuditEvent(string? eventId = null)
	{
		return new AuditEvent
		{
			EventId = eventId ?? Guid.NewGuid().ToString(),
			EventType = AuditEventType.DataAccess,
			Action = "Read",
			Outcome = AuditOutcome.Success,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "user-123",
			ActorType = "User",
			ResourceId = "resource-456",
			ResourceType = "Customer",
			TenantId = "tenant-789",
			CorrelationId = "correlation-abc"
		};
	}

	/// <summary>
	/// Mock HTTP message handler for testing.
	/// </summary>
	private sealed class MockHttpMessageHandler : HttpMessageHandler
	{
		private HttpStatusCode _statusCode = HttpStatusCode.OK;
		private string _responseContent = "";
		private Exception? _exception;

		public HttpRequestMessage? LastRequest { get; private set; }
		public string? LastRequestContent { get; private set; }

		public void SetResponse(HttpStatusCode statusCode, string content)
		{
			_statusCode = statusCode;
			_responseContent = content;
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
			LastRequest = request;

			if (request.Content != null)
			{
				LastRequestContent = await request.Content.ReadAsStringAsync(cancellationToken);
			}

			if (_exception != null)
			{
				throw _exception;
			}

			return new HttpResponseMessage(_statusCode)
			{
				Content = new StringContent(_responseContent)
			};
		}
	}

	/// <summary>
	/// Mock handler that throws HttpRequestException N times then succeeds.
	/// </summary>
	private sealed class ExceptionThenSuccessHandler : HttpMessageHandler
	{
		private readonly int _exceptionsToThrow;
		private readonly HttpStatusCode _successCode;
		private int _requestIndex;

		public int RequestCount => _requestIndex;

		public ExceptionThenSuccessHandler(int exceptionsToThrow, HttpStatusCode successCode)
		{
			_exceptionsToThrow = exceptionsToThrow;
			_successCode = successCode;
		}

		protected override Task<HttpResponseMessage> SendAsync(
			HttpRequestMessage request,
			CancellationToken cancellationToken)
		{
			var index = _requestIndex++;
			if (index < _exceptionsToThrow)
			{
				throw new HttpRequestException("Connection refused");
			}

			return Task.FromResult(new HttpResponseMessage(_successCode)
			{
				Content = new StringContent("{}")
			});
		}
	}

	/// <summary>
	/// Mock handler that returns specified failure codes then a final success code.
	/// </summary>
	private sealed class RetryMockHttpMessageHandler : HttpMessageHandler
	{
		private readonly HttpStatusCode[] _failureCodes;
		private readonly HttpStatusCode _successCode;
		private int _requestIndex;

		public int RequestCount => _requestIndex;

		public RetryMockHttpMessageHandler(HttpStatusCode[] failureCodes, HttpStatusCode successCode)
		{
			_failureCodes = failureCodes;
			_successCode = successCode;
		}

		protected override Task<HttpResponseMessage> SendAsync(
			HttpRequestMessage request,
			CancellationToken cancellationToken)
		{
			var index = _requestIndex++;
			var statusCode = index < _failureCodes.Length ? _failureCodes[index] : _successCode;
			return Task.FromResult(new HttpResponseMessage(statusCode)
			{
				Content = new StringContent("{}")
			});
		}
	}
}
