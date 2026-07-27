// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.LeaderElection;
using Excalibur.LeaderElection.Postgres;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Tests.Shared.Conformance.LeaderElection;
using Tests.Shared.Fixtures;

using Xunit;

namespace Excalibur.Integration.Tests.LeaderElection;

/// <summary>
/// Runs the shared leader-election conformance kit against the Postgres provider, so that mutual exclusion
/// and takeover are verified on a provider that actually coordinates across processes.
/// </summary>
/// <remarks>
/// <para>
/// WHY THIS EXISTS. The kit carries sixteen arms, including the safety arm that four concurrent starters
/// yield exactly one leader, and until now exactly one class derived it -- the in-memory provider, whose
/// mutual exclusion is process-local and therefore the least load-bearing of the seven. Every provider that
/// coordinates across processes had no arm enforcing at-most-one-leader. Nothing here says the Postgres
/// provider was broken; it says nothing would have reported it if a future edit broke it.
/// </para>
/// <para>
/// THE TWO THINGS THAT MAKE THIS NON-VACUOUS, both easy to get silently wrong:
/// </para>
/// <para>
/// 1. The competing election MUST share the lock key. Postgres arbitrates leadership with
/// <c>pg_try_advisory_lock</c> on that key, so two instances given different keys never contend and the
/// safety arm passes without ever testing anything. The key is therefore a single field read by both
/// factory methods, not a value each computes.
/// </para>
/// <para>
/// 2. The competing election MUST have a DIFFERENT instance id. Two candidates sharing an id are one
/// candidate as far as the election is concerned, and the arm would again pass while contending with
/// itself.
/// </para>
/// <para>
/// The key is randomised per test-class instance -- xUnit constructs a fresh instance per arm -- so
/// concurrently executing arms cannot collide on the same advisory lock and produce a spurious failure.
/// Advisory locks are session-scoped and each election opens its own connection, so two instances against
/// one container are genuinely separate sessions: real contention, not simulated.
/// </para>
/// </remarks>
/// <summary>
/// Declares the shared Postgres container for the leader-election conformance run.
/// </summary>
/// <remarks>
/// xUnit resolves collection definitions PER ASSEMBLY. <c>ContainerCollections.Postgres</c> names a
/// collection defined in Tests.Shared and in a different integration assembly, so referencing that name
/// here binds to nothing and every arm fails with "constructor parameters did not have matching fixture
/// data" before a container is ever started -- sixteen failures in twenty-eight milliseconds, which looks
/// like a broken provider and is not. The definition has to live in the assembly that uses it.
/// </remarks>
[CollectionDefinition(CollectionName)]
public sealed class PostgresLeaderElectionTestCollection : ICollectionFixture<PostgresContainerFixture>
{
	public const string CollectionName = "Postgres LeaderElection Integration Tests";
}

[Collection(PostgresLeaderElectionTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "LeaderElection")]
[Trait("Infrastructure", "Postgres")]
public sealed class PostgresLeaderElectionConformanceShould : LeaderElectionConformanceTestBase
{
	private readonly PostgresContainerFixture _fixture;

	/// <summary>
	/// Shared by both the primary and the competing election. Randomised per test-class instance so that
	/// arms running concurrently contend only with their own competitor.
	/// </summary>
	private readonly long _lockKey = Random.Shared.NextInt64(1_000_000, long.MaxValue);

	public PostgresLeaderElectionConformanceShould(PostgresContainerFixture fixture) => _fixture = fixture;

	protected override Task<ILeaderElection> CreateElectionAsync() =>
		Task.FromResult(CreateElection());

	/// <summary>
	/// A second, independent candidate for the SAME lock key -- the competitor the safety arm needs.
	/// </summary>
	protected override Task<ILeaderElection> CreateCompetingElectionAsync() =>
		Task.FromResult(CreateElection());

	/// <summary>
	/// Postgres arbitrates by advisory lock, so a follower cannot name the leader. This asserts the half
	/// the contract actually guarantees.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The inherited arm requires EVERY candidate to converge on a non-null <c>CurrentLeaderId</c>.
	/// <c>ILeaderElection</c> guarantees that only for an instance that is ITSELF the leader — whether any
	/// other instance can name the holder depends on the arbitration mechanism, and
	/// <c>pg_try_advisory_lock</c> returns nothing but true or false to a candidate that loses. There is no
	/// owner identity to read, so the inherited form asserts a property this provider is not promised to
	/// have.
	/// </para>
	/// <para>
	/// This is NOT a skip. A skipped arm proves nothing while reading as covered — the exact failure this
	/// kit exists to prevent. The guaranteed half is asserted here in full: contention still resolves to
	/// exactly one leader, and that leader can name itself. What is dropped is only the follower-side
	/// observation the contract never promised. Leader DISCOVERY is therefore recorded UNVERIFIED for this
	/// provider while mutual EXCLUSION is verified by the sibling arm.
	/// </para>
	/// </remarks>
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
				"advisory-lock arbitration must still yield exactly one leader even though followers cannot "
				+ "name it");

			// The guaranteed half of the contract: the leader can name itself.
			leaders[0].CurrentLeaderId.ShouldNotBeNull(
				"CurrentLeaderId is guaranteed non-null while the instance asked IS the leader; if the "
				+ "leader itself cannot name the leader, the contract is broken rather than merely limited");
		}
		finally
		{
			foreach (var competitor in competitors.Skip(1))
			{
				await competitor.DisposeAsync().ConfigureAwait(false);
			}
		}
	}

	protected override Task CleanupAsync() =>
		// Advisory locks are released when the owning session ends, and each election disposes its own
		// connection, so there is no shared row or key to tidy between arms.
		Task.CompletedTask;

	private ILeaderElection CreateElection()
	{
		var pgOptions = Options.Create(new PostgresLeaderElectionOptions
		{
			ConnectionString = _fixture.ConnectionString,
			LockKey = _lockKey,
		});

		var electionOptions = Options.Create(new LeaderElectionOptions
		{
			// Distinct per candidate. Two candidates sharing an id are one candidate, and the mutual
			// exclusion arm would pass while contending with itself.
			InstanceId = $"cand-{Guid.NewGuid():N}"[..24],
			LeaseDuration = TimeSpan.FromSeconds(5),
			RenewInterval = TimeSpan.FromSeconds(1),
			RetryInterval = TimeSpan.FromMilliseconds(250),
		});

		return new PostgresLeaderElection(
			pgOptions,
			electionOptions,
			NullLogger<PostgresLeaderElection>.Instance);
	}
}
