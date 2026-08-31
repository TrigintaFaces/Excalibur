// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.LeaderElection;
using Excalibur.Integration.Tests.LeaderElection;
using Excalibur.LeaderElection.Postgres;
using Excalibur.Testing.Conformance;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Integration.Tests.LeaderElection.Conformance;

/// <summary>
/// Binds the SHIPPED <see cref="LeaderElectionConformanceTestKit"/> to
/// <see cref="PostgresHealthBasedLeaderElection"/>, resolved through its own registration.
/// </summary>
/// <remarks>
/// <para>
/// The health-based provider is a DIFFERENT TYPE from <c>PostgresLeaderElection</c>, and the sibling
/// <c>PostgresLeaderElectionKitConformanceShould</c> binds the plain one. A consumer who calls
/// <c>AddPostgresHealthBasedLeaderElection</c> deploys a durable backend the published kit had never
/// been run against — it declares <c>IHealthBasedLeaderElection</c>, which refines
/// <see cref="ILeaderElection"/>, so a coverage census matching the contract name literally could not
/// see it either.
/// </para>
/// <para>
/// WHY A CONTAINER PER ELECTION. The registration is singleton and unnamed, so one provider serves one
/// election. The kit's contention arms need two candidates on ONE resource with DIFFERENT identities,
/// which is exactly what a single singleton cannot express. Each call therefore builds its own
/// <see cref="ServiceProvider"/> against the SAME Postgres container and resolves
/// <see cref="ILeaderElection"/> from it — so what the arms exercise is the object a consumer's host
/// would get, wrapped decorators and all, rather than one this test constructed by hand.
/// </para>
/// <para>
/// WHAT MAKES THE CONTENTION ARMS NON-VACUOUS. The health-based provider delegates arbitration to the
/// plain Postgres election, which takes <c>pg_try_advisory_lock</c> on a 64-bit key. Two candidates
/// given different keys never contend, and the mutual-exclusion arms would pass without testing
/// anything. The kit's seam hands out a resource NAME, so the name is folded to the key by the same
/// deterministic FNV-1a used by the plain sibling: same name in, same key out, in this process and any
/// other.
/// </para>
/// </remarks>
[Collection(PostgresLeaderElectionTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "LeaderElection")]
[Trait("Infrastructure", "Postgres")]
public sealed class PostgresHealthBasedLeaderElectionKitConformanceShould
	: LeaderElectionConformanceTestKit, IAsyncLifetime
{
	private readonly PostgresContainerFixture _fixture;
	private readonly List<ServiceProvider> _providers = [];

	public PostgresHealthBasedLeaderElectionKitConformanceShould(PostgresContainerFixture fixture)
		=> _fixture = fixture;

	public ValueTask InitializeAsync() => ValueTask.CompletedTask;

	/// <summary>
	/// Disposes every host this arm built.
	/// </summary>
	/// <remarks>
	/// The kit's own teardown is <c>(election as IDisposable)?.Dispose()</c>, and this provider implements
	/// <see cref="IAsyncDisposable"/> only, so that cast yields null and the kit disposes nothing. Left
	/// alone each arm strands a provider holding a pooled connection. Disposing the host disposes the
	/// election it owns; no assertion is altered, because every arm still ran against the object the
	/// container built and the kit stopped.
	/// </remarks>
	public async ValueTask DisposeAsync()
	{
		foreach (var provider in _providers)
		{
			try
			{
				await provider.DisposeAsync().ConfigureAwait(false);
			}
			catch (ObjectDisposedException)
			{
				// Already disposed by an arm that owns its own teardown.
			}
		}

		_providers.Clear();
	}

	/// <inheritdoc/>
	protected override ILeaderElection CreateElection(string resourceName, string? candidateId)
	{
		var instanceId = candidateId ?? GenerateCandidateId()[..24];
		var lockKey = LockKeyFor(resourceName);

		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = services.AddPostgresHealthBasedLeaderElection(
			pg =>
			{
				pg.ConnectionString = _fixture.ConnectionString;
				pg.LockKey = lockKey;
			},
			health =>
			{
				// The health table is shared by every candidate on this container, which is the point:
				// a step-down decision has to see the other candidate's heartbeat.
				health.AutoCreateTable = true;
				health.StepDownWhenUnhealthy = true;
			},
			election =>
			{
				// Distinct per candidate unless the kit pins one. Two candidates sharing an id are one
				// candidate as far as the election is concerned, and the exclusion arms would pass while
				// contending with themselves.
				election.InstanceId = instanceId;
				election.LeaseDuration = TimeSpan.FromSeconds(15);
				election.RenewInterval = TimeSpan.FromSeconds(1);
				election.RetryInterval = TimeSpan.FromMilliseconds(250);

				// Resolving through the real registration means ValidateOnStart runs, and the timing
				// invariant it enforces is RenewInterval + GracePeriod < LeaseDuration. The default
				// GracePeriod is 5s, which the plain siblings never hit because they construct their
				// options directly and no validator is registered on that path.
				election.GracePeriod = TimeSpan.FromSeconds(3);
				election.EnableHealthChecks = false;
			});

		var provider = services.BuildServiceProvider();
		_providers.Add(provider);

		return provider.GetRequiredService<ILeaderElection>();
	}

	/// <summary>
	/// Folds a resource name to the 64-bit advisory-lock key, deterministically.
	/// </summary>
	/// <remarks>
	/// FNV-1a rather than <see cref="string.GetHashCode()"/>: the framework hash is randomised per
	/// process, so two candidates would agree only by virtue of running in the same process — the
	/// contention arms would still pass here and would stop meaning what they claim the moment anything
	/// ran them apart.
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
	/// connection, so no key survives an arm. Health rows are keyed by instance id and every arm draws a
	/// fresh resource name from the kit, so there is nothing for a reset to remove.
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
	public Task ConcurrentContention_ExactlyOneLeader_Test() => ConcurrentContention_ExactlyOneLeader();

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
