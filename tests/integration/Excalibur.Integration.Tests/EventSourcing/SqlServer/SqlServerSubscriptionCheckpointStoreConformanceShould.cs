// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing.Subscriptions;
using Excalibur.Integration.Tests.Data.EventStore;
using Excalibur.Testing.Conformance;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Integration.Tests.EventSourcing.SqlServer;

/// <summary>
/// The SQL Server subscription checkpoint store against the conformance contract, on a real engine.
/// </summary>
/// <remarks>
/// <para>
/// <b>Real infrastructure, never skipped.</b> The property under test is a compare-and-set decided by
/// the database — a mocked connection returns whatever it was told and would satisfy every arm here
/// while proving nothing about which concurrent advance actually wins.
/// </para>
/// <para>
/// Each arm resolves the store through DI rather than constructing it, so the registration extension is
/// exercised too. A store that works but is not reachable through <c>AddSqlServerSubscriptionCheckpointStore</c>
/// would leave every consumer on the in-memory default without knowing it.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Database", "SqlServer")]
[Trait("Component", "EventStore")]
[Trait("Pattern", "Conformance")]
public sealed class SqlServerSubscriptionCheckpointStoreConformanceShould
	: SubscriptionCheckpointStoreConformanceTestKit,
		IClassFixture<SqlServerEventStoreContainerFixture>,
		IAsyncLifetime
{
	private const string CheckpointTable = "SubscriptionCheckpoints";

	private readonly SqlServerEventStoreContainerFixture _fixture;

	public SqlServerSubscriptionCheckpointStoreConformanceShould(SqlServerEventStoreContainerFixture fixture)
		=> _fixture = fixture;

	public async ValueTask InitializeAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"The checkpoint contract is a compare-and-set decided by the database, so it is verified "
			+ "against a real SQL Server. This suite must never be skipped.");

		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await EnsureCheckpointTableAsync().ConfigureAwait(false);
	}

	public ValueTask DisposeAsync() => ValueTask.CompletedTask;

	/// <summary>
	/// Resolves a store from a FRESH provider each call, so every read-back in the kit crosses an
	/// instance boundary and an implementation holding state in a field cannot pass.
	/// </summary>
	protected override ISubscriptionCheckpointStore CreateStore()
	{
		var connectionString = _fixture.ConnectionString;

		var services = new ServiceCollection();
		_ = services.AddSqlServerSubscriptionCheckpointStore(
			() => new SqlConnection(connectionString),
			_fixture.SchemaName,
			CheckpointTable);

		return services.BuildServiceProvider().GetRequiredService<ISubscriptionCheckpointStore>();
	}

	[Fact]
	public Task Advance_WithInvalidPositions_ShouldThrowAndChangeNothing_Test() =>
		Advance_WithInvalidPositions_ShouldThrowAndChangeNothing();

	[Fact]
	public Task Advance_WithEqualPositions_ShouldCompareWithoutClaimingOwnership_Test() =>
		Advance_WithEqualPositions_ShouldCompareWithoutClaimingOwnership();

	[Fact]
	public Task GetCheckpoint_ForUnknownSubscription_ShouldReturnNull_Test() =>
		GetCheckpoint_ForUnknownSubscription_ShouldReturnNull();

	[Fact]
	public Task Advance_FromNoCheckpoint_ShouldBeAcceptedAndPersist_Test() =>
		Advance_FromNoCheckpoint_ShouldBeAcceptedAndPersist();

	[Fact]
	public Task Advance_WithMatchingExpectedPosition_ShouldBeAccepted_Test() =>
		Advance_WithMatchingExpectedPosition_ShouldBeAccepted();

	[Fact]
	public Task Advance_WithStaleExpectedPosition_ShouldBeSupersededAndChangeNothing_Test() =>
		Advance_WithStaleExpectedPosition_ShouldBeSupersededAndChangeNothing();

	[Fact]
	public Task Advance_ExpectingNoCheckpoint_WhenOneExists_ShouldBeSuperseded_Test() =>
		Advance_ExpectingNoCheckpoint_WhenOneExists_ShouldBeSuperseded();

	[Fact]
	public Task ConcurrentAdvances_FromTheSamePosition_ShouldAcceptExactlyOne_Test() =>
		ConcurrentAdvances_FromTheSamePosition_ShouldAcceptExactlyOne();

	[Fact]
	public Task Enumerate_ShouldReportStoredCheckpoints_Test() =>
		Enumerate_ShouldReportStoredCheckpoints();

	/// <summary>
	/// Asserts this suite wraps EVERY arm the kit declares, so an arm added to the kit later cannot
	/// silently never run here. An unwired arm is indistinguishable from a passing one.
	/// </summary>
	[Fact]
	public Task ConformanceSuite_ShouldWireEveryArm_Test() => ConformanceSuite_ShouldWireEveryArm();

	/// <summary>
	/// Creates the checkpoint table in the shape the shipped schema script produces.
	/// </summary>
	private async Task EnsureCheckpointTableAsync()
	{
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		var sql = $"""
			IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = '{CheckpointTable}'
				AND schema_id = SCHEMA_ID('{_fixture.SchemaName}'))
			CREATE TABLE [{_fixture.SchemaName}].[{CheckpointTable}] (
				[SubscriptionName] NVARCHAR(255) COLLATE Latin1_General_BIN2 NOT NULL,
				[Position]         BIGINT NOT NULL,
				[UpdatedAt]        DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
				CONSTRAINT [PK_{CheckpointTable}] PRIMARY KEY CLUSTERED ([SubscriptionName])
			);
			""";

#pragma warning disable CA2100 // Schema and table are test-controlled constants
		await using var command = new SqlCommand(sql, connection);
#pragma warning restore CA2100
		_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
	}
}
