// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

using Excalibur.Dispatch;
using Excalibur.EventSourcing;

using Google.Cloud.Storage.V1;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Excalibur.Integration.Tests.TieredStorage;

/// <summary>
/// Real-infrastructure lost-update lock for the Google Cloud Storage cold event store: a concurrent archive
/// of the same aggregate must never drop events a racing writer already committed. The optimistic-concurrency
/// read-modify-write (capture the object's generation on read, conditional IfGenerationMatch upload, re-read
/// and retry on a precondition failure) guarantees the union survives.
/// </summary>
/// <remarks>
/// <para>
/// Shape, mirroring the S3 and Azure Blob siblings: seed v0..v2, then concurrently <c>WriteAsync([v3,v4])</c>
/// against <c>WriteAsync([v3,v4,v5,v6])</c> - superset over subset, same base. With the conditional upload in
/// place, in EITHER commit order the loser's upload fails its precondition, it re-reads, and only its
/// still-absent versions append, so the final archive is v0..v6: seven events, deterministic. The RED mutant
/// is to drop <c>IfGenerationMatch</c> - if the subset commits last it blind-overwrites the superset and v5,
/// v6 are lost (five events). Never skipped.
/// </para>
/// <para>
/// <strong>The image tag is a correctness pin, not a routine version.</strong> This lock's entire subject is
/// the generation precondition, and the emulator has to enforce it or the test measures nothing. Simple
/// uploads ignored <c>ifGenerationMatch</c> outright until 1.56.0 - the parameter was parsed and then
/// discarded, so a deliberately wrong generation was accepted and the object overwritten. Under such a build
/// both racing writers succeed, last-writer-wins silently drops the superset's tail, and this test would
/// report a lost update that the production store does not have: the emulator cannot enforce the primitive
/// under test. Do not lower this pin.
/// </para>
/// <para>
/// There is no Testcontainers module for this image, so it is built through the generic
/// <see cref="ContainerBuilder"/> - a module is only ever a convenience wrapper over exactly that, and the
/// sibling Consul fixture in this assembly takes the same route. The server defaults to HTTPS with a
/// self-signed certificate; <c>-scheme http</c> avoids having to trust it, and <c>-backend memory</c> keeps
/// the archive off the container filesystem.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Database", "Gcs")]
[Trait("Component", "EventStore")]
public sealed class GcsColdEventStoreLostUpdateShould : IAsyncLifetime
{
	// A SCOPED tenant, not Untenanted. Untenanted compiles equally well and would leave the tenant-keyed
	// object path unexercised -- the shape the cold-store contract exists to enforce.
	private static readonly KeyedTenantPartition Tenant =
		KeyedTenantPartition.Scoped("cold-store-lost-update-tenant");

	private const string BucketName = "cold-events-lostupdate-test";
	private const string AggregateType = "ColdLostUpdateAggregate";
	private const int ServerPort = 4443;

	private IContainer? _container;
	private ServiceProvider? _serviceProvider;
	private IColdEventStore? _store;
	private bool _available;

	/// <summary>Diagnostic: why initialization failed, so an unavailable emulator names its own reason.</summary>
	/// <remarks>
	/// Discarding this exception makes the failure unclassifiable: "the emulator must be available" reads the
	/// same whether the image could not be pulled, the container died during startup, or the storage client
	/// rejected the endpoint - three problems with three different fixes.
	/// </remarks>
	private string? _initError;

