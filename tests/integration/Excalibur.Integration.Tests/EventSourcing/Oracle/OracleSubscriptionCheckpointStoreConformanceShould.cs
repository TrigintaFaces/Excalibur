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
    [Fact]
    public Task GenerationPointer_ShouldGateSwitchAndProtectReferencedLifetime_Test() => GenerationPointer_ShouldGateSwitchAndProtectReferencedLifetime();

    [Fact]
    public Task GenerationPointer_ConcurrentRetirement_ShouldNeverLeaveDanglingReference_Test() => GenerationPointer_ConcurrentRetirement_ShouldNeverLeaveDanglingReference();

    [Fact]
    public async Task KeepIndependentReaderRetirementRacesSafeWithAnIndexedChildKey()
    {
        await using var connection = _fixture.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM USER_IND_COLUMNS WHERE TABLE_NAME = :TableName AND COLUMN_NAME = 'BINDINGID' AND COLUMN_POSITION = 1";
        command.Parameters.Add(new OracleParameter("TableName", (CheckpointTable + "READERS").ToUpperInvariant()));
        Convert.ToInt32(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture).ShouldBeGreaterThan(0);
        await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => GenerationPointer_ConcurrentRetirement_ShouldNeverLeaveDanglingReference()));
    }



    [Fact]
    public async Task ReplaceLegacyCheckpointTablesWithoutChangingLegacyRows()
    {
        // A fresh name is deliberate: re-running CREATE against the old shape is not an upgrade.
        var legacy = "LEGACY_" + Guid.NewGuid().ToString("N")[..12];
        var fresh = "replacement_" + Guid.NewGuid().ToString("N")[..12];
        await using var connection = _fixture.CreateConnection();
        await connection.OpenAsync();
#pragma warning disable CA2100 // Identifiers are generated exclusively by this test; no external SQL input.
        async Task ExecuteAsync(string sql)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            _ = await command.ExecuteNonQueryAsync();
        }
        async Task<long> ReadLegacyAsync(string sql)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            return Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
        }
