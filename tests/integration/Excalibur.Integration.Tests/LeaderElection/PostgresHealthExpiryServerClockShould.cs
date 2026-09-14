// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.LeaderElection;
using Excalibur.LeaderElection.Postgres;

using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Shouldly;

using Tests.Shared.Fixtures;

using Xunit;

namespace Excalibur.Integration.Tests.LeaderElection;

/// <summary>
/// Author≠impl real-infra lock for qyw7v2: <see cref="PostgresHealthBasedLeaderElection.GetCandidateHealthAsync"/>
/// decides candidate-health expiry entirely on the Postgres SERVER clock, so a local process clock cannot
/// influence which peers are considered live.
/// </summary>
/// <remarks>
/// <para>
/// The pre-fix code computed <c>expirationThreshold = _timeProvider.GetUtcNow().AddSeconds(-HealthExpirationSeconds)</c>
/// on the LOCAL clock, then filtered rows written by other nodes against it. Local-vs-remote wall-clock skew
/// therefore decided which candidates were considered live: a node whose clock ran fast pruned healthy peers; one
/// running slow kept dead ones qualified.
/// </para>
/// <para>
/// <b>SAFETY is proven STRUCTURALLY, not by injecting a skewed clock.</b> Unlike the sibling MongoDB fix (which
/// still accepts a <c>TimeProvider</c> for other purposes and proves skew-immunity by injecting a skewed one), this
/// class's public constructor accepts NO <c>TimeProvider</c>, <c>DateTimeOffset</c> or <c>DateTime</c> parameter at
/// all — there is no seam through which a caller could hand it a skewed clock for this decision. That is a
/// stronger guarantee than "skewing it doesn't change the answer": the type cannot express a local-clock input in
/// the first place. <see cref="ConstructorAcceptsNoLocalClockInput"/> RED-detects a regression that reintroduces
/// one.
/// </para>
/// <para>
/// <b>LIVENESS is proven against real Postgres</b> (verify-against-real-infra-not-mock): a row whose
/// <c>last_updated</c> is backdated past <c>HealthExpirationSeconds</c> via a direct SQL <c>UPDATE ... last_updated
/// = NOW() - INTERVAL</c> (computed by the SERVER, not this process) is excluded; a freshly-written row is
/// included. Both computations run through the SAME server clock that decided the WHERE predicate, so the arm
/// proves the expiry mechanism actually prunes on elapsed server time rather than merely being safe by omission.
/// <see cref="ContainerFixtureBase.DockerAvailable"/> makes this NON-SKIPPED.
/// </para>
/// <para>
/// <b>RED-on-pre-fix:</b> restoring the client-clock predicate
/// (<c>WHERE last_updated >= @LocalThreshold</c> with a C#-computed <c>@LocalThreshold</c>) reintroduces a
/// <c>TimeProvider</c>-shaped constructor parameter, which fails <see cref="ConstructorAcceptsNoLocalClockInput"/>
/// immediately — the structural arm is what goes RED, since the whole point of the fix is that a live parameter is
/// the defect.
/// </para>
/// </remarks>
[Collection(PostgresLeaderElectionTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "LeaderElection")]
[Trait("Infrastructure", "Postgres")]
public sealed class PostgresHealthExpiryServerClockShould : IAsyncLifetime
{
	private const int HealthExpirationSeconds = 30;

	private readonly PostgresContainerFixture _fixture;
	private ServiceProvider? _provider;
	private IHealthBasedLeaderElection? _election;
	private string _schemaName = "public";
	private string _tableName = "leader_election_health";

	public PostgresHealthExpiryServerClockShould(PostgresContainerFixture fixture) => _fixture = fixture;

	public async ValueTask InitializeAsync()
	{
		if (!_fixture.DockerAvailable)
		{
			return;
		}

		_tableName = $"leader_election_health_{Guid.NewGuid():N}";

		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddPostgresHealthBasedLeaderElection(
			pg =>
			{
				pg.ConnectionString = _fixture.ConnectionString;
				pg.LockKey = Random.Shared.NextInt64();
			},
			health =>
			{
				health.AutoCreateTable = true;
				health.SchemaName = _schemaName;
				health.TableName = _tableName;
				health.HealthExpirationSeconds = HealthExpirationSeconds;
			},
			election =>
			{
				election.InstanceId = $"candidate-{Guid.NewGuid():N}";
				election.EnableHealthChecks = false;
			});

		_provider = services.BuildServiceProvider();

		// Resolve IHealthBasedLeaderElection directly, NOT ILeaderElection: the DI registration wraps the
		// unkeyed ILeaderElection in TelemetryLeaderElection (a decorator implementing only ILeaderElection),
		// while IHealthBasedLeaderElection is registered straight at the concrete PostgresHealthBasedLeaderElection
		// singleton (PostgresHealthBasedLeaderElectionExtensions.cs) -- this is the unwrapped instance whose
		// UpdateHealthAsync/GetCandidateHealthAsync this lock needs, and it is itself the public contract
		// consumers use, so no internal cast is needed.
		_election = _provider.GetRequiredService<IHealthBasedLeaderElection>();
	}