	public async ValueTask InitializeAsync()
	{
		try
		{
			using var startCts = new CancellationTokenSource(TimeSpan.FromMinutes(3));

			_container = new ContainerBuilder()
				.WithImage("fsouza/fake-gcs-server:1.56.0")
				.WithName($"gcs-cold-store-lostupdate-{Guid.NewGuid():N}")
				.WithPortBinding(ServerPort, true)
				.WithCommand("-scheme", "http", "-backend", "memory")
				.WithWaitStrategy(Wait.ForUnixContainer()
					.UntilHttpRequestIsSucceeded(r => r.ForPath("/storage/v1/b").ForPort(ServerPort)))
				.Build();

			await _container.StartAsync(startCts.Token).ConfigureAwait(false);

			var endpoint = $"http://{_container.Hostname}:{_container.GetMappedPublicPort(ServerPort)}";

			// BaseUri rather than the STORAGE_EMULATOR_HOST environment variable: the variable is
			// process-global, and this assembly runs its collections in parallel, so pointing one test's
			// client at an emulator would point every client in the process at it.
			var storageClient = new StorageClientBuilder
			{
				BaseUri = $"{endpoint}/storage/v1/",
				UnauthenticatedAccess = true,
			}.Build();

			_ = await storageClient.CreateBucketAsync("test-project", BucketName, cancellationToken: startCts.Token)
				.ConfigureAwait(false);

			var services = new ServiceCollection();
			_ = services.AddLogging();
			_ = services.AddExcaliburEventSourcing(builder =>
				builder.UseGcsColdEventStore(gcs =>
					gcs.Client(storageClient)
						.BucketName(BucketName)
						.ObjectPrefix("events")));

			_serviceProvider = services.BuildServiceProvider();
			_store = _serviceProvider.GetRequiredService<IColdEventStore>();
			_available = true;
		}
		catch (Exception ex)
		{
			_available = false;
			_initError = ex.ToString();
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
			// best-effort teardown
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

	/// <summary>
	/// Number of independent aggregates raced in one run.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <strong>One race is not enough, and that is measured rather than assumed.</strong> A single
	/// superset/subset pair only loses data when the SUBSET happens to commit second, which is a scheduling
	/// accident: with the precondition deliberately removed, one pair reproduced the loss in 1 of 5 runs.
	/// A lock that detects its own defect a fifth of the time is a lock that reports green on a broken store
	/// four times out of five.
	/// </para>
	/// <para>
	/// Distinct aggregates address distinct objects, so they do not contend with each other - each pair is an
	/// INDEPENDENT trial of the same race. Racing this many pairs turns a coin flip into a near-certainty
	/// while still costing one container and a couple of seconds.
	/// </para>
	/// <para>
	/// This also repairs what the green MEANS. With one pair, a green run is equally consistent with an
	/// emulator that enforces the generation precondition and one that ignores it entirely - both are green
	/// most of the time. Across this many pairs, an emulator that ignored preconditions could not keep every
	/// aggregate whole, so passing is itself evidence that the primitive under test is really being enforced.
	/// </para>
	/// </remarks>
	private const int RaceCount = 25;

	[Fact]
	public async Task Preserve_a_racing_writers_events_under_concurrent_same_aggregate_archive()
	{
		_available.ShouldBeTrue(
			"the Google Cloud Storage emulator must be available - a real-infra lost-update lock is never "
			+ "skipped. Initialization error: " + (_initError ?? "(none recorded - InitializeAsync did not run)"));

		var ct = CancellationToken.None;
		var aggregateIds = Enumerable.Range(0, RaceCount).Select(_ => $"agg-{Guid.NewGuid():N}").ToArray();

		// Seed each aggregate with a committed prefix v0..v2.
		foreach (var aggregateId in aggregateIds)
		{
			_ = await _store!.WriteAsync(
				Tenant, aggregateId, [Event(aggregateId, 0), Event(aggregateId, 1), Event(aggregateId, 2)], ct);
		}

		// Concurrent superset/subset archive, per aggregate. Every writer is started before any is awaited,
		// so the pairs genuinely overlap rather than running one pair at a time.
		await Task.WhenAll(aggregateIds.SelectMany(aggregateId => new[]
		{
			_store!.WriteAsync(Tenant, aggregateId, [Event(aggregateId, 3), Event(aggregateId, 4)], ct),
			_store.WriteAsync(
				Tenant,
				aggregateId,
				[Event(aggregateId, 3), Event(aggregateId, 4), Event(aggregateId, 5), Event(aggregateId, 6)],
				ct),
		})).ConfigureAwait(false);

		// Report EVERY aggregate that lost an event, not just the first. Which ones lost is the difference
		// between "the precondition is missing" and "one aggregate hit something else".
		var expected = Enumerable.Range(0, 7).Select(i => (long)i).ToArray();
		var lost = new List<string>();
		foreach (var aggregateId in aggregateIds)
		{
			var versions = (await _store!.ReadAsync(Tenant, aggregateId, ct)).Select(e => e.Version).ToArray();
			if (!versions.SequenceEqual(expected))
			{
				lost.Add($"{aggregateId}: [{string.Join(",", versions)}]");
			}
		}

		// The optimistic IfGenerationMatch retry merges both writers - no committed event is ever dropped.
		lost.ShouldBeEmpty(
			$"a concurrent archive must never drop events a racing writer already committed: {lost.Count} of "
			+ $"{RaceCount} raced aggregates did not end at v0..v6. {string.Join(" | ", lost)}");
	}
}
