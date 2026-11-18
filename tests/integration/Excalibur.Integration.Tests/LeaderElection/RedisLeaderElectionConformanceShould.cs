// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.LeaderElection;
using Excalibur.LeaderElection.Redis;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using StackExchange.Redis;

using Tests.Shared.Conformance.LeaderElection;
using Tests.Shared.Fixtures;

using Xunit;

namespace Excalibur.Integration.Tests.LeaderElection;

/// <summary>
/// Declares the shared Redis container for the leader-election conformance run.
/// </summary>
/// <remarks>
/// xUnit resolves collection definitions PER ASSEMBLY, so naming a collection defined in another
/// integration assembly binds to nothing and every arm fails with "constructor parameters did not have
/// matching fixture data" before a container is ever started — which looks like a broken provider and is
/// not. The definition has to live in the assembly that uses it.
/// </remarks>
[CollectionDefinition(CollectionName)]
public sealed class RedisLeaderElectionTestCollection : ICollectionFixture<RedisContainerFixture>
{
	public const string CollectionName = "Redis LeaderElection Integration Tests";
}

/// <summary>
/// Runs the shared leader-election conformance kit against the Redis provider, so mutual exclusion and
/// takeover are verified on a provider that coordinates across processes.
/// </summary>
/// <remarks>
/// <para>
/// WHY THIS EXISTS. Nothing here says the Redis provider was broken; it says nothing would have reported
/// it if a future edit broke it. Redis was one of five providers with no arm of this kit enforcing
/// at-most-one-leader or takeover.
/// </para>
/// <para>
/// THE TWO THINGS THAT MAKE THIS NON-VACUOUS, both easy to get silently wrong and both inherited from the
/// sibling Postgres deriver's hard-won notes:
/// </para>
/// <para>
/// 1. The competing election MUST share the lock key. Redis arbitrates with <c>SET key value NX PX</c> on
/// that exact key, so two instances given different keys never contend and the safety arm passes while
/// testing nothing. The key is therefore a single field read by both factory methods, not a value each
/// computes.
/// </para>
/// <para>
/// 2. The competing election MUST have a DIFFERENT instance id. Redis stores the candidate id as the
/// lock's VALUE and the renewal is an owner-token compare-then-extend, so two candidates sharing an id are
/// one candidate to the provider: each would be able to extend the other's lock and the arm would pass
/// while contending with itself.
/// </para>
/// <para>
/// WHAT MAKES THE SAFETY ARM REAL HERE RATHER THAN SIMULATED. Expiry is the Redis server's own <c>PX</c>
/// TTL, not a client clock, and each candidate opens its OWN multiplexer — so the contention is between
/// two independent connections against one server, which is what two processes look like. Nothing is
/// faked and no clock is injected.
/// </para>
/// <para>
/// The key is randomised per test-class instance, and xUnit constructs a fresh instance per arm, so arms
/// executing concurrently contend only with their own competitor and cannot produce a spurious failure.
/// </para>
/// </remarks>
[Collection(RedisLeaderElectionTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "LeaderElection")]
[Trait("Infrastructure", "Redis")]
public sealed class RedisLeaderElectionConformanceShould : LeaderElectionConformanceTestBase
{
	private readonly RedisContainerFixture _fixture;

	/// <summary>
	/// Shared by the primary and every competing election. Randomised per test-class instance so that
	/// arms running concurrently contend only with their own competitor.
	/// </summary>
	private readonly string _lockKey = $"le-conformance:{Guid.NewGuid():N}";

	/// <summary>
	/// Every multiplexer this arm opened. Each candidate gets its own, so they must all be disposed —
	/// leaking them holds connections against the shared container for the whole run.
	/// </summary>
	private readonly List<IConnectionMultiplexer> _connections = [];

	public RedisLeaderElectionConformanceShould(RedisContainerFixture fixture) => _fixture = fixture;

	protected override async Task<ILeaderElection> CreateElectionAsync() =>
		await CreateElectionCoreAsync().ConfigureAwait(false);

	/// <summary>
	/// A second, independent candidate for the SAME lock key — the competitor the safety arm needs.
	/// </summary>
	protected override async Task<ILeaderElection> CreateCompetingElectionAsync() =>
		await CreateElectionCoreAsync().ConfigureAwait(false);

	protected override async Task CleanupAsync()
	{
		// The key is randomised per class instance, so there is no shared key to tidy between arms; the
		// connections are what must not leak.
		foreach (var connection in _connections)
		{
			await connection.DisposeAsync().ConfigureAwait(false);
		}

		_connections.Clear();
	}

	private async Task<ILeaderElection> CreateElectionCoreAsync()
	{
		// One multiplexer PER CANDIDATE, so two candidates are two connections against one server rather
		// than two objects sharing one. Sharing would still contend correctly on the key, but it would
		// stop resembling the deployment this kit is here to verify.
		var connection = await ConnectionMultiplexer.ConnectAsync(_fixture.ConnectionString)
			.ConfigureAwait(false);
		_connections.Add(connection);

		var electionOptions = Options.Create(new LeaderElectionOptions
		{
			// Distinct per candidate. Two candidates sharing an id are one candidate to an owner-token
			// renewal, and the mutual exclusion arm would pass while contending with itself.
			InstanceId = $"cand-{Guid.NewGuid():N}"[..24],
			LeaseDuration = TimeSpan.FromSeconds(5),
			RenewInterval = TimeSpan.FromSeconds(1),
			GracePeriod = TimeSpan.FromSeconds(2),
			RetryInterval = TimeSpan.FromMilliseconds(250),
		});

		return new RedisLeaderElection(
			connection,
			_lockKey,
			electionOptions,
			NullLogger<RedisLeaderElection>.Instance);
	}
}
