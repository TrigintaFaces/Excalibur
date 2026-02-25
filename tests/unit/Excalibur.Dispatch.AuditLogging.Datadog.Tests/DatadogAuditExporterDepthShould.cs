// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;
using System.Net;

using Excalibur.Dispatch.AuditLogging.Datadog;
using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.AuditLogging.Datadog.Tests;

/// <summary>
/// Depth coverage tests for <see cref="DatadogAuditExporter"/> covering
/// classification tag inclusion, hostname fallback, URI construction,
/// DataAnnotations validation, and batch exception paths.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class DatadogAuditExporterDepthShould : IDisposable
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

	private readonly NullLogger<DatadogAuditExporter> _logger = NullLogger<DatadogAuditExporter>.Instance;
	private readonly FakeHttpMessageHandler _handler = new();

	[Fact]
	public void ImplementIAuditLogExporter()
	{
		var sut = CreateExporter();
		sut.ShouldBeAssignableTo<IAuditLogExporter>();
	}

	[Fact]
	public void OptionsApiKey_HaveRequiredAttribute()
	{
		var prop = typeof(DatadogExporterOptions).GetProperty(nameof(DatadogExporterOptions.ApiKey));
		prop.ShouldNotBeNull();
		prop!.GetCustomAttributes(typeof(RequiredAttribute), false).ShouldNotBeEmpty();
	}

	[Fact]
	public async Task IncludeClassificationTag_WhenResourceClassificationPresent()
	{
		// Arrange
		_handler.SetResponse(HttpStatusCode.OK);
		_handler.CaptureContent = true;
		var sut = CreateExporter();
		var auditEvent = CreateAuditEvent() with
		{
			ResourceClassification = DataClassification.Restricted
		};

		// Act
		await sut.ExportAsync(auditEvent, CancellationToken.None);

		// Assert
		_handler.CapturedContent.ShouldNotBeNull();
		_handler.CapturedContent.ShouldContain("classification:Restricted");
	}

	[Fact]
	public async Task ExcludeClassificationTag_WhenNull()
	{
		// Arrange
		_handler.SetResponse(HttpStatusCode.OK);
		_handler.CaptureContent = true;
		var sut = CreateExporter();
		var auditEvent = CreateAuditEvent() with { ResourceClassification = null };

		// Act
		await sut.ExportAsync(auditEvent, CancellationToken.None);

		// Assert
		_handler.CapturedContent.ShouldNotBeNull();
		_handler.CapturedContent.ShouldNotContain("classification:");
	}

	[Fact]
	public async Task UseCustomSite_InApiUri()
	{
		// Arrange
		_options.Site = "us5.datadoghq.com";
		_handler.SetResponse(HttpStatusCode.OK);
		var sut = CreateExporter();

		// Act
		await sut.ExportAsync(CreateAuditEvent(), CancellationToken.None);

		// Assert
		_handler.LastRequest.ShouldNotBeNull();
		_handler.LastRequest!.RequestUri!.Host.ShouldContain("us5.datadoghq.com");
	}

	[Fact]
	public async Task UseHostnameFallback_WhenNotConfigured()
	{
		// Arrange
		_options.Hostname = null;
		_handler.SetResponse(HttpStatusCode.OK);
		_handler.CaptureContent = true;
		var sut = CreateExporter();

		// Act
		await sut.ExportAsync(CreateAuditEvent(), CancellationToken.None);

		// Assert — hostname should fall back to Environment.MachineName
		_handler.CapturedContent.ShouldNotBeNull();
		_handler.CapturedContent.ShouldContain(Environment.MachineName);
	}

	[Fact]
	public async Task UseConfiguredHostname_WhenSet()
	{
		// Arrange
		_options.Hostname = "custom-host-123";
		_handler.SetResponse(HttpStatusCode.OK);
		_handler.CaptureContent = true;
		var sut = CreateExporter();

		// Act
		await sut.ExportAsync(CreateAuditEvent(), CancellationToken.None);

		// Assert
		_handler.CapturedContent.ShouldNotBeNull();
		_handler.CapturedContent.ShouldContain("custom-host-123");
	}

	[Fact]
	public async Task ExportBatch_TrackErrors_WhenChunkThrowsException()
	{
		// Arrange
		_options.MaxBatchSize = 1;
		_handler.SetException(new HttpRequestException("Network failure"));
		var sut = CreateExporter();
		var events = new List<AuditEvent>
		{
			CreateAuditEvent("evt-1"),
			CreateAuditEvent("evt-2")
		};

		// Act
		var result = await sut.ExportBatchAsync(events, CancellationToken.None);

		// Assert
		result.FailedCount.ShouldBe(2);
		result.Errors.ShouldNotBeNull();
		result.Errors!["evt-1"].ShouldContain("Network failure");
	}

	[Fact]
	public async Task CheckHealth_IncludeSiteInDiagnostics()
	{
		// Arrange
		_handler.SetResponse(HttpStatusCode.OK);
		var sut = CreateExporter();

		// Act
		var result = await sut.CheckHealthAsync(CancellationToken.None);

		// Assert
		result.Diagnostics.ShouldNotBeNull();
		result.Diagnostics!["Site"].ShouldBe("datadoghq.com");
		result.Diagnostics["Source"].ShouldBe("test-source");
	}

	[Fact]
	public async Task CheckHealth_ReturnEndpoint_WithApiHost()
	{
		// Arrange
		_handler.SetResponse(HttpStatusCode.OK);
		var sut = CreateExporter();

		// Act
		var result = await sut.CheckHealthAsync(CancellationToken.None);

		// Assert
		result.Endpoint.ShouldContain("datadoghq.com");
	}

	[Fact]
	public async Task ExportAsync_SetExportedAt_OnFailure()
	{
		// Arrange
		_handler.SetResponse(HttpStatusCode.BadRequest, "bad");
		var sut = CreateExporter();
		var before = DateTimeOffset.UtcNow;

		// Act
		var result = await sut.ExportAsync(CreateAuditEvent(), CancellationToken.None);

		// Assert
		result.ExportedAt.ShouldBeGreaterThanOrEqualTo(before);
	}

	[Fact]
	public async Task ExportBatch_ReturnCorrectTotalCount_WithMultipleChunks()
	{
		// Arrange — 5 events with batch size 2 = 3 chunks
		_options.MaxBatchSize = 2;
		_handler.SetResponse(HttpStatusCode.OK);
		var sut = CreateExporter();
		var events = Enumerable.Range(1, 5)
			.Select(i => CreateAuditEvent($"evt-{i}"))
			.ToList();

		// Act
		var result = await sut.ExportBatchAsync(events, CancellationToken.None);

		// Assert
		result.TotalCount.ShouldBe(5);
		result.SuccessCount.ShouldBe(5);
	}

	[Fact]
	public async Task SendContentAsJson_WhenCompressionDisabled()
	{
		// Arrange
		_options.UseCompression = false;
		_handler.SetResponse(HttpStatusCode.OK);
		var sut = CreateExporter();

		// Act
		await sut.ExportAsync(CreateAuditEvent(), CancellationToken.None);

		// Assert — should NOT have gzip content encoding
		_handler.LastRequest.ShouldNotBeNull();
		_handler.LastRequest!.Content!.Headers.ContentEncoding.ShouldNotContain("gzip");
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
		CorrelationId = "corr-1"
	};
}
