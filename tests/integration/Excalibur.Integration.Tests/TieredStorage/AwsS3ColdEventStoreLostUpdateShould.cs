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
			_container = new LocalStackBuilder().WithImage("localstack/localstack:latest").Build();
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

	[Fact]
	public async Task Preserve_a_racing_writers_events_under_concurrent_same_aggregate_archive()
	{
		_available.ShouldBeTrue("LocalStack S3 must be available - real-infra lost-update lock is never skipped.");

		var aggregateId = $"agg-{Guid.NewGuid():N}";
		var ct = CancellationToken.None;

		// Seed a committed prefix v0..v2.
		await _store!.WriteAsync(aggregateId, [Event(aggregateId, 0), Event(aggregateId, 1), Event(aggregateId, 2)], ct);

		// Concurrent superset/subset archive of the same aggregate.
		var subset = new StoredEvent[] { Event(aggregateId, 3), Event(aggregateId, 4) };
		var superset = new StoredEvent[] { Event(aggregateId, 3), Event(aggregateId, 4), Event(aggregateId, 5), Event(aggregateId, 6) };

		await Task.WhenAll(
			_store.WriteAsync(aggregateId, subset, ct),
			_store.WriteAsync(aggregateId, superset, ct)).ConfigureAwait(false);

		var read = await _store.ReadAsync(aggregateId, ct);

		// The optimistic IfMatch retry merges both writers — no committed event is ever dropped.
		read.Select(e => e.Version).ShouldBe(
			Enumerable.Range(0, 7).Select(i => (long)i),
			"a concurrent archive must never drop events a racing writer already committed (v0..v6 all survive)");
	}
}
