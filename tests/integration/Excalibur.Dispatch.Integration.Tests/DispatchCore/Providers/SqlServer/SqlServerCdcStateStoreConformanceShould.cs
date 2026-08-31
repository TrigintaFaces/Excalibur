// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Excalibur.Cdc.SqlServer;
using Excalibur.Dispatch;

using Microsoft.Data.SqlClient;

using Shouldly;

using Tests.Shared.Fixtures;

using MsOptions = Microsoft.Extensions.Options.Options;

#pragma warning disable CA1812 // Instantiated by the xUnit test runner.

using Excalibur.Testing.Conformance;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Providers.SqlServer;

/// <summary>
/// slurug (j75aiz Tests) — per-provider real-store durability conformance for the SQL Server
/// <see cref="ICdcStateStore"/> implementation (<see cref="CdcStateStore"/>), driven by the shared
/// <see cref="CdcProviderConformanceTestKit"/> against a real SQL Server container.
/// </summary>
/// <remarks>
/// <para>
/// SQL Server was the sole CDC state-store not conforming to <see cref="ICdcStateStore"/> until j75aiz
/// added the explicit-interface adapters; its 5 peers (Postgres/Cosmos/Mongo/Dynamo/Firestore) already
/// had real-store conformance coverage while SQL Server had only in-memory/unit tests. This deriver runs
/// the full contract (save/get/overwrite/multi-consumer/resume/GetAll/validity) through the real MERGE
/// upsert + SELECT against a live database — a checkpoint that survives the store, not a mock
/// (per <c>verify-against-real-infra-not-mock</c>). Author≠impl: authored by TestsDeveloper, not the
/// j75aiz implementer.
/// </para>
/// <para>
/// Each instance uses a unique state table so runs are isolated and the empty-store cases hold. The
/// store does not create its own table, so the deriver provisions the schema/table to match the SQL the
/// store issues against <see cref="SqlServerCdcStateStoreOptions.QualifiedTableName"/>. A live container
/// is a hard requirement (never skipped).
/// </para>
/// </remarks>
[Collection(ContainerCollections.SqlServer)]
[Trait("Category", "Integration")]
[Trait("Component", "Cdc")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerCdcStateStoreConformanceShould : CdcProviderConformanceTestKit
{
	private const string SchemaName = "Cdc";
	private readonly SqlServerContainerFixture _fixture;
	private readonly string _tableName = $"CdcState_{Guid.NewGuid():N}";

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerCdcStateStoreConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The shared SQL Server container fixture.</param>
	public SqlServerCdcStateStoreConformanceShould(SqlServerContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	protected override async Task<ICdcStateStore> CreateStateStoreAsync()
	{
		_fixture.ConnectionString.ShouldNotBeNullOrWhiteSpace(
			"Docker/SQL Server must be available — the CDC state-store durability conformance is a real-infra lock and must never be skipped.");

		await EnsureStateTableAsync().ConfigureAwait(false);

		var options = MsOptions.Create(new SqlServerCdcStateStoreOptions { SchemaName = SchemaName, TableName = _tableName });
		// A plain connection string, deliberately: the store opens one connection per operation, so the
		// concurrent-save conformance case does not need multiple active result sets on a shared one. If
		// the store goes back to holding a single connection, this arm fails rather than being propped up.
		var connectionString = _fixture.ConnectionString;
		ICdcStateStore store = new CdcStateStore(() => new SqlConnection(connectionString), options);
		return store;
	}

	/// <inheritdoc/>
	protected override ChangePosition CreateTestPosition(int index)
	{
		// A strictly-increasing SQL Server LSN encoded big-endian into a 10-byte value (index 0 must still
		// be valid: an all-zero LSN maps to ChangePosition.Empty, so offset by 1). Round-trip through the
		// provider's CdcPosition so the persisted byte[] format matches what the store reads back.
		var lsn = new byte[10];
		var value = (ulong)((index + 1) * 1000);
		for (var i = 0; i < 8; i++)
		{
			lsn[9 - i] = (byte)(value >> (8 * i));
		}

		return new CdcPosition(lsn, seqVal: null).ToChangePosition();
	}

	/// <inheritdoc/>
	protected override Task CleanupAsync() =>
		// Each instance uses a unique table name, so state is naturally isolated; container teardown
		// reclaims the tables. No per-test cleanup required.
		Task.CompletedTask;

	private async Task EnsureStateTableAsync()
	{
		await using var connection = new SqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync().ConfigureAwait(false);

		_ = await connection.ExecuteAsync(
			$"IF SCHEMA_ID('{SchemaName}') IS NULL EXEC('CREATE SCHEMA [{SchemaName}]');").ConfigureAwait(false);

		// Columns + PK mirror the store's MERGE/SELECT against QualifiedTableName ([Cdc].[<table>]).
		_ = await connection.ExecuteAsync($"""
			IF OBJECT_ID('[{SchemaName}].[{_tableName}]', 'U') IS NULL
			CREATE TABLE [{SchemaName}].[{_tableName}] (
			    MessageId                    BIGINT IDENTITY(1,1) NOT NULL,
			    DatabaseConnectionIdentifier NVARCHAR(255) NOT NULL,
			    DatabaseName                 NVARCHAR(255) NOT NULL,
			    TableName                    NVARCHAR(255) NOT NULL,
			    LastProcessedLsn             VARBINARY(16) NOT NULL,
			    LastProcessedSequenceValue   VARBINARY(16) NULL,
			    LastCommitTime               DATETIME2     NULL,
			    FencingToken                 BIGINT        NULL,
			    ProcessedAt                  DATETIME2     NOT NULL,
			    CONSTRAINT [PK_{_tableName}] PRIMARY KEY
			        (DatabaseConnectionIdentifier, DatabaseName, TableName)
			);
			""").ConfigureAwait(false);
	}

	// ---------------------------------------------------------------------------------------------
	// Conformance arm wiring.
	//
	// The published kit ships without test-framework attributes so a consumer is not forced onto our
	// runner. Discovery is this suite's job: one attributed member per arm. An arm nobody wires never
	// executes, and an arm that never executes cannot fail -- in the results it is indistinguishable
	// from one that passed.
	// ---------------------------------------------------------------------------------------------

	[Fact] public Task SaveAndGetPosition_RoundTrips_Test() => SaveAndGetPosition_RoundTrips();
	[Fact] public Task GetPosition_NoCheckpoint_ReturnsNull_Test() => GetPosition_NoCheckpoint_ReturnsNull();
	[Fact] public Task SavePosition_MultipleConsumers_Independent_Test() => SavePosition_MultipleConsumers_Independent();
	[Fact] public Task SavePosition_Overwrites_PreviousCheckpoint_Test() => SavePosition_Overwrites_PreviousCheckpoint();
	[Fact] public Task SavePosition_PreservesPositionValidity_Test() => SavePosition_PreservesPositionValidity();
	[Fact] public Task Resume_FromSavedCheckpoint_ReturnsCorrectPosition_Test() => Resume_FromSavedCheckpoint_ReturnsCorrectPosition();
	[Fact] public Task Resume_AfterDelete_ReturnsNull_Test() => Resume_AfterDelete_ReturnsNull();
	[Fact] public Task DeletePosition_ExistingCheckpoint_ReturnsTrue_Test() => DeletePosition_ExistingCheckpoint_ReturnsTrue();
	[Fact] public Task DeletePosition_NonExistentCheckpoint_ReturnsFalse_Test() => DeletePosition_NonExistentCheckpoint_ReturnsFalse();
	[Fact] public Task DeletePosition_DoesNotAffectOtherConsumers_Test() => DeletePosition_DoesNotAffectOtherConsumers();
	[Fact] public Task GetAllPositions_ReturnsAllConsumerCheckpoints_Test() => GetAllPositions_ReturnsAllConsumerCheckpoints();
	[Fact] public Task GetAllPositions_EmptyStore_ReturnsEmpty_Test() => GetAllPositions_EmptyStore_ReturnsEmpty();
	[Fact] public Task ConcurrentSavePosition_AllSucceed_Test() => ConcurrentSavePosition_AllSucceed();
	[Fact] public Task ConcurrentSavePosition_SameConsumer_LastWriteWins_Test() => ConcurrentSavePosition_SameConsumer_LastWriteWins();
	[Fact] public Task ConformanceSuite_ShouldWireEveryArm_Test() => ConformanceSuite_ShouldWireEveryArm();

}
