// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.LeaderElection;
using Excalibur.LeaderElection.MongoDB;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using MongoDB.Driver;

using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.LeaderElection;

/// <summary>
/// Author≠impl split-brain regression lock for 5fswhd — the MongoDB leader-election <b>takeover</b> path
/// decides lease expiry on the <b>server clock</b> (<c>$$NOW</c> vs the stored <c>expiresAt</c>). Two REAL
/// candidates contend over one resource: while the incumbent renews, a second candidate must NOT seize
/// leadership (the lease is valid); once the incumbent stops and its lease expires on the server clock, the
/// second candidate must take over and advance the fencing token (fencing off the stale leader).
/// </summary>
/// <remarks>
/// verify-against-real-infra-not-mock: runs against a real MongoDB (TestContainers) and drives the real
/// atomic aggregation-pipeline <c>findOneAndUpdate</c> takeover CAS (<c>$$NOW</c>/<c>$lte</c>) — a mocked
/// collection cannot evaluate it. Both candidates are real <see cref="MongoDbLeaderElection"/> instances, so
/// the lock document is written by the implementation itself (no hand-seeded shape). Short lease (2s) keeps
/// the handover deterministic; polling (no fixed sleeps) per testing standards.
/// <c>DockerAvailable.ShouldBeTrue</c> makes it NON-SKIPPED. RED-on-mutant: drop the
/// <c>expiresAt &lt;= $$NOW</c> guard (always take over) ⇒
/// <see cref="NotTakeOver_WhileTheIncumbentIsActivelyRenewing"/> goes RED (a live lease is stolen).
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "LeaderElection")]
[Trait("Database", "MongoDb")]
public sealed class MongoDbLeaderElectionTakeoverShould : IClassFixture<MongoDbContainerFixture>
{
	private const string CollectionName = "leader_elections";

	private readonly MongoDbContainerFixture _fixture;

	public MongoDbLeaderElectionTakeoverShould(MongoDbContainerFixture fixture) => _fixture = fixture;

	/// <remarks>
	/// The lease and renew intervals are parameters because the two arms need opposite things from them.
	/// The expiry arm needs a short lease so the wait is short. The still-renewing arm needs a lease long
	/// enough that a missed renewal slot cannot end it: at a two-second lease and a one-second renew, a
	/// single scheduling delay under load expires the lease for real, the challenger then takes over
	/// correctly, and the arm reports a split-brain that did not happen. Raising only the lease keeps the
	/// property under test identical -- the challenger still contends across many renewals -- while
	/// requiring a run of consecutive misses rather than one to produce a false red.
	/// </remarks>
	private MongoDbLeaderElection CreateCandidate(
		string databaseName,
		string resourceName,
		string candidateId,
		int leaseSeconds = 2,
		int renewSeconds = 1)
	{
		var client = new MongoClient(_fixture.ConnectionString);
		var mongoOptions = Options.Create(new MongoDbLeaderElectionOptions
		{
			ConnectionString = _fixture.ConnectionString,
			DatabaseName = databaseName,
			CollectionName = CollectionName,
			LeaseDurationSeconds = leaseSeconds,
			RenewIntervalSeconds = renewSeconds,
			TimeoutInSeconds = 5,
			TakeoverGraceSeconds = 0,
		});
		var electionOptions = Options.Create(new LeaderElectionOptions
		{
			InstanceId = candidateId,
			LeaseDuration = TimeSpan.FromSeconds(leaseSeconds),
			RenewInterval = TimeSpan.FromSeconds(renewSeconds),
			RetryInterval = TimeSpan.FromSeconds(1),
			GracePeriod = TimeSpan.Zero,
			EnableHealthChecks = false,
		});
		return new MongoDbLeaderElection(
			client, resourceName, mongoOptions, electionOptions, NullLogger<MongoDbLeaderElection>.Instance);
	}

	private static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
	{
		var deadline = DateTime.UtcNow + timeout;
		while (DateTime.UtcNow < deadline)
		{
			if (condition())
			{
				return true;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(100), CancellationToken.None).ConfigureAwait(false);
		}

		return condition();
	}

