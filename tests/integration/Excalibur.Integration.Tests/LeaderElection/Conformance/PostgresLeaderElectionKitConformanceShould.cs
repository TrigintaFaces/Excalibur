// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.LeaderElection;
using Excalibur.Integration.Tests.LeaderElection;
using Excalibur.LeaderElection.Postgres;
using Excalibur.Testing.Conformance;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Integration.Tests.LeaderElection.Conformance;

/// <summary>
/// Binds the SHIPPED <see cref="LeaderElectionConformanceTestKit"/> to the real Postgres provider.
/// </summary>
/// <remarks>
/// <para>
/// This is distinct from the sibling <c>PostgresLeaderElectionConformanceShould</c>, which derives the
/// PRIVATE base in <c>tests/Shared</c>. A contract that only a private base enforces is a contract no
/// consumer can obtain: a third party writing their own <c>ILeaderElection</c> can reference the shipped
/// kit and nothing else, so the shipped kit is the only surface whose arms are a promise to anybody
/// outside this repository. Until this class existed the shipped kit had exactly one deriver — the
/// in-memory provider, whose mutual exclusion is process-local and therefore the least load-bearing of the
/// seven.
/// </para>
/// <para>
/// WHAT MAKES THE CONTENTION ARMS NON-VACUOUS. Postgres arbitrates with <c>pg_try_advisory_lock</c> on a
/// 64-bit key, so two candidates given different keys never contend and the mutual-exclusion arms pass
/// without testing anything. The kit's seam hands out a resource NAME, so the name is folded to the key by
/// a deterministic hash below: same name in, same key out, in this process and any other. Advisory locks
/// are session-scoped and each election opens its own connection, so two instances against one container
/// are genuinely separate sessions.
/// </para>
/// </remarks>
[Collection(PostgresLeaderElectionTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "LeaderElection")]
[Trait("Infrastructure", "Postgres")]
public sealed class PostgresLeaderElectionKitConformanceShould : LeaderElectionConformanceTestKit, IAsyncLifetime
{
	private readonly PostgresContainerFixture _fixture;
	private readonly List<ILeaderElection> _created = [];

	public PostgresLeaderElectionKitConformanceShould(PostgresContainerFixture fixture) => _fixture = fixture;

	public ValueTask InitializeAsync() => ValueTask.CompletedTask;

	/// <summary>
	/// Disposes every election this arm constructed.
	/// </summary>
	/// <remarks>
	/// The kit's own teardown is <c>(election as IDisposable)?.Dispose()</c>, and no shipped provider
	/// implements <see cref="IDisposable"/> — all four implement <see cref="IAsyncDisposable"/> only — so
	/// that cast yields null and the kit disposes nothing at all. Left alone, each arm strands a provider
	/// holding a pooled connection. Disposing here is resource hygiene, not an alteration of any arm: every
	/// assertion still runs against the object the kit built and stopped.
	/// </remarks>
	public async ValueTask DisposeAsync()
	{
		foreach (var election in _created)
		{
			try
			{
				await election.DisposeAsync().ConfigureAwait(false);
			}
			catch (ObjectDisposedException)
			{
				// Already disposed by an arm that owns its own teardown.
			}
		}

		_created.Clear();
	}

	/// <inheritdoc/>
	protected override ILeaderElection CreateElection(string resourceName, string? candidateId)
	{
		var pgOptions = Options.Create(new PostgresLeaderElectionOptions
		{
			ConnectionString = _fixture.ConnectionString,
			LockKey = LockKeyFor(resourceName),
		});

		var electionOptions = Options.Create(new LeaderElectionOptions
		{
			// Distinct per candidate unless the kit pins one. Two candidates sharing an id are one
			// candidate as far as the election is concerned, and the exclusion arms would pass while
			// contending with themselves.
			InstanceId = candidateId ?? GenerateCandidateId()[..24],
			LeaseDuration = TimeSpan.FromSeconds(5),
			RenewInterval = TimeSpan.FromSeconds(1),
			RetryInterval = TimeSpan.FromMilliseconds(250),
			EnableHealthChecks = false,
		});

		var election = new PostgresLeaderElection(
			pgOptions,
			electionOptions,
			NullLogger<PostgresLeaderElection>.Instance);

		_created.Add(election);
		return election;
	}

