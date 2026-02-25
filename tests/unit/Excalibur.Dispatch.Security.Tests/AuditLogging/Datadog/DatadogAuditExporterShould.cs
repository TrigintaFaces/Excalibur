// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json;

using Excalibur.Dispatch.AuditLogging.Datadog;

namespace Excalibur.Dispatch.Security.Tests.AuditLogging.Datadog;

/// <summary>
/// Unit tests for <see cref="DatadogAuditExporter"/>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class DatadogAuditExporterShould : IDisposable
{
	private readonly MockHttpMessageHandler _mockHandler;
	private readonly HttpClient _httpClient;
	private readonly DatadogAuditExporter _sut;
	private readonly DatadogExporterOptions _options;

	public DatadogAuditExporterShould()
	{
		_options = new DatadogExporterOptions
		{
			ApiKey = CreateNonSecretApiKey(),
			Site = "datadoghq.com",
			Service = "dispatch-audit",
			Source = "dispatch",
			MaxRetryAttempts = 1,
			RetryBaseDelay = TimeSpan.FromMilliseconds(10),
			UseCompression = false // Disable for easier testing
		};

		_mockHandler = new MockHttpMessageHandler();
		_httpClient = new HttpClient(_mockHandler);

		_sut = new DatadogAuditExporter(
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
			new DatadogAuditExporter(null!, options, logger));
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
			new DatadogAuditExporter(client, null!, logger));
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
			new DatadogAuditExporter(client, options, null!));
	}

	#endregion Constructor Tests

	#region ExportAsync Tests

	[Fact]
	public async Task ExportAsync_ReturnsSuccess_WhenApiAcceptsEvent()
	{
		// Arrange
		var auditEvent = CreateTestAuditEvent();
		_mockHandler.SetResponse(HttpStatusCode.Accepted, "{}");

		// Act
		var result = await _sut.ExportAsync(auditEvent, CancellationToken.None);

		// Assert
		result.Success.ShouldBeTrue();
		result.EventId.ShouldBe(auditEvent.EventId);
		result.ExportedAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddMinutes(-1));
	}

	[Fact]
	public async Task ExportAsync_ReturnsFailure_WhenApiRejectsEvent()
	{
		// Arrange
		var auditEvent = CreateTestAuditEvent();
		_mockHandler.SetResponse(HttpStatusCode.Forbidden, "{\"errors\":[\"Invalid API key\"]}");

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
	public async Task ExportAsync_IncludesApiKeyHeader()
	{
		// Arrange
		var auditEvent = CreateTestAuditEvent();
		_mockHandler.SetResponse(HttpStatusCode.Accepted, "{}");

		// Act
		_ = await _sut.ExportAsync(auditEvent, CancellationToken.None);

		// Assert
			_ = _mockHandler.LastRequest.ShouldNotBeNull();
			_mockHandler.LastRequest.Headers.Contains("DD-API-KEY").ShouldBeTrue();
			_mockHandler.LastRequest.Headers.GetValues("DD-API-KEY").First().ShouldBe(CreateNonSecretApiKey());
	}

	[Fact]
	public async Task ExportAsync_SendsCorrectJsonPayload()
	{
		// Arrange
		var auditEvent = CreateTestAuditEvent();
		_mockHandler.SetResponse(HttpStatusCode.Accepted, "{}");

		// Act
		_ = await _sut.ExportAsync(auditEvent, CancellationToken.None);

		// Assert
		_ = _mockHandler.LastRequestContent.ShouldNotBeNull();
		var jsonArray = JsonDocument.Parse(_mockHandler.LastRequestContent);
		jsonArray.RootElement.GetArrayLength().ShouldBe(1);
		var payload = jsonArray.RootElement[0];
		payload.GetProperty("service").GetString().ShouldBe("dispatch-audit");
		payload.GetProperty("source").GetString().ShouldBe("dispatch");
		payload.GetProperty("ddsource").GetString().ShouldBe("dispatch");
		payload.GetProperty("attributes").GetProperty("event_id").GetString().ShouldBe(auditEvent.EventId);
	}

	[Fact]
	public async Task ExportAsync_SendsToCorrectEndpoint()
	{
		// Arrange
		var auditEvent = CreateTestAuditEvent();
		_mockHandler.SetResponse(HttpStatusCode.Accepted, "{}");

		// Act
		_ = await _sut.ExportAsync(auditEvent, CancellationToken.None);

		// Assert
		_ = _mockHandler.LastRequest.ShouldNotBeNull();
		_ = _mockHandler.LastRequest.RequestUri.ShouldNotBeNull();
		_mockHandler.LastRequest.RequestUri.Host.ShouldBe("http-intake.logs.datadoghq.com");
		_mockHandler.LastRequest.RequestUri.PathAndQuery.ShouldContain("api/v2/logs");
	}

	[Fact]
	public async Task ExportAsync_IncludesTags()
	{
		// Arrange
		var auditEvent = CreateTestAuditEvent();
		_mockHandler.SetResponse(HttpStatusCode.Accepted, "{}");

		// Act
		_ = await _sut.ExportAsync(auditEvent, CancellationToken.None);

		// Assert
		_ = _mockHandler.LastRequestContent.ShouldNotBeNull();
		var jsonArray = JsonDocument.Parse(_mockHandler.LastRequestContent);
		var payload = jsonArray.RootElement[0];
		var ddtags = payload.GetProperty("ddtags").GetString();
		_ = ddtags.ShouldNotBeNull();
		ddtags.ShouldContain("event_type:DataAccess");
		ddtags.ShouldContain("outcome:Success");
		ddtags.ShouldContain("action:Read");
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
		_mockHandler.SetResponse(HttpStatusCode.Accepted, "{}");

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
		_mockHandler.SetResponse(HttpStatusCode.Accepted, "{}");

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
		_mockHandler.SetResponse(HttpStatusCode.Accepted, "{}");

		// Act
		var result = await _sut.CheckHealthAsync(CancellationToken.None);

		// Assert
		result.IsHealthy.ShouldBeTrue();
		result.ExporterName.ShouldBe("Datadog");
		result.Endpoint.ShouldContain("datadoghq.com");
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
		_mockHandler.SetResponse(HttpStatusCode.Accepted, "{}");

		// Act
		var result = await _sut.CheckHealthAsync(CancellationToken.None);

		// Assert
		_ = result.Diagnostics.ShouldNotBeNull();
		result.Diagnostics["Site"].ShouldBe("datadoghq.com");
		result.Diagnostics["Service"].ShouldBe("dispatch-audit");
		result.Diagnostics["Source"].ShouldBe("dispatch");
	}

	#endregion CheckHealthAsync Tests

	#region Compression Tests

	[Fact]
	public async Task ExportAsync_UsesGzipCompression_WhenEnabled()
	{
		// Arrange
			var compressionOptions = new DatadogExporterOptions
			{
				ApiKey = CreateNonSecretApiKey(),
				UseCompression = true,
				MaxRetryAttempts = 0
		};

		var compressedHandler = new MockHttpMessageHandler();
		using var compressedClient = new HttpClient(compressedHandler);
		var compressedExporter = new DatadogAuditExporter(
			compressedClient,
			Microsoft.Extensions.Options.Options.Create(compressionOptions),
			CreateEnabledLogger());

		var auditEvent = CreateTestAuditEvent();
		compressedHandler.SetResponse(HttpStatusCode.Accepted, "{}");

		// Act
		_ = await compressedExporter.ExportAsync(auditEvent, CancellationToken.None);

		// Assert
		_ = compressedHandler.LastRequest.ShouldNotBeNull();
		_ = compressedHandler.LastRequest.Content.ShouldNotBeNull();
		compressedHandler.LastRequest.Content.Headers.ContentEncoding
			.ShouldContain("gzip");

		// Verify we can decompress the content
		var compressedBytes = compressedHandler.LastRequestBytes;
		_ = compressedBytes.ShouldNotBeNull();
		var decompressed = DecompressGzip(compressedBytes);
		var jsonArray = JsonDocument.Parse(decompressed);
		jsonArray.RootElement.GetArrayLength().ShouldBe(1);
	}

	#endregion Compression Tests

	#region Name Property Tests

	[Fact]
	public void Name_ReturnsDatadog()
	{
		// Assert
		_sut.Name.ShouldBe("Datadog");
	}

	#endregion Name Property Tests

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
		_mockHandler.SetResponse(HttpStatusCode.Forbidden, "Invalid API key");

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

	#region Tag and Classification Tests

	[Fact]
	public async Task ExportAsync_IncludesResourceClassificationTag()
	{
		// Arrange
		var auditEvent = CreateTestAuditEvent() with { ResourceClassification = DataClassification.Confidential };
		_mockHandler.SetResponse(HttpStatusCode.Accepted, "{}");

		// Act
		_ = await _sut.ExportAsync(auditEvent, CancellationToken.None);

		// Assert
		_ = _mockHandler.LastRequestContent.ShouldNotBeNull();
		var jsonArray = JsonDocument.Parse(_mockHandler.LastRequestContent);
		var payload = jsonArray.RootElement[0];
		var ddtags = payload.GetProperty("ddtags").GetString();
		_ = ddtags.ShouldNotBeNull();
		ddtags.ShouldContain("classification:");
	}

	[Fact]
	public async Task ExportAsync_IncludesCustomTags_WhenConfigured()
	{
		// Arrange
			var optionsWithTags = new DatadogExporterOptions
			{
				ApiKey = CreateNonSecretApiKey(),
				Tags = "env:test,team:security",
				MaxRetryAttempts = 0
		};

		var handler = new MockHttpMessageHandler();
		using var client = new HttpClient(handler);
		var exporter = new DatadogAuditExporter(
			client,
			Microsoft.Extensions.Options.Options.Create(optionsWithTags),
			CreateEnabledLogger());

		var auditEvent = CreateTestAuditEvent();
		handler.SetResponse(HttpStatusCode.Accepted, "{}");

		// Act
		_ = await exporter.ExportAsync(auditEvent, CancellationToken.None);

		// Assert
		_ = handler.LastRequestContent.ShouldNotBeNull();
		var jsonArray = JsonDocument.Parse(handler.LastRequestContent);
		var payload = jsonArray.RootElement[0];
		var ddtags = payload.GetProperty("ddtags").GetString();
		_ = ddtags.ShouldNotBeNull();
		ddtags.ShouldContain("env:test,team:security");
	}

	[Fact]
	public async Task ExportAsync_ExcludesTenantTag_WhenTenantIdIsNull()
	{
		// Arrange
		var auditEvent = CreateTestAuditEvent() with { TenantId = null };
		_mockHandler.SetResponse(HttpStatusCode.Accepted, "{}");

		// Act
		_ = await _sut.ExportAsync(auditEvent, CancellationToken.None);

		// Assert
		_ = _mockHandler.LastRequestContent.ShouldNotBeNull();
		var jsonArray = JsonDocument.Parse(_mockHandler.LastRequestContent);
		var payload = jsonArray.RootElement[0];
		var ddtags = payload.GetProperty("ddtags").GetString();
		_ = ddtags.ShouldNotBeNull();
		ddtags.ShouldNotContain("tenant:");
	}

	#endregion Tag and Classification Tests

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

	#region Retry Logic Tests

	[Fact]
	public async Task ExportAsync_RetriesOnTransientFailure_ThenSucceeds()
	{
		// Arrange - Use a handler that fails once then succeeds
		var retryHandler = new RetryMockHttpMessageHandler(
			new[] { HttpStatusCode.ServiceUnavailable },
			HttpStatusCode.Accepted);
		using var client = new HttpClient(retryHandler);

			var retryOptions = new DatadogExporterOptions
			{
				ApiKey = CreateNonSecretApiKey(),
				MaxRetryAttempts = 2,
				RetryBaseDelay = TimeSpan.FromMilliseconds(10)
		};

		var exporter = new DatadogAuditExporter(
			client,
			Microsoft.Extensions.Options.Options.Create(retryOptions),
			CreateEnabledLogger());

		var auditEvent = CreateTestAuditEvent();

		// Act
		var result = await exporter.ExportAsync(auditEvent, CancellationToken.None);

		// Assert
		result.Success.ShouldBeTrue();
		retryHandler.RequestCount.ShouldBe(2); // 1 failure + 1 success
	}

	[Fact]
	public async Task ExportAsync_ReturnsFailure_WhenAllRetriesExhausted()
	{
		// Arrange - all attempts fail with transient status
		var retryHandler = new RetryMockHttpMessageHandler(
			new[] { HttpStatusCode.ServiceUnavailable, HttpStatusCode.ServiceUnavailable, HttpStatusCode.ServiceUnavailable },
			HttpStatusCode.ServiceUnavailable);
		using var client = new HttpClient(retryHandler);

			var retryOptions = new DatadogExporterOptions
			{
				ApiKey = CreateNonSecretApiKey(),
				MaxRetryAttempts = 2,
				RetryBaseDelay = TimeSpan.FromMilliseconds(1)
		};

		var exporter = new DatadogAuditExporter(
			client,
			Microsoft.Extensions.Options.Options.Create(retryOptions),
			CreateEnabledLogger());

		var auditEvent = CreateTestAuditEvent();

		// Act
		var result = await exporter.ExportAsync(auditEvent, CancellationToken.None);

		// Assert - should return the last transient failure
		result.Success.ShouldBeFalse();
		result.IsTransientError.ShouldBeTrue();
	}

	[Fact]
	public async Task ExportAsync_RetriesOnHttpRequestException_ThenSucceeds()
	{
		// Arrange - handler throws HttpRequestException once then succeeds
		var handler = new ExceptionThenSuccessHandler(
			exceptionsToThrow: 1,
			successCode: HttpStatusCode.Accepted);
		using var client = new HttpClient(handler);

			var retryOptions = new DatadogExporterOptions
			{
				ApiKey = CreateNonSecretApiKey(),
				MaxRetryAttempts = 2,
				RetryBaseDelay = TimeSpan.FromMilliseconds(1)
		};

		var exporter = new DatadogAuditExporter(
			client,
			Microsoft.Extensions.Options.Options.Create(retryOptions),
			CreateEnabledLogger());

		var auditEvent = CreateTestAuditEvent();

		// Act
		var result = await exporter.ExportAsync(auditEvent, CancellationToken.None);

		// Assert
		result.Success.ShouldBeTrue();
		handler.RequestCount.ShouldBe(2); // 1 exception + 1 success
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
		// Arrange - create event with ALL optional properties set
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
			Metadata = new Dictionary<string, string> { ["key1"] = "val1", ["key2"] = "val2" },
			EventHash = "abc123hash"
		};
		_mockHandler.SetResponse(HttpStatusCode.Accepted, "{}");

		// Act
		_ = await _sut.ExportAsync(auditEvent, CancellationToken.None);

		// Assert
		_ = _mockHandler.LastRequestContent.ShouldNotBeNull();
		var jsonArray = JsonDocument.Parse(_mockHandler.LastRequestContent);
		var payload = jsonArray.RootElement[0];
		var attrs = payload.GetProperty("attributes");
		attrs.GetProperty("session_id").GetString().ShouldBe("session-full");
		attrs.GetProperty("ip_address").GetString().ShouldBe("192.168.1.1");
		attrs.GetProperty("user_agent").GetString().ShouldBe("TestAgent/1.0");
		attrs.GetProperty("reason").GetString().ShouldBe("Suspicious activity");
		attrs.GetProperty("event_hash").GetString().ShouldBe("abc123hash");
		attrs.GetProperty("resource_classification").GetString().ShouldBe("Restricted");
	}

	[Fact]
	public async Task ExportBatchAsync_SerializesAllEventsWithAllProperties()
	{
		// Arrange
		var events = new List<AuditEvent>
		{
			new AuditEvent
			{
				EventId = "batch-full-1",
				EventType = AuditEventType.ConfigurationChange,
				Action = "Update",
				Outcome = AuditOutcome.Success,
				Timestamp = DateTimeOffset.UtcNow,
				ActorId = "admin",
				SessionId = "sess-1",
				IpAddress = "10.0.0.1",
				Metadata = new Dictionary<string, string> { ["env"] = "prod" }
			},
			new AuditEvent
			{
				EventId = "batch-full-2",
				EventType = AuditEventType.Authentication,
				Action = "Logout",
				Outcome = AuditOutcome.Success,
				Timestamp = DateTimeOffset.UtcNow,
				ActorId = "user-1",
				UserAgent = "Chrome/120"
			}
		};
		_mockHandler.SetResponse(HttpStatusCode.Accepted, "{}");

		// Act
		var result = await _sut.ExportBatchAsync(events, CancellationToken.None);

		// Assert
		result.AllSucceeded.ShouldBeTrue();
		result.TotalCount.ShouldBe(2);
	}

	[Fact]
	public async Task ExportAsync_UsesCustomHostname_WhenConfigured()
	{
		// Arrange
			var optionsWithHostname = new DatadogExporterOptions
			{
				ApiKey = CreateNonSecretApiKey(),
				Hostname = "custom-host.example.com",
				MaxRetryAttempts = 0
		};

		var handler = new MockHttpMessageHandler();
		using var client = new HttpClient(handler);
		var exporter = new DatadogAuditExporter(
			client,
			Microsoft.Extensions.Options.Options.Create(optionsWithHostname),
			CreateEnabledLogger());

		var auditEvent = CreateTestAuditEvent();
		handler.SetResponse(HttpStatusCode.Accepted, "{}");

		// Act
		_ = await exporter.ExportAsync(auditEvent, CancellationToken.None);

		// Assert
		_ = handler.LastRequestContent.ShouldNotBeNull();
		var jsonArray = JsonDocument.Parse(handler.LastRequestContent);
		var payload = jsonArray.RootElement[0];
		payload.GetProperty("hostname").GetString().ShouldBe("custom-host.example.com");
	}

	[Fact]
	public async Task ExportAsync_UsesMachineName_WhenHostnameNotConfigured()
	{
		// Arrange - default options have no hostname set
		var auditEvent = CreateTestAuditEvent();
		_mockHandler.SetResponse(HttpStatusCode.Accepted, "{}");

		// Act
		_ = await _sut.ExportAsync(auditEvent, CancellationToken.None);

		// Assert
		_ = _mockHandler.LastRequestContent.ShouldNotBeNull();
		var jsonArray = JsonDocument.Parse(_mockHandler.LastRequestContent);
		var payload = jsonArray.RootElement[0];
		payload.GetProperty("hostname").GetString().ShouldBe(Environment.MachineName);
	}

	#endregion Full Property Coverage Tests

	private static string DecompressGzip(byte[] compressedData)
	{
		using var input = new MemoryStream(compressedData);
		using var gzip = new GZipStream(input, CompressionMode.Decompress);
		using var reader = new StreamReader(gzip, Encoding.UTF8);
		return reader.ReadToEnd();
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

	private static string CreateNonSecretApiKey()
	{
		return string.Concat("datadog-", "fixture-", "key");
	}

	private static ILogger<DatadogAuditExporter> CreateEnabledLogger()
	{
		var factory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Debug));
		return factory.CreateLogger<DatadogAuditExporter>();
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
		public byte[]? LastRequestBytes { get; private set; }

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
				LastRequestBytes = await request.Content.ReadAsByteArrayAsync(cancellationToken);

				// Check if content is gzip compressed
				if (request.Content.Headers.ContentEncoding.Contains("gzip"))
				{
					// Decompress for inspection
					using var input = new MemoryStream(LastRequestBytes);
					using var gzip = new GZipStream(input, CompressionMode.Decompress);
					using var reader = new StreamReader(gzip, Encoding.UTF8);
					LastRequestContent = await reader.ReadToEndAsync(cancellationToken);
				}
				else
				{
					LastRequestContent = Encoding.UTF8.GetString(LastRequestBytes);
				}
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
