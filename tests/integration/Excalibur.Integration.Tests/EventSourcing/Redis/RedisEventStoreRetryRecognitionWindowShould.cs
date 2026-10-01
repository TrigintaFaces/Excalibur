// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Redis;

using Microsoft.Extensions.Logging.Abstractions;

using StackExchange.Redis;

namespace Excalibur.Integration.Tests.EventSourcing.Redis;

/// <summary>
/// Locks the WIDTH of the Redis event store's retry-recognition window against a real Redis.
/// </summary>
/// <remarks>
/// <para>
/// The committed-append guarantee says an append reports success if and only if its events are durably
/// present, decided by the identity of the events rather than by a version slot. Redis cannot probe a
/// stream by event identifier, so it records each append's identity in a companion sorted set inside the
/// same Lua script as the append and looks a retry up there. An earlier revision recorded a SINGLE marker
/// per stream, so it recognised a retry of the most recent append only: a retry arriving after any other
/// writer appended was reported as a concurrency conflict, and the documented reload-and-retry then wrote
/// the same business event a second time at a different version, where no uniqueness key can catch it.
/// </para>
/// <para>
/// <b>Real infrastructure, never skipped.</b> Every fact runs against a real Redis (TestContainers) through
/// the real seam. The recognition lives inside the Lua script, atomic with the append, which is precisely
/// the property a mocked <c>IDatabase</c> cannot exercise. <c>DockerAvailable.ShouldBeTrue(...)</c> makes
/// these locks non-skipped: a skipped silent-duplication lock is the gap that ships the defect.
/// </para>
/// <para>
/// <b>Non-vacuity.</b> Each fact has a mutant that reddens it and leaves the others green.
/// <list type="bullet">
/// <item><description>
/// <see cref="RecogniseARetryOfAnAppendAnotherWriterHasSinceFollowed"/> — set
/// <c>RetryRecognitionWindow</c>'s default to 1, which is exactly the pre-fix single-marker width: the
/// intervening append evicts the retry's identity, the retry is reported as a conflict, RED. The other two
/// stay green: one configures the window explicitly, the other asserts conflicts that hold at any width.
/// </description></item>
/// <item><description>
/// <see cref="StillReportAConflictRatherThanRecognisingAWriterItCannotIdentify"/> — remove the
/// <c>first_event_id ~= ''</c> guard around the record and the probe: two writers that both omit event
/// identifiers then recognise each other's appends, the second writer's event is silently never written,
/// RED. The other two carry real identifiers and stay green.
/// </description></item>
/// <item><description>
/// <see cref="ReportAConflictForARetryOlderThanTheConfiguredWindow"/> — delete the
/// <c>ZREMRANGEBYRANK</c> call: the window becomes unbounded, the evicted retry is recognised, RED. The
/// other two stay green.
/// </description></item>
/// <item><description>
/// <see cref="KeepAppendingWhenAStreamStillCarriesTheEarlierSingleMarkerShape"/> — remove the
/// <c>TYPE</c>/<c>DEL</c> guard at the top of the script: reading a sorted set out of the stale hash
/// aborts the script, the append is reported as a failure, RED. No other fact ever creates a hash there,
/// so all three stay green.
/// </description></item>
/// </list>
/// The conflict assertion in the second fact's first half is additionally reddened by any over-recognition
/// mutant, which it shares with the third fact; the blank-identifier half is what it alone detects.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "EventSourcing")]
[Trait("Database", "Redis")]
public sealed class RedisEventStoreRetryRecognitionWindowShould : IntegrationTestBase, IClassFixture<RedisContainerFixture>
{
	private const string AggregateType = "RetryRecognitionTestAggregate";

	private const string NeverSkip =
		"the committed-append guarantee is a silent-duplication safety control — this real-Redis lock must never be skipped";

	private readonly RedisContainerFixture _redisFixture;

	public RedisEventStoreRetryRecognitionWindowShould(RedisContainerFixture redisFixture)
	{
		_redisFixture = redisFixture;
	}

