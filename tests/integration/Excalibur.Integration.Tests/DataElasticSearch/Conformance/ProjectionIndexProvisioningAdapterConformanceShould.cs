// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Elastic.Clients.Elasticsearch;

using Excalibur.Data.ElasticSearch.Internal;
using Excalibur.Integration.Tests.DataElasticSearch.Infrastructure.TestBaseClasses;

using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.DataElasticSearch.Conformance;

/// <summary>
/// Real-SDK conformance smoke for <see cref="ProjectionIndexProvisioningAdapter"/>
/// (S801 seam 4/6 <c>bd-r3xkes</c>, landed in <c>1cd409ad2</c>).
/// Verifies exists/create passthrough against a real TestContainer across all
/// three <see cref="ConsistencyIndexKind"/> mappings.
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
public sealed class ProjectionIndexProvisioningAdapterConformanceShould : IDisposable
{
	private readonly ElasticsearchClient _client;
	private readonly ProjectionIndexProvisioningAdapter _adapter;
	private readonly List<string> _createdIndices = new();
	private bool _disposed;

	public ProjectionIndexProvisioningAdapterConformanceShould(ElasticsearchContainerFixture fixture)
	{
		ArgumentNullException.ThrowIfNull(fixture);

		var settings = new ElasticsearchClientSettings(new Uri(fixture.ConnectionString));
		_client = new ElasticsearchClient(settings);
		_adapter = new ProjectionIndexProvisioningAdapter(_client);
	}

	[Fact]
	public void Construct_WithNullClient_ThrowsArgumentNullException() =>
		Should.Throw<ArgumentNullException>(() =>
			new ProjectionIndexProvisioningAdapter(null!));

	[Fact]
	public async Task IndexExistsAsync_ReturnsFalseForAbsentIndex()
	{
		var name = $"conformance-prov-absent-{Guid.NewGuid():N}";

		var exists = await _adapter
			.IndexExistsAsync(name, CancellationToken.None)
			.ConfigureAwait(false);

		exists.ShouldBeFalse();
	}

	[Fact]
	public Task CreateIndexAsync_WriteEvents_CreatesAndBecomesVisible() =>
		CreateAndAssertVisibleAsync(ConsistencyIndexKind.WriteEvents, "writes");

	[Fact]
	public Task CreateIndexAsync_ReadEvents_CreatesAndBecomesVisible() =>
		CreateAndAssertVisibleAsync(ConsistencyIndexKind.ReadEvents, "reads");

	[Fact]
	public Task CreateIndexAsync_Checkpoints_CreatesAndBecomesVisible() =>
		CreateAndAssertVisibleAsync(ConsistencyIndexKind.Checkpoints, "checkpoints");

	private async Task CreateAndAssertVisibleAsync(ConsistencyIndexKind kind, string label)
	{
		var name = $"conformance-prov-{label}-{Guid.NewGuid():N}";
		_createdIndices.Add(name);

		// Act — create.
		var created = await _adapter
			.CreateIndexAsync(name, kind, CancellationToken.None)
			.ConfigureAwait(false);
		created.ShouldBeTrue();

		// Assert — adapter's Exists check agrees with the real SDK.
		var exists = await _adapter
			.IndexExistsAsync(name, CancellationToken.None)
			.ConfigureAwait(false);
		exists.ShouldBeTrue();
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		if (_createdIndices.Count == 0)
		{
			return;
		}

		foreach (var name in _createdIndices)
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
