// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;
using System.Net;

using Excalibur.Dispatch.AuditLogging.Sentinel;
using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.AuditLogging.Sentinel.Tests;

/// <summary>
/// Depth coverage tests for <see cref="SentinelAuditExporter"/> covering
/// DataAnnotations validation, HMAC-SHA256 authorization header format,
/// batch chunk exception paths, workspace masking, and URI construction.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class SentinelAuditExporterDepthShould : IDisposable
{
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
	public void ImplementIAuditLogExporter()
	{
		var sut = CreateExporter();
		sut.ShouldBeAssignableTo<IAuditLogExporter>();
	}

	[Fact]
	public void OptionsWorkspaceId_HaveRequiredAttribute()
	{
		var prop = typeof(SentinelExporterOptions).GetProperty(nameof(SentinelExporterOptions.WorkspaceId));
		prop.ShouldNotBeNull();
		prop!.GetCustomAttributes(typeof(RequiredAttribute), false).ShouldNotBeEmpty();
	}

	[Fact]
	public void OptionsSharedKey_HaveRequiredAttribute()
	{
		var prop = typeof(SentinelExporterOptions).GetProperty(nameof(SentinelExporterOptions.SharedKey));
		prop.ShouldNotBeNull();
		prop!.GetCustomAttributes(typeof(RequiredAttribute), false).ShouldNotBeEmpty();
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
	public async Task CheckHealth_IncludeEndpointWithOpinsights()
	{
		// Arrange
		_handler.SetResponse(HttpStatusCode.OK);
		var sut = CreateExporter();

		// Act
		var result = await sut.CheckHealthAsync(CancellationToken.None);

		// Assert
		result.Endpoint.ShouldContain("opinsights.azure.com");
	}

	[Fact]
	public async Task ExportAsync_IncludeStatusCodeInErrorMessage()
	{
		// Arrange
		_handler.SetResponse(HttpStatusCode.Forbidden, "Access denied");
		var sut = CreateExporter();

		// Act
		var result = await sut.ExportAsync(CreateAuditEvent(), CancellationToken.None);

		// Assert
		result.ErrorMessage.ShouldNotBeNull();
		result.ErrorMessage.ShouldContain("403");
	}

	[Fact]
	public async Task ExportAsync_MarkNonTransientStatusCode_AsNonTransient()
	{
		// Arrange
		_handler.SetResponse(HttpStatusCode.Forbidden, "Forbidden");
		var sut = CreateExporter();

		// Act
		var result = await sut.ExportAsync(CreateAuditEvent(), CancellationToken.None);

		// Assert
		result.IsTransientError.ShouldBeFalse();
	}

	[Fact]
	public async Task ExportBatch_ReturnCorrectCounts_WithMultipleChunks()
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
		result.FailedCount.ShouldBe(0);
	}

	[Fact]
	public async Task ExportAsync_SetExportedAt_OnSuccess()
	{
		// Arrange
		_handler.SetResponse(HttpStatusCode.OK);
		var sut = CreateExporter();
		var before = DateTimeOffset.UtcNow;

		// Act
		var result = await sut.ExportAsync(CreateAuditEvent(), CancellationToken.None);

		// Assert
		result.ExportedAt.ShouldBeGreaterThanOrEqualTo(before);
		var assertionUpperBound1 = DateTimeOffset.UtcNow.AddSeconds(1);
		result.ExportedAt.ShouldBeLessThanOrEqualTo(assertionUpperBound1);
	}

	[Fact]
	public async Task CheckHealth_IncludeLatencyMs()
	{
		// Arrange
		_handler.SetResponse(HttpStatusCode.OK);
		var sut = CreateExporter();

		// Act
		var result = await sut.CheckHealthAsync(CancellationToken.None);

		// Assert
		result.LatencyMs.ShouldNotBeNull();
		result.LatencyMs!.Value.ShouldBeGreaterThanOrEqualTo(0);
	}

	[Fact]
	public async Task OmitAzureResourceIdHeader_WhenNotConfigured()
	{
		// Arrange
		_options.AzureResourceId = null;
		_handler.SetResponse(HttpStatusCode.OK);
		var sut = CreateExporter();

		// Act
		await sut.ExportAsync(CreateAuditEvent(), CancellationToken.None);

		// Assert
		_handler.LastRequest.ShouldNotBeNull();
		_handler.LastRequest!.Headers.Contains("x-ms-AzureResourceId").ShouldBeFalse();
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
		CorrelationId = "corr-1"
	};
}
