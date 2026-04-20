// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Elastic.Clients.Elasticsearch;

using Excalibur.Data.ElasticSearch.Internal;
using Excalibur.Integration.Tests.DataElasticSearch.Infrastructure.TestBaseClasses;

using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.DataElasticSearch.Conformance;

/// <summary>
/// Real-SDK conformance smoke for <see cref="ProjectionEventLookupAdapter"/>
/// (S801 seam 4/6 <c>bd-r3xkes</c>, landed in <c>1cd409ad2</c>).
/// Verifies point-lookup passthrough via <see cref="ElasticsearchClient.GetAsync"/>
/// and <see cref="ElasticsearchClient.SearchAsync"/> against a real TestContainer.
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
public sealed class ProjectionEventLookupAdapterConformanceShould : IDisposable
{
	private readonly ElasticsearchClient _client;
	private readonly string _writeIndex;
	private readonly string _readIndex;
	private readonly string _checkpointIndex;
	private readonly ProjectionEventLookupAdapter _adapter;
	private readonly ProjectionEventIngestAdapter _seed;
	private bool _disposed;

	public ProjectionEventLookupAdapterConformanceShould(ElasticsearchContainerFixture fixture)
	{
		ArgumentNullException.ThrowIfNull(fixture);

		var settings = new ElasticsearchClientSettings(new Uri(fixture.ConnectionString));
		_client = new ElasticsearchClient(settings);

		var suffix = Guid.NewGuid().ToString("N");
		_writeIndex = $"conformance-lookup-writes-{suffix}";
		_readIndex = $"conformance-lookup-reads-{suffix}";
		_checkpointIndex = $"conformance-lookup-checkpoints-{suffix}";

		_adapter = new ProjectionEventLookupAdapter(_client, _writeIndex, _readIndex, _checkpointIndex);
		_seed = new ProjectionEventIngestAdapter(_client, _writeIndex, _readIndex, _checkpointIndex);
	}

	[Fact]
	public void Construct_WithNullClient_ThrowsArgumentNullException() =>
		Should.Throw<ArgumentNullException>(() =>
			new ProjectionEventLookupAdapter(null!, "w", "r", "c"));

	[Fact]
	public async Task GetWriteEventByIdAsync_ReturnsNullWhenAbsent()
	{
		var result = await _adapter
			.GetWriteEventByIdAsync($"missing-{Guid.NewGuid():N}", CancellationToken.None)
			.ConfigureAwait(false);

		result.ShouldBeNull();
	}

	[Fact]
	public async Task GetWriteEventByIdAsync_RoundTripsSeededDocument()
	{
		var eventId = $"evt-{Guid.NewGuid():N}";
		var doc = new WriteEventDocument
		{
			EventId = eventId,
			AggregateId = "agg-1",
			EventType = "OrderPlaced",
			WriteTimestamp = DateTimeOffset.UtcNow,
		};

		(await _seed.IndexWriteEventAsync(doc, eventId, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeTrue();

		// Force immediate visibility; the adapter issues no refresh itself.
		_ = await _client.Indices.RefreshAsync(_writeIndex, CancellationToken.None).ConfigureAwait(false);

		var fetched = await _adapter
			.GetWriteEventByIdAsync(eventId, CancellationToken.None)
			.ConfigureAwait(false);

		fetched.ShouldNotBeNull();
		fetched!.EventId.ShouldBe(eventId);
		fetched.EventType.ShouldBe("OrderPlaced");
	}

	[Fact]
	public async Task GetProjectionTypesAsync_ReturnsEmptyForAbsentIndex()
	{
		var types = await _adapter
			.GetProjectionTypesAsync(CancellationToken.None)
			.ConfigureAwait(false);

		types.ShouldNotBeNull();
		types.ShouldBeEmpty();
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		foreach (var name in new[] { _writeIndex, _readIndex, _checkpointIndex })
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
