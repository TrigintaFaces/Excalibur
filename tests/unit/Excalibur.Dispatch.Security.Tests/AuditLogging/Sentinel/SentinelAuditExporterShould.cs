// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Net;
using System.Text.Json;

using Excalibur.Dispatch.AuditLogging.Sentinel;

namespace Excalibur.Dispatch.Security.Tests.AuditLogging.Sentinel;

/// <summary>
/// Unit tests for <see cref="SentinelAuditExporter"/>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class SentinelAuditExporterShould : IDisposable
{
	private readonly MockHttpMessageHandler _mockHandler;
	private readonly HttpClient _httpClient;
	private readonly SentinelAuditExporter _sut;
	private readonly SentinelExporterOptions _options;

	public SentinelAuditExporterShould()
	{
		_options = new SentinelExporterOptions
		{
			WorkspaceId = "test-workspace-id-12345",
			SharedKey = Convert.ToBase64String(new byte[32]), // Valid base64 key
			LogType = "DispatchAudit",
			MaxRetryAttempts = 1,
			RetryBaseDelay = TimeSpan.FromMilliseconds(10)
		};

		_mockHandler = new MockHttpMessageHandler();
		_httpClient = new HttpClient(_mockHandler);

		_sut = new SentinelAuditExporter(
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
			new SentinelAuditExporter(null!, options, logger));
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
			new SentinelAuditExporter(client, null!, logger));
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
			new SentinelAuditExporter(client, options, null!));
	}

	#endregion Constructor Tests

	#region ExportAsync Tests

	[Fact]
	public async Task ExportAsync_ReturnsSuccess_WhenApiAcceptsEvent()
	{
		// Arrange
		var auditEvent = CreateTestAuditEvent();
		_mockHandler.SetResponse(HttpStatusCode.OK, "");
		var lowerBound = DateTimeOffset.UtcNow;

		// Act
		var result = await _sut.ExportAsync(auditEvent, CancellationToken.None);
		var upperBound = DateTimeOffset.UtcNow;

		// Assert
		result.Success.ShouldBeTrue();
		result.EventId.ShouldBe(auditEvent.EventId);
		result.ExportedAt.ShouldBeGreaterThanOrEqualTo(lowerBound);
		result.ExportedAt.ShouldBeLessThanOrEqualTo(upperBound);
	}

	[Fact]
	public async Task ExportAsync_ReturnsFailure_WhenApiRejectsEvent()
	{
		// Arrange
		var auditEvent = CreateTestAuditEvent();
		_mockHandler.SetResponse(HttpStatusCode.Forbidden, "Invalid authorization");

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
	public async Task ExportAsync_IncludesRequiredHeaders()
	{
		// Arrange
		var auditEvent = CreateTestAuditEvent();
		_mockHandler.SetResponse(HttpStatusCode.OK, "");

		// Act
		_ = await _sut.ExportAsync(auditEvent, CancellationToken.None);

		// Assert
		_ = _mockHandler.LastRequest.ShouldNotBeNull();
		_mockHandler.LastRequest.Headers.Contains("Authorization").ShouldBeTrue();
		_mockHandler.LastRequest.Headers.Contains("Log-Type").ShouldBeTrue();
		_mockHandler.LastRequest.Headers.Contains("x-ms-date").ShouldBeTrue();
		_mockHandler.LastRequest.Headers.Contains("time-generated-field").ShouldBeTrue();
	}

	[Fact]
	public async Task ExportAsync_IncludesSharedKeyAuthorization()
	{
		// Arrange
		var auditEvent = CreateTestAuditEvent();
		_mockHandler.SetResponse(HttpStatusCode.OK, "");

		// Act
		_ = await _sut.ExportAsync(auditEvent, CancellationToken.None);

		// Assert
		_ = _mockHandler.LastRequest.ShouldNotBeNull();
		var authHeader = _mockHandler.LastRequest.Headers.GetValues("Authorization").First();
		authHeader.ShouldStartWith("SharedKey test-workspace-id-12345:");
	}

	[Fact]
	public async Task ExportAsync_SendsCorrectJsonPayload()
	{
		// Arrange
		var auditEvent = CreateTestAuditEvent();
		_mockHandler.SetResponse(HttpStatusCode.OK, "");

		// Act
		_ = await _sut.ExportAsync(auditEvent, CancellationToken.None);

		// Assert
		_ = _mockHandler.LastRequestContent.ShouldNotBeNull();
		var jsonArray = JsonDocument.Parse(_mockHandler.LastRequestContent);
		jsonArray.RootElement.GetArrayLength().ShouldBe(1);
		var payload = jsonArray.RootElement[0];
		payload.GetProperty("event_id").GetString().ShouldBe(auditEvent.EventId);
		payload.GetProperty("event_type").GetString().ShouldBe("DataAccess");
		payload.GetProperty("action").GetString().ShouldBe("Read");
	}

	[Fact]
	public async Task ExportAsync_SendsToCorrectEndpoint()
	{
		// Arrange
		var auditEvent = CreateTestAuditEvent();
		_mockHandler.SetResponse(HttpStatusCode.OK, "");

		// Act
		_ = await _sut.ExportAsync(auditEvent, CancellationToken.None);

		// Assert
		_ = _mockHandler.LastRequest.ShouldNotBeNull();
		_ = _mockHandler.LastRequest.RequestUri.ShouldNotBeNull();
		_mockHandler.LastRequest.RequestUri.Host.ShouldBe($"{_options.WorkspaceId}.ods.opinsights.azure.com");
		_mockHandler.LastRequest.RequestUri.PathAndQuery.ShouldContain("api/logs");
	}

	#endregion ExportAsync Tests

	#region ExportBatchAsync Tests

	[Fact]
	public async Task ExportBatchAsync_ReturnsAllSuccess_WhenApiAcceptsBatch()
	{
		// Arrange
		var events = new List<AuditEvent>
		{
			CreateTestAuditEvent("event-1"),
			CreateTestAuditEvent("event-2"),
			CreateTestAuditEvent("event-3")
		};
		_mockHandler.SetResponse(HttpStatusCode.OK, "");

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
	public async Task ExportBatchAsync_SendsJsonArray()
	{
		// Arrange
		var events = new List<AuditEvent>
		{
			CreateTestAuditEvent("event-1"),
			CreateTestAuditEvent("event-2")
		};
		_mockHandler.SetResponse(HttpStatusCode.OK, "");

		// Act
		_ = await _sut.ExportBatchAsync(events, CancellationToken.None);

		// Assert
		_ = _mockHandler.LastRequestContent.ShouldNotBeNull();
		var jsonArray = JsonDocument.Parse(_mockHandler.LastRequestContent);
		jsonArray.RootElement.ValueKind.ShouldBe(JsonValueKind.Array);
		jsonArray.RootElement.GetArrayLength().ShouldBe(2);
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
		result.ExporterName.ShouldBe("AzureSentinel");
		result.Endpoint.ShouldContain("opinsights.azure.com");
		_ = result.LatencyMs.ShouldNotBeNull();
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
		result.Diagnostics["LogType"].ShouldBe("DispatchAudit");
		result.Diagnostics["WorkspaceId"].ShouldContain("...");
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
		_mockHandler.SetResponse(HttpStatusCode.Forbidden, "Invalid authorization");

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

	#region Header Tests

	[Fact]
	public async Task ExportAsync_OmitsOptionalHeaders_WhenNotConfigured()
	{
		// Arrange
		var optionsNoOptional = new SentinelExporterOptions
		{
			WorkspaceId = "test-workspace-id-12345",
			SharedKey = Convert.ToBase64String(new byte[32]),
			LogType = "DispatchAudit",
			TimeGeneratedField = null,
			AzureResourceId = null,
			MaxRetryAttempts = 0
		};

		var handler = new MockHttpMessageHandler();
		using var client = new HttpClient(handler);
		var exporter = new SentinelAuditExporter(
			client,
			Microsoft.Extensions.Options.Options.Create(optionsNoOptional),
			CreateEnabledLogger());

		var auditEvent = CreateTestAuditEvent();
		handler.SetResponse(HttpStatusCode.OK, "");

		// Act
		_ = await exporter.ExportAsync(auditEvent, CancellationToken.None);

		// Assert
		_ = handler.LastRequest.ShouldNotBeNull();
		handler.LastRequest.Headers.Contains("time-generated-field").ShouldBeFalse();
		handler.LastRequest.Headers.Contains("x-ms-AzureResourceId").ShouldBeFalse();
	}

	[Fact]
	public async Task ExportAsync_IncludesAzureResourceIdHeader_WhenConfigured()
	{
		// Arrange
		var optionsWithResource = new SentinelExporterOptions
		{
			WorkspaceId = "test-workspace-id-12345",
			SharedKey = Convert.ToBase64String(new byte[32]),
			LogType = "DispatchAudit",
			AzureResourceId = "/subscriptions/sub-123/resourceGroups/rg/providers/ns/type/name",
			MaxRetryAttempts = 0
		};

		var handler = new MockHttpMessageHandler();
		using var client = new HttpClient(handler);
		var exporter = new SentinelAuditExporter(
			client,
			Microsoft.Extensions.Options.Options.Create(optionsWithResource),
			CreateEnabledLogger());

		var auditEvent = CreateTestAuditEvent();
		handler.SetResponse(HttpStatusCode.OK, "");

		// Act
		_ = await exporter.ExportAsync(auditEvent, CancellationToken.None);

		// Assert
		_ = handler.LastRequest.ShouldNotBeNull();
		handler.LastRequest.Headers.Contains("x-ms-AzureResourceId").ShouldBeTrue();
	}

	#endregion Header Tests

	#region Health Check Edge Cases

	[Fact]
	public async Task CheckHealthAsync_ReturnsUnhealthy_WhenNonSuccessStatusCode()
	{
		// Arrange
		_mockHandler.SetResponse(HttpStatusCode.Forbidden, "Forbidden");

		// Act
		var result = await _sut.CheckHealthAsync(CancellationToken.None);

		// Assert
		result.IsHealthy.ShouldBeFalse();
		result.ErrorMessage.ShouldNotBeNullOrEmpty();
		result.ErrorMessage.ShouldContain("Unexpected status code");
		result.Diagnostics.ShouldNotBeNull();
	}

	#endregion Health Check Edge Cases

	#region Payload Content Tests

	[Fact]
	public async Task ExportAsync_IncludesResourceClassification_WhenSet()
	{
		// Arrange
		var auditEvent = CreateTestAuditEvent() with { ResourceClassification = DataClassification.Confidential };
		_mockHandler.SetResponse(HttpStatusCode.OK, "");

		// Act
		_ = await _sut.ExportAsync(auditEvent, CancellationToken.None);

		// Assert
		_ = _mockHandler.LastRequestContent.ShouldNotBeNull();
		var jsonArray = JsonDocument.Parse(_mockHandler.LastRequestContent);
		var payload = jsonArray.RootElement[0];
		payload.GetProperty("resource_classification").GetString().ShouldNotBeNullOrEmpty();
	}

	#endregion Payload Content Tests

	#region Name Property Tests

	[Fact]
	public void Name_ReturnsAzureSentinel()
	{
		// Assert
		_sut.Name.ShouldBe("AzureSentinel");
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

		var retryOptions = new SentinelExporterOptions
		{
			WorkspaceId = "test-workspace-id-12345",
			SharedKey = Convert.ToBase64String(new byte[32]),
			MaxRetryAttempts = 2,
			RetryBaseDelay = TimeSpan.FromMilliseconds(10)
		};

		var exporter = new SentinelAuditExporter(
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

		var retryOptions = new SentinelExporterOptions
		{
			WorkspaceId = "test-workspace-id-12345",
			SharedKey = Convert.ToBase64String(new byte[32]),
			MaxRetryAttempts = 2,
			RetryBaseDelay = TimeSpan.FromMilliseconds(1)
		};

		var exporter = new SentinelAuditExporter(
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

		var retryOptions = new SentinelExporterOptions
		{
			WorkspaceId = "test-workspace-id-12345",
			SharedKey = Convert.ToBase64String(new byte[32]),
			MaxRetryAttempts = 2,
			RetryBaseDelay = TimeSpan.FromMilliseconds(1)
		};

		var exporter = new SentinelAuditExporter(
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
		_mockHandler.SetResponse(HttpStatusCode.OK, "");

		// Act
		_ = await _sut.ExportAsync(auditEvent, CancellationToken.None);

		// Assert
		_ = _mockHandler.LastRequestContent.ShouldNotBeNull();
		var jsonArray = JsonDocument.Parse(_mockHandler.LastRequestContent);
		var payload = jsonArray.RootElement[0];
		payload.GetProperty("session_id").GetString().ShouldBe("session-full");
		payload.GetProperty("ip_address").GetString().ShouldBe("192.168.1.1");
		payload.GetProperty("user_agent").GetString().ShouldBe("TestAgent/1.0");
		payload.GetProperty("reason").GetString().ShouldBe("Suspicious activity");
		payload.GetProperty("event_hash").GetString().ShouldBe("abc123hash");
		payload.GetProperty("resource_classification").GetString().ShouldBe("Restricted");
	}

	#endregion Full Property Coverage Tests

	private static ILogger<SentinelAuditExporter> CreateEnabledLogger()
	{
		var factory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Debug));
		return factory.CreateLogger<SentinelAuditExporter>();
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
