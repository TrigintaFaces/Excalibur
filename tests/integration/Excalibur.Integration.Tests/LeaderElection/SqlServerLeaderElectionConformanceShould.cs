// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.LeaderElection;
using Excalibur.Integration.Tests.Data;
using Excalibur.LeaderElection.SqlServer;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Tests.Shared.Conformance.LeaderElection;
using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.LeaderElection;

/// <summary>
/// Runs the leader-election conformance kit against the SQL Server provider on a real server.
/// </summary>
/// <remarks>
/// <para>
/// WHY THIS MATTERS MORE THAN ANOTHER FENCING ARM. The kit's load-bearing arm is that four concurrent
/// starters yield exactly one leader, and mutual exclusion is the guarantee whose violation is split brain.
/// A provider that coordinates across processes with no arm asserting it is not reported broken by anything
/// if a future edit breaks it, which is the state this provider was in.
/// </para>
/// <para>
/// THE TWO WAYS THIS DERIVER COULD PASS WHILE TESTING NOTHING, both easy to write by accident. The
/// candidates MUST share the lock resource: SQL Server arbitrates with <c>sp_getapplock</c> on that name,
/// so candidates given different names never contend and the mutual-exclusion arm passes without ever
/// racing. It is therefore one field read by both factory methods rather than a value each computes. And
/// the candidates MUST have DIFFERENT instance ids, because two candidates sharing an id are one candidate
/// as far as the election is concerned and the arm would contend with itself.
/// </para>
/// <para>
/// Each election builds its own connection from the connection string, and an application lock is scoped to
/// a session, so two candidates are genuinely two sessions competing for one lock rather than two objects
/// sharing one holder. The lock resource is randomised per test-class instance — xUnit constructs a fresh
/// instance per arm — so arms running back to back cannot collide on one another's lock.
/// </para>
/// </remarks>
[Collection(SqlServerTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "LeaderElection")]
[Trait("Infrastructure", "SqlServer")]
public sealed class SqlServerLeaderElectionConformanceShould : LeaderElectionConformanceTestBase
{
	private readonly SqlServerContainerFixture _fixture;

	/// <summary>Shared by the primary and every competitor; randomised per test-class instance.</summary>
	private readonly string _lockResource = $"le-conformance-{Guid.NewGuid():N}";

	public SqlServerLeaderElectionConformanceShould(SqlServerContainerFixture fixture) => _fixture = fixture;

	/// <inheritdoc/>
	/// <remarks>
	/// Generous because a takeover waits on the predecessor's session releasing the lock and on the
	/// successor's next retry, against a container carrying whatever else the shard is running. The base
	/// polls to this deadline rather than sleeping, so a provider that is quick costs nothing.
	/// </remarks>
	protected override TimeSpan EventTimeout => TimeSpan.FromSeconds(60);

	/// <inheritdoc/>
	protected override Task<ILeaderElection> CreateElectionAsync() => Task.FromResult(CreateCandidate());

	/// <inheritdoc/>
	protected override Task<ILeaderElection> CreateCompetingElectionAsync() =>
		Task.FromResult(CreateCandidate());

