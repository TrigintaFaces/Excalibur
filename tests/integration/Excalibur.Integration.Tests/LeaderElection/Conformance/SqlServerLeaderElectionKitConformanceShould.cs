// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.LeaderElection;
using Excalibur.Integration.Tests.Data;
using Excalibur.LeaderElection.SqlServer;
using Excalibur.Testing.Conformance;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Integration.Tests.LeaderElection.Conformance;

/// <summary>
/// Binds the SHIPPED <see cref="LeaderElectionConformanceTestKit"/> to the real SQL Server provider.
/// </summary>
/// <remarks>
/// <para>
/// SQL Server arbitrates with <c>sp_getapplock</c> at <c>@LockOwner = 'Session'</c>, so the lock resource
/// string is what two candidates must share to contend at all, and the instance id is what must differ.
/// The kit's resource name is passed through verbatim as the lock resource — <c>sp_getapplock</c> takes an
/// <c>nvarchar(255)</c> and the kit's names are 46 characters — so no folding is needed and no two arms
/// can collide.
/// </para>
/// <para>
/// The suite joins the assembly's existing SQL Server collection rather than declaring its own. A new
/// <c>ICollectionFixture</c> declaration would construct a SECOND fixture instance and therefore start a
/// second container for the same engine; sharing the collection costs serialisation against the other SQL
/// Server suites and nothing else, and these arms hold sub-second leases on keys nothing else names.
/// </para>
/// </remarks>
[Collection(SqlServerTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "LeaderElection")]
[Trait("Infrastructure", "SqlServer")]
public sealed class SqlServerLeaderElectionKitConformanceShould : LeaderElectionConformanceTestKit, IAsyncLifetime
{
	private readonly SqlServerContainerFixture _fixture;
	private readonly List<ILeaderElection> _created = [];

	public SqlServerLeaderElectionKitConformanceShould(SqlServerContainerFixture fixture) => _fixture = fixture;

	public ValueTask InitializeAsync() => ValueTask.CompletedTask;

	/// <summary>
	/// Disposes every election this arm constructed; see the Postgres sibling for why the kit cannot.
	/// </summary>
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
			}
		}

		_created.Clear();
	}

	/// <inheritdoc/>
	protected override ILeaderElection CreateElection(string resourceName, string? candidateId)
	{
		var electionOptions = Options.Create(new LeaderElectionOptions
		{
			InstanceId = candidateId ?? GenerateCandidateId()[..24],
			LeaseDuration = TimeSpan.FromSeconds(5),
			RenewInterval = TimeSpan.FromSeconds(1),
			RetryInterval = TimeSpan.FromMilliseconds(250),
			EnableHealthChecks = false,
		});

		var election = new SqlServerLeaderElection(
			_fixture.ConnectionString,
			resourceName,
			electionOptions,
			NullLogger<SqlServerLeaderElection>.Instance);

		_created.Add(election);
		return election;
	}

	/// <inheritdoc/>
	/// <remarks>
	/// A session-scoped application lock is released when its session ends, and each election owns its own
	/// connection, so nothing outlives an arm. Arms also draw a fresh resource name from the kit.
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
