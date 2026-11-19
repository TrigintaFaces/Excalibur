// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;

using Excalibur.Dispatch.LeaderElection;
using Excalibur.Dispatch.LeaderElection.Fencing;
using Excalibur.LeaderElection.MongoDB;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

using MongoDB.Driver;

using Tests.Shared.Infrastructure;

namespace Excalibur.Data.MongoDB.Tests.MongoDB;

/// <summary>
/// A candidate that took the lock document and then could not mint a fencing token must DELETE that
/// document, whatever the mint failed for.
/// </summary>
/// <remarks>
/// <para>
/// THE DEFECT THESE ARMS BIND. The mint was wrapped in a catch of the exhaustion type alone, so only
/// exhaustion reached the relinquish branch. Every other mint failure unwound to the election step's
/// generic handler, which reports a failed acquisition and deletes nothing — so the lock document stayed,
/// naming this candidate as holder, while the candidate never declared leadership. No other candidate
/// can acquire against a live document, so the resource has a leader that may not write and no successor
/// until the TTL index reaps the row.
/// </para>
/// <para>
/// THE SUPERSEDED CLAIM, quoted because it was written down as intended behaviour and is the strongest
/// evidence the gap was real rather than cosmetic. The mint helper's own documentation said: "A transient
/// mint error propagates to the election loop's handler; the lock lease expires on its own, so leadership
/// is never declared without an advanced fence." Both halves are true and the conclusion does not follow
/// — never declaring leadership is the SAFETY half, and waiting out a lease nobody is using is the
/// liveness cost the three correct sibling providers do not pay.
/// </para>
/// <para>
/// WHERE THIS FILE LIVES, AND WHY NOT BESIDE ITS SIBLINGS. The collection is typed on
/// <c>MongoDbLeaderElectionDocument</c>, which is <c>internal sealed</c>, and this is the only assembly
/// granted <c>InternalsVisibleTo</c> on that package — so faking the collection is expressible here and
/// nowhere else. Widening the grant to reach the leader-election suite would change a shipped assembly's
/// attributes to suit a test.
/// </para>
/// <para>
/// WHAT MAKES EACH ARM RED. Narrowing the mint's catch back to the exhaustion type reddens the transient
/// arm at the delete assertion. Releasing on a mint that SUCCEEDED reddens the liveness arm. The
/// exhaustion arm is a regression — green before the fix, and it must stay green.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MongoDbRelinquishesOnMintFailureShould
{
	private const string Us = "candidate-us";
	private const string Resource = "orders";

	private readonly IMongoClient _client = A.Fake<IMongoClient>();
	private readonly IMongoDatabase _database = A.Fake<IMongoDatabase>();
	private readonly IMongoCollection<MongoDbLeaderElectionDocument> _collection =
		A.Fake<IMongoCollection<MongoDbLeaderElectionDocument>>();

	private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

	public MongoDbRelinquishesOnMintFailureShould()
	{
		A.CallTo(() => _client.GetDatabase(A<string>._, A<MongoDatabaseSettings>._)).Returns(_database);
		A.CallTo(() => _database.GetCollection<MongoDbLeaderElectionDocument>(
			A<string>._, A<MongoCollectionSettings>._)).Returns(_collection);

		// LOAD-BEARING: the upsert must SUCCEED in every arm, answering with a document that names THIS
		// candidate as holder. The defect is about what happens to a lock this candidate already took, so
		// an arrangement that loses the race proves nothing.
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
	public async Task DeleteTheLockDocumentWhenTheMintFailsForATransientReason()
	{
		// The reachable case, and the one with no exhaustion anywhere. This provider has no
		// unreadable-mark defect at all — its provider reads a typed value — and it still reached the
		// non-releasing path on a store error.
		var provider = A.Fake<IFencingTokenIssuer>();
		A.CallTo(() => provider.IssueTokenAsync(A<string>._, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("the fencing counter collection is unreachable"));

		var (sut, failures) = await StartAsync(provider).ConfigureAwait(false);

		sut.IsLeader.ShouldBeFalse("a candidate that cannot fence its writes must not lead");
		DeleteMustHaveHappened();

		await AwaitOneFailureAsync(failures).ConfigureAwait(false);
		failures.ShouldHaveSingleItem().Exception.ShouldBeOfType<InvalidOperationException>(
			"the cause must reach a subscriber; a reason string alone cannot say what failed");

		await sut.DisposeAsync().ConfigureAwait(false);
	}

	[Fact]
	public async Task DeleteTheLockDocumentWhenTheTokenDomainIsExhausted()
	{
		// REGRESSION. Green before the fix — here so the merged path cannot be satisfied by a provider
		// that stopped handling the one cause it already handled.
		var provider = A.Fake<IFencingTokenIssuer>();
		A.CallTo(() => provider.IssueTokenAsync(A<string>._, A<CancellationToken>._))
			.ThrowsAsync(new FencingTokenExhaustedException("no values left") { ResourceId = Resource });

		var (sut, failures) = await StartAsync(provider).ConfigureAwait(false);

		sut.IsLeader.ShouldBeFalse("an exhausted domain still relinquishes");
		DeleteMustHaveHappened();
		await AwaitOneFailureAsync(failures).ConfigureAwait(false);
		failures.ShouldHaveSingleItem();

		await sut.DisposeAsync().ConfigureAwait(false);
	}

	[Fact]
	public async Task LeadAndKeepTheLockDocumentWhenTheMintSucceeds()
	{
		// LIVENESS. Every arm above asserts the lock was GIVEN BACK; a provider that deleted
		// unconditionally — or never acquired at all — passes all of them while leading nobody.
		var provider = A.Fake<IFencingTokenIssuer>();
		A.CallTo(() => provider.IssueTokenAsync(A<string>._, A<CancellationToken>._)).Returns(7L);

		var (sut, failures) = await StartAsync(provider).ConfigureAwait(false);

		sut.IsLeader.ShouldBeTrue("a minted fence is the whole precondition for leading");
		A.CallTo(_collection).Where(call => call.Method.Name == "DeleteOneAsync").MustNotHaveHappened();
		// A negative needs a bounded wait of its own. Asserting emptiness the instant StartAsync returns
		// is satisfied by a notification that has simply not been dispatched yet, so this arm would hold
		// for a provider that DOES report a spurious failure, slightly later.
		(await WaitHelpers.WaitUntilAsync(
			() => !failures.IsEmpty,
			TimeSpan.FromMilliseconds(250),
			TimeSpan.FromMilliseconds(10),
			CancellationToken.None).ConfigureAwait(false)).ShouldBeFalse(
			"a successfully minted fence must raise no acquisition failure");

		await sut.DisposeAsync().ConfigureAwait(false);
	}

	// ---------------------------------------------------------------------------------------------

	/// <summary>
	/// Starts a candidate whose upsert wins, with the supplied fencing provider as the only thing that
	/// varies between arms. <c>StartAsync</c> awaits one acquisition attempt, so the outcome is
	/// observable on the next statement with no polling and no clock advance.
	/// </summary>
	// AcquisitionFailed is dispatched on the election's notification worker, so it may not have been
	// delivered by the time StartAsync returns. Wait for it rather than assume it: asserting the count
	// immediately passed on an idle machine and failed whenever the suite ran under load.
	private static async Task AwaitOneFailureAsync(ConcurrentQueue<LeaderElectionAcquisitionFailedEventArgs> failures) =>
		(await WaitHelpers.WaitUntilAsync(
			() => !failures.IsEmpty,
			TimeSpan.FromSeconds(5),
			TimeSpan.FromMilliseconds(10),
			CancellationToken.None).ConfigureAwait(false)).ShouldBeTrue(
			"the acquisition failure must reach a subscriber");

	// ConcurrentQueue, not List: AcquisitionFailed is delivered on the election's notification worker,
	// so the handler writes this collection from a different thread than the arm that reads it. A List
	// there is mutated while being enumerated, which is why the failure read "should have single item
	// but had 1 items" -- the count changed between the assertion and the message being formatted.
	private async Task<(MongoDbLeaderElection Sut, ConcurrentQueue<LeaderElectionAcquisitionFailedEventArgs> Failures)> StartAsync(
		IFencingTokenIssuer provider)
	{
		var mongoOptions = Options.Create(new MongoDbLeaderElectionOptions
		{
			// Required by the options validator. Never dialled: the client is supplied already
			// constructed, so nothing here resolves this address.
			ConnectionString = "mongodb://localhost:27017",
			DatabaseName = "excalibur",
			CollectionName = "leader_elections",

			// Far longer than any arm, so the renewal loop parks after the awaited first attempt and no
			// arm depends on wall-clock timing.
			LeaseDurationSeconds = 7200,
			RenewIntervalSeconds = 3600,
		});

		var electionOptions = Options.Create(new LeaderElectionOptions
		{
			// LOAD-BEARING. The provider takes its CandidateId from here; left unset it defaults to
			// "{MachineName}-{guid}", the faked upsert answers with a document naming someone else, and
			// every arm goes green against a candidate that never led.
			InstanceId = Us,
			RetryInterval = TimeSpan.FromHours(1),
		});

		var sut = new MongoDbLeaderElection(
			_client,
			Resource,
			mongoOptions,
			electionOptions,
			NullLogger<MongoDbLeaderElection>.Instance,
			provider,
			_clock);

		var failures = new ConcurrentQueue<LeaderElectionAcquisitionFailedEventArgs>();
		sut.AcquisitionFailed += (_, e) => failures.Enqueue(e);

		await sut.StartAsync(CancellationToken.None).ConfigureAwait(false);
		return (sut, failures);
	}

	/// <summary>
	/// The release deletes the lock document. Matched by member name rather than by a hand-written
	/// overload, which can compile without matching the call the provider actually makes.
	/// </summary>
	private void DeleteMustHaveHappened() =>
		A.CallTo(_collection)
			.Where(call => call.Method.Name == "DeleteOneAsync")
			.MustHaveHappened();
}
