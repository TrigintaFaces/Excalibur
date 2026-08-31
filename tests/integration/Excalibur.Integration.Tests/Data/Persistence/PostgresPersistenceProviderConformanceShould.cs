// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;
using Dapper;
using Npgsql;
using Excalibur.Data;
using Excalibur.Data.Persistence;
using Excalibur.Data.Postgres.Persistence;
using Excalibur.Integration.Tests.Data;
using Excalibur.Testing.Conformance;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.Data.Persistence;

/// <summary>
/// Real-infrastructure conformance tests for <see cref="PostgresPersistenceProvider"/> using the
/// Persistence Provider Conformance Test Kit against a live Postgres container.
/// </summary>
/// <remarks>
/// <para>
/// Never skipped: when Docker is unavailable the fixture fails fast, so a missing container surfaces as a
/// failure rather than a silent pass. The provider is constructed via its consumer-default surface — an
/// <c>IOptions&lt;PostgresPersistenceOptions&gt;</c> bound to the fixture's connection string.
/// </para>
/// <para>
/// This binds the provider that offers <see cref="IPersistenceProviderHealth"/> and
/// <see cref="IPersistenceProviderTransaction"/> through
/// <see cref="IPersistenceProvider.GetService"/>. That choice is load-bearing rather than incidental: the
/// kit's health, metrics and transaction arms return without asserting when a provider declines those
/// optional capabilities, which is the documented contract but makes "declined" and "verified"
/// indistinguishable in a green report. Declaring both in <c>RequiredCapabilities</c> pins the choice, so a
/// swap to a declining implementation fails loudly instead of quietly emptying eight arms.
/// </para>
/// </remarks>
[Collection(PostgresTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Postgres")]
[Trait("Pattern", "PROVIDER")]
public sealed class PostgresPersistenceProviderConformanceShould : PersistenceProviderConformanceTestKit, IClassFixture<PostgresContainerFixture>
{
	private readonly PostgresContainerFixture _fixture;

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresPersistenceProviderConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The Postgres container fixture.</param>
	public PostgresPersistenceProviderConformanceShould(PostgresContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	protected override string ExpectedProviderType => "SQL";

	/// <inheritdoc/>
	/// <remarks>
	/// One specification for every deriver: the suite configures this name on the provider and
	/// expects the provider to report it back. An expectation set to whatever the provider happens
	/// to return cannot detect a provider that ignores its configured name -- it certifies it.
	/// </remarks>

	/// <inheritdoc/>
	protected override IReadOnlyCollection<Type> RequiredCapabilities =>
		[typeof(IPersistenceProviderHealth), typeof(IPersistenceProviderConnection), typeof(IPersistenceProviderTransaction)];

	/// <inheritdoc/>
	protected override IPersistenceProvider CreateProvider(string providerName)
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Postgres container must be available - real-infra conformance is never skipped.");

		// Binds Excalibur.Data.Postgres.Persistence.PostgresPersistenceProvider -- the type the
		// production registration actually resolves. The root-namespace provider it previously bound
		// was removed as an unreferenced duplicate; this one now offers the health capability, so the
		// arms assert rather than returning early.
		var options = Options.Create(new PostgresPersistenceOptions
		{
			Name = providerName,
			ConnectionString = _fixture.ConnectionString,
		});

		return new PostgresPersistenceProvider(options, NullLogger<PostgresPersistenceProvider>.Instance);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// A private table per invocation so the arm never collides with another. The first request inserts;
	/// the second targets a table that does not exist, so it fails inside the batch after the insert has
	/// run. The observation reads with a SEPARATE connection: reading on the batch's own connection could
	/// see its uncommitted row and would report a rollback that never happened.
	/// </remarks>
	protected override async Task<(IReadOnlyList<IDataRequest<IDbConnection, object>> Requests, Func<Task<bool>> FirstEffectVisibleAsync, Func<Task<bool>> FirstEffectPersistsWhenBatchSucceedsAsync)?>
		CreateBatchAtomicityProbeAsync(ISqlPersistenceProvider provider)
	{
		// This provider refuses to work until InitializeAsync is called. The KIT deliberately never
		// calls it -- a provider that needs an explicit initialize is itself a defect, and two other arms
		// exist to report exactly that. So the initialize happens HERE, in the probe, rather than in the
		// kit: this arm is about transaction semantics, and it must not red for a readiness defect that
		// already has its own arm. One arm, one property.
		await provider.InitializeAsync(
			new PostgresPersistenceOptions { Name = provider.Name, ConnectionString = _fixture.ConnectionString },
			CancellationToken.None).ConfigureAwait(false);

		var table = "batch_atomicity_" + Guid.NewGuid().ToString("N");

		await using (var setup = new NpgsqlConnection(_fixture.ConnectionString))
		{
			await setup.OpenAsync(CancellationToken.None).ConfigureAwait(false);
			#pragma warning disable CA2100 // table name is a locally generated hex GUID; no caller input reaches this
			await using var create = new NpgsqlCommand($"CREATE TABLE {table} (id int primary key)", setup);
			#pragma warning restore CA2100
			_ = await create.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(false);
		}

		return (
			[
				new SqlProbeRequest($"INSERT INTO {table} (id) VALUES (1)"),
				new SqlProbeRequest($"INSERT INTO {table}_missing (id) VALUES (2)"),
			],
			async () =>
			{
				await using var read = new NpgsqlConnection(_fixture.ConnectionString);
				await read.OpenAsync(CancellationToken.None).ConfigureAwait(false);
				#pragma warning disable CA2100 // table name is a locally generated hex GUID; no caller input reaches this
				await using var count = new NpgsqlCommand($"SELECT COUNT(*) FROM {table}", read);
				#pragma warning restore CA2100
				return Convert.ToInt64(await count.ExecuteScalarAsync(CancellationToken.None).ConfigureAwait(false),
					System.Globalization.CultureInfo.InvariantCulture) > 0;
			},
			async () =>
			{
				// The liveness half: an ALL-VALID batch against a second table must leave its row behind.
				// If it does not, the probe requests are not participating in the batch and the atomicity
				// assertion above would be reading an absence it did not cause.
				var live = "batch_liveness_" + Guid.NewGuid().ToString("N");
				await using (var setup = new NpgsqlConnection(_fixture.ConnectionString))
				{
					await setup.OpenAsync(CancellationToken.None).ConfigureAwait(false);
					#pragma warning disable CA2100 // locally generated hex GUID table name
					await using var create = new NpgsqlCommand($"CREATE TABLE {live} (id int primary key)", setup);
					#pragma warning restore CA2100
					_ = await create.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(false);
				}

				_ = await provider.ExecuteBatchAsync(
					[new SqlProbeRequest($"INSERT INTO {live} (id) VALUES (1)")],
					CancellationToken.None).ConfigureAwait(false);

				await using var read2 = new NpgsqlConnection(_fixture.ConnectionString);
				await read2.OpenAsync(CancellationToken.None).ConfigureAwait(false);
				#pragma warning disable CA2100 // locally generated hex GUID table name
				await using var count2 = new NpgsqlCommand($"SELECT COUNT(*) FROM {live}", read2);
				#pragma warning restore CA2100
				return Convert.ToInt64(await count2.ExecuteScalarAsync(CancellationToken.None).ConfigureAwait(false),
					System.Globalization.CultureInfo.InvariantCulture) > 0;
			});
	}

	/// <summary>A data request that runs one statement, used only by the batch-atomicity probe.</summary>
	private sealed class SqlProbeRequest : IDataRequest<IDbConnection, object>
	{
		public SqlProbeRequest(string sql)
		{
			Command = new CommandDefinition(sql);
			ResolveAsync = async connection =>
			{
				using var command = connection.CreateCommand();
				command.CommandText = sql;
				_ = command.ExecuteNonQuery();
				return await Task.FromResult<object>(0).ConfigureAwait(false);
			};
		}

		public string RequestId { get; } = Guid.NewGuid().ToString();
		public string RequestType => nameof(SqlProbeRequest);
		public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
		public string? CorrelationId => null;
		public IDictionary<string, object>? Metadata => null;
		public CommandDefinition Command { get; }
		public DynamicParameters Parameters { get; } = new();
		public Func<IDbConnection, Task<object>> ResolveAsync { get; }
	}

	// -------------------------------------------------------------------------------------------------
	// Conformance arm wiring.
	//
	// The kit ships without test-framework attributes so that a consumer is not forced onto our runner,
	// which means discovery is this suite's job: one member per arm, attributed for xUnit. The kit's
	// ConformanceSuite_ShouldWireEveryArm arm fails if any arm here is missing, so an arm cannot be
	// silently dropped.
	// -------------------------------------------------------------------------------------------------

	[Fact] public void Provider_ShouldHaveNonNullName_Test() => Provider_ShouldHaveNonNullName();
	[Fact] public void Provider_ShouldHaveExpectedName_Test() => Provider_ShouldHaveExpectedName();
	[Fact] public void Provider_NameShouldRoundTripEveryConfiguredName_Test() => Provider_NameShouldRoundTripEveryConfiguredName();
	[Fact] public Task Provider_NameShouldBeStableAcrossLifecycle_Test() => Provider_NameShouldBeStableAcrossLifecycle();
	[Fact] public void Provider_ShouldHaveNonNullProviderType_Test() => Provider_ShouldHaveNonNullProviderType();
	[Fact] public void Provider_ShouldHaveExpectedProviderType_Test() => Provider_ShouldHaveExpectedProviderType();
	[Fact] public void Provider_ShouldHaveNonNullConnectionString_Test() => Provider_ShouldHaveNonNullConnectionString();
	[Fact] public void Provider_ShouldHaveNonNullRetryPolicy_Test() => Provider_ShouldHaveNonNullRetryPolicy();
	[Fact] public void CreateTransactionScope_ShouldReturnNonNullScope_Test() => CreateTransactionScope_ShouldReturnNonNullScope();
	[Fact] public void CreateTransactionScope_WithIsolationLevel_ShouldReturnScope_Test() => CreateTransactionScope_WithIsolationLevel_ShouldReturnScope();
	[Fact] public void CreateTransactionScope_WithTimeout_ShouldReturnScope_Test() => CreateTransactionScope_WithTimeout_ShouldReturnScope();
	[Fact] public Task GetMetricsAsync_ShouldReturnNonNullDictionary_Test() => GetMetricsAsync_ShouldReturnNonNullDictionary();
	[Fact] public Task TestConnectionAsync_ShouldReachTheDatabase_WithoutAnExplicitInitialize_Test() => TestConnectionAsync_ShouldReachTheDatabase_WithoutAnExplicitInitialize();
	[Fact] public Task GetMetricsAsync_ShouldContainProviderKey_Test() => GetMetricsAsync_ShouldContainProviderKey();
	[Fact] public void Dispose_ShouldNotThrow_Test() => Dispose_ShouldNotThrow();
	[Fact] public void Dispose_CalledMultipleTimes_ShouldNotThrow_Test() => Dispose_CalledMultipleTimes_ShouldNotThrow();
	[Fact] public Task DisposeAsync_ShouldNotThrow_Test() => DisposeAsync_ShouldNotThrow();
	[Fact] public void IsAvailable_AfterDispose_ShouldBeFalse_Test() => IsAvailable_AfterDispose_ShouldBeFalse();
	[Fact] public void Provider_ShouldOfferRequiredCapabilities_Test() => Provider_ShouldOfferRequiredCapabilities();
	[Fact] public void Provider_ShouldImplementIDisposable_Test() => Provider_ShouldImplementIDisposable();
	[Fact] public void Provider_ShouldImplementIAsyncDisposable_Test() => Provider_ShouldImplementIAsyncDisposable();
	[Fact] public Task ConformanceSuite_ShouldWireEveryArm_Test() => ConformanceSuite_ShouldWireEveryArm();
	[Fact] public void ConformanceSuite_ShouldDeclareEveryCapabilityTheProviderOffers_Test() => ConformanceSuite_ShouldDeclareEveryCapabilityTheProviderOffers();
	[Fact] public Task ExecuteBatchAsync_WhenARequestFails_ShouldLeaveNothingCommitted_Test() => ExecuteBatchAsync_WhenARequestFails_ShouldLeaveNothingCommitted();
}
