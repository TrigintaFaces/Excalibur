// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.LeaderElection;
using Excalibur.Integration.Tests.Data;
using Excalibur.LeaderElection.SqlServer;

using Tests.Shared.Infrastructure;

namespace Excalibur.Integration.Tests.LeaderElection;

/// <summary>
/// Real-DI + real-SQL-Server arm for the <c>leader-election-mutual-exclusion</c> manifest row: two
/// independently composed hosts contend for one resource, exactly one leads, and leadership transfers when
/// that one stops.
/// </summary>
/// <remarks>
/// <para>
/// <b>Guarantee under test.</b> <c>Excalibur.LeaderElection/ARCHITECTURE.md</c> - "At most one leader per
/// resource" (M1), with M2 as its named counterpart: "Mutual exclusion alone is satisfied by electing
/// nobody, so this arm is part of the contract."
/// </para>
/// <para>
/// <b>Why this is not the existing conformance suite.</b> <c>LeaderElectionConformanceTestKit</c> already
/// proves M1 and M2 against real SQL Server, and proves them well - but every provider binding constructs
/// its candidate with <c>new SqlServerLeaderElection(connectionString, resource, options, logger)</c>. That
/// proves the provider behaves when handed its dependencies; it cannot observe whether a consumer's own
/// <c>AddExcalibur(x =&gt; x.AddLeaderElection(le =&gt; le.UseSqlServer(...)))</c> produces a working
/// election at all. These arms resolve <see cref="ILeaderElection"/> from a real container built the
/// documented way, so a correct provider that is never reachable through the registration fails here.
/// </para>
/// <para>
/// <b>Why two containers and not two candidates from one.</b> Two separate <see cref="ServiceProvider"/>
/// instances are what two hosts are. A single provider registers one election, so pulling two candidates out
/// of it would require going around the registration - which is the thing under test. Each provider mints
/// its own <c>InstanceId</c> by default, so the candidates differ without either being configured to.
/// </para>
/// <para>
/// <b>Both halves.</b> SAFETY (M1) - the two candidates never both hold leadership. LIVENESS (M2) - one of
/// them acquires it, and after that one stops the other takes over. Without the liveness arms an election
/// that elects nobody, or one whose loser is permanently wedged, satisfies mutual exclusion perfectly.
/// </para>
/// <para>
/// <b>What this arm deliberately does not cover.</b> Fencing (F1) and the grace bound (G1) have their own
/// dedicated real-infrastructure suites; this row is the mutual-exclusion guarantee, and widening it here
/// would duplicate those rather than strengthen this one.
/// </para>
/// </remarks>
[Collection(SqlServerTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "LeaderElection")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerLeaderElectionMutualExclusionEndToEndShould
{
	private static readonly TimeSpan AcquireTimeout = TimeSpan.FromSeconds(30);

	private readonly SqlServerContainerFixture _fixture;

	public SqlServerLeaderElectionMutualExclusionEndToEndShould(SqlServerContainerFixture fixture) =>
		_fixture = fixture;

	[Fact]
	public async Task ElectExactlyOneLeader_AndTransferOnStop_ThroughTheRealDIRegistration()
	{
		var cancellationToken = TestContext.Current.CancellationToken;

		_fixture.DockerAvailable.ShouldBeTrue(
			"two hosts each believing they lead is the failure this guarantee exists to prevent - this "
			+ "real-SQL-Server arm must never be skipped");

		// Unique per run: sp_getapplock arbitrates on this string, so a name shared with another suite would
		// make the two arms contend with each other rather than with each other's candidates.
		var resource = "e2e-mutex-" + Guid.NewGuid().ToString("N");

		await using var firstHost = BuildConsumerComposition(resource);
		await using var secondHost = BuildConsumerComposition(resource);

		var first = firstHost.GetRequiredService<ILeaderElection>();
		var second = secondHost.GetRequiredService<ILeaderElection>();

		first.CandidateId.ShouldNotBe(
			second.CandidateId,
			"the two hosts must be distinct candidates, or the contention being measured is a candidate "
			+ "against itself");

		// Started concurrently, so the acquire really is contended rather than sequenced by the test.
		await Task.WhenAll(first.StartAsync(cancellationToken), second.StartAsync(cancellationToken))
			.ConfigureAwait(false);

		try
		{
			// LIVENESS (M2, acquisition). Someone must lead. An election that resolves, starts cleanly and
			// elects nobody satisfies every safety assertion below.
			(await WaitHelpers.WaitUntilAsync(
				() => first.IsLeader || second.IsLeader,
				AcquireTimeout,
				cancellationToken: cancellationToken).ConfigureAwait(false)).ShouldBeTrue(
				"neither host acquired leadership within the timeout, so the election is not reachable "
				+ "through the public registration - mutual exclusion held only because nothing happened");

			// SAFETY (M1). The two never both lead. SQL Server's claim IS the session-owned application lock,
			// so there is no lease-expiry window to allow here: both candidates are alive and renewing.
			(first.IsLeader && second.IsLeader).ShouldBeFalse(
				"both hosts hold leadership for the same resource at the same instant, so any leader-only "
				+ "work runs twice");

			var leader = first.IsLeader ? first : second;
			var challenger = ReferenceEquals(leader, first) ? second : first;

			// The winner is not merely "the one whose flag happens to be set" - it can name itself as the
			// leader, which is the half of the identity contract this arbitration primitive guarantees.
			// A follower CANNOT name the leader here and is not asked to: sp_getapplock carries no holder
			// identity, so the provider has nothing to report to a loser. That limit is the provider's, is
			// stated in its conformance suite, and asserting against it would be asserting a property the
			// framework never claimed.
			leader.CurrentLeaderId.ShouldBe(
				leader.CandidateId,
				"the winning host cannot name itself as leader, so a consumer fencing its writes with the "
				+ "leader's identity has nothing to fence with");
			challenger.IsLeader.ShouldBeFalse(
				"the losing host reports itself as leader alongside the winner");

			// LIVENESS (M2, transfer). The leader stands down; the challenger must be able to take over. This
			// is the arm that fails if the loser is wedged rather than waiting - a state indistinguishable
			// from correct mutual exclusion for as long as the incumbent lives.
			await leader.StopAsync(cancellationToken).ConfigureAwait(false);

			(await WaitHelpers.WaitUntilAsync(
				() => challenger.IsLeader,
				AcquireTimeout,
				cancellationToken: cancellationToken).ConfigureAwait(false)).ShouldBeTrue(
				"leadership never transferred after the incumbent stopped, so a rolling restart leaves the "
				+ "resource with no leader at all");

			leader.IsLeader.ShouldBeFalse(
				"the stopped host still reports itself as leader while its successor also leads, which is "
				+ "the double-leader state the guarantee forbids");
		}
		finally
		{
			await first.StopAsync(CancellationToken.None).ConfigureAwait(false);
			await second.StopAsync(CancellationToken.None).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Composes one host the way the package documents it: the Excalibur builder, the leader election
	/// feature, and the SQL Server provider pointed at the shared container.
	/// </summary>
	private ServiceProvider BuildConsumerComposition(string resource)
	{
		var connectionString = _fixture.ConnectionString;

		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = services.AddExcalibur(excalibur =>
			excalibur.AddLeaderElection(election =>
				election.UseSqlServer(sql => sql
					.ConnectionString(connectionString)
					.LockResource(resource))));

		// Health-based step-down is off because no health provider is composed here, and a candidate that
		// cannot score its own health must not be read as a candidate that lost the election. The retry
		// interval is shortened so the transfer arm observes a takeover rather than the default two-second
		// retry cadence. Lease, renew and grace keep their shipped defaults: they are bound to each other by
		// the options validator (renew + grace + skew must stay under the lease), so overriding one of them
		// here would be tuning a timing relationship this arm does not test.
		_ = services.Configure<LeaderElectionOptions>(options =>
		{
			options.EnableHealthChecks = false;
			options.StepDownWhenUnhealthy = false;
			options.RetryInterval = TimeSpan.FromMilliseconds(250);
		});

		return services.BuildServiceProvider();
	}
}
