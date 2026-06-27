// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Redis;

using Microsoft.Extensions.Logging.Abstractions;

using StackExchange.Redis;

namespace Excalibur.Integration.Tests.EventSourcing.Redis;

/// <summary>
/// Author≠impl regression lock for bd-834a9c: <see cref="RedisEventStore.AppendAsync"/> must enforce
/// optimistic concurrency on the <b>new-aggregate create</b> path (<c>expectedVersion == -1</c>). The
/// fix is a single guard in the append Lua script (<c>RedisEventStore.cs:49</c>): when
/// <c>expected_version == -1</c> the stream MUST be empty (<c>current_length == 0</c>), otherwise the
/// script returns the conflict sentinel and the C# path maps it to
/// <see cref="AppendResult.CreateConcurrencyConflict"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>verify-against-real-infra-not-mock:</b> both facts run against a real Redis (TestContainers) and
/// assert observable BEHAVIOR through the real seam — the atomicity of the Lua check-and-set on the single
/// threaded Redis server is exactly the thing under test and cannot be reproduced by a mocked
/// <c>IDatabase</c>. <c>_redisFixture.DockerAvailable.ShouldBeTrue(...)</c> makes the lock NON-SKIPPED (a
/// skipped concurrency-safety test is the exact gap that ships a lost-update / corrupt-stream bug).
/// Serial (<c>-m:1</c>); per-test isolation via a unique aggregate id + a unique stream key prefix so each
/// run starts on an empty stream.
/// </para>
/// <para>
/// <b>Seam note:</b> <see cref="RedisEventStore.AppendAsync"/> signals a concurrency conflict by RETURNING
/// an <see cref="AppendResult"/> with <see cref="AppendResult.Success"/> <c>== false</c> and
/// <see cref="AppendResult.IsConcurrencyConflict"/> <c>== true</c> — it does not throw an exception. These
/// assertions are written against that returned-result contract (the bead text's "throws the concurrency
/// exception" describes the conflict semantics, not the C# mechanism).
/// </para>
/// <para>
/// <b>Non-vacuity (RED on the pre-fix code):</b> the pre-fix Lua had the existing-aggregate check
/// (<c>expected_version &gt;= 0 and current_length ~= expected_version</c>) but NO guard for the
/// <c>expected_version == -1</c> sentinel, so a create unconditionally <c>XADD</c>ed regardless of the
/// current stream length. Against that mutant: (1) two concurrent creates would BOTH succeed → the stream
/// holds two events both labelled version 0 (a lost-update / corrupt stream) → "exactly one success" and
/// "stream length 1" assertions RED; (2) an erroneous re-create at <c>-1</c> onto a non-empty stream would
/// append a duplicate version-0 event instead of conflicting → the conflict + "stream unchanged"
/// assertions RED. The line-49 guard makes both GREEN.
/// </para>
/// <para>
/// Production RED-proof against the pre-fix impl is deferred to post-commit (impl reserved by
/// BackendDeveloper; this test must not modify <c>src/</c>).
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "EventSourcing")]
[Trait("Database", "Redis")]
public sealed class RedisEventStoreConcurrentCreateGuardShould : IntegrationTestBase, IClassFixture<RedisContainerFixture>
{
	private const string AggregateType = "ConcurrentCreateTestAggregate";

	private readonly RedisContainerFixture _redisFixture;

	public RedisEventStoreConcurrentCreateGuardShould(RedisContainerFixture redisFixture)
	{
		_redisFixture = redisFixture;
	}