	public async ValueTask DisposeAsync()
	{
		if (_election is not null)
		{
			await _election.DisposeAsync().ConfigureAwait(false);
		}

		if (_provider is not null)
		{
			await _provider.DisposeAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// SAFETY (structural). The public constructor has no local-clock input, so no caller can construct this type
	/// with a skewed clock for the health-expiry decision -- the class the sibling MongoDB fix needed a runtime
	/// skew injection to prove, this one proves by the absence of the parameter that would carry it.
	/// </summary>
	[Fact]
	public void ConstructorAcceptsNoLocalClockInput()
	{
		var localClockTypeNames = new[] { "TimeProvider", "DateTimeOffset", "DateTime" };

		var publicConstructors = typeof(PostgresHealthBasedLeaderElection).GetConstructors();
		publicConstructors.ShouldNotBeEmpty("the type must be constructible for the query to matter at all");

		foreach (var ctor in publicConstructors)
		{
			foreach (var parameter in ctor.GetParameters())
			{
				localClockTypeNames.ShouldNotContain(
					parameter.ParameterType.Name,
					$"constructor parameter '{parameter.Name}' of type '{parameter.ParameterType.Name}' reintroduces a " +
					"local-clock input into the health-expiry decision -- the qyw7v2 fix's safety property depends on no " +
					"such parameter existing.");
			}
		}
	}

	/// <summary>
	/// LIVENESS. A candidate whose health row is genuinely stale by the SERVER's own clock is pruned -- proving the
	/// expiry mechanism actively works, not merely that it cannot be fooled by a local clock.
	/// </summary>
	[Fact]
	[SuppressMessage("Security", "CA2100", Justification = "Schema/table names are test-fixture-generated GUIDs, not user input; values are parameterized.")]
	public async Task AGenuinelyStaleCandidate_ByTheServerClock_IsEventuallyPruned()
	{
		_fixture.DockerAvailable.ShouldBeTrue("this lock must never soft-skip -- see verify-against-real-infra-not-mock.md");

		await _election!.StartAsync(CancellationToken.None).ConfigureAwait(false);
		await _election.UpdateHealthAsync(true, null, CancellationToken.None).ConfigureAwait(false);

		// Confirm it is initially visible (fresh row) before backdating it.
		var before = await _election.GetCandidateHealthAsync(CancellationToken.None).ConfigureAwait(false);
		before.ShouldContain(c => c.CandidateId == _election.CandidateId);

		// Backdate last_updated using the SERVER's own clock (NOW() - INTERVAL), not a C#-computed timestamp --
		// this is what a peer's health row looks like after it has genuinely gone silent for longer than the
		// expiration window, as measured by the one clock the fixed query trusts.
		await using (var connection = new NpgsqlConnection(_fixture.ConnectionString))
		{
			await connection.OpenAsync(CancellationToken.None).ConfigureAwait(false);
			await using var command = new NpgsqlCommand(
				$"UPDATE \"{_schemaName}\".\"{_tableName}\" SET last_updated = NOW() - make_interval(secs => @Seconds) WHERE candidate_id = @CandidateId",
				connection);
			command.Parameters.AddWithValue("Seconds", (double)(HealthExpirationSeconds + 10));
			command.Parameters.AddWithValue("CandidateId", _election.CandidateId);
			_ = await command.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(false);
		}

		var after = await _election.GetCandidateHealthAsync(CancellationToken.None).ConfigureAwait(false);
		after.ShouldNotContain(c => c.CandidateId == _election.CandidateId);
	}

	/// <summary>
	/// LIVENESS. A candidate whose health row is fresh by the server clock is still reported -- the paired
	/// liveness half so the stale-pruning arm above cannot be satisfied by a query that excludes everyone.
	/// </summary>
	[Fact]
	public async Task AFreshCandidate_ByTheServerClock_IsReported()
	{
		_fixture.DockerAvailable.ShouldBeTrue("this lock must never soft-skip -- see verify-against-real-infra-not-mock.md");

		await _election!.StartAsync(CancellationToken.None).ConfigureAwait(false);
		await _election.UpdateHealthAsync(true, null, CancellationToken.None).ConfigureAwait(false);

		var result = await _election.GetCandidateHealthAsync(CancellationToken.None).ConfigureAwait(false);

		result.ShouldContain(c => c.CandidateId == _election.CandidateId && c.IsHealthy);
	}
}