	[Fact]
	public async Task NotTakeOver_WhileTheIncumbentIsActivelyRenewing()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"5fswhd server-clock takeover is a split-brain safety control — this real-Mongo lock must never be skipped");

		var db = "takeover_" + Guid.NewGuid().ToString("N");
		var resource = "res-" + Guid.NewGuid().ToString("N");
		var incumbent = CreateCandidate(db, resource, "incumbent", leaseSeconds: 12, renewSeconds: 1);
		var challenger = CreateCandidate(db, resource, "challenger", leaseSeconds: 12, renewSeconds: 1);

		var reasons = new System.Collections.Concurrent.ConcurrentQueue<string>();
		incumbent.AcquisitionFailed += (_, e) => reasons.Enqueue(e.Reason);
		var docs = new MongoClient(_fixture.ConnectionString).GetDatabase(db).GetCollection<MongoDB.Bson.BsonDocument>(CollectionName);
		await using (incumbent)
		await using (challenger)
		{
			// Incumbent acquires and keeps renewing (its lease stays valid on the server clock).
			await incumbent.StartAsync(CancellationToken.None);
			var led = await WaitUntilAsync(() => incumbent.IsLeader, TimeSpan.FromSeconds(15));
			var d = await (await docs.FindAsync(Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("_id", resource))).FirstOrDefaultAsync();
			led.ShouldBeTrue(
				$"the first candidate must acquire leadership. reasons=[{string.Join(";", reasons)}]; doc={(d is null ? "MISSING" : d.ToString())}");

			// A second candidate contends while the incumbent is actively renewing.
			await challenger.StartAsync(CancellationToken.None);

			// Across several lease+renew cycles the challenger must NEVER seize a still-valid lease.
			var challengerStole = await WaitUntilAsync(() => challenger.IsLeader, TimeSpan.FromSeconds(6));

			// Check the incumbent FIRST. If it stopped renewing, its lease ended legitimately and any
			// takeover that followed is correct behaviour, not a stolen lease -- reporting that as a
			// split-brain would name the wrong defect. The arm's precondition is that the incumbent held
			// its lease throughout, so state that separately and let it fail on its own terms.
			incumbent.IsLeader.ShouldBeTrue(
				"the incumbent must retain leadership throughout; if it did not, its lease ended rather than "
				+ "being stolen and this arm did not exercise the property it exists to prove");
			challengerStole.ShouldBeFalse(
				"a candidate must not take over while the incumbent's lease is still valid ($$NOW < expiresAt)");

			await challenger.StopAsync(CancellationToken.None);
			await incumbent.StopAsync(CancellationToken.None);
		}
	}

	[Fact]
	public async Task TakeOver_AndAdvanceFencingToken_OnceTheIncumbentLeaseExpires()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"5fswhd server-clock takeover is a split-brain safety control — this real-Mongo lock must never be skipped");

		var db = "takeover_" + Guid.NewGuid().ToString("N");
		var resource = "res-" + Guid.NewGuid().ToString("N");
		var incumbent = CreateCandidate(db, resource, "incumbent");
		var challenger = CreateCandidate(db, resource, "challenger");

		long incumbentToken;
		await using (incumbent)
		await using (challenger)
		{
			await incumbent.StartAsync(CancellationToken.None);
			(await WaitUntilAsync(() => incumbent.IsLeader, TimeSpan.FromSeconds(15))).ShouldBeTrue(
				"the first candidate must acquire leadership");
			var incumbentLeadershipToken = incumbent.CurrentLeadership!.Value.FencingToken;
			incumbentLeadershipToken.ShouldNotBeNull("a fenced leader must present a fencing token (>= 1), never null");
			incumbentToken = incumbentLeadershipToken.Value;

			// Incumbent stops renewing; its lease will expire on the server clock (2s lease, no grace).
			await incumbent.StopAsync(CancellationToken.None);

			// The challenger takes over once the lease has expired.
			await challenger.StartAsync(CancellationToken.None);
			(await WaitUntilAsync(() => challenger.IsLeader, TimeSpan.FromSeconds(20))).ShouldBeTrue(
				"a candidate must take over once the incumbent lease has expired on the server clock");

			// Takeover must advance the fencing token, fencing off the stale (former) leader.
			var challengerToken = challenger.CurrentLeadership!.Value.FencingToken;
			challengerToken.ShouldNotBeNull("a fenced leader must present a fencing token (>= 1), never null");
			challengerToken.Value.ShouldBeGreaterThan(incumbentToken,
				"takeover must advance the fencing token past the prior leader's (monotonic, fences off the stale leader)");

			await challenger.StopAsync(CancellationToken.None);
		}
	}
}
