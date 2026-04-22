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
public sealed class ProjectionEventScanAdapterConformanceShould : IAsyncLifetime, IDisposable
{
	private readonly ElasticsearchClient _client;
	private readonly string _writeIndex;
	private readonly string _readIndex;
	private readonly ProjectionEventScanAdapter _adapter;
	private readonly ProjectionEventIngestAdapter _seed;
	private readonly ProjectionIndexProvisioningAdapter _provisioning;
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
		_provisioning = new ProjectionIndexProvisioningAdapter(_client);
	}

	/// <summary>
	/// Pre-create indexes with explicit keyword mappings so that Term queries
	/// in <see cref="ProjectionEventScanAdapter"/> work correctly.
	/// Without this, ES auto-creates indexes with dynamic text mapping on first
	/// document ingest, causing Term queries on eventId/projectionType to miss.
	/// </summary>
	public async Task InitializeAsync()
	{
		await _provisioning.CreateIndexAsync(_writeIndex, ConsistencyIndexKind.WriteEvents, CancellationToken.None)
			.ConfigureAwait(false);
		await _provisioning.CreateIndexAsync(_readIndex, ConsistencyIndexKind.ReadEvents, CancellationToken.None)
			.ConfigureAwait(false);
	}

	public Task DisposeAsync() => Task.CompletedTask;

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

		// ES 9 may need a longer settling window after refresh before
		// documents are visible in search results — especially in CI where
		// resource contention can delay near-realtime visibility.
		IReadOnlyList<ReadEventDocument>? results = null;
		for (var attempt = 0; attempt < 10; attempt++)
		{
			results = await _adapter
				.SearchReadsAsync(
					new ReadEventSearch(ProjectionType: "OrderProjection", MaxResults: 10),
					CancellationToken.None)
				.ConfigureAwait(false);

			if (results is { Count: > 0 })
			{
				break;
			}

			await Task.Delay(500, CancellationToken.None).ConfigureAwait(false);
			_ = await _client.Indices.RefreshAsync(_readIndex, CancellationToken.None).ConfigureAwait(false);
		}

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

		// ES 9 may need a longer settling window after refresh — especially
		// in CI where resource contention can delay near-realtime visibility.
		long count = 0;
		for (var attempt = 0; attempt < 10; attempt++)
		{
			count = await _adapter
				.GetDocumentCountAsync(_readIndex, ProjectionCountFilter.ReadsByProjectionType, projection, CancellationToken.None)
				.ConfigureAwait(false);

			if (count == 3)
			{
				break;
			}

			await Task.Delay(500, CancellationToken.None).ConfigureAwait(false);
			_ = await _client.Indices.RefreshAsync(_readIndex, CancellationToken.None).ConfigureAwait(false);
		}

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
