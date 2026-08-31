// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Data.OpenSearch.Projections;
using Excalibur.EventSourcing;

using Microsoft.Extensions.DependencyInjection;

using OpenSearch.Client;

using Xunit;

namespace Excalibur.Integration.Tests.OpenSearch;

/// <summary>
/// T.14 (efok50): Integration tests for OpenSearchProjectionStore using
/// OpenSearch TestContainers. Tests CRUD operations through real OpenSearch.
/// Gracefully skips when OpenSearch container is unavailable.
/// </summary>
/// <remarks>
/// <para>
/// <b>Container ownership lives on <see cref="OpenSearchContainerFixture"/>, not here.</b> xUnit
/// constructs a new instance of a test class for every fact, so the container this class used to start
/// from its own <c>InitializeAsync</c> was started once per fact — five OpenSearch containers for five
/// facts, and five full start timeouts when OpenSearch was unavailable. The collection fixture is
/// constructed once, so the container starts once.
/// </para>
/// <para>
/// <b>Isolation:</b> the container is shared, so each test instance gets its own index
/// (<c>test-projections-{guid}</c>) and its own service provider and store bound to it. No test can see
/// another's documents, which is the same isolation a per-test container gave — without the per-test
/// container.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Database", "OpenSearch")]
[Trait("Component", "Projections")]
[Collection(OpenSearchTestCollection.CollectionName)]
[SuppressMessage("Design", "CA1506", Justification = "Integration test")]
public sealed class OpenSearchProjectionStoreIntegrationShould : IAsyncLifetime
{
	private readonly OpenSearchContainerFixture _fixture;

	/// <summary>
	/// Per-test-instance index. xUnit builds one instance per fact, so this is unique per fact and is
	/// what keeps facts isolated from each other on the shared container.
	/// </summary>
	private readonly string _indexName = $"test-projections-{Guid.NewGuid():N}";

	private ServiceProvider? _serviceProvider;
	private IProjectionStore<TestOpenSearchProjection>? _store;
	private bool _available;
	private Exception? _unavailableCause;

	public OpenSearchProjectionStoreIntegrationShould(OpenSearchContainerFixture fixture) =>
		_fixture = fixture;

	/// <summary>Appends the captured startup cause to a skip reason, so the log says WHY.</summary>
	private string SkipReason(string reason) =>
		_unavailableCause is null ? reason : reason + " Cause: " + _unavailableCause.GetType().Name + ": " + _unavailableCause.Message;

	public async ValueTask InitializeAsync()
	{
		if (!_fixture.Available)
		{
			// The container never started. The fixture already paid — and recorded — that cost once;
			// this instance must not pay it again.
			_unavailableCause = _fixture.UnavailableCause;
			_available = false;
			return;
		}

		try
		{
			var services = new ServiceCollection();
			services.AddLogging();

			// This overload now resolves an IOpenSearchClient from DI when one is registered, and otherwise
			// connects using OpenSearchProjectionStoreOptions.NodeUri. Nothing is registered here, so the
			// NodeUri path is what this test exercises — and it must be set, because the default is
			// https://localhost:9200 while the container's port is mapped to a random host port, so
			// pointing the store at the container means setting NodeUri to the fixture's endpoint.
			services.AddOpenSearchProjectionStore<TestOpenSearchProjection>(options =>
			{
				options.NodeUri = _fixture.Endpoint.ToString();
				options.IndexName = _indexName;
			});

			_serviceProvider = services.BuildServiceProvider();
			_store = _serviceProvider.GetRequiredService<IProjectionStore<TestOpenSearchProjection>>();

			// Reachability was already established once by the fixture's ping. It is deliberately NOT
			// re-probed here: the previous per-fact probe counted documents in this fact's own index,
			// which does not exist yet, so it could never succeed and every fact skipped itself against
			// a healthy node.
			_available = true;
		}
		catch (Exception ex)
		{
			// Preserve the cause. Reporting every startup failure as "infrastructure unavailable"
			// makes a fixable fault -- an image pull, a port collision, a schema-init error --
			// indistinguishable from an absent daemon, and undiagnosable from the CI log.
			_unavailableCause = ex;
			_available = false;
		}
	}

	public async ValueTask DisposeAsync()
	{
		// The container is owned by the collection fixture and deliberately NOT stopped here: it is
		// shared with every other fact in this collection. Only this instance's own index and service
		// provider are torn down.
		try
		{
			if (_available)
			{
				var client = new OpenSearchClient(new ConnectionSettings(_fixture.Endpoint));

				// The store prefixes the configured index name (OpenSearchProjectionStoreOptions
				// .IndexPrefix defaults to "projections"), so delete by that pattern rather than the
				// bare name. Scoped to this fact's unique index, so no other fact is touched.
				_ = await client.Indices.DeleteAsync($"*{_indexName}").ConfigureAwait(false);
			}
		}
		catch (Exception)
		{
			// Best effort cleanup — a leftover index cannot affect another fact, which uses its own.
		}

		try
		{
			if (_serviceProvider is not null)
			{
				await _serviceProvider.DisposeAsync().ConfigureAwait(false);
			}
		}
		catch (Exception)
		{
			// Best effort cleanup
		}
	}

