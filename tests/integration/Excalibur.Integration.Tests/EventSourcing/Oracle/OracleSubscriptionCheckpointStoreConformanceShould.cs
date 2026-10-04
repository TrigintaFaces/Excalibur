// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing.Subscriptions;
using Excalibur.Integration.Tests.Data.EventStore;
using Excalibur.Testing.Conformance;

using Microsoft.Extensions.DependencyInjection;

using Oracle.ManagedDataAccess.Client;

namespace Excalibur.Integration.Tests.EventSourcing.Oracle;

/// <summary>
/// The Oracle subscription checkpoint store against the conformance contract, on a real engine.
/// </summary>
/// <remarks>
/// <para>
/// Real infrastructure, never skipped: the property under test is a compare-and-set decided by the
/// database, and a mocked connection returns whatever it was told.
/// </para>
/// <para>
/// The dialect differs from the other providers and is proved separately rather than inferred. Oracle
/// creates a first checkpoint with a plain INSERT refused by the primary key, and reports that refusal
/// as ORA-00001 rather than as an affected-row count — a mechanism the SQL Server and PostgreSQL suites
/// do not exercise.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Database", "Oracle")]
[Trait("Component", "EventStore")]
[Trait("Pattern", "Conformance")]
public sealed class OracleSubscriptionCheckpointStoreConformanceShould
	: SubscriptionCheckpointStoreConformanceTestKit,
		IClassFixture<OracleEventStoreContainerFixture>,
		IAsyncLifetime
{
	private const string CheckpointTable = "SUBSCRIPTIONCHECKPOINTS";

	private readonly OracleEventStoreContainerFixture _fixture;

	public OracleSubscriptionCheckpointStoreConformanceShould(OracleEventStoreContainerFixture fixture)
		=> _fixture = fixture;

	public async ValueTask InitializeAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"The checkpoint contract is a compare-and-set decided by the database, so it is verified "
			+ "against a real Oracle. This suite must never be skipped.");

		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await EnsureCheckpointTableAsync().ConfigureAwait(false);
	}

	public ValueTask DisposeAsync() => ValueTask.CompletedTask;

	/// <summary>
	/// Resolves from a FRESH provider each call, so every read-back in the kit crosses an instance
	/// boundary and an implementation holding state in a field cannot pass.
	/// </summary>
	protected override ISubscriptionCheckpointStore CreateStore()
	{
		var connectionString = _fixture.ConnectionString;

		var services = new ServiceCollection();
		_ = services.AddOracleSubscriptionCheckpointStore(
			() => new OracleConnection(connectionString), _fixture.Schema, CheckpointTable);

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
	/// Asserts this suite wraps EVERY arm the kit declares, so an arm added later cannot silently never
	/// run here. An unwired arm is indistinguishable from a passing one.
	/// </summary>
	[Fact]
	public Task ConformanceSuite_ShouldWireEveryArm_Test() => ConformanceSuite_ShouldWireEveryArm();

	private async Task EnsureCheckpointTableAsync()
	{
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		var sql =
			"DECLARE n NUMBER; BEGIN "
			+ $"SELECT COUNT(*) INTO n FROM USER_TABLES WHERE TABLE_NAME = '{CheckpointTable}'; "
			+ "IF n = 0 THEN EXECUTE IMMEDIATE '"
			+ $"CREATE TABLE {CheckpointTable} ("
			+ "SUBSCRIPTIONNAME VARCHAR2(255) NOT NULL, "
			+ "POSITION NUMBER(19) NOT NULL, "
			+ "UPDATEDAT TIMESTAMP WITH TIME ZONE DEFAULT SYSTIMESTAMP NOT NULL, "
			+ $"CONSTRAINT PK_{CheckpointTable} PRIMARY KEY (SUBSCRIPTIONNAME))'; "
			+ "END IF; END;";

#pragma warning disable CA2100 // Table name is a test-controlled constant
		await using var command = connection.CreateCommand();
		command.CommandText = sql;
#pragma warning restore CA2100
		_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
	}
}