#pragma warning restore CA2100
        await ExecuteAsync($"CREATE TABLE {legacy} (SubscriptionName VARCHAR2(255) NOT NULL PRIMARY KEY, Position NUMBER(19) NOT NULL)");
        await ExecuteAsync($"CREATE TABLE {legacy}DEFINITIONS (SubscriptionName VARCHAR2(255) NOT NULL PRIMARY KEY, DefinitionHash VARCHAR2(255) NOT NULL)");
        await ExecuteAsync($"INSERT INTO {legacy} (SubscriptionName, Position) VALUES ('upgrade', 42)");
        await ExecuteAsync($"INSERT INTO {legacy}DEFINITIONS (SubscriptionName, DefinitionHash) VALUES ('upgrade', '{new string('A', 64)}')");
        foreach (var sql in global::Tests.Shared.Helpers.ShippedSchemaScript.ReadOracleUnits(
            "src/Excalibur/Excalibur.EventSourcing.Oracle/Scripts/003_CreateSubscriptionCheckpointSchema.sql"))
        {
            await ExecuteAsync(sql.Replace("EXCALIBUR.SUBSCRIPTIONCHECKPOINTS", fresh, StringComparison.Ordinal));
        }
        var services = new ServiceCollection();
        _ = services.AddOracleSubscriptionCheckpointStore(() => new OracleConnection(_fixture.ConnectionString), _fixture.Schema, fresh);
        await using var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<ISubscriptionCheckpointStore>();
        var member = (await store.BindSubscriptionAsync("upgrade",
            [new SubscriptionMember("replacement-v1", new string('B', 64), "fresh-destination")], CancellationToken.None)).Members.Single();
        member.Position.ShouldBeNull("a legacy cursor must never be copied into an empty destination");
        var readers = (IProjectionGenerationStore)store;
        (await readers.TrySetGenerationAsync("upgrade", "reader", null, member.ProjectionKey, member.BindingId, 42, CancellationToken.None)).ShouldBeFalse();
        _ = await store.AdvanceCheckpointAsync("upgrade", member.ProjectionKey, member.BindingId, null, 42, CancellationToken.None);
        (await readers.TrySetGenerationAsync("upgrade", "reader", null, member.ProjectionKey, member.BindingId, 42, CancellationToken.None)).ShouldBeTrue();
        (await readers.ReadGenerationAsync("upgrade", "reader", CancellationToken.None)).ShouldNotBeNull().Member.Destination.ShouldBe("fresh-destination");
        (await ReadLegacyAsync($"SELECT Position FROM {legacy} WHERE SubscriptionName = 'upgrade'")).ShouldBe(42);
        (await ReadLegacyAsync($"SELECT COUNT(*) FROM {legacy}DEFINITIONS WHERE SubscriptionName = 'upgrade' AND DefinitionHash = '{new string('A', 64)}'")).ShouldBe(1);
        // Rerunning initialization joins the existing binding instead of resetting its cursor.
        var rebound = (await store.BindSubscriptionAsync("upgrade",
            [new SubscriptionMember("replacement-v1", new string('B', 64), "fresh-destination")], CancellationToken.None)).Members.Single();
        rebound.BindingId.ShouldBe(member.BindingId);
        rebound.Position.ShouldBe(42);
    }

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
			() =>
            {
                var connection = new OracleConnection(connectionString);
                connection.StateChange += (_, change) =>
                {
                    if (change.CurrentState != System.Data.ConnectionState.Open) { return; }
                    using var command = connection.CreateCommand();
                    command.CommandText = "ALTER SESSION SET NLS_COMP=LINGUISTIC";
                    command.ExecuteNonQuery();
                    command.CommandText = "ALTER SESSION SET NLS_SORT=BINARY_CI";
                    command.ExecuteNonQuery();
                };
                return connection;
            }, _fixture.Schema, CheckpointTable);

		return services.BuildServiceProvider().GetRequiredService<ISubscriptionCheckpointStore>();
	}

    [Fact]
    public Task RetiredBindings_ShouldNeverAcknowledgeOrRetireAReplacement_Test() =>
        RetiredBindings_ShouldNeverAcknowledgeOrRetireAReplacement();

	[Fact]
	public Task Bind_IdenticalRoster_ShouldJoinWithoutCreatingCheckpoint_Test() =>
		Bind_IdenticalRoster_ShouldJoinWithoutCreatingCheckpoint();

	[Fact]
	public Task Bind_NewMember_ShouldBeAdditive_AndLeaveSiblingCheckpointsUntouched_Test() =>
		Bind_NewMember_ShouldBeAdditive_AndLeaveSiblingCheckpointsUntouched();

	[Fact]
	public Task Bind_ChangedMemberFingerprint_ShouldRefuseNamingThatProjection_AndLeaveOthersBound_Test() =>
		Bind_ChangedMemberFingerprint_ShouldRefuseNamingThatProjection_AndLeaveOthersBound();

	[Fact]
	public Task Bind_ShouldReturnRosterIncludingMembersTheCallerDidNotSupply_Test() =>
		Bind_ShouldReturnRosterIncludingMembersTheCallerDidNotSupply();

	[Fact]
	public Task ConcurrentBindings_OfOneMember_WithDifferentFingerprints_ShouldAcceptExactlyOne_Test() =>
		ConcurrentBindings_OfOneMember_WithDifferentFingerprints_ShouldAcceptExactlyOne();

	[Fact]
	public Task Release_ShouldRemoveMemberAndItsCheckpoint_Atomically_Test() =>
		Release_ShouldRemoveMemberAndItsCheckpoint_Atomically();

	[Fact]
	public Task Release_AbsentMember_ShouldReturnFalse_AndWriteNothing_Test() =>
		Release_AbsentMember_ShouldReturnFalse_AndWriteNothing();

	[Fact]
	public Task Bind_CaseDistinctProjectionKeys_ShouldRemainIndependent_Test() =>
		Bind_CaseDistinctProjectionKeys_ShouldRemainIndependent();





	[Fact]
	public Task Bind_CaseDistinctNames_ShouldRemainIndependent_Test() =>
		Bind_CaseDistinctNames_ShouldRemainIndependent();

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
	public Task Advance_WhenNameAndKeyTogetherExceedTheNameWidth_ShouldRoundTrip_Test() =>
		Advance_WhenNameAndKeyTogetherExceedTheNameWidth_ShouldRoundTrip();

	[Fact]
	public Task Advance_CaseDistinctProjectionKeys_ShouldRemainDistinctCursors_Test() =>
		Advance_CaseDistinctProjectionKeys_ShouldRemainDistinctCursors();

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


        foreach (var script in global::Tests.Shared.Helpers.ShippedSchemaScript.ReadOracleUnits(
            "src/Excalibur/Excalibur.EventSourcing.Oracle/Scripts/003_CreateSubscriptionCheckpointSchema.sql"))
        {
            await using var command = connection.CreateCommand();
#pragma warning disable CA2100 // Shipped provider SQL; only test-owned schema names are substituted.
            command.CommandText = script.Replace("EXCALIBUR.SUBSCRIPTIONCHECKPOINTS", CheckpointTable, StringComparison.Ordinal);
#pragma warning restore CA2100
            _ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
    }
}