	/// <summary>
	/// Contention resolves to exactly one leader, and that leader can name itself — which is all an
	/// application lock promises.
	/// </summary>
	/// <remarks>
	/// <para>
	/// OVERRIDDEN BECAUSE THE PRIMITIVE CARRIES NO HOLDER IDENTITY. <c>sp_getapplock</c> answers the session
	/// that attempted it and nothing else; it records no owner a third party can read back. So a follower
	/// has nothing to report and returns <see langword="null"/>, which the contract permits —
	/// <see langword="null"/> on a follower means <em>unknown</em> and never <em>no leader</em>. The base arm
	/// requires every candidate to name the leader, which this provider cannot do and never promised to.
	/// </para>
	/// <para>
	/// WHAT IS DROPPED AND WHAT IS NOT. Only the follower-side observation is dropped. The guaranteed half is
	/// asserted in full: contention still resolves to exactly one leader, and that leader can name itself. A
	/// follower naming someone who is not the leader would still be a violation, and the sibling
	/// mutual-exclusion arm is unweakened. Leader DISCOVERY is therefore UNVERIFIED for this provider, by the
	/// nature of the primitive rather than by omission, while mutual EXCLUSION is verified.
	/// </para>
	/// <para>
	/// This is the same override the Postgres deriver carries for the same reason —
	/// <c>pg_try_advisory_lock</c> is the same shape of primitive — and the reasoning there is where this
	/// came from.
	/// </para>
	/// </remarks>
	/// <returns> A task that completes when the arm finishes. </returns>
	public override async Task ConcurrentContention_AllCandidatesAgreeOnLeader()
	{
		var competitors = new List<ILeaderElection> { Election };

		for (var i = 0; i < 2; i++)
		{
			competitors.Add(await CreateCompetingElectionAsync().ConfigureAwait(false));
		}

		try
		{
			await Task.WhenAll(competitors.Select(c => c.StartAsync(CancellationToken.None)))
				.ConfigureAwait(false);

			await AwaitConditionAsync(
				() => competitors.Count(c => c.IsLeader) == 1,
				EventTimeout).ConfigureAwait(false);

			var leaders = competitors.Where(c => c.IsLeader).ToList();

			leaders.Count.ShouldBe(
				1,
				"application-lock arbitration must still yield exactly one leader even though followers "
				+ "cannot name it");

			leaders[0].CurrentLeaderId.ShouldNotBeNull(
				"CurrentLeaderId is guaranteed non-null while the instance asked IS the leader; if the "
				+ "leader itself cannot name the leader, the contract is broken rather than merely limited");

			// The two assertions above are satisfied by an arrangement in which nobody ever contended, so
			// without the two below this override has no power to detect that it stopped contending — the
			// over-narrowed-arrangement hole that an override opens by construction. These close it: there
			// must BE followers, and what a follower may not do is name somebody who is not the leader.
			var followers = competitors.Where(c => !c.IsLeader).ToList();

			followers.ShouldNotBeEmpty(
				"the arrangement must actually contend: with no follower, exactly-one-leader and "
				+ "the-leader-names-itself both hold trivially and this arm would certify a fixture in "
				+ "which nothing raced");

			foreach (var follower in followers)
			{
				// null means UNKNOWN here and is conforming, because the primitive carries no owner a third
				// party can read. Naming a non-leader is not conforming, and that is the half this asserts.
				if (follower.CurrentLeaderId is not null)
				{
					follower.CurrentLeaderId.ShouldBe(
						leaders[0].CandidateId,
						"a follower may report null for unknown, but it may never name an instance that is "
						+ "not the leader");
				}
			}
		}
		finally
		{
			foreach (var competitor in competitors.Skip(1))
			{
				await competitor.StopAsync(CancellationToken.None).ConfigureAwait(false);
				await competitor.DisposeAsync().ConfigureAwait(false);
			}
		}
	}

	/// <inheritdoc/>
	protected override Task CleanupAsync()
	{
		// Nothing to tidy: the lock is session-scoped and dies with each election's own connection, and the
		// lock resource is randomised per class instance so no later arm can observe it.
		return Task.CompletedTask;
	}

	private ILeaderElection CreateCandidate()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"mutual exclusion is the guarantee whose violation is split brain, so these arms must never be "
			+ "skipped for want of a container");

		return new SqlServerLeaderElection(
			_fixture.ConnectionString,
			_lockResource,
			Options.Create(new LeaderElectionOptions
			{
				InstanceId = $"cand-{Guid.NewGuid():N}"[..24],
				LeaseDuration = TimeSpan.FromSeconds(5),
				RenewInterval = TimeSpan.FromSeconds(1),
				GracePeriod = TimeSpan.FromSeconds(2),
				RetryInterval = TimeSpan.FromMilliseconds(250),
			}),
			NullLogger<SqlServerLeaderElection>.Instance);
	}
}
