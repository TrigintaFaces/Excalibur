// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Elastic.Clients.Elasticsearch;

using Excalibur.Data.ElasticSearch.Internal;
using Excalibur.Integration.Tests.DataElasticSearch.Infrastructure.TestBaseClasses;

using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.DataElasticSearch.Conformance;

/// <summary>
/// Real-SDK conformance smoke for <see cref="ProjectionEventScanAdapter"/>
/// (S801 seam 4/6 <c>bd-r3xkes</c>, landed in <c>1cd409ad2</c>).
/// Verifies parameterized scan/count passthrough against a real TestContainer.
/// </summary>
/// <remarks>
/// ADR-142 §D7 seam-passthrough contract: one real-SDK smoke per adapter. Pays
/// the S801 coverage gap identified in CRUCIBLE msg 1919.
/// </remarks>
[Collection(nameof(ElasticsearchHostTests))]
[Trait("Category", "Integration")]
[Trait("Component", "Elasticsearch")]
[Trait("Database", "Elasticsearch")]
[Trait("Pattern", "SEAM-PASSTHROUGH")]
public sealed class ProjectionEventScanAdapterConformanceShould : IDisposable
{
	private readonly ElasticsearchClient _client;
	private readonly string _writeIndex;
	private readonly string _readIndex;
	private readonly ProjectionEventScanAdapter _adapter;
	private readonly ProjectionEventIngestAdapter _seed;
	private bool _disposed;

	public ProjectionEventScanAdapterConformanceShould(ElasticsearchContainerFixture fixture)
	{
		ArgumentNullException.ThrowIfNull(fixture);

		var settings = new ElasticsearchClientSettings(new Uri(fixture.ConnectionString));
		_client = new ElasticsearchClient(settings);

		var suffix = Guid.NewGuid().ToString("N");
		_writeIndex = $"conformance-scan-writes-{suffix}";
		_readIndex = $"conformance-scan-reads-{suffix}";
		var checkpointIndex = $"conformance-scan-checkpoints-{suffix}";

		_adapter = new ProjectionEventScanAdapter(_client, _writeIndex, _readIndex);
		_seed = new ProjectionEventIngestAdapter(_client, _writeIndex, _readIndex, checkpointIndex);
	}

	[Fact]
	public void Construct_WithNullClient_ThrowsArgumentNullException() =>
		Should.Throw<ArgumentNullException>(() =>
			new ProjectionEventScanAdapter(null!, "w", "r"));

	[Fact]
	public async Task GetLatestWriteTimestampAsync_ReturnsNullForEmptyIndex()
	{
		var latest = await _adapter
			.GetLatestWriteTimestampAsync(CancellationToken.None)
			.ConfigureAwait(false);

		latest.ShouldBeNull();
	}

	[Fact]
	public async Task SearchReadsAsync_ReturnsSeededDocumentsFilteredByProjectionType()
	{
		var eventId = $"evt-{Guid.NewGuid():N}";
		var doc = new ReadEventDocument
		{
			EventId = eventId,
			ProjectionType = "OrderProjection",
			ReadTimestamp = DateTimeOffset.UtcNow,
		};
		(await _seed.IndexReadEventAsync(doc, $"{eventId}:{doc.ProjectionType}", CancellationToken.None).ConfigureAwait(false))
			.ShouldBeTrue();

		_ = await _client.Indices.RefreshAsync(_readIndex, CancellationToken.None).ConfigureAwait(false);

		var results = await _adapter
			.SearchReadsAsync(
				new ReadEventSearch(ProjectionType: "OrderProjection", MaxResults: 10),
				CancellationToken.None)
			.ConfigureAwait(false);

		results.ShouldNotBeNull();
		results.ShouldContain(d => d.EventId == eventId);
	}

	[Fact]
	public async Task GetDocumentCountAsync_CountsReadsByProjectionType()
	{
		var projection = $"proj-{Guid.NewGuid():N}";
		for (var i = 0; i < 3; i++)
		{
			var doc = new ReadEventDocument
			{
				EventId = $"evt-{i}-{Guid.NewGuid():N}",
				ProjectionType = projection,
				ReadTimestamp = DateTimeOffset.UtcNow,
			};
			_ = await _seed.IndexReadEventAsync(doc, $"{doc.EventId}:{projection}", CancellationToken.None)
				.ConfigureAwait(false);
		}

		_ = await _client.Indices.RefreshAsync(_readIndex, CancellationToken.None).ConfigureAwait(false);

		var count = await _adapter
			.GetDocumentCountAsync(_readIndex, ProjectionCountFilter.ReadsByProjectionType, projection, CancellationToken.None)
			.ConfigureAwait(false);

		count.ShouldBe(3);
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		foreach (var name in new[] { _writeIndex, _readIndex })
		{
			try
			{
				_ = _client.Indices.DeleteAsync(name)
					.ConfigureAwait(false).GetAwaiter().GetResult();
			}
			catch
			{
				// Best-effort cleanup.
			}
		}
	}
}