	[Fact]
	public async Task UpsertAndGetById()
	{
		Assert.SkipWhen(!_available, SkipReason("[infrastructure-unavailable] OpenSearch (Docker) is not available, so this fact did NOT execute. It is reported skipped, never passed: a test that returns early on missing infrastructure is satisfied by doing nothing."));

		var projection = new TestOpenSearchProjection { Id = "proj-1", Name = "Test", Value = 42 };
		await _store!.UpsertAsync("proj-1", projection, CancellationToken.None);
		(await global::Tests.Shared.Infrastructure.WaitHelpers.WaitUntilAsync(
			async () => await _store.GetByIdAsync("proj-1", CancellationToken.None) is not null,
			TimeSpan.FromSeconds(30)).ConfigureAwait(false)).ShouldBeTrue();

		var result = await _store.GetByIdAsync("proj-1", CancellationToken.None);
		result.ShouldNotBeNull();
		result.Name.ShouldBe("Test");
		result.Value.ShouldBe(42);
	}

	[Fact]
	public async Task ReturnNullForNonexistent()
	{
		Assert.SkipWhen(!_available, SkipReason("[infrastructure-unavailable] OpenSearch (Docker) is not available, so this fact did NOT execute. It is reported skipped, never passed: a test that returns early on missing infrastructure is satisfied by doing nothing."));

		var result = await _store!.GetByIdAsync("nonexistent", CancellationToken.None);
		result.ShouldBeNull();
	}

	[Fact]
	public async Task DeleteExistingProjection()
	{
		Assert.SkipWhen(!_available, SkipReason("[infrastructure-unavailable] OpenSearch (Docker) is not available, so this fact did NOT execute. It is reported skipped, never passed: a test that returns early on missing infrastructure is satisfied by doing nothing."));

		await _store!.UpsertAsync("proj-del", new TestOpenSearchProjection { Id = "proj-del", Name = "ToDelete" }, CancellationToken.None);
		(await global::Tests.Shared.Infrastructure.WaitHelpers.WaitUntilAsync(
			async () => await _store.GetByIdAsync("proj-del", CancellationToken.None) is not null,
			TimeSpan.FromSeconds(30)).ConfigureAwait(false)).ShouldBeTrue();
		await _store.DeleteAsync("proj-del", CancellationToken.None);
		(await global::Tests.Shared.Infrastructure.WaitHelpers.WaitUntilAsync(
			async () => await _store.GetByIdAsync("proj-del", CancellationToken.None) is null,
			TimeSpan.FromSeconds(30)).ConfigureAwait(false)).ShouldBeTrue();

		var result = await _store.GetByIdAsync("proj-del", CancellationToken.None);
		result.ShouldBeNull();
	}

	[Fact]
	public async Task CountDocuments()
	{
		Assert.SkipWhen(!_available, SkipReason("[infrastructure-unavailable] OpenSearch (Docker) is not available, so this fact did NOT execute. It is reported skipped, never passed: a test that returns early on missing infrastructure is satisfied by doing nothing."));

		await _store!.UpsertAsync("proj-c1", new TestOpenSearchProjection { Id = "proj-c1", Name = "A" }, CancellationToken.None);
		await _store.UpsertAsync("proj-c2", new TestOpenSearchProjection { Id = "proj-c2", Name = "B" }, CancellationToken.None);
		(await global::Tests.Shared.Infrastructure.WaitHelpers.WaitUntilAsync(
			async () => await _store.CountAsync(null, CancellationToken.None) >= 2,
			TimeSpan.FromSeconds(30)).ConfigureAwait(false)).ShouldBeTrue();

		var count = await _store.CountAsync(null, CancellationToken.None);
		count.ShouldBeGreaterThanOrEqualTo(2);
	}

	// Author≠impl regression lock for bd-60460q (MS-A5): OpenSearchProjectionStore.QueryAsync/CountAsync
	// previously ignored the `filters` parameter and returned/counted the WHOLE index (silent wrong
	// results). The fix applies the filters as exact-match term queries. Non-vacuity: a unique per-run
	// `status` value tags exactly one matching doc (A); the pre-fix whole-index scan would return BOTH A
	// and B (and count >= 2) regardless of the filter -> RED. The fix returns only A and count == 1.
	[Fact]
	public async Task ApplyFiltersInQueryAndCount()
	{
		Assert.SkipWhen(!_available, SkipReason("[infrastructure-unavailable] OpenSearch (Docker) is not available, so this fact did NOT execute. It is reported skipped, never passed: a test that returns early on missing infrastructure is satisfied by doing nothing."));

		// Unique status value so the filter targets exactly this test's docs, independent of any other
		// documents the shared index/container may hold.
		var activeStatus = $"active-{Guid.NewGuid():N}";
		var closedStatus = $"closed-{Guid.NewGuid():N}";

		await _store!.UpsertAsync("filter-a",
			new TestOpenSearchProjection { Id = "filter-a", Name = "Alpha", Status = activeStatus }, CancellationToken.None);
		await _store.UpsertAsync("filter-b",
			new TestOpenSearchProjection { Id = "filter-b", Name = "Beta", Status = closedStatus }, CancellationToken.None);
		(await global::Tests.Shared.Infrastructure.WaitHelpers.WaitUntilAsync(
			async () => await _store.GetByIdAsync("filter-a", CancellationToken.None) is not null
				&& await _store.GetByIdAsync("filter-b", CancellationToken.None) is not null,
			TimeSpan.FromSeconds(30)).ConfigureAwait(false)).ShouldBeTrue();

		var filters = new Dictionary<string, object>(StringComparer.Ordinal) { ["status"] = activeStatus };

		var results = await _store.QueryAsync(filters, null, CancellationToken.None);
		results.Count.ShouldBe(1, "only the doc whose status matches the filter must be returned");
		results[0].Id.ShouldBe("filter-a");

		var count = await _store.CountAsync(filters, CancellationToken.None);
		count.ShouldBe(1, "CountAsync must apply the same filter, not count the whole index");
	}
}

public sealed class TestOpenSearchProjection
{
	public string Id { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public int Value { get; set; }
	public string Status { get; set; } = string.Empty;
}
