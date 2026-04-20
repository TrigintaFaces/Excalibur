// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Elastic.Clients.Elasticsearch;

using Excalibur.Data.ElasticSearch.Internal;
using Excalibur.Integration.Tests.DataElasticSearch.Infrastructure.TestBaseClasses;

using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.DataElasticSearch.Conformance;

/// <summary>
/// Real-SDK conformance smoke for <see cref="ProjectionEventIngestAdapter"/>
/// (S801 seam 4/6 <c>bd-r3xkes</c>, landed in <c>1cd409ad2</c>).
/// Verifies the adapter faithfully passes through to
/// <see cref="ElasticsearchClient.IndexAsync"/> for all three ingest paths
/// (write / read / checkpoint) against a real Elasticsearch TestContainer.
/// </summary>
/// <remarks>
/// ADR-142 §D7 seam-passthrough contract: one real-SDK smoke per adapter.
/// Behaviorally-exhaustive <see cref="Excalibur.Data.ElasticSearch.Projections.EventualConsistencyTracker"/>
/// tests live in the unit suite via faked <see cref="IProjectionEventIngest"/>;
/// this smoke is strictly the seam-passthrough assertion. Pays the S801
/// coverage gap identified in CRUCIBLE msg 1919.
/// </remarks>
[Collection(nameof(ElasticsearchHostTests))]
[Trait("Category", "Integration")]
[Trait("Component", "Elasticsearch")]
[Trait("Database", "Elasticsearch")]
[Trait("Pattern", "SEAM-PASSTHROUGH")]
public sealed class ProjectionEventIngestAdapterConformanceShould : IDisposable
{
	private readonly ElasticsearchClient _client;
	private readonly string _writeIndex;
	private readonly string _readIndex;
	private readonly string _checkpointIndex;
	private readonly ProjectionEventIngestAdapter _adapter;
	private bool _disposed;

	public ProjectionEventIngestAdapterConformanceShould(ElasticsearchContainerFixture fixture)
	{
		ArgumentNullException.ThrowIfNull(fixture);

		var settings = new ElasticsearchClientSettings(new Uri(fixture.ConnectionString));
		_client = new ElasticsearchClient(settings);

		var suffix = Guid.NewGuid().ToString("N");
		_writeIndex = $"conformance-ingest-writes-{suffix}";
		_readIndex = $"conformance-ingest-reads-{suffix}";
		_checkpointIndex = $"conformance-ingest-checkpoints-{suffix}";

		_adapter = new ProjectionEventIngestAdapter(_client, _writeIndex, _readIndex, _checkpointIndex);
	}

	[Fact]
	public void Construct_WithNullClient_ThrowsArgumentNullException() =>
		Should.Throw<ArgumentNullException>(() =>
			new ProjectionEventIngestAdapter(null!, "w", "r", "c"));

	[Fact]
	public async Task IndexWriteEventAsync_PassesThroughToRealClient()
	{
		var doc = new WriteEventDocument
		{
			EventId = "evt-write-1",
			AggregateId = "agg-1",
			EventType = "OrderPlaced",
			WriteTimestamp = DateTimeOffset.UtcNow,
		};

		var ok = await _adapter
			.IndexWriteEventAsync(doc, doc.EventId, CancellationToken.None)
			.ConfigureAwait(false);

		ok.ShouldBeTrue();
	}

	[Fact]
	public async Task IndexReadEventAsync_PassesThroughToRealClient()
	{
		var doc = new ReadEventDocument
		{
			EventId = "evt-read-1",
			ProjectionType = "OrderProjection",
			ReadTimestamp = DateTimeOffset.UtcNow,
		};

		var ok = await _adapter
			.IndexReadEventAsync(doc, $"{doc.EventId}:{doc.ProjectionType}", CancellationToken.None)
			.ConfigureAwait(false);

		ok.ShouldBeTrue();
	}

	[Fact]
	public async Task IndexCheckpointAsync_PassesThroughToRealClient()
	{
		var doc = new ProjectionCheckpointDocument
		{
			ProjectionType = "OrderProjection",
			LastEventId = "evt-1",
			LastProcessedAt = DateTimeOffset.UtcNow,
			UpdatedAt = DateTimeOffset.UtcNow,
		};

		var ok = await _adapter
			.IndexCheckpointAsync(doc, doc.ProjectionType, CancellationToken.None)
			.ConfigureAwait(false);

		ok.ShouldBeTrue();
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		// Best-effort cleanup of ephemeral test indices. ES container is shared
		// across the collection so per-test isolation via unique GUID suffixes
		// is the primary guarantee.
		foreach (var name in new[] { _writeIndex, _readIndex, _checkpointIndex })
		{
			try
			{
				_ = _client.Indices.DeleteAsync(name)
					.ConfigureAwait(false).GetAwaiter().GetResult();
			}
			catch
			{
				// Cleanup is best-effort; TestContainer lifetime bounds disk usage.
			}
		}
	}
}
