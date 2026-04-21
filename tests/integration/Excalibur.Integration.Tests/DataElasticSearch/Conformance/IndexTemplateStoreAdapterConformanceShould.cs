// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Elastic.Clients.Elasticsearch;

using Excalibur.Data.ElasticSearch.IndexManagement;
using Excalibur.Data.ElasticSearch.Internal;
using Excalibur.Integration.Tests.DataElasticSearch.Infrastructure.TestBaseClasses;

using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.DataElasticSearch.Conformance;

/// <summary>
/// Real-SDK conformance smoke for the paired
/// <see cref="IndexTemplateStoreAdapter"/> + <see cref="ComponentTemplateStoreAdapter"/>.
/// Verifies both S799-A seams (post-F1 split per OVERWATCH msg 1818)
/// faithfully pass through to the underlying <see cref="ElasticsearchClient"/> —
/// exercising <see cref="IIndexTemplateStore.PutAsync"/>,
/// <see cref="IIndexTemplateStore.ExistsAsync"/>,
/// <see cref="IIndexTemplateStore.ListAsync"/>,
/// <see cref="IIndexTemplateStore.DeleteAsync"/>,
/// <see cref="IComponentTemplateStore.PutAsync"/>, and
/// <see cref="IComponentTemplateStore.DeleteAsync"/>
/// against a real Elasticsearch TestContainer.
/// </summary>
/// <remarks>
/// ADR-142 §D7 seam-passthrough contract: one real-SDK smoke per adapter
/// under the integration shard. Behaviorally-exhaustive index-template
/// tests already live in <see cref="IndexTemplateManagerIntegrationShould"/>;
/// this smoke is strictly the seam-passthrough assertion, not a re-test of
/// Elasticsearch behavior. Per OVERWATCH msg 1807 (S799 A-stream scope lock)
/// + msg 1818 (F1 split remediation).
/// </remarks>
[Collection(nameof(ElasticsearchHostTests))]
[Trait("Category", "Integration")]
[Trait("Component", "Elasticsearch")]
[Trait("Database", "Elasticsearch")]
[Trait("Pattern", "SEAM-PASSTHROUGH")]
public sealed class IndexTemplateStoreAdapterConformanceShould : IDisposable
{
	private readonly ElasticsearchClient _client;
	private readonly IndexTemplateStoreAdapter _indexAdapter;
	private readonly ComponentTemplateStoreAdapter _componentAdapter;
	private bool _disposed;

	public IndexTemplateStoreAdapterConformanceShould(ElasticsearchContainerFixture fixture)
	{
		ArgumentNullException.ThrowIfNull(fixture);

		var settings = new ElasticsearchClientSettings(new Uri(fixture.ConnectionString));
		_client = new ElasticsearchClient(settings);
		_indexAdapter = new IndexTemplateStoreAdapter(_client);
		_componentAdapter = new ComponentTemplateStoreAdapter(_client);
	}

	[Fact]
	public void Construct_IndexAdapter_WithNullClient_ThrowsArgumentNullException() =>
		Should.Throw<ArgumentNullException>(() => new IndexTemplateStoreAdapter(null!));

	[Fact]
	public void Construct_ComponentAdapter_WithNullClient_ThrowsArgumentNullException() =>
		Should.Throw<ArgumentNullException>(() => new ComponentTemplateStoreAdapter(null!));

	[Fact]
	public async Task PutAsync_ExistsAsync_ListAsync_DeleteAsync_RoundTripsAgainstRealClient()
	{
		// Arrange
		var templateName = $"conformance-smoke-{Guid.NewGuid():N}";
		var config = new IndexTemplateConfiguration
		{
			IndexPatterns = [$"{templateName}-*"],
			Priority = 10,
		};

		try
		{
			// Act 1 — PutAsync: the adapter builds a PutIndexTemplateRequest
			// and dispatches to _inner.Indices.PutIndexTemplateAsync. A real
			// ES acknowledges the operation; passthrough success = true.
			var put = await _indexAdapter
				.PutAsync(templateName, config, CancellationToken.None)
				.ConfigureAwait(false);
			put.Success.ShouldBeTrue(put.ErrorDetails);

			// Act 2 — ExistsAsync: adapter routes to
			// _inner.Indices.ExistsIndexTemplateAsync; real ES reports true.
			var exists = await _indexAdapter
				.ExistsAsync(templateName, CancellationToken.None)
				.ConfigureAwait(false);
			exists.ShouldBeTrue();

			// Act 3 — ListAsync: adapter routes to
			// _inner.Indices.GetIndexTemplateAsync; real ES returns our item.
			var listed = await _indexAdapter
				.ListAsync(templateName, CancellationToken.None)
				.ConfigureAwait(false);
			listed.ShouldNotBeNull();
			listed.Count.ShouldBeGreaterThanOrEqualTo(1);
		}
		finally
		{
			// Act 4 — DeleteAsync: adapter routes to
			// _inner.Indices.DeleteIndexTemplateAsync; leaves the container
			// in its pre-test state for the next test in the collection.
			var delete = await _indexAdapter
				.DeleteAsync(templateName, CancellationToken.None)
				.ConfigureAwait(false);
			delete.Success.ShouldBeTrue(delete.ErrorDetails);
		}
	}

	[Fact]
	public async Task ComponentPutAsync_DeleteAsync_RoundTripsAgainstRealClient()
	{
		// Arrange
		var componentName = $"conformance-component-{Guid.NewGuid():N}";
		var config = new ComponentTemplateConfiguration
		{
			Version = 1,
		};

		try
		{
			// Act — adapter routes to _inner.Cluster.PutComponentTemplateAsync.
			var put = await _componentAdapter
				.PutAsync(componentName, config, CancellationToken.None)
				.ConfigureAwait(false);
			put.Success.ShouldBeTrue(put.ErrorDetails);
		}
		finally
		{
			// Cleanup via adapter; also exercises
			// _inner.Cluster.DeleteComponentTemplateAsync.
			var delete = await _componentAdapter
				.DeleteAsync(componentName, CancellationToken.None)
				.ConfigureAwait(false);
			delete.Success.ShouldBeTrue(delete.ErrorDetails);
		}
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		// ElasticsearchClient does not implement IDisposable in v8; the
		// underlying transport is managed by the client's lifetime — nothing
		// to dispose here. Method retained for xUnit IDisposable contract.
	}
}
