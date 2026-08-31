// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.LeaderElection;
using Excalibur.LeaderElection.Redis;
using Excalibur.Testing.Conformance;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using StackExchange.Redis;

namespace Excalibur.Integration.Tests.LeaderElection.Conformance;

/// <summary>
/// Binds the SHIPPED <see cref="LeaderElectionConformanceTestKit"/> to the real Redis provider.
/// </summary>
/// <remarks>
/// <para>
/// Redis is the one of these four whose arbitration primitive carries the owner's identity: the lock is a
/// string key whose VALUE is the holder's candidate id, so a candidate that loses the race can read the
/// key and name the leader. That makes it the provider on which the kit's leader-discovery arm is a real
/// assertion rather than a demand the primitive cannot satisfy.
/// </para>
/// <para>
/// One multiplexer is shared by every candidate, and that does NOT weaken the contention arms. Postgres
/// and SQL Server arbitrate per SESSION, so sharing a connection there would make two candidates one and
/// the exclusion arms would test nothing; Redis arbitrates with <c>SET NX</c> executed on the server, and
/// the second setter is refused no matter which connection it arrives on. Ownership is carried in the
/// value, not in the connection.
/// </para>
/// </remarks>
[Collection(Excalibur.Integration.Tests.Redis.RedisTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "LeaderElection")]
[Trait("Infrastructure", "Redis")]
public sealed class RedisLeaderElectionKitConformanceShould : LeaderElectionConformanceTestKit, IAsyncLifetime
{
	private readonly RedisContainerFixture _fixture;
	private readonly List<ILeaderElection> _created = [];
	private IConnectionMultiplexer? _multiplexer;

	public RedisLeaderElectionKitConformanceShould(RedisContainerFixture fixture) => _fixture = fixture;

	public async ValueTask InitializeAsync() =>
		_multiplexer = await ConnectionMultiplexer.ConnectAsync(_fixture.ConnectionString).ConfigureAwait(false);

	/// <summary>
	/// Disposes every election this arm constructed, then the shared multiplexer.
	/// </summary>
	/// <remarks>
	/// See the Postgres sibling: the kit's teardown casts to <see cref="IDisposable"/> and no shipped
	/// provider implements it, so the kit disposes nothing.
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
			}
		}

		_created.Clear();

		if (_multiplexer is not null)
		{
			await _multiplexer.DisposeAsync().ConfigureAwait(false);
		}
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

		var election = new RedisLeaderElection(
			_multiplexer ?? throw new InvalidOperationException("Multiplexer not initialised"),
			// Verbatim. The kit already draws a fresh GUID-suffixed name per arm, so prefixing would add
			// no isolation and would hide which key an arm actually took when reading the container.
			resourceName,
			electionOptions,
			NullLogger<RedisLeaderElection>.Instance);

		_created.Add(election);
		return election;
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Each arm's key is unique and carries the lease TTL, so an abandoned key expires on its own. Nothing
	/// an arm writes can be observed by another arm.
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
