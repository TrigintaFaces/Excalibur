// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.LeaderElection;
using Excalibur.Integration.Tests.Data.Persistence;
using Excalibur.LeaderElection.MongoDB;
using Excalibur.Testing.Conformance;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using MongoDB.Driver;

namespace Excalibur.Integration.Tests.LeaderElection.Conformance;

/// <summary>
/// Binds the SHIPPED <see cref="LeaderElectionConformanceTestKit"/> to the real MongoDB provider.
/// </summary>
/// <remarks>
/// <para>
/// Mongo arbitrates with a lease DOCUMENT in a collection, keyed by resource name, so the resource name is
/// passed through verbatim and two candidates naming the same resource genuinely contend on the same
/// document. Ownership is recorded in that document, so a follower can name the leader.
/// </para>
/// <para>
/// One client is shared by every candidate. As with Redis and unlike the two session-lock providers, that
/// does not collapse two candidates into one: arbitration is a conditional update executed by the server
/// against the document, and the loser is refused regardless of which connection it arrived on.
/// </para>
/// <para>
/// Each arm uses its OWN collection, named from the arm's resource name. The provider's lease documents are
/// swept by a TTL index it creates on the collection, and a TTL index is a property of the collection
/// rather than of the document — so arms sharing a collection would share that index and one arm's
/// provisioning would be observable by the next. A collection per arm makes that impossible.
/// </para>
/// </remarks>
[Collection(MongoDbPersistenceProviderTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "LeaderElection")]
[Trait("Infrastructure", "MongoDb")]
public sealed class MongoDbLeaderElectionKitConformanceShould : LeaderElectionConformanceTestKit, IAsyncLifetime
{
	private const string DatabaseName = "le_conformance";

	private readonly MongoDbContainerFixture _fixture;
	private readonly List<ILeaderElection> _created = [];
	private IMongoClient? _client;

	public MongoDbLeaderElectionKitConformanceShould(MongoDbContainerFixture fixture) => _fixture = fixture;

	public ValueTask InitializeAsync()
	{
		_client = new MongoClient(_fixture.ConnectionString);
		return ValueTask.CompletedTask;
	}

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
		_client?.Dispose();
	}

	/// <inheritdoc/>
	protected override ILeaderElection CreateElection(string resourceName, string? candidateId)
	{
		var mongoOptions = Options.Create(new MongoDbLeaderElectionOptions
		{
			ConnectionString = _fixture.ConnectionString,
			DatabaseName = DatabaseName,
			// Derived from the resource name, so it is the SAME collection for both candidates of one arm
			// and a different one from every other arm.
			CollectionName = "le_" + resourceName.Replace("-", "_", StringComparison.Ordinal),
			LeaseDurationSeconds = 5,
			RenewIntervalSeconds = 1,
			TimeoutInSeconds = 10,
			TakeoverGraceSeconds = 1,
		});

		var electionOptions = Options.Create(new LeaderElectionOptions
		{
			InstanceId = candidateId ?? GenerateCandidateId()[..24],
			LeaseDuration = TimeSpan.FromSeconds(5),
			RenewInterval = TimeSpan.FromSeconds(1),
			RetryInterval = TimeSpan.FromMilliseconds(250),
			EnableHealthChecks = false,
		});

		var election = new MongoDbLeaderElection(
			_client ?? throw new InvalidOperationException("Client not initialised"),
			resourceName,
			mongoOptions,
			electionOptions,
			NullLogger<MongoDbLeaderElection>.Instance);

		_created.Add(election);
		return election;
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Every arm writes into a collection nothing else names, so there is nothing an arm could observe from
	/// its predecessor. The database itself is left in place: dropping it between arms would race the arms
	/// this collection runs alongside.
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
