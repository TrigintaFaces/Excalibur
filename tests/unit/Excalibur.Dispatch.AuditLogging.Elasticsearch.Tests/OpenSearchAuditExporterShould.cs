// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Net;

using Excalibur.Dispatch.AuditLogging.OpenSearch;
using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.AuditLogging.Elasticsearch.Tests;

/// <summary>
/// Tests for <see cref="OpenSearchAuditExporter"/> covering NodeUrls cluster support,
/// round-robin request distribution, and health check URL selection.
/// Sprint 736: AOT Wave 1 -- cluster-aware OpenSearch exporter.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class OpenSearchAuditExporterShould : IDisposable
{
	private readonly OpenSearchExporterOptions _options = new()
	{
		OpenSearchUrl = "https://os.example.com:9200",
		IndexPrefix = "test-audit",
		MaxRetryAttempts = 0,
		BulkBatchSize = 500,
	};

	private readonly ILogger<OpenSearchAuditExporter> _logger = EnabledTestLogger.Create<OpenSearchAuditExporter>();
	private readonly FakeHttpMessageHandler _handler = new();

	[Fact]
	public void HaveNameOpenSearch()
	{
		var sut = CreateExporter();
		sut.Name.ShouldBe("OpenSearch");
	}

	[Fact]
	public async Task ExportSingleEventSuccessfully()
	{
		_handler.SetResponse(HttpStatusCode.OK);
		var sut = CreateExporter();
		var auditEvent = CreateAuditEvent();

		var result = await sut.ExportAsync(auditEvent, CancellationToken.None).ConfigureAwait(false);

		result.Success.ShouldBeTrue();
		result.EventId.ShouldBe(auditEvent.EventId);
	}

	[Fact]
	public async Task UseSingleOpenSearchUrlWhenNodeUrlsIsNull()
	{
		_handler.SetResponse(HttpStatusCode.OK);
		_options.NodeUrls = null;
		var sut = CreateExporter();

		await sut.ExportAsync(CreateAuditEvent(), CancellationToken.None).ConfigureAwait(false);

		_handler.LastRequest.ShouldNotBeNull();
		_handler.LastRequest!.RequestUri!.Host.ShouldBe("os.example.com");
	}

	[Fact]
	public async Task UseSingleOpenSearchUrlWhenNodeUrlsIsEmpty()
	{
		_handler.SetResponse(HttpStatusCode.OK);
		_options.NodeUrls = [];
		var sut = CreateExporter();

		await sut.ExportAsync(CreateAuditEvent(), CancellationToken.None).ConfigureAwait(false);

		_handler.LastRequest.ShouldNotBeNull();
		_handler.LastRequest!.RequestUri!.Host.ShouldBe("os.example.com");
	}

	[Fact]
	public async Task PreferNodeUrlsOverOpenSearchUrl()
	{
		_handler.SetResponse(HttpStatusCode.OK);
		_options.NodeUrls = ["https://node1.example.com:9200"];
		var sut = CreateExporter();

		await sut.ExportAsync(CreateAuditEvent(), CancellationToken.None).ConfigureAwait(false);

		_handler.LastRequest.ShouldNotBeNull();
		_handler.LastRequest!.RequestUri!.Host.ShouldBe("node1.example.com");
	}

	[Fact]
	public async Task RoundRobinAcrossMultipleNodes()
	{
		_handler.SetResponse(HttpStatusCode.OK);
		_options.NodeUrls =
		[
			"https://node1.example.com:9200",
			"https://node2.example.com:9200",
			"https://node3.example.com:9200",
		];
		var sut = CreateExporter();

		// Send 6 requests and track which hosts are hit
		var hosts = new List<string>();
		for (var i = 0; i < 6; i++)
		{
			await sut.ExportAsync(CreateAuditEvent($"evt-{i}"), CancellationToken.None).ConfigureAwait(false);
			hosts.Add(_handler.LastRequest!.RequestUri!.Host);
		}

		// Should cycle through all 3 nodes twice
		hosts[0].ShouldBe("node1.example.com");
		hosts[1].ShouldBe("node2.example.com");
		hosts[2].ShouldBe("node3.example.com");
		hosts[3].ShouldBe("node1.example.com");
		hosts[4].ShouldBe("node2.example.com");
		hosts[5].ShouldBe("node3.example.com");
	}

	[Fact]
	public async Task IncludeBulkApiPathInNodeUrl()
	{
		_handler.SetResponse(HttpStatusCode.OK);
		_options.NodeUrls = ["https://node1.example.com:9200"];
		_options.RefreshPolicy = "wait_for";
		var sut = CreateExporter();

		await sut.ExportAsync(CreateAuditEvent(), CancellationToken.None).ConfigureAwait(false);

		_handler.LastRequest.ShouldNotBeNull();
		var uri = _handler.LastRequest!.RequestUri!;
		uri.AbsolutePath.ShouldBe("/_bulk");
		uri.Query.ShouldContain("refresh=wait_for");
	}

	[Fact]
	public async Task TrimTrailingSlashFromNodeUrls()
	{
		_handler.SetResponse(HttpStatusCode.OK);
		_options.NodeUrls = ["https://node1.example.com:9200/"];
		var sut = CreateExporter();

		await sut.ExportAsync(CreateAuditEvent(), CancellationToken.None).ConfigureAwait(false);

		_handler.LastRequest.ShouldNotBeNull();
		var uri = _handler.LastRequest!.RequestUri!;
		uri.AbsolutePath.ShouldBe("/_bulk");
	}

	[Fact]
	public async Task HealthCheckUseNodeUrlsFirstNodeWhenSet()
	{
		_handler.SetResponse(HttpStatusCode.OK);
		_options.NodeUrls =
		[
			"https://primary-node.example.com:9200",
			"https://secondary-node.example.com:9200",
		];
		var sut = CreateExporter();

		var result = await sut.CheckHealthAsync(CancellationToken.None).ConfigureAwait(false);

		result.IsHealthy.ShouldBeTrue();
		_handler.LastRequest.ShouldNotBeNull();
		_handler.LastRequest!.RequestUri!.Host.ShouldBe("primary-node.example.com");
		_handler.LastRequest.RequestUri.AbsolutePath.ShouldBe("/_cluster/health");
	}

	[Fact]
	public async Task HealthCheckUseSingleUrlWhenNodeUrlsNull()
	{
		_handler.SetResponse(HttpStatusCode.OK);
		_options.NodeUrls = null;
		var sut = CreateExporter();

		var result = await sut.CheckHealthAsync(CancellationToken.None).ConfigureAwait(false);

		result.IsHealthy.ShouldBeTrue();
		_handler.LastRequest.ShouldNotBeNull();
		_handler.LastRequest!.RequestUri!.Host.ShouldBe("os.example.com");
	}

	[Fact]
	public async Task ReturnFailureOnNonSuccessStatus()
	{
		_handler.SetResponse(HttpStatusCode.BadRequest, "Bad request");
		var sut = CreateExporter();

		var result = await sut.ExportAsync(CreateAuditEvent(), CancellationToken.None).ConfigureAwait(false);

		result.Success.ShouldBeFalse();
		result.ErrorMessage.ShouldNotBeNull();
	}

	[Fact]
	public async Task ThrowForNullAuditEvent()
	{
		var sut = CreateExporter();

		await Should.ThrowAsync<ArgumentNullException>(
			() => sut.ExportAsync(null!, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public void ThrowForNullHttpClient()
	{
		Should.Throw<ArgumentNullException>(() =>
			new OpenSearchAuditExporter(
				null!,
				Microsoft.Extensions.Options.Options.Create(_options),
				_logger));
	}

	[Fact]
	public void ThrowForNullOptions()
	{
		Should.Throw<ArgumentNullException>(() =>
			new OpenSearchAuditExporter(new HttpClient(_handler), null!, _logger));
	}

	[Fact]
	public void ThrowForNullLogger()
	{
		Should.Throw<ArgumentNullException>(() =>
			new OpenSearchAuditExporter(
				new HttpClient(_handler),
				Microsoft.Extensions.Options.Options.Create(_options),
				null!));
	}

	public void Dispose()
	{
		_handler.Dispose();
	}

	private OpenSearchAuditExporter CreateExporter()
	{
		var httpClient = new HttpClient(_handler);
		return new OpenSearchAuditExporter(
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
		EventHash = "hash-123",
	};
}