	[Fact]
	public async Task RecogniseARetryOfAnAppendAnotherWriterHasSinceFollowed()
	{
		// The defect, stated as a test: a lost acknowledgement plus ONE interleaved append by another
		// writer. The retry must be answered with the version its own earlier attempt reached, not with a
		// conflict — a conflict sends the caller to reload-and-retry, which appends the same business
		// event a second time at the next version, permanently and undetectably.
		_redisFixture.DockerAvailable.ShouldBeTrue(NeverSkip);

		await using var connection = await ConnectionMultiplexer.ConnectAsync(_redisFixture.ConnectionString);
		var store = CreateEventStore(connection);
		var aggregateId = Guid.NewGuid().ToString();

		// Writer one creates the aggregate. Its acknowledgement is lost — the store does not know that.
		var first = new TestDomainEvent(aggregateId);
		var created = await store.AppendAsync(aggregateId, AggregateType, [first], -1, TestCancellationToken);
		created.Success.ShouldBeTrue("the create on an empty stream must succeed");
		created.NextExpectedVersion.ShouldBe(0);

		// Writer two appends to the same stream in between. This is the step that used to destroy the
		// recognition: the single per-stream marker was overwritten with writer two's identity.
		var intervening = new TestDomainEvent(aggregateId);
		var second = await store.AppendAsync(aggregateId, AggregateType, [intervening], 0, TestCancellationToken);
		second.Success.ShouldBeTrue("the second writer's append at the current version must succeed");
		second.NextExpectedVersion.ShouldBe(1);

		// Writer one retries, presenting the SAME event at the SAME expected version it originally used —
		// which is all a caller who never received an acknowledgement can do.
		var retry = await store.AppendAsync(aggregateId, AggregateType, [first], -1, TestCancellationToken);

		retry.IsConcurrencyConflict.ShouldBeFalse(
			"a retry of an append this store already committed is not a conflict — reporting one invites the duplicating re-append");
		retry.Success.ShouldBeTrue("the retried events are durably present, so the append succeeded");
		retry.Outcome.ShouldBe(
			AppendOutcome.AlreadyCommitted,
			"the store recognised its own earlier write rather than performing a new one, and says so");
		retry.NextExpectedVersion.ShouldBe(
			0,
			"the honest answer is the version the earlier attempt reached, not the stream's current head");

		// The load-bearing half: nothing was written twice.
		var loaded = await store.LoadAsync(aggregateId, AggregateType, TestCancellationToken);
		loaded.Count.ShouldBe(2, "the retry must not have appended a third event — that duplicate is permanent and invisible");
		loaded.Select(e => e.EventId).ShouldBe([first.EventId, intervening.EventId]);
	}

	[Fact]
	public async Task StillReportAConflictRatherThanRecognisingAWriterItCannotIdentify()
	{
		// Liveness, and the more dangerous direction. Widening recognition is satisfiable by recognising
		// EVERYTHING, which swallows genuine conflicts — a lost update rather than a duplicate. Both halves
		// pin that shut: an unrelated writer at a stale version, and two writers the store cannot tell
		// apart because neither carried an event identifier.
		_redisFixture.DockerAvailable.ShouldBeTrue(NeverSkip);

		await using var connection = await ConnectionMultiplexer.ConnectAsync(_redisFixture.ConnectionString);
		var store = CreateEventStore(connection);

		// Half one — a different writer, a different event, a stale expected version. A real conflict.
		var aggregateId = Guid.NewGuid().ToString();
		var first = new TestDomainEvent(aggregateId);
		(await store.AppendAsync(aggregateId, AggregateType, [first], -1, TestCancellationToken))
			.Success.ShouldBeTrue();

		var intervening = new TestDomainEvent(aggregateId);
		(await store.AppendAsync(aggregateId, AggregateType, [intervening], 0, TestCancellationToken))
			.Success.ShouldBeTrue();

		var rival = new TestDomainEvent(aggregateId);
		var rivalResult = await store.AppendAsync(aggregateId, AggregateType, [rival], 0, TestCancellationToken);

		rivalResult.Success.ShouldBeFalse("a writer working from a stale version has genuinely lost the race");
		rivalResult.IsConcurrencyConflict.ShouldBeTrue(
			"it must be reported as a conflict — recognising it would silently discard the caller's event");
		rivalResult.NextExpectedVersion.ShouldBe(1, "the conflict reports the version the stream actually reached");

		(await store.LoadAsync(aggregateId, AggregateType, TestCancellationToken)).Count
			.ShouldBe(2, "the rival's event must not have been appended");

		// Half two — two writers whose events carry no identifier. The store cannot tell them apart, so it
		// must recognise NEITHER. Treating a blank identifier as a match would make the second writer's
		// append report success while its event was never written.
		var anonymousId = Guid.NewGuid().ToString();
		var anonymousFirst = new TestDomainEvent(anonymousId, eventId: string.Empty);
		(await store.AppendAsync(anonymousId, AggregateType, [anonymousFirst], -1, TestCancellationToken))
			.Success.ShouldBeTrue("an event with no identifier is still appendable — it simply gets no recognition");

		var anonymousRival = new TestDomainEvent(anonymousId, eventId: string.Empty);
		var anonymousResult = await store.AppendAsync(anonymousId, AggregateType, [anonymousRival], -1, TestCancellationToken);

		anonymousResult.Success.ShouldBeFalse(
			"an unidentifiable writer must never be mistaken for a retry of an unrelated unidentifiable writer");
		anonymousResult.IsConcurrencyConflict.ShouldBeTrue("it is a conflict, and the caller must be told so");

		(await store.LoadAsync(anonymousId, AggregateType, TestCancellationToken)).Count
			.ShouldBe(1, "exactly the first anonymous event — a reported success here would have lost the second");
	}