	/// <summary>
	/// Folds a resource name to the 64-bit advisory-lock key, deterministically.
	/// </summary>
	/// <remarks>
	/// FNV-1a rather than <see cref="string.GetHashCode()"/>: the framework hash is randomised per process,
	/// so two candidates would agree only by virtue of running in the same process — the contention arms
	/// would still pass here and would stop meaning what they claim the moment anything ran them apart.
	/// The sign bit is cleared because the key is only ever compared for equality and a stable positive
	/// value reads better in <c>pg_locks</c>.
	/// </remarks>
	private static long LockKeyFor(string resourceName)
	{
		var hash = 14695981039346656037UL;

		foreach (var b in System.Text.Encoding.UTF8.GetBytes(resourceName))
		{
			hash ^= b;
			hash *= 1099511628211UL;
		}

		return (long)(hash & 0x7FFF_FFFF_FFFF_FFFFUL);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Advisory locks are released when the owning session ends and each election disposes its own
	/// connection, so no row or key survives an arm. Every arm also draws a fresh resource name from the
	/// kit, so there is nothing for a reset to remove.
	/// </remarks>
	protected override Task CleanupAsync() => Task.CompletedTask;

	[Fact]
	public Task StartAsync_ShouldInitiateParticipation_Test() => StartAsync_ShouldInitiateParticipation();

	[Fact]
	public Task StopAsync_ShouldRelinquishLeadership_Test() => StopAsync_ShouldRelinquishLeadership();

	[Fact]
	public Task StartAsync_AfterStop_ShouldRestartElection_Test() => StartAsync_AfterStop_ShouldRestartElection();

	[Fact]
	public Task StartAsync_SingleCandidate_ShouldBecomeLeader_Test() => StartAsync_SingleCandidate_ShouldBecomeLeader();

	[Fact]
	public Task StartAsync_SingleCandidate_IsLeaderShouldBeTrue_Test() => StartAsync_SingleCandidate_IsLeaderShouldBeTrue();

	[Fact]
	public Task StartAsync_SingleCandidate_CurrentLeaderIdShouldMatchCandidateId_Test() => StartAsync_SingleCandidate_CurrentLeaderIdShouldMatchCandidateId();

	[Fact]
	public Task MultipleCandidate_OnlyOneBecomesLeader_Test() => MultipleCandidate_OnlyOneBecomesLeader();

	[Fact]
	public Task MultipleCandidate_ReportedLeaderIdShouldNameTheLeader_Test() => MultipleCandidate_ReportedLeaderIdShouldNameTheLeader();

	[Fact]
	public Task MultipleCandidate_IncumbentShouldExcludeLaterCandidate_Test() => MultipleCandidate_IncumbentShouldExcludeLaterCandidate();

	[Fact]
	public Task ConcurrentContention_ExactlyOneLeader_Test() =>
		ConcurrentContention_ExactlyOneLeader();

	[Fact]
	public Task BecameLeader_ShouldFireWhenElected_Test() => BecameLeader_ShouldFireWhenElected();

	[Fact]
	public Task LostLeadership_ShouldFireWhenStopped_Test() => LostLeadership_ShouldFireWhenStopped();

	[Fact]
	public Task LeaderChanged_ShouldFireOnLeadershipChange_Test() => LeaderChanged_ShouldFireOnLeadershipChange();

	[Fact]
	public Task CandidateId_ShouldBeUniquePerInstance_Test() => CandidateId_ShouldBeUniquePerInstance();

	[Fact]
	public Task CurrentLeadership_AfterStop_ShouldBeNull_Test() => CurrentLeadership_AfterStop_ShouldBeNull();

	[Fact]
	public Task ConformanceSuite_ShouldWireEveryArm_Test() => ConformanceSuite_ShouldWireEveryArm();
}
