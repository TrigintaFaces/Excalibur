// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.LeaderElection;
using Excalibur.LeaderElection.MongoDB;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using MongoDB.Driver;

using Tests.Shared.Conformance.LeaderElection;
using Tests.Shared.Fixtures;

using Xunit;

namespace Excalibur.Integration.Tests.LeaderElection;

/// <summary>
/// Declares the shared MongoDB container for the leader-election conformance run.
/// </summary>
/// <remarks>
/// A separate definition from the other MongoDB collections in this assembly, and deliberately so: xUnit
/// serialises every class in one collection against the others in it, so reusing a store collection
/// would make these sixteen arms wait on unrelated suites and vice versa. They share the container
/// fixture type, which is what actually costs time to start.
/// </remarks>
[CollectionDefinition(CollectionName)]
public sealed class MongoDbLeaderElectionTestCollection : ICollectionFixture<MongoDbContainerFixture>
{
	public const string CollectionName = "MongoDB LeaderElection Integration Tests";
}

/// <summary>
/// Runs the shared leader-election conformance kit against the MongoDB provider, so mutual exclusion and
/// takeover are verified on a provider that coordinates across processes.
/// </summary>
/// <remarks>
/// <para>
/// WHY THIS EXISTS. Nothing here says the MongoDB provider was broken; it says nothing would have
/// reported it if a future edit broke it. MongoDB was one of the providers with no arm of this kit
/// enforcing at-most-one-leader or takeover.
/// </para>
/// <para>
/// THE TWO THINGS THAT MAKE THIS NON-VACUOUS, inherited from the sibling derivers' hard-won notes:
/// </para>
/// <para>
/// 1. The competing election MUST share the lock identity — here the resource name, the database and the
/// collection together, since those are what select the one lock DOCUMENT the candidates contend for. Two
/// instances pointed at different resource names never contend and the safety arm passes while testing
/// nothing. All three are single fields read by both factory calls, not values each computes.
/// </para>
/// <para>
/// 2. The competing election MUST have a DIFFERENT instance id. Takeover eligibility is decided against
/// the stored holder, so two candidates sharing an id are one candidate: each would read the other's
/// document as its own self-renew and the arm would pass while contending with itself.
/// </para>
/// <para>
/// WHY ONE CLIENT IS SHARED HERE AND POSTGRES NEEDED TWO SESSIONS, which is a real difference and not a
/// shortcut. Postgres arbitrates with <c>pg_try_advisory_lock</c>, which is SESSION-scoped — two
/// candidates on one connection would hold one lock and could not contend at all, so each must open its
/// own. MongoDB decides takeover SERVER-SIDE inside a single <c>FindOneAndUpdate</c> whose update is an
/// aggregation pipeline, so the arbitration is a property of the document and the server's own clock, not
/// of the connection. Two candidates on one client contend exactly as two processes do, and sharing the
/// client avoids holding sixteen connection pools open against the shared container.
/// </para>
/// <para>
/// The resource name is randomised per test-class instance — xUnit constructs a fresh instance per arm —
/// so arms executing concurrently contend only with their own competitor and cannot collide on one
/// document.
/// </para>
/// <para>
/// The fencing provider is left at its default, so the arms exercise the mint a consumer gets rather than
/// a hand-wired one: the election falls back to the durable per-resource counter in its own collection.
/// </para>
/// </remarks>
[Collection(MongoDbLeaderElectionTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "LeaderElection")]
[Trait("Infrastructure", "MongoDB")]
public sealed class MongoDbLeaderElectionConformanceShould : LeaderElectionConformanceTestBase
{
	private readonly MongoDbContainerFixture _fixture;

	/// <summary>
	/// The lock identity, shared by the primary and every competing election. Randomised per test-class
	/// instance so that arms running concurrently contend only with their own competitor.
	/// </summary>
	private readonly string _resourceName = $"le-conformance-{Guid.NewGuid():N}";

	private readonly string _databaseName = $"le_conf_{Guid.NewGuid():N}";

	private IMongoClient? _client;

	public MongoDbLeaderElectionConformanceShould(MongoDbContainerFixture fixture) => _fixture = fixture;

	protected override Task<ILeaderElection> CreateElectionAsync() => Task.FromResult(CreateElection());

	/// <summary>
	/// A second, independent candidate for the SAME lock document — the competitor the safety arm needs.
	/// </summary>
	protected override Task<ILeaderElection> CreateCompetingElectionAsync() => Task.FromResult(CreateElection());

	protected override async Task CleanupAsync()
	{
		// Each arm gets its own database, so dropping it leaves the container clean rather than
		// accumulating a lock document and a fencing-counter document per arm across the run.
		if (_client is not null)
		{
			await _client.DropDatabaseAsync(_databaseName).ConfigureAwait(false);
			_client = null;
		}
	}

	private ILeaderElection CreateElection()
	{
		// One client per test-class instance, shared by the candidates. See the class remarks: MongoDB
		// arbitrates server-side inside the document, so this is contention-faithful, unlike Postgres
		// where the lock is session-scoped and each candidate must bring its own connection.
		_client ??= new MongoClient(_fixture.ConnectionString);

		var mongoOptions = Options.Create(new MongoDbLeaderElectionOptions
		{
			ConnectionString = _fixture.ConnectionString,
			DatabaseName = _databaseName,
			CollectionName = "leader_elections",
			LeaseDurationSeconds = 5,
			RenewIntervalSeconds = 1,
			TakeoverGraceSeconds = 1,
		});

		var electionOptions = Options.Create(new LeaderElectionOptions
		{
			// Distinct per candidate. Two candidates sharing an id are one candidate to a holder-based
			// takeover decision, and the mutual exclusion arm would pass while contending with itself.
			InstanceId = $"cand-{Guid.NewGuid():N}"[..24],
			LeaseDuration = TimeSpan.FromSeconds(5),
			RenewInterval = TimeSpan.FromSeconds(1),
			RetryInterval = TimeSpan.FromMilliseconds(250),
		});

		return new MongoDbLeaderElection(
			_client,
			_resourceName,
			mongoOptions,
			electionOptions,
			NullLogger<MongoDbLeaderElection>.Instance);
	}
}
