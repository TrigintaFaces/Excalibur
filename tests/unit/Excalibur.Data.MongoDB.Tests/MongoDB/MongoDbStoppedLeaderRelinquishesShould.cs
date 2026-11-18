// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.LeaderElection;
using Excalibur.Dispatch.LeaderElection.Fencing;
using Excalibur.LeaderElection.MongoDB;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

using MongoDB.Driver;

namespace Excalibur.Data.MongoDB.Tests.MongoDB;

/// <summary>
/// A stopped or disposed candidate must report <c>IsLeader == false</c>, whether or not the lock document
/// was deleted from the collection.
/// </summary>
/// <remarks>
/// <para>
/// THE STARKEST OF THE THREE REAL DEFECTS, because this provider's release has no exception handling at
/// all. <c>ReleaseLockAsync</c> awaits <c>DeleteOneAsync</c> with no <c>try</c> and no <c>catch</c>, and
/// the stop sequence used to read <c>await ReleaseLockAsync(); SetLeader(false, null);</c> — two
/// statements in sequence. So a throwing delete did not merely skip a clear inside a success branch; it
/// unwound past the clear entirely. The fix wraps the release in <c>try</c>/<c>finally</c> with the clear
/// in the <c>finally</c>.
/// </para>
/// <para>
/// THE EXCEPTION STILL PROPAGATES FROM STOP, AND THAT IS DELIBERATE — only a <c>finally</c> was added,
/// not a <c>catch</c>. So a caller who stops a leader whose store is unreachable is told about it, and
/// the candidate has relinquished anyway. The arm below asserts both halves together, because asserting
/// only the relinquish would be satisfied by a stop that silently swallowed a store failure.
/// </para>
/// <para>
/// WHERE THIS FILE LIVES, AND WHY NOT BESIDE ITS SIBLINGS. The collection is typed on
/// <c>MongoDbLeaderElectionDocument</c>, which is <c>internal sealed</c>. The only assembly granted
/// <c>InternalsVisibleTo</c> on that package is this one, so faking
/// <c>IMongoCollection&lt;MongoDbLeaderElectionDocument&gt;</c> is expressible here and nowhere else.
/// The alternative — widening the grant to reach the leader-election suite — would change a shipped
/// assembly's attributes to suit a test, which is the wrong direction.
/// </para>
/// <para>
/// NO SYNCHRONOUS DISPOSE EXISTS ON THIS PROVIDER, so there is no sync-dispose arm. And unlike the
/// sibling providers, <c>DisposeAsync</c> runs the stop sequence rather than clearing silently, so it
/// DOES raise. That divergence is bound explicitly below rather than left untested.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "Regression")]
public sealed class MongoDbStoppedLeaderRelinquishesShould
{
	private const string Us = "candidate-us";
	private const string Resource = "orders";

	private readonly IMongoClient _client = A.Fake<IMongoClient>();
	private readonly IMongoDatabase _database = A.Fake<IMongoDatabase>();
	private readonly IMongoCollection<MongoDbLeaderElectionDocument> _collection =
		A.Fake<IMongoCollection<MongoDbLeaderElectionDocument>>();

	private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

	public MongoDbStoppedLeaderRelinquishesShould()
	{
		A.CallTo(() => _client.GetDatabase(A<string>._, A<MongoDatabaseSettings>._)).Returns(_database);
		A.CallTo(() => _database.GetCollection<MongoDbLeaderElectionDocument>(
			A<string>._, A<MongoCollectionSettings>._)).Returns(_collection);

		// The upsert answers with a document naming THIS candidate as the holder, which is the whole of
		// what the acquisition path checks. Nothing here needs a cursor: the Find-based holder read is
		// only reached when acquisition FAILS.
		//
		// The projection generic closes over the document type, so this binds the same method the
		// provider calls: FindOneAndUpdateOptions<TDocument, TProjection> with both arguments equal.
		A.CallTo(() => _collection.FindOneAndUpdateAsync(
				A<FilterDefinition<MongoDbLeaderElectionDocument>>._,
				A<UpdateDefinition<MongoDbLeaderElectionDocument>>._,
				A<FindOneAndUpdateOptions<MongoDbLeaderElectionDocument, MongoDbLeaderElectionDocument>>._,
				A<CancellationToken>._))
			.Returns(Task.FromResult(new MongoDbLeaderElectionDocument
			{
				ResourceName = Resource,
				CandidateId = Us,
				AcquiredAt = _clock.GetUtcNow().UtcDateTime,
				ExpiresAt = _clock.GetUtcNow().AddSeconds(7200).UtcDateTime,
				LastRenewedAt = _clock.GetUtcNow().UtcDateTime,
				FencingToken = 1,
			}));
	}

