// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Dapper;

using Excalibur.Data;
using Excalibur.Data.MySql;
using Excalibur.Data.Persistence;
using Excalibur.Testing.Conformance;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Excalibur.Integration.Tests.Data.Persistence;

/// <summary>
/// Real-infrastructure conformance tests for <see cref="MySqlPersistenceProvider"/> using the Persistence
/// Provider Conformance Test Kit against a live MySQL container.
/// </summary>
/// <remarks>
/// Never skipped: when Docker is unavailable the fixture fails fast, so a missing container surfaces as a
/// failure rather than a silent pass. The provider is constructed via its consumer-default surface — an
/// <c>IOptions&lt;MySqlProviderOptions&gt;</c> bound to the fixture's connection string, with
/// <c>UseSsl = false</c> because the test container does not present TLS. The provider offers both
/// <c>IPersistenceProviderHealth</c> and <c>IPersistenceProviderTransaction</c>, so every arm asserts;
/// the two capability arms below are the liveness half that keeps it that way.
/// </remarks>
[Collection(MySqlPersistenceProviderTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "MySql")]
[Trait("Pattern", "PROVIDER")]
public sealed class MySqlPersistenceProviderConformanceShould
	: PersistenceProviderConformanceTestKit
{
	private readonly MySqlPersistenceProviderContainerFixture _fixture;

	/// <summary>
	/// Initializes a new instance of the <see cref="MySqlPersistenceProviderConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The MySQL container fixture.</param>
	public MySqlPersistenceProviderConformanceShould(MySqlPersistenceProviderContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	/// <remarks>
	/// An OPEN connection the arm owns, so it can still be observed after the scope is disposed. Built
	/// from the fixture's connection string rather than the provider's own, because the arm must hold the
	/// only remaining handle.
	/// </remarks>
	/// <inheritdoc/>
	protected override string? ExpectedDatabaseType => "MySQL";

	/// <inheritdoc/>
	protected override async Task<System.Data.IDbConnection?> CreateEnlistableConnectionAsync(
		IPersistenceProvider provider)
	{
		var connection = new MySqlConnector.MySqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync(CancellationToken.None).ConfigureAwait(false);
		return connection;
	}

	/// <inheritdoc/>
	/// <remarks>
	/// <para>
	/// A private table per invocation so the arm never collides with another. The first request inserts;
	/// the second targets a table that does not exist, so it fails inside the batch after the insert has
	/// run. The observation reads with a SEPARATE connection: reading on the batch's own connection could
	/// see its uncommitted row and would report a rollback that never happened.
	/// </para>
	/// <para>
	/// <b>ENGINE=InnoDB is stated explicitly and is load-bearing.</b> MySQL is the one provider here whose
	/// storage engine decides whether rollback happens at all — a MyISAM table accepts the same DDL, the
	/// same INSERT and the same failing batch, and simply keeps the row. This arm would then report a
	/// provider that does not roll back, when what it had actually found was a table that cannot. Naming
	/// the engine removes an ambiguity the other two SQL derivers do not have to carry.
	/// </para>
	/// </remarks>
	protected override async Task<(IReadOnlyList<IDataRequest<System.Data.IDbConnection, object>> Requests, Func<Task<bool>> FirstEffectVisibleAsync, Func<Task<bool>> FirstEffectPersistsWhenBatchSucceedsAsync)?>
		CreateBatchAtomicityProbeAsync(ISqlPersistenceProvider provider)
	{
		// Nothing initializes the provider here, and nothing in the kit does either. The provider is used
		// exactly as its own DI extension constructs it, which is the contract this suite holds every
		// provider to.
		var table = "batch_atomicity_" + Guid.NewGuid().ToString("N");
		await CreateInnoDbTableAsync(table).ConfigureAwait(false);

		return (
			[
				new SqlProbeRequest($"INSERT INTO {table} (id) VALUES (1)"),
				new SqlProbeRequest($"INSERT INTO {table}_missing (id) VALUES (2)"),
			],
			() => RowExistsAsync(table),
			async () =>
			{
				// The liveness half: an ALL-VALID batch against a second table must leave its row behind.
				// If it does not, the probe requests are not participating in the batch at all, and the
				// atomicity assertion above would be reading an absence it did not cause.
				var live = "batch_liveness_" + Guid.NewGuid().ToString("N");
				await CreateInnoDbTableAsync(live).ConfigureAwait(false);

				_ = await provider.ExecuteBatchAsync(
					[new SqlProbeRequest($"INSERT INTO {live} (id) VALUES (1)")],
					CancellationToken.None).ConfigureAwait(false);

				return await RowExistsAsync(live).ConfigureAwait(false);
			});
	}

	/// <inheritdoc/>
	/// <remarks>
	/// A fresh table per call, which is what the kit requires: the commit half and the rollback half each
	/// get their own, so a row committed by the first cannot be mistaken for a failed rollback in the
	/// second. Every request succeeds — this probe is about ENLISTMENT, not failure.
	/// </remarks>
	protected override async Task<(IReadOnlyList<IDataRequest<System.Data.IDbConnection, object>> Requests, Func<Task<bool>> EffectVisibleAsync)?>
		CreateScopedBatchProbeAsync(ISqlPersistenceProvider provider)
	{
		var table = "scoped_batch_" + Guid.NewGuid().ToString("N");
		await CreateInnoDbTableAsync(table).ConfigureAwait(false);

		return (
			[
				new SqlProbeRequest($"INSERT INTO {table} (id) VALUES (1)"),
				new SqlProbeRequest($"INSERT INTO {table} (id) VALUES (2)"),
			],
			() => RowExistsAsync(table));
	}

	/// <summary>Creates a single-column transactional table for a probe.</summary>
	/// <param name="table">A locally generated hex-GUID table name.</param>
	private async Task CreateInnoDbTableAsync(string table)
	{
		await using var setup = new MySqlConnector.MySqlConnection(_fixture.ConnectionString);
		await setup.OpenAsync(CancellationToken.None).ConfigureAwait(false);
#pragma warning disable CA2100 // table name is a locally generated hex GUID; no caller input reaches this
		await using var create = new MySqlConnector.MySqlCommand(
			$"CREATE TABLE {table} (id int primary key) ENGINE=InnoDB", setup);
#pragma warning restore CA2100
		_ = await create.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(false);
	}

	/// <summary>Reads on a SEPARATE connection, so an uncommitted row cannot be mistaken for a committed one.</summary>
	/// <param name="table">The probe table to count.</param>
	/// <returns><see langword="true"/> when at least one row is visible outside the batch's own connection.</returns>
	private async Task<bool> RowExistsAsync(string table)
	{
		await using var read = new MySqlConnector.MySqlConnection(_fixture.ConnectionString);
		await read.OpenAsync(CancellationToken.None).ConfigureAwait(false);
#pragma warning disable CA2100 // table name is a locally generated hex GUID; no caller input reaches this
		await using var count = new MySqlConnector.MySqlCommand($"SELECT COUNT(*) FROM {table}", read);
#pragma warning restore CA2100
		return Convert.ToInt64(
			await count.ExecuteScalarAsync(CancellationToken.None).ConfigureAwait(false),
			System.Globalization.CultureInfo.InvariantCulture) > 0;
	}

	/// <summary>A data request that runs one statement, used only by the batch probes.</summary>
	private sealed class SqlProbeRequest : IDataRequest<System.Data.IDbConnection, object>
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
		public Func<System.Data.IDbConnection, Task<object>> ResolveAsync { get; }
	}

	/// <inheritdoc/>
	protected override string ExpectedProviderType => "SQL";

	/// <inheritdoc/>
	protected override IReadOnlyCollection<Type> RequiredCapabilities =>
		[typeof(IPersistenceProviderHealth), typeof(IPersistenceProviderConnection), typeof(IPersistenceProviderTransaction)];

	/// <inheritdoc/>
	protected override IPersistenceProvider CreateProvider(string providerName)
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"MySQL container must be available - real-infra conformance is never skipped.");

		var options = Options.Create(new MySqlProviderOptions
		{
			Name = providerName,
			ConnectionString = _fixture.ConnectionString,
			UseSsl = false,
		});

		return new MySqlPersistenceProvider(options, NullLogger<MySqlPersistenceProvider>.Instance);
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
	[Fact] public Task IsAvailable_AfterDispose_ShouldBeFalse_Test() => IsAvailable_AfterDispose_ShouldBeFalse();
	[Fact] public void Provider_ShouldOfferRequiredCapabilities_Test() => Provider_ShouldOfferRequiredCapabilities();
	[Fact] public void Provider_ShouldImplementIDisposable_Test() => Provider_ShouldImplementIDisposable();
	[Fact] public void Provider_ShouldImplementIAsyncDisposable_Test() => Provider_ShouldImplementIAsyncDisposable();
	[Fact] public Task ExecuteBatchAsync_WhenARequestFails_ShouldLeaveNothingCommitted_Test() => ExecuteBatchAsync_WhenARequestFails_ShouldLeaveNothingCommitted();
	[Fact] public void SqlProvider_ShouldReportItsDatabaseType_Test() => SqlProvider_ShouldReportItsDatabaseType();
	[Fact] public Task SqlProvider_ValidateRequest_ShouldAcceptAValidRequestAndRejectAnInvalidOne_Test() => SqlProvider_ValidateRequest_ShouldAcceptAValidRequestAndRejectAnInvalidOne();
	[Fact] public Task ExecuteBatchInTransactionAsync_ShouldEnlistInTheCallersScope_Test() => ExecuteBatchInTransactionAsync_ShouldEnlistInTheCallersScope();
	[Fact] public Task TransactionScope_DisposedSynchronously_ShouldReleaseEnlistedConnections_Test() => TransactionScope_DisposedSynchronously_ShouldReleaseEnlistedConnections();
	[Fact] public Task ExecuteBatchAsync_CloudNative_WhenARequestFails_ShouldLeaveNothingCommitted_Test() => ExecuteBatchAsync_CloudNative_WhenARequestFails_ShouldLeaveNothingCommitted();
	[Fact] public Task ConformanceSuite_ShouldWireEveryArm_Test() => ConformanceSuite_ShouldWireEveryArm();
	[Fact] public void ConformanceSuite_ShouldDeclareEveryCapabilityTheProviderOffers_Test() => ConformanceSuite_ShouldDeclareEveryCapabilityTheProviderOffers();
}
