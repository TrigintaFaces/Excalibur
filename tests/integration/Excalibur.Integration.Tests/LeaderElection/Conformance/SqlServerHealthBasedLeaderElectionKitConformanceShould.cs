// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.LeaderElection;
using Excalibur.Integration.Tests.Data;
using Excalibur.Integration.Tests.LeaderElection;
using Excalibur.LeaderElection.SqlServer;
using Excalibur.Testing.Conformance;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Integration.Tests.LeaderElection.Conformance;

/// <summary>
/// Binds the SHIPPED <see cref="LeaderElectionConformanceTestKit"/> to
/// <see cref="SqlServerHealthBasedLeaderElection"/>, resolved through its own registration.
/// </summary>
/// <remarks>
/// <para>
/// The health-based provider is a DIFFERENT TYPE from <c>SqlServerLeaderElection</c>, and the sibling
/// <c>SqlServerLeaderElectionKitConformanceShould</c> binds the plain one. A consumer who calls
/// <c>AddSqlServerHealthBasedLeaderElection</c> deploys a durable backend the published kit had never
/// been run against — it declares <c>IHealthBasedLeaderElection</c>, which refines
/// <see cref="ILeaderElection"/>, so a coverage census matching the contract name literally could not
/// see it either.
/// </para>
/// <para>
/// WHY A CONTAINER PER ELECTION. The registration is singleton and unnamed, so one provider serves one
/// election. The kit's contention arms need two candidates on ONE resource with DIFFERENT identities,
/// which is exactly what a single singleton cannot express. Each call therefore builds its own
/// <see cref="ServiceProvider"/> against the SAME SQL Server container and resolves
/// <see cref="ILeaderElection"/> from it — so what the arms exercise is the object a consumer's host
/// would get, wrapped decorators and all, rather than one this test constructed by hand.
/// </para>
/// <para>
/// WHAT MAKES THE CONTENTION ARMS NON-VACUOUS. Arbitration is a session-scoped
/// <c>sp_getapplock</c> keyed by the lock RESOURCE, and the kit hands out a distinct resource name per
/// arm, so the name is passed through unchanged. Two candidates on one name are two sessions competing
/// for one lock; two candidates on different names would never contend and the mutual-exclusion arms
/// would pass without testing anything.
/// </para>
/// </remarks>
[Collection(SqlServerTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "LeaderElection")]
[Trait("Infrastructure", "SqlServer")]
public sealed class SqlServerHealthBasedLeaderElectionKitConformanceShould
	: LeaderElectionConformanceTestKit, IAsyncLifetime
{
	private readonly SqlServerContainerFixture _fixture;
	private readonly List<ServiceProvider> _providers = [];

	public SqlServerHealthBasedLeaderElectionKitConformanceShould(SqlServerContainerFixture fixture)
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

		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = services.AddSqlServerHealthBasedLeaderElection(
			_fixture.ConnectionString,
			resourceName,
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

	/// <inheritdoc/>
	/// <remarks>
	/// A session-scoped application lock is released when its session ends and each election disposes its
	/// own connection, so nothing survives an arm. Health rows are keyed by instance id and every arm
	/// draws a fresh resource name from the kit, so there is nothing for a reset to remove.
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
