// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing;

using Microsoft.Extensions.DependencyInjection;

using Testcontainers.Azurite;

using Xunit;

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
	// The cold-store contract keys every object by tenant. These arms use a SCOPED tenant
	// rather than Untenanted deliberately: Untenanted is the weakest path and would leave the
	// tenant-carrying key shape — the thing the contract change exists to enforce — unexercised.
	private static readonly KeyedTenantPartition Tenant =
		KeyedTenantPartition.Scoped("cold-store-tenant");

	private AzuriteContainer? _container;
	private ServiceProvider? _serviceProvider;
	private IColdEventStore? _store;
	private bool _available;
	private Exception? _unavailableCause;

	/// <summary>Appends the captured startup cause to a skip reason, so the log says WHY.</summary>
	private string SkipReason(string reason) =>
		_unavailableCause is null ? reason : reason + " Cause: " + _unavailableCause.GetType().Name + ": " + _unavailableCause.Message;

	public async ValueTask InitializeAsync()
	{
		try
		{
			// --skipApiVersionCheck is REQUIRED, not a convenience, and its absence is why this suite
			// reported "Azure Blob is not available" while the container was up and serving the sibling
			// lost-update suite. The Azure.Storage.Blobs SDK this repo references negotiates a service
			// API version newer than any published Azurite image accepts, so Azurite rejects the request
			// outright ("The API version 2026-02-06 is not supported") before any blob operation runs.
			// That is client/emulator VERSION SKEW, not an outage: the container starts and answers, it
			// just refuses the header. The flag is Azurite's own documented remedy, named in its error.
			//
			// It relaxes only the version GATE, not blob semantics -- conditional writes and ETag
			// behaviour remain enforced -- so the arms below still test what they claim to test.
			_container = new AzuriteBuilder()
				.WithImage("mcr.microsoft.com/azure-storage/azurite:3.36.0")
				.WithCommand("--skipApiVersionCheck")
				.Build();

			await _container.StartAsync().ConfigureAwait(false);

			var services = new ServiceCollection();
			services.AddLogging();
			services.AddExcaliburEventSourcing(builder =>
			{
				builder.UseAzureBlobColdEventStore(blob =>
				{
					blob.ConnectionString(_container.GetConnectionString())
						.ContainerName("cold-events-test")
						.CreateContainerIfNotExists();
				});
			});

			_serviceProvider = services.BuildServiceProvider();
			_store = _serviceProvider.GetRequiredService<IColdEventStore>();
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
	public async Task WriteAndReadEvents()
	{
		Assert.SkipWhen(!_available, SkipReason("[infrastructure-unavailable] Azure Blob (Azurite/Docker) is not available, so this fact did NOT execute. It is reported skipped, never passed: a test that returns early on missing infrastructure is satisfied by doing nothing."));

		var events = CreateEvents("blob-agg-1", 1, 2, 3);
		await _store!.WriteAsync(Tenant, "blob-agg-1", events, CancellationToken.None);

		var read = await _store.ReadAsync(Tenant, "blob-agg-1", CancellationToken.None);
		read.Count.ShouldBe(3);
		read[0].Version.ShouldBe(1);
		read[2].Version.ShouldBe(3);
	}

	[Fact]
	public async Task ReadFromVersionFiltersCorrectly()
	{
		Assert.SkipWhen(!_available, SkipReason("[infrastructure-unavailable] Azure Blob (Azurite/Docker) is not available, so this fact did NOT execute. It is reported skipped, never passed: a test that returns early on missing infrastructure is satisfied by doing nothing."));

		await _store!.WriteAsync(Tenant, "blob-agg-v", CreateEvents("blob-agg-v", 1, 2, 3, 4, 5), CancellationToken.None);

		var fromV3 = await _store.ReadAsync(Tenant, "blob-agg-v", 3, CancellationToken.None);
		fromV3.Count.ShouldBe(2);
		fromV3[0].Version.ShouldBe(4);
	}

	[Fact]
	public async Task MergeNewEventsWithExisting()
	{
		Assert.SkipWhen(!_available, SkipReason("[infrastructure-unavailable] Azure Blob (Azurite/Docker) is not available, so this fact did NOT execute. It is reported skipped, never passed: a test that returns early on missing infrastructure is satisfied by doing nothing."));

		await _store!.WriteAsync(Tenant, "blob-agg-m", CreateEvents("blob-agg-m", 1, 2, 3), CancellationToken.None);
		await _store.WriteAsync(Tenant, "blob-agg-m", CreateEvents("blob-agg-m", 3, 4, 5), CancellationToken.None);

		var all = await _store.ReadAsync(Tenant, "blob-agg-m", CancellationToken.None);
		all.Count.ShouldBe(5);
	}

	[Fact]
	public async Task HasArchivedReturnsTrueWhenPresent()
	{
		Assert.SkipWhen(!_available, SkipReason("[infrastructure-unavailable] Azure Blob (Azurite/Docker) is not available, so this fact did NOT execute. It is reported skipped, never passed: a test that returns early on missing infrastructure is satisfied by doing nothing."));

		await _store!.WriteAsync(Tenant, "blob-agg-h", CreateEvents("blob-agg-h", 1), CancellationToken.None);
		(await _store.HasArchivedEventsAsync(Tenant, "blob-agg-h", CancellationToken.None)).ShouldBeTrue();
	}

	[Fact]
	public async Task HasArchivedReturnsFalseWhenAbsent()
	{
		Assert.SkipWhen(!_available, SkipReason("[infrastructure-unavailable] Azure Blob (Azurite/Docker) is not available, so this fact did NOT execute. It is reported skipped, never passed: a test that returns early on missing infrastructure is satisfied by doing nothing."));
		(await _store!.HasArchivedEventsAsync(Tenant, "blob-nonexistent", CancellationToken.None)).ShouldBeFalse();
	}

	[Fact]
	public async Task ReadReturnsEmptyForNonexistent()
	{
		Assert.SkipWhen(!_available, SkipReason("[infrastructure-unavailable] Azure Blob (Azurite/Docker) is not available, so this fact did NOT execute. It is reported skipped, never passed: a test that returns early on missing infrastructure is satisfied by doing nothing."));
		(await _store!.ReadAsync(Tenant, "blob-no-such", CancellationToken.None)).Count.ShouldBe(0);
	}

	private static List<StoredEvent> CreateEvents(string aggregateId, params long[] versions) =>
		versions.Select(v => new StoredEvent(
			Guid.NewGuid().ToString(), aggregateId, "Test", "TestEvent",
			System.Text.Encoding.UTF8.GetBytes($"{{\"v\":{v}}}"), null,
			v, DateTimeOffset.UtcNow)).ToList();
}