    [Fact]
    public async Task DisposeAfterStopWithoutReusingARetiredRunCancellationSource()
    {
        var sut = await LeaderAsync();
        sut.IsLeader.ShouldBeTrue();
        await sut.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        sut.IsLeader.ShouldBeFalse();
        await sut.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        await sut.NotificationsCompleted.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task JoinAnAdmittedHeldStopBeforeDisposingItsRunCancellationSource()
    {
        var sut = await LeaderAsync();
        sut.IsLeader.ShouldBeTrue();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        A.CallTo(_collection).Where(call => call.Method.Name == "DeleteOneAsync")
            .WithReturnType<Task<DeleteResult>>()
            .ReturnsLazily(async _ => { entered.TrySetResult(); await release.Task.ConfigureAwait(false); return new DeleteResult.Acknowledged(1); });
        Task? stop = null;
        Task? disposal = null;
        try
        {
            stop = sut.StopAsync(CancellationToken.None);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            disposal = sut.DisposeAsync().AsTask();
            disposal.IsCompleted.ShouldBeFalse("permanent disposal must join the admitted stop's held SDK release");
            release.TrySetResult();
            await stop.WaitAsync(TimeSpan.FromSeconds(5));
            await disposal.WaitAsync(TimeSpan.FromSeconds(5));
            sut.IsLeader.ShouldBeFalse();
            await sut.NotificationsCompleted.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            release.TrySetResult();
            if (stop is not null) { await stop.WaitAsync(TimeSpan.FromSeconds(5)); }
            await (disposal ?? sut.DisposeAsync().AsTask()).WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

	[Fact]
	public async Task ReportNotLeaderAfterStopWhenTheDeleteThrows()
	{
		// THE ARM THAT BINDS THE FIX. Pre-fix the throw unwound past the clear, so the instance kept
		// reporting leadership for the life of the process with the election loop already cancelled.
		var sut = await LeaderAsync().ConfigureAwait(false);
		DeleteThrows();

		// Both halves, together. The relinquish must happen AND the caller must still be told the store
		// could not be reached — asserting only the first would be satisfied by a stop that swallowed it.
		_ = await Should.ThrowAsync<MongoException>(
			async () => await sut.StopAsync(CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);

		sut.IsLeader.ShouldBeFalse(
			"the clear must run whether or not the delete landed; as two sequential statements the throw " +
			"unwound past it and this instance reported itself leader for the life of the process");
		sut.CurrentLeaderId.ShouldBeNull("a candidate that relinquished names no holder of its own");

		await sut.DisposeAsync().ConfigureAwait(false);
	}

	[Fact]
	public async Task ReportNotLeaderAfterAsyncDisposeWhenTheDeleteThrows()
	{
        // Local truth must clear while the original failed native release remains observable.
        var sut = await LeaderAsync().ConfigureAwait(false);
        MongoException? original = null;
        await JoinCheckedCleanupAsync(sut, () => original, async () =>
        {
            sut.CurrentLeaderId.ShouldBe(Us);
            sut.CurrentLeadership.ShouldNotBeNull().FencingToken.ShouldBe(1);
            original = DeleteThrows();
            var lost = 0;
            sut.LostLeadership += (_, _) => Interlocked.Increment(ref lost);
            var first = sut.DisposeAsync().AsTask();
            var repeated = sut.DisposeAsync().AsTask();
            first.ShouldBeSameAs(repeated, "resource disposal retains one actual pending or faulted task");
            (await Should.ThrowAsync<MongoException>(() => first).ConfigureAwait(false)).ShouldBeSameAs(original);
            (await Should.ThrowAsync<MongoException>(() => repeated).ConfigureAwait(false)).ShouldBeSameAs(original);
            sut.IsLeader.ShouldBeFalse("native cleanup failure must not retain local leadership");
            sut.CurrentLeaderId.ShouldBeNull("a candidate that relinquished names no holder of its own");
            sut.CurrentLeadership.ShouldBeNull();
            await sut.NotificationsCompleted.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            lost.ShouldBe(1, "the queued original-tenure loss is delivered exactly once after resource failure");
            A.CallTo(_collection).Where(call => call.Method.Name == "DeleteOneAsync")
                .WithReturnType<Task<DeleteResult>>().MustHaveHappenedOnceExactly();
    	
        });
	}

	[Fact]
	public async Task ReportNotLeaderAfterAsyncDisposeWhenTheDeleteSucceeds()
	{
		// REGRESSION, and honest about what it is worth: the defective code passes this one, because a
		// delete that lands reached the clear on the next statement. It is here so the failed-delete arms
		// above cannot be satisfied by a provider that simply stopped acquiring.
		var sut = await LeaderAsync().ConfigureAwait(false);

		await sut.DisposeAsync().ConfigureAwait(false);

		sut.IsLeader.ShouldBeFalse("a disposed candidate holds no leadership");
	}

	[Fact]
	public async Task RaiseLostLeadershipExactlyOnceAcrossStopThenDispose()
	{
		// IDEMPOTENCE across the paths a host actually takes: stop on shutdown, then dispose. One tenure
		// ends once, however many shutdown paths reach the clear.
		var sut = await LeaderAsync().ConfigureAwait(false);
		DeleteThrows();

		var lost = 0;
		sut.LostLeadership += (_, _) => Interlocked.Increment(ref lost);

		_ = await Should.ThrowAsync<MongoException>(
			async () => await sut.StopAsync(CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);
		await sut.DisposeAsync().ConfigureAwait(false);
		await sut.NotificationsCompleted.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

		lost.ShouldBe(1, "the stop relinquished; dispose must find nothing left to do");
	}

	[Fact]
	public async Task RaiseLostLeadershipExactlyOnceFromAsyncDisposeAlone()
	{
        // Resource cleanup and queued callback delivery are independently joined.
        var sut = await LeaderAsync().ConfigureAwait(false);
        MongoException? original = null;
        await JoinCheckedCleanupAsync(sut, () => original, async () =>
        {
            sut.CurrentLeaderId.ShouldBe(Us);
            sut.CurrentLeadership.ShouldNotBeNull().FencingToken.ShouldBe(1);
            original = DeleteThrows();
            var lost = 0;
            sut.LostLeadership += (_, _) => Interlocked.Increment(ref lost);
            var first = sut.DisposeAsync().AsTask();
            var repeated = sut.DisposeAsync().AsTask();
            first.ShouldBeSameAs(repeated, "resource disposal retains one actual pending or faulted task");
            (await Should.ThrowAsync<MongoException>(() => first).ConfigureAwait(false)).ShouldBeSameAs(original);
            (await Should.ThrowAsync<MongoException>(() => repeated).ConfigureAwait(false)).ShouldBeSameAs(original);
            sut.IsLeader.ShouldBeFalse("native cleanup failure must not retain local leadership");
            sut.CurrentLeaderId.ShouldBeNull("a candidate that relinquished names no holder of its own");
            sut.CurrentLeadership.ShouldBeNull();
            await sut.NotificationsCompleted.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            lost.ShouldBe(1, "the queued original-tenure loss is delivered exactly once after resource failure");
            A.CallTo(_collection).Where(call => call.Method.Name == "DeleteOneAsync")
                .WithReturnType<Task<DeleteResult>>().MustHaveHappenedOnceExactly();
    	
        });
	}

	[Fact]
	public async Task RetainTheOriginalDeleteFailureAndTheSameTaskAcrossRepeatedAsyncDisposal()
	{
        // Repeated disposal cannot turn failed cleanup into a successful result or repeat the delete.
        var sut = await LeaderAsync().ConfigureAwait(false);
        MongoException? original = null;
        await JoinCheckedCleanupAsync(sut, () => original, async () =>
        {
            sut.CurrentLeaderId.ShouldBe(Us);
            sut.CurrentLeadership.ShouldNotBeNull().FencingToken.ShouldBe(1);
            original = DeleteThrows();
            var lost = 0;
            sut.LostLeadership += (_, _) => Interlocked.Increment(ref lost);
            var first = sut.DisposeAsync().AsTask();
            var repeated = sut.DisposeAsync().AsTask();
            first.ShouldBeSameAs(repeated, "resource disposal retains one actual pending or faulted task");
            (await Should.ThrowAsync<MongoException>(() => first).ConfigureAwait(false)).ShouldBeSameAs(original);
            (await Should.ThrowAsync<MongoException>(() => repeated).ConfigureAwait(false)).ShouldBeSameAs(original);
            sut.IsLeader.ShouldBeFalse("native cleanup failure must not retain local leadership");
            sut.CurrentLeaderId.ShouldBeNull("a candidate that relinquished names no holder of its own");
            sut.CurrentLeadership.ShouldBeNull();
            await sut.NotificationsCompleted.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            lost.ShouldBe(1, "the queued original-tenure loss is delivered exactly once after resource failure");
            A.CallTo(_collection).Where(call => call.Method.Name == "DeleteOneAsync")
                .WithReturnType<Task<DeleteResult>>().MustHaveHappenedOnceExactly();
    	
        });
	}

	// ---------------------------------------------------------------------------------------------

	/// <summary>
	/// A candidate holding leadership against a healthy faked collection. <c>StartAsync</c> awaits one
	/// acquisition attempt before returning, so leadership is observable on the next statement with no
	/// polling and no clock advance.
	/// </summary>
	private async Task<MongoDbLeaderElection> LeaderAsync()
	{
		var mongoOptions = Options.Create(new MongoDbLeaderElectionOptions
		{
			// Required by the options validator. Never dialled: the client is supplied already
			// constructed, so nothing here resolves this address.
			ConnectionString = "mongodb://localhost:27017",
			DatabaseName = "excalibur",
			CollectionName = "leader_elections",

			// Far longer than any arm, so the renewal loop parks after the awaited first attempt and no
			// arm depends on wall-clock timing. The lease must exceed the renew interval (the options
			// validator enforces it), and neither value is otherwise reachable here: the lease arithmetic
			// lives in the upsert pipeline, which the fake answers without evaluating.
			LeaseDurationSeconds = 7200,
			RenewIntervalSeconds = 3600,
		});

		var electionOptions = Options.Create(new LeaderElectionOptions
		{
			// LOAD-BEARING. The provider takes its CandidateId from here, and the default is
			// "{MachineName}-{guid}". Left unset, the faked upsert answers with a document naming a
			// candidate that is not this one, the acquisition path correctly reads that as losing the
			// race, and every arm in this file goes green against a candidate that never led.
			InstanceId = Us,
			RetryInterval = TimeSpan.FromHours(1),
		});

		// Supplied rather than left null: the constructor otherwise builds the real provider against this
		// faked client, which would put an unrelated store path inside the arrangement.
		var fencing = A.Fake<IFencingTokenIssuer>();
		A.CallTo(() => fencing.IssueTokenAsync(A<string>._, A<CancellationToken>._)).Returns(1L);

		var sut = new MongoDbLeaderElection(
			_client,
			Resource,
			mongoOptions,
			electionOptions,
			NullLogger<MongoDbLeaderElection>.Instance,
			fencing,
			_clock);

		// The election step absorbs its own exceptions by design — a follower losing the race is the
		// ordinary outcome, so it must not fail a start. That also means a BROKEN ARRANGEMENT is silent
		// here, and the only report of it is this event. Capturing it turns "the candidate never
		// acquired" into the actual reason, which is the difference between a minute and an hour.
		string? failure = null;
		sut.AcquisitionFailed += (_, e) => failure = $"{e.Reason}: {e.Exception}";

		await sut.StartAsync(CancellationToken.None).ConfigureAwait(false);

		// LIVENESS. Every arm above asserts leadership was DROPPED; against a candidate that never
		// acquired it, all of them pass while proving nothing.
		sut.IsLeader.ShouldBeTrue(
			$"arrangement failed: the candidate never acquired leadership. Acquisition reported: {failure ?? "nothing"}");
		return sut;
	}

	/// <summary>
	/// The delete cannot reach the collection. The release path has no handler of its own, so this
	/// propagates — which is exactly the shape that used to unwind past the clear.
	/// </summary>
	/// <remarks>
	/// Matched by member name for the same reason as the upsert above: a hand-written overload can
	/// compile without matching, and a release that was never made to fail would leave every arm below
	/// green against the defect it was written for.
	/// </remarks>
    private MongoException DeleteThrows()
    {
        var original = new MongoException("the primary is unreachable");
        A.CallTo(_collection).Where(call => call.Method.Name == "DeleteOneAsync").Throws(original);
        return original;
    }
    private static async Task JoinCheckedCleanupAsync(MongoDbLeaderElection sut, Func<MongoException?> expectedFailure, Func<Task> checks)
    {
        Exception? assertionFailure = null;
        Exception? cleanupFailure = null;
        try { await checks().ConfigureAwait(false); }
        catch (Exception error) { assertionFailure = error; }
        try
        {
            var observed = await Record.ExceptionAsync(() => sut.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)));
            var expected = expectedFailure();
            if (expected is null) { observed.ShouldBeNull(); }
            else { observed.ShouldBeSameAs(expected); }
            await sut.NotificationsCompleted.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }
        catch (Exception error) { cleanupFailure = error; }
        if (assertionFailure is not null && cleanupFailure is not null) { throw new AggregateException(assertionFailure, cleanupFailure); }
        if (assertionFailure is not null) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(assertionFailure).Throw(); }
        if (cleanupFailure is not null) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(cleanupFailure).Throw(); }
    }

}
