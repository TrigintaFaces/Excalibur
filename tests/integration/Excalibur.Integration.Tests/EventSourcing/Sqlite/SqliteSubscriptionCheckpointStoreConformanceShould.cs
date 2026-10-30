// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing.Subscriptions;
using Excalibur.Integration.Tests.Data.EventStore;
using Excalibur.Testing.Conformance;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Integration.Tests.EventSourcing.Sqlite;

/// <summary>
/// The SQLite subscription checkpoint store against the conformance contract, on a real engine.
/// </summary>
/// <remarks>
/// SQLite is embedded, so this is real infrastructure with no container and is never skipped. The
/// contract must hold identically here or a consumer's retry logic is correct on some providers and
/// wrong on others.
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Database", "Sqlite")]
[Trait("Component", "EventStore")]
[Trait("Pattern", "Conformance")]
public sealed class SqliteSubscriptionCheckpointStoreConformanceShould
	: SubscriptionCheckpointStoreConformanceTestKit,
		IClassFixture<SqliteEventStoreFixture>,
		IAsyncLifetime
{
    [Fact]
    public Task GenerationPointer_ShouldGateSwitchAndProtectReferencedLifetime_Test() => GenerationPointer_ShouldGateSwitchAndProtectReferencedLifetime();

    [Fact]
    public Task GenerationPointer_ConcurrentRetirement_ShouldNeverLeaveDanglingReference_Test() => GenerationPointer_ConcurrentRetirement_ShouldNeverLeaveDanglingReference();



    [Fact]
    public async Task ReplaceLegacyCheckpointTablesWithoutChangingLegacyRows()
    {
        // A fresh name is deliberate: re-running CREATE against the old shape is not an upgrade.
        var legacy = "legacy_" + Guid.NewGuid().ToString("N")[..12];
        var fresh = "replacement_" + Guid.NewGuid().ToString("N")[..12];
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection(_fixture.ConnectionString);
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
        await ExecuteAsync($"CREATE TABLE {legacy} (SubscriptionName TEXT NOT NULL PRIMARY KEY, Position INTEGER NOT NULL)");
        await ExecuteAsync($"CREATE TABLE {legacy}Definitions (SubscriptionName TEXT NOT NULL PRIMARY KEY, DefinitionHash TEXT NOT NULL)");
        await ExecuteAsync($"INSERT INTO {legacy} (SubscriptionName, Position) VALUES ('upgrade', 42)");
        await ExecuteAsync($"INSERT INTO {legacy}Definitions (SubscriptionName, DefinitionHash) VALUES ('upgrade', '{new string('A', 64)}')");
        // SQLite provisions the replacement through the public store on first use.
        var services = new ServiceCollection();
        _ = services.AddSqliteSubscriptionCheckpointStore(_fixture.ConnectionString, fresh);
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
        (await ReadLegacyAsync($"SELECT COUNT(*) FROM {legacy}Definitions WHERE SubscriptionName = 'upgrade' AND DefinitionHash = '{new string('A', 64)}'")).ShouldBe(1);
        // Rerunning initialization joins the existing binding instead of resetting its cursor.
        var rebound = (await store.BindSubscriptionAsync("upgrade",
            [new SubscriptionMember("replacement-v1", new string('B', 64), "fresh-destination")], CancellationToken.None)).Members.Single();
        rebound.BindingId.ShouldBe(member.BindingId);
        rebound.Position.ShouldBe(42);
    }

	private const string CheckpointTable = "SubscriptionCheckpoints";

	private readonly SqliteEventStoreFixture _fixture;

	public SqliteSubscriptionCheckpointStoreConformanceShould(SqliteEventStoreFixture fixture)
		=> _fixture = fixture;

	/// <summary>
	/// Deliberately creates NOTHING.
	/// </summary>
	/// <remarks>
	/// Every other SQLite store in this package creates its own table on first use, and this one must
	/// too. A fixture that provisioned the table by hand would pass identically whether the store
	/// initialized or not, so the suite would be blind to exactly the defect that leaves a consumer with
	/// "no such table" on a path where every sibling simply works.
	/// </remarks>
	public ValueTask InitializeAsync() => ValueTask.CompletedTask;

	public ValueTask DisposeAsync() => ValueTask.CompletedTask;

	/// <summary>
	/// Resolves from a FRESH provider each call, so every read-back in the kit crosses an instance
	/// boundary and an implementation holding state in a field cannot pass.
	/// </summary>
	protected override ISubscriptionCheckpointStore CreateStore()
	{
		var services = new ServiceCollection();
		_ = services.AddSqliteSubscriptionCheckpointStore(_fixture.ConnectionString, CheckpointTable);

		return services.BuildServiceProvider().GetRequiredService<ISubscriptionCheckpointStore>();
	}

    [Fact]
    public async Task PreserveTheBindingAndCursorAcrossStoreInstances()
    {
        var name = "restart-" + Guid.NewGuid().ToString("N");
        var first = CreateStore();
        var member = (await first.BindSubscriptionAsync(name,
            [new SubscriptionMember("A", new string('A', 64), "dest-a")], CancellationToken.None)).Members.Single();
        await first.AdvanceCheckpointAsync(name, "A", member.BindingId, null, 42, CancellationToken.None);
        var restored = (await CreateStore().ReadSubscriptionAsync(name, CancellationToken.None)).Members.Single();
        restored.BindingId.ShouldBe(member.BindingId);
        restored.Position.ShouldBe(42);
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
}
