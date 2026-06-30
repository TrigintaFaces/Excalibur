// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Testcontainers.Azurite;

namespace Excalibur.Integration.Tests.TieredStorage;

/// <summary>
/// Real-infrastructure lost-update lock for the Azure Blob cold event store (4xnwo9): a concurrent archive of the
/// same aggregate must never drop events a racing writer already committed, via the optimistic <c>If-Match</c> ETag
/// read-modify-write + re-read-retry.
/// </summary>
/// <remarks>
/// Same superset/subset shape as the S3 lock (SoftwareArchitect-confirmed): seed v0..v2, concurrently
/// <c>WriteAsync([v3,v4])</c> ‖ <c>WriteAsync([v3,v4,v5,v6])</c> → final = v0..v6 (7) in either order. RED mutant:
/// drop <c>If-Match</c> → subset-writes-last blind-overwrites the superset → v5,v6 lost. Never skipped.
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Database", "AzureBlob")]
[Trait("Component", "EventStore")]
public sealed class AzureBlobColdEventStoreLostUpdateShould : IAsyncLifetime
{
	private const string AggregateType = "ColdLostUpdateAggregate";

	private AzuriteContainer? _container;
	private ServiceProvider? _serviceProvider;
	private IColdEventStore? _store;
	private bool _available;

	public async ValueTask InitializeAsync()
	{
		try
		{
			_container = new AzuriteBuilder().WithImage("mcr.microsoft.com/azure-storage/azurite:latest").Build();
			await _container.StartAsync().ConfigureAwait(false);

			var services = new ServiceCollection();
			services.AddLogging();
			services.AddExcaliburEventSourcing(builder =>
			{
				builder.UseAzureBlobColdEventStore(blob =>
				{
					blob.ConnectionString(_container.GetConnectionString())
						.ContainerName("cold-events-lostupdate-test")
						.CreateContainerIfNotExists();
				});
			});

			_serviceProvider = services.BuildServiceProvider();
			_store = _serviceProvider.GetRequiredService<IColdEventStore>();
			_available = true;
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
			// best-effort
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
			// suppress teardown errors
		}
	}

	private static StoredEvent Event(string aggregateId, long version) => new(
		EventId: Guid.NewGuid().ToString(),
		AggregateId: aggregateId,
		AggregateType: AggregateType,
		EventType: "TestEvent",
		EventData: System.Text.Encoding.UTF8.GetBytes($"data-{version}"),
		Metadata: null,
		Version: version,
		Timestamp: DateTimeOffset.UtcNow);

	[Fact]
	public async Task Preserve_a_racing_writers_events_under_concurrent_same_aggregate_archive()
	{
		_available.ShouldBeTrue("Azurite must be available - real-infra lost-update lock is never skipped.");

		var aggregateId = $"agg-{Guid.NewGuid():N}";
		var ct = CancellationToken.None;

		await _store!.WriteAsync(aggregateId, [Event(aggregateId, 0), Event(aggregateId, 1), Event(aggregateId, 2)], ct);

		var subset = new StoredEvent[] { Event(aggregateId, 3), Event(aggregateId, 4) };
		var superset = new StoredEvent[] { Event(aggregateId, 3), Event(aggregateId, 4), Event(aggregateId, 5), Event(aggregateId, 6) };

		await Task.WhenAll(
			_store.WriteAsync(aggregateId, subset, ct),
			_store.WriteAsync(aggregateId, superset, ct)).ConfigureAwait(false);

		var read = await _store.ReadAsync(aggregateId, ct);

		read.Select(e => e.Version).ShouldBe(
			Enumerable.Range(0, 7).Select(i => (long)i),
			"a concurrent archive must never drop events a racing writer already committed (v0..v6 all survive)");
	}
}
