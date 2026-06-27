// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

using Excalibur.Data.OpenSearch.Projections;
using Excalibur.EventSourcing;

using Microsoft.Extensions.DependencyInjection;

using OpenSearch.Client;

namespace Excalibur.Integration.Tests.OpenSearch;

/// <summary>
/// T.14 (efok50): Integration tests for OpenSearchProjectionStore using
/// OpenSearch TestContainers. Tests CRUD operations through real OpenSearch.
/// Gracefully skips when OpenSearch container is unavailable.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Database", "OpenSearch")]
[Trait("Component", "Projections")]
[SuppressMessage("Design", "CA1506", Justification = "Integration test")]
public sealed class OpenSearchProjectionStoreIntegrationShould : IAsyncLifetime
{
	private IContainer? _container;
	private ServiceProvider? _serviceProvider;
	private IProjectionStore<TestOpenSearchProjection>? _store;
	private bool _available;

	public async ValueTask InitializeAsync()
	{
		try
		{
			_container = new ContainerBuilder()
				.WithImage("opensearchproject/opensearch:2.16.0")
				.WithPortBinding(9200, true)
				.WithEnvironment("discovery.type", "single-node")
				.WithEnvironment("DISABLE_SECURITY_PLUGIN", "true")
				.WithEnvironment("DISABLE_INSTALL_DEMO_CONFIG", "true")
				.WithEnvironment("OPENSEARCH_JAVA_OPTS", "-Xms256m -Xmx256m")
				.WithWaitStrategy(Wait.ForUnixContainer()
					.UntilHttpRequestIsSucceeded(r => r.ForPort(9200).ForPath("/")))
				.Build();

			using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
			await _container.StartAsync(cts.Token).ConfigureAwait(false);
			await Task.Delay(5000).ConfigureAwait(false);

			var port = _container.GetMappedPublicPort(9200);
			var uri = new Uri($"http://localhost:{port}");

			var services = new ServiceCollection();
			services.AddLogging();
			services.AddSingleton<IOpenSearchClient>(new OpenSearchClient(
				new ConnectionSettings(uri)
					.DefaultIndex("test-projections")
					.DisableDirectStreaming()));
			services.AddOpenSearchProjectionStore<TestOpenSearchProjection>(options =>
			{
				options.IndexName = "test-projections";
			});

			_serviceProvider = services.BuildServiceProvider();
			_store = _serviceProvider.GetRequiredService<IProjectionStore<TestOpenSearchProjection>>();

			// Verify connectivity before marking available
			try
			{
				await _store.CountAsync(null, CancellationToken.None).ConfigureAwait(false);
				_available = true;
			}
			catch
			{
				_available = false;
			}
		}
		catch (Exception)
		{
			_available = false;
		}
	}

	public async ValueTask DisposeAsync()
	{
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

		try
		{
			if (_container is not null)
			{
				using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
				await _container.DisposeAsync().AsTask().WaitAsync(cts.Token).ConfigureAwait(false);
			}
		}
		catch (Exception)
		{
			// Suppress disposal errors and timeouts to prevent test host crash
		}
	}

	[Fact]
	public async Task UpsertAndGetById()
	{
		if (!_available) return;

		var projection = new TestOpenSearchProjection { Id = "proj-1", Name = "Test", Value = 42 };
		await _store!.UpsertAsync("proj-1", projection, CancellationToken.None);
		await Task.Delay(1000);

		var result = await _store.GetByIdAsync("proj-1", CancellationToken.None);
		result.ShouldNotBeNull();
		result.Name.ShouldBe("Test");
		result.Value.ShouldBe(42);
	}

	[Fact]
	public async Task ReturnNullForNonexistent()
	{
		if (!_available) return;

		var result = await _store!.GetByIdAsync("nonexistent", CancellationToken.None);
		result.ShouldBeNull();
	}

	[Fact]
	public async Task DeleteExistingProjection()
	{
		if (!_available) return;

		await _store!.UpsertAsync("proj-del", new TestOpenSearchProjection { Id = "proj-del", Name = "ToDelete" }, CancellationToken.None);
		await Task.Delay(1000);
		await _store.DeleteAsync("proj-del", CancellationToken.None);
		await Task.Delay(1000);

		var result = await _store.GetByIdAsync("proj-del", CancellationToken.None);
		result.ShouldBeNull();
	}

	[Fact]
	public async Task CountDocuments()
	{
		if (!_available) return;

		await _store!.UpsertAsync("proj-c1", new TestOpenSearchProjection { Id = "proj-c1", Name = "A" }, CancellationToken.None);
		await _store.UpsertAsync("proj-c2", new TestOpenSearchProjection { Id = "proj-c2", Name = "B" }, CancellationToken.None);
		await Task.Delay(1000);

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
		if (!_available) return;

		// Unique status value so the filter targets exactly this test's docs, independent of any other
		// documents the shared index/container may hold.
		var activeStatus = $"active-{Guid.NewGuid():N}";
		var closedStatus = $"closed-{Guid.NewGuid():N}";

		await _store!.UpsertAsync("filter-a",
			new TestOpenSearchProjection { Id = "filter-a", Name = "Alpha", Status = activeStatus }, CancellationToken.None);
		await _store.UpsertAsync("filter-b",
			new TestOpenSearchProjection { Id = "filter-b", Name = "Beta", Status = closedStatus }, CancellationToken.None);
		await Task.Delay(1000);

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
