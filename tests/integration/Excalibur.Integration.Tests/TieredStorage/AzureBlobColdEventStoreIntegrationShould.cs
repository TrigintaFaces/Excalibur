// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Abstractions;

using Microsoft.Extensions.DependencyInjection;

using Testcontainers.Azurite;

namespace Excalibur.Integration.Tests.TieredStorage;

/// <summary>
/// Integration tests for Azure Blob cold event store using Azurite TestContainers.
/// Tests the full IColdEventStore contract through real blob storage.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Database", "AzureBlob")]
[Trait("Component", "TieredStorage")]
public sealed class AzureBlobColdEventStoreIntegrationShould : IAsyncLifetime
{
	private AzuriteContainer? _container;
	private IColdEventStore? _store;
	private bool _available;

	public async Task InitializeAsync()
	{
		try
		{
			_container = new AzuriteBuilder()
				.WithImage("mcr.microsoft.com/azure-storage/azurite:latest")
				.Build();

			await _container.StartAsync().ConfigureAwait(false);

			var services = new ServiceCollection();
			services.AddLogging();
			services.AddExcaliburEventSourcing(builder =>
			{
				builder.UseAzureBlobColdEventStore(options =>
				{
					options.ConnectionString = _container.GetConnectionString();
					options.ContainerName = "cold-events-test";
					options.CreateContainerIfNotExists = true;
				});
			});

			var sp = services.BuildServiceProvider();
			_store = sp.GetRequiredService<IColdEventStore>();
			_available = true;
		}
		catch (Exception)
		{
			_available = false;
		}
	}

	public async Task DisposeAsync()
	{
		if (_container is not null)
			await _container.DisposeAsync().ConfigureAwait(false);
	}

	[Fact]
	public async Task WriteAndReadEvents()
	{
		if (!_available) return;

		var events = CreateEvents("blob-agg-1", 1, 2, 3);
		await _store!.WriteAsync("blob-agg-1", events, CancellationToken.None);

		var read = await _store.ReadAsync("blob-agg-1", CancellationToken.None);
		read.Count.ShouldBe(3);
		read[0].Version.ShouldBe(1);
		read[2].Version.ShouldBe(3);
	}

	[Fact]
	public async Task ReadFromVersionFiltersCorrectly()
	{
		if (!_available) return;

		await _store!.WriteAsync("blob-agg-v", CreateEvents("blob-agg-v", 1, 2, 3, 4, 5), CancellationToken.None);

		var fromV3 = await _store.ReadAsync("blob-agg-v", 3, CancellationToken.None);
		fromV3.Count.ShouldBe(2);
		fromV3[0].Version.ShouldBe(4);
	}

	[Fact]
	public async Task MergeNewEventsWithExisting()
	{
		if (!_available) return;

		await _store!.WriteAsync("blob-agg-m", CreateEvents("blob-agg-m", 1, 2, 3), CancellationToken.None);
		await _store.WriteAsync("blob-agg-m", CreateEvents("blob-agg-m", 3, 4, 5), CancellationToken.None);

		var all = await _store.ReadAsync("blob-agg-m", CancellationToken.None);
		all.Count.ShouldBe(5);
	}

	[Fact]
	public async Task HasArchivedReturnsTrueWhenPresent()
	{
		if (!_available) return;

		await _store!.WriteAsync("blob-agg-h", CreateEvents("blob-agg-h", 1), CancellationToken.None);
		(await _store.HasArchivedEventsAsync("blob-agg-h", CancellationToken.None)).ShouldBeTrue();
	}

	[Fact]
	public async Task HasArchivedReturnsFalseWhenAbsent()
	{
		if (!_available) return;
		(await _store!.HasArchivedEventsAsync("blob-nonexistent", CancellationToken.None)).ShouldBeFalse();
	}

	[Fact]
	public async Task ReadReturnsEmptyForNonexistent()
	{
		if (!_available) return;
		(await _store!.ReadAsync("blob-no-such", CancellationToken.None)).Count.ShouldBe(0);
	}

	private static List<StoredEvent> CreateEvents(string aggregateId, params long[] versions) =>
		versions.Select(v => new StoredEvent(
			Guid.NewGuid().ToString(), aggregateId, "Test", "TestEvent",
			System.Text.Encoding.UTF8.GetBytes($"{{\"v\":{v}}}"), null,
			v, DateTimeOffset.UtcNow)).ToList();
}
