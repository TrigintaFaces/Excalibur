// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.LeaderElection;
using Excalibur.Integration.Tests.LeaderElection;
using Excalibur.LeaderElection.Consul;
using Excalibur.Testing.Conformance;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Excalibur.Integration.Tests.LeaderElection.Conformance;

/// <summary>
/// Binds the SHIPPED <see cref="LeaderElectionConformanceTestKit"/> to <see cref="ConsulLeaderElection"/>,
/// resolved through its own registration against a real Consul server.
/// </summary>
/// <remarks>
/// <para>
/// kbjd0b: <see cref="ConsulLeaderElection"/> declares <see cref="IHealthBasedLeaderElection"/> directly --
/// there is no separate plain/health-based split the way Postgres and SQL Server have one -- so a single
/// deriver closes the gap for this provider. Until this class existed, Consul had only a mocked unit
/// suite (<c>tests/unit/Excalibur.LeaderElection.Tests/Consul/</c>): mutual exclusion is arbitrated by a
/// real Consul session and KV <c>Acquire</c>/<c>Release</c>, which a mock cannot establish.
/// </para>
/// <para>
/// WHY A SERVICE PROVIDER PER ELECTION. <c>AddConsulLeaderElectionForResource</c> registers <see
/// cref="ILeaderElection"/> as a singleton, so one provider serves one candidate. The kit's contention arms
/// need two candidates on ONE resource with DIFFERENT identities, which a single singleton cannot express.
/// Each call therefore builds its own <see cref="ServiceProvider"/> against the SAME Consul server and
/// resolves through the real <c>AddConsulLeaderElection</c> + <c>AddConsulLeaderElectionForResource</c>
/// registration path -- the same one a consumer's host uses -- so the arms exercise the object a consumer
/// would get, not one this test constructed by hand.
/// </para>
/// <para>
/// WHY LockDelay IS FORCED TO ZERO. Consul enforces a lock-delay (default 15s) after a session is
/// invalidated -- including the explicit <c>Session.Destroy</c> that <c>StopAsync</c> performs -- before
/// the same key can be re-acquired by a new session. <see cref="ILeaderElectionConsulBuilder"/> exposes no
/// setter for it, so it is set via a second, later <c>Configure&lt;ConsulLeaderElectionOptions&gt;</c> call,
/// which composes after the builder's own (the options pipeline runs every registered configure delegate
/// in order, so the one registered second wins for the fields it touches). Left at its default, every
/// restart/takeover arm below would wait out a real 15-second delay before a new session could re-acquire
/// the same key -- several multiples of the kit's default event timeout.
/// </para>
/// </remarks>
[Collection(ConsulLeaderElectionTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "LeaderElection")]
[Trait("Infrastructure", "Consul")]
public sealed class ConsulLeaderElectionKitConformanceShould
	: LeaderElectionConformanceTestKit, IAsyncLifetime
{
	private readonly ConsulContainerFixture _fixture;
	private readonly List<ServiceProvider> _providers = [];

	public ConsulLeaderElectionKitConformanceShould(ConsulContainerFixture fixture) => _fixture = fixture;

	public ValueTask InitializeAsync() => ValueTask.CompletedTask;

	/// <summary>
	/// Disposes every host this arm built, which disposes the election it owns.
	/// </summary>
	/// <remarks>
	/// The kit's own teardown is <c>(election as IDisposable)?.Dispose()</c>, and
	/// <see cref="ConsulLeaderElection"/> implements only <see cref="IAsyncDisposable"/>, so that cast
	/// yields null and the kit disposes nothing. Left alone each arm strands a live Consul session.
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
		_fixture.DockerAvailable.ShouldBeTrue(
			"LeaderElectionConformanceTestKit arms against ConsulLeaderElection must run against a real "
			+ "Consul server -- never skipped. " + (_fixture.InitializationError ?? "Consul container required."));

		var instanceId = candidateId ?? GenerateCandidateId()[..24];

		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = services.AddConsulLeaderElection(consul =>
		{
			consul.Address(_fixture.ConsulAddress);

			// Consul rejects any session TTL below 10s server-side ("Invalid Session TTL"); this is the
			// minimum the real server accepts, not an arbitrary test choice.
			consul.SessionTtl(TimeSpan.FromSeconds(10));

			// Shared across every candidate created against this fixture: the leader KV key is
			// {KeyPrefix}/leader/{resourceName}, so candidates contend on the prefix+resourceName pair,
			// not the prefix alone.
			consul.LockKey("le-conformance");
		});

		// See the WHY LockDelay IS FORCED TO ZERO remark on the class.
		_ = services.Configure<ConsulLeaderElectionOptions>(o => o.LockDelay = TimeSpan.Zero);

		_ = services.AddConsulLeaderElectionForResource(resourceName, instanceId);

		var provider = services.BuildServiceProvider();
		_providers.Add(provider);

		return provider.GetRequiredService<ILeaderElection>();
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Every arm draws a fresh <see cref="LeaderElectionConformanceTestKit.GenerateResourceName"/>, which
	/// becomes a distinct KV key, so there is nothing shared across arms for a reset to remove. Consul
	/// sessions are torn down by each provider's own disposal (see <see cref="DisposeAsync"/>).
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