	[Fact]
	public async Task ReportAConflictForARetryOlderThanTheConfiguredWindow()
	{
		// The bound, asserted rather than assumed. The window is finite by design — an unbounded set per
		// stream is a leak with no eviction story — so the documented behaviour past it is a conflict, the
		// same answer a caller minting fresh identifiers per attempt receives. Both halves are needed: the
		// conflict past the bound, and a recognition INSIDE it, so a lookup that had simply stopped working
		// could not pass as correct eviction.
		_redisFixture.DockerAvailable.ShouldBeTrue(NeverSkip);

		await using var connection = await ConnectionMultiplexer.ConnectAsync(_redisFixture.ConnectionString);
		var store = CreateEventStore(connection, recognitionWindow: 2);
		var aggregateId = Guid.NewGuid().ToString();

		var oldest = new TestDomainEvent(aggregateId);
		(await store.AppendAsync(aggregateId, AggregateType, [oldest], -1, TestCancellationToken))
			.Success.ShouldBeTrue();

		var middle = new TestDomainEvent(aggregateId);
		(await store.AppendAsync(aggregateId, AggregateType, [middle], 0, TestCancellationToken))
			.Success.ShouldBeTrue();

		var newest = new TestDomainEvent(aggregateId);
		(await store.AppendAsync(aggregateId, AggregateType, [newest], 1, TestCancellationToken))
			.Success.ShouldBeTrue();

		// Three appends against a window of two: the oldest identity has been evicted.
		var staleRetry = await store.AppendAsync(aggregateId, AggregateType, [oldest], -1, TestCancellationToken);

		staleRetry.Success.ShouldBeFalse("past the window the store can no longer identify the retry");
		staleRetry.IsConcurrencyConflict.ShouldBeTrue(
			"and it reports that honestly as a conflict rather than guessing — the documented behaviour past the bound");

		// Inside the window, recognition still works — so the conflict above is the bound doing its job.
		var freshRetry = await store.AppendAsync(aggregateId, AggregateType, [middle], 0, TestCancellationToken);

		freshRetry.Success.ShouldBeTrue("an append still inside the window is recognised");
		freshRetry.Outcome.ShouldBe(AppendOutcome.AlreadyCommitted);
		freshRetry.NextExpectedVersion.ShouldBe(1, "the version that append reached");

		(await store.LoadAsync(aggregateId, AggregateType, TestCancellationToken)).Count
			.ShouldBe(3, "neither retry appended anything: the recognised one was a no-op, the unrecognised one was refused");
	}