	[Fact]
	public async Task AllowExactlyOneWinnerWhenConcurrentlyCreatingTheSameNewAggregate()
	{
		// bd-834a9c — two racing new-aggregate creates (both expectedVersion == -1) must NOT both succeed:
		// exactly one wins, the other gets a concurrency conflict, and no lost update corrupts the stream.
		_redisFixture.DockerAvailable.ShouldBeTrue(
			"bd-834a9c concurrent-create concurrency control is a data-corruption safety control — this real-Redis lock must never be skipped");

		await using var connection = await ConnectionMultiplexer.ConnectAsync(_redisFixture.ConnectionString);
		var store = CreateEventStore(connection);

		var aggregateId = Guid.NewGuid().ToString();
		var eventA = new TestDomainEvent(aggregateId, 0);
		var eventB = new TestDomainEvent(aggregateId, 0);

		// Act — fire both creates concurrently at the same expected version (-1, the documented new-aggregate sentinel).
		var appendA = Task.Run(
			() => store.AppendAsync(aggregateId, AggregateType, [eventA], -1, TestCancellationToken).AsTask(),
			TestCancellationToken);
		var appendB = Task.Run(
			() => store.AppendAsync(aggregateId, AggregateType, [eventB], -1, TestCancellationToken).AsTask(),
			TestCancellationToken);

		var results = await Task.WhenAll(appendA, appendB);

		// Assert — exactly one create succeeded; the other reported a concurrency conflict (not a generic failure).
		var successes = results.Count(r => r.Success);
		successes.ShouldBe(1, "exactly one of two concurrent new-aggregate creates may succeed");

		var loser = results.Single(r => !r.Success);
		loser.IsConcurrencyConflict.ShouldBeTrue(
			"the create that lost the race must report a concurrency conflict, not a generic failure");

		var winner = results.Single(r => r.Success);
		winner.NextExpectedVersion.ShouldBe(0, "the winning single-event create advances the stream to version 0");

		// Assert — NO lost update: the surviving stream holds EXACTLY one event (the winner's), at version 0.
		var loaded = await store.LoadAsync(aggregateId, AggregateType, TestCancellationToken);
		loaded.Count.ShouldBe(1, "the loser's events must never have been appended — a second event is a corrupt stream / lost update");
		loaded[0].Version.ShouldBe(0);
		loaded[0].EventId.ShouldBeOneOf(eventA.EventId, eventB.EventId);
	}

	[Fact]
	public async Task RejectAnErroneousRecreateAtTheNewAggregateSentinelOnANonEmptyStream()
	{
		// bd-834a9c — the guard must also reject a stray expectedVersion == -1 append onto an ALREADY-CREATED
		// (non-empty) stream, rather than silently appending a duplicate first event.
		_redisFixture.DockerAvailable.ShouldBeTrue(
			"bd-834a9c new-aggregate guard is a data-corruption safety control — this real-Redis lock must never be skipped");

		await using var connection = await ConnectionMultiplexer.ConnectAsync(_redisFixture.ConnectionString);
		var store = CreateEventStore(connection);

		var aggregateId = Guid.NewGuid().ToString();

		// Arrange — a legitimate create succeeds and the stream is now non-empty (one event at version 0).
		var created = new TestDomainEvent(aggregateId, 0);
		var createResult = await store.AppendAsync(aggregateId, AggregateType, [created], -1, TestCancellationToken);
		createResult.Success.ShouldBeTrue("the first new-aggregate create on an empty stream must succeed");

		// Act — an erroneous second create reusing the -1 new-aggregate sentinel on the now non-empty stream.
		var stray = new TestDomainEvent(aggregateId, 0);
		var strayResult = await store.AppendAsync(aggregateId, AggregateType, [stray], -1, TestCancellationToken);

		// Assert — the stray create is rejected as a concurrency conflict; it is NOT appended.
		strayResult.Success.ShouldBeFalse("a -1 create onto a non-empty stream must not succeed");
		strayResult.IsConcurrencyConflict.ShouldBeTrue("the rejection must be a concurrency conflict");

		// Assert — the stream is unchanged: still exactly the original single event.
		var loaded = await store.LoadAsync(aggregateId, AggregateType, TestCancellationToken);
		loaded.Count.ShouldBe(1, "the rejected -1 re-create must not have appended a duplicate first event");
		loaded[0].EventId.ShouldBe(created.EventId);
		loaded[0].Version.ShouldBe(0);
	}

	private RedisEventStore CreateEventStore(ConnectionMultiplexer connection)
	{
		var options = Microsoft.Extensions.Options.Options.Create(new RedisEventStoreOptions
		{
			ConnectionString = _redisFixture.ConnectionString,
			// Unique per-instance stream key prefix so each test's streams cannot collide across runs/classes.
			StreamKeyPrefix = $"es-cc-{Guid.NewGuid():N}",
			DatabaseIndex = -1,
		});

		return new RedisEventStore(connection, options, NullLogger<RedisEventStore>.Instance);
	}

	private sealed record TestDomainEvent : IDomainEvent
	{
		public TestDomainEvent(string aggregateId, long version)
		{
			EventId = Guid.NewGuid().ToString();
			AggregateId = aggregateId;
			Version = version;
			OccurredAt = DateTimeOffset.UtcNow;
			EventType = nameof(TestDomainEvent);
		}

		public string EventId { get; init; }
		public string AggregateId { get; init; }
		public long Version { get; init; }
		public DateTimeOffset OccurredAt { get; init; }
		public string EventType { get; init; }
		public IDictionary<string, object>? Metadata => null;
	}
}
