// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.S3;

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.AwsS3;

using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Testcontainers.LocalStack;

namespace Excalibur.Integration.Tests.TieredStorage;

/// <summary>
/// Real-infrastructure lost-update lock for <see cref="AwsS3ColdEventStore"/> (4xnwo9): a concurrent archive of the
/// same aggregate must never drop events a racing writer already committed. The optimistic-concurrency read-modify-
/// write (capture ETag → conditional <c>IfMatch</c> put → re-read+retry on 412/409) guarantees the union survives.
/// </summary>
/// <remarks>
/// Shape (SoftwareArchitect-confirmed): seed v0..v2, then concurrently <c>WriteAsync([v3,v4])</c> ‖
/// <c>WriteAsync([v3,v4,v5,v6])</c> (superset ⊇ subset, same base). With the fix, in EITHER commit order the loser's
/// conditional put fails, it re-reads, and only its still-greater versions append → final = v0..v6 (7 events,
/// deterministic). RED mutant: drop the <c>IfMatch</c> condition → if the subset writes last it blind-overwrites the
/// superset → v5,v6 LOST (5 events). Never skipped.
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Database", "S3")]
[Trait("Component", "EventStore")]
public sealed class AwsS3ColdEventStoreLostUpdateShould : IAsyncLifetime
{
	// A SCOPED tenant, not Untenanted. Untenanted compiles equally well and would leave the
	// tenant-keyed object path unexercised -- the shape the cold-store contract exists to enforce.
	private static readonly KeyedTenantPartition Tenant =
		KeyedTenantPartition.Scoped("cold-store-tenant");

	private const string BucketName = "cold-events-lostupdate-test";
	private const string AggregateType = "ColdLostUpdateAggregate";

	private LocalStackContainer? _container;
	private IAmazonS3? _s3Client;
	private IColdEventStore? _store;
	private bool _available;

	public async ValueTask InitializeAsync()
	{
		try
		{
			// LocalStack 4+ is REQUIRED here, not a routine version bump. This lock's whole subject is S3
			// conditional writes (IfMatch / IfNoneMatch), which AWS added to PutObject in Nov 2024 and
			// LocalStack 3.8 does not implement: it ACCEPTS a PutObject carrying a deliberately wrong
			// If-Match and returns 200, overwriting the object. Under 3.8 both racing writers therefore
			// succeed, last-writer-wins silently drops the superset's tail, and this test reports a
			// lost update that the production store does not actually have — the emulator cannot enforce
			// the primitive under test. Verified on 4: wrong If-Match -> 412, If-None-Match:* on an
			// existing key -> 412, object unchanged. Do not lower this pin.
			_container = new LocalStackBuilder().WithImage("localstack/localstack:4").Build();
			using var startCts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
			await _container.StartAsync(startCts.Token).ConfigureAwait(false);

			var config = new AmazonS3Config
			{
				ServiceURL = _container.GetConnectionString(),
				ForcePathStyle = true,
				UseHttp = true,
				Timeout = TimeSpan.FromSeconds(10),
				MaxErrorRetry = 1,
			};
			_s3Client = new AmazonS3Client("test", "test", config);
			await _s3Client.PutBucketAsync(BucketName, startCts.Token).ConfigureAwait(false);
			_store = new AwsS3ColdEventStore(_s3Client, BucketName, "events", NullLogger<AwsS3ColdEventStore>.Instance);
			_available = true;
		}
		catch (Exception)
		{
			_available = false;
		}
	}

	public async ValueTask DisposeAsync()
	{
		_s3Client?.Dispose();
		if (_container is not null)
		{
			using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
			try
			{
				await _container.DisposeAsync().AsTask().WaitAsync(cts.Token).ConfigureAwait(false);
			}
			catch (Exception)
			{
				// best-effort teardown
			}
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
	/// accident. Measured on the sibling Google Cloud Storage lock with the conditional write deliberately
	/// removed, one pair reproduced the loss in 1 of 5 runs; this many pairs reproduced it in 5 of 5. A lock
	/// that detects its own defect a fifth of the time reports green on a broken store four times out of five.
	/// </para>
	/// <para>
	/// Distinct aggregates address distinct objects, so they do not contend with each other - each pair is an
	/// INDEPENDENT trial of the same race. Racing this many pairs turns a coin flip into a near-certainty
	/// while still costing one container and a couple of seconds.
	/// </para>
	/// <para>
	/// This also repairs what the green MEANS. With one pair, a green run is equally consistent with an
	/// emulator that enforces the conditional write and one that ignores it - both are green most of the time.
	/// Across this many pairs, an emulator that ignored the condition could not keep every aggregate whole, so
	/// passing is itself evidence that the primitive under test is really being enforced.
	/// </para>
	/// </remarks>
	private const int RaceCount = 25;

	[Fact]
	public async Task Preserve_a_racing_writers_events_under_concurrent_same_aggregate_archive()
	{
		_available.ShouldBeTrue("LocalStack S3 must be available - real-infra lost-update lock is never skipped.");

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
		// between "the conditional write is missing" and "one aggregate hit something else".
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

		// The optimistic conditional-write retry merges both writers - no committed event is ever dropped.
		lost.ShouldBeEmpty(
			$"a concurrent archive must never drop events a racing writer already committed: {lost.Count} of "
			+ $"{RaceCount} raced aggregates did not end at v0..v6. {string.Join(" | ", lost)}");
	}
}