	[Fact]
	public async Task KeepAppendingWhenAStreamStillCarriesTheEarlierSingleMarkerShape()
	{
		// The in-place upgrade path. An earlier revision kept a single-field HASH where the recognition set
		// now lives. Reading a sorted set out of a hash aborts the Lua script, so without the type guard a
		// store upgraded over existing streams would fail EVERY append to every one of them — a total
		// outage on upgrade, not a narrowed guarantee.
		_redisFixture.DockerAvailable.ShouldBeTrue(NeverSkip);

		await using var connection = await ConnectionMultiplexer.ConnectAsync(_redisFixture.ConnectionString);
		var prefix = $"es-rw-{Guid.NewGuid():N}";
		var store = CreateEventStore(connection, streamKeyPrefix: prefix);
		var aggregateId = Guid.NewGuid().ToString();

		var first = new TestDomainEvent(aggregateId);
		(await store.AppendAsync(aggregateId, AggregateType, [first], -1, TestCancellationToken))
			.Success.ShouldBeTrue();

		// Find the store's own recognition key rather than reconstructing it — the key layout is the
		// store's business, and a test that rebuilt it would pass while the real key went untouched.
		var db = connection.GetDatabase();
		var server = connection.GetServer(connection.GetEndPoints()[0]);
		var markerKey = server.Keys(db.Database, pattern: $"*{prefix}*:retry").Single();

		// Put the stream back into the shape the earlier revision left: a single-field hash.
		await db.KeyDeleteAsync(markerKey);
		await db.HashSetAsync(markerKey, [new HashEntry("eventId", first.EventId), new HashEntry("version", 0)]);
		(await db.KeyTypeAsync(markerKey)).ShouldBe(RedisType.Hash, "the arm must actually reproduce the old shape");

		// Act — an ordinary append onto that stream.
		var next = new TestDomainEvent(aggregateId);
		var result = await store.AppendAsync(aggregateId, AggregateType, [next], 0, TestCancellationToken);

		result.Success.ShouldBeTrue(
			$"an append onto an upgraded stream must not fail on the stale key shape: {result.ErrorMessage}");
		result.NextExpectedVersion.ShouldBe(1);

		(await db.KeyTypeAsync(markerKey)).ShouldBe(
			RedisType.SortedSet,
			"the stale shape is replaced on first contact, so the stream self-heals rather than failing forever");

		(await store.LoadAsync(aggregateId, AggregateType, TestCancellationToken)).Count.ShouldBe(2);
	}

	private RedisEventStore CreateEventStore(
		ConnectionMultiplexer connection,
		int? recognitionWindow = null,
		string? streamKeyPrefix = null)
	{
		var options = new RedisEventStoreOptions
		{
			ConnectionString = _redisFixture.ConnectionString,
			// Unique per-instance stream key prefix so each fact's streams cannot collide across runs/classes.
			StreamKeyPrefix = streamKeyPrefix ?? $"es-rw-{Guid.NewGuid():N}",
			DatabaseIndex = -1,
		};

		if (recognitionWindow is int window)
		{
			options.RetryRecognitionWindow = window;
		}

		return new RedisEventStore(
			connection,
			Microsoft.Extensions.Options.Options.Create(options),
			NullLogger<RedisEventStore>.Instance,
			new SingleTenantDefaultContext());
	}

	[MessageName("Test.RedisEventStoreRetryRecognitionWindow.TestDomainEvent")]
	private sealed record TestDomainEvent : IDomainEvent
	{
		public TestDomainEvent(string aggregateId, string? eventId = null)
		{
			EventId = eventId ?? Guid.NewGuid().ToString();
			AggregateId = aggregateId;
			OccurredAt = DateTimeOffset.UtcNow;
		}

		public string EventId { get; init; }

		public string AggregateId { get; init; }

		public DateTimeOffset OccurredAt { get; init; }

		public IDictionary<string, object>? Metadata => null;
	}

	/// <summary>Mirrors the framework single-tenant default: always present, always the one canonical tenant.</summary>
	private sealed class SingleTenantDefaultContext : ITenantContext
	{
		public string? TenantId => TenantDefaults.DefaultTenantId;

		public bool HasTenant => true;
	}
}
