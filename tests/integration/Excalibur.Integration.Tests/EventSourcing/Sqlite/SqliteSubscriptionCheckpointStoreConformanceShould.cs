// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing.Subscriptions;
using Excalibur.Integration.Tests.Data.EventStore;
using Excalibur.Testing.Conformance;

using Microsoft.Data.Sqlite;
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
	public async Task CreateDefinitionCompanionWithoutChangingAnExistingCheckpoint()
	{
		// Keep this predecessor schema literal independent of the current DDL generator.
		// A unique named database prevents initialization caches from making the test vacuous.
		var connectionString = $"Data Source=checkpoint-upgrade-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
		await using var keeper = new SqliteConnection(connectionString);
		await keeper.OpenAsync().ConfigureAwait(false);
		await using (var create = keeper.CreateCommand())
		{
			create.CommandText = """
				CREATE TABLE SubscriptionCheckpoints (
				    SubscriptionName TEXT NOT NULL COLLATE BINARY PRIMARY KEY,
				    Position INTEGER NOT NULL,
				    UpdatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP);
				INSERT INTO SubscriptionCheckpoints (SubscriptionName, Position)
				VALUES ('existing-subscription', 42);
				""";
			_ = await create.ExecuteNonQueryAsync().ConfigureAwait(false);
		}

		var services = new ServiceCollection();
		_ = services.AddSqliteSubscriptionCheckpointStore(connectionString, CheckpointTable);
		await using var provider = services.BuildServiceProvider();
		var store = provider.GetRequiredService<ISubscriptionCheckpointStore>();
		await store.BindSubscriptionAsync("existing-subscription", new string('A', 64), CancellationToken.None).ConfigureAwait(false);
		(await store.GetCheckpointAsync("existing-subscription", CancellationToken.None).ConfigureAwait(false)).ShouldBe(42);
		await store.BindSubscriptionAsync("existing-subscription", new string('A', 64), CancellationToken.None).ConfigureAwait(false);

		await using var read = keeper.CreateCommand();
		read.CommandText = "SELECT DefinitionHash FROM SubscriptionCheckpointsDefinitions WHERE SubscriptionName = 'existing-subscription';";
		(await read.ExecuteScalarAsync().ConfigureAwait(false)).ShouldBe(new string('A', 64));
		(await store.GetCheckpointAsync("existing-subscription", CancellationToken.None).ConfigureAwait(false)).ShouldBe(42);
	}

	[Fact]
	public Task Bind_IdenticalDefinition_ShouldJoinWithoutCreatingCheckpoint_Test() =>
		Bind_IdenticalDefinition_ShouldJoinWithoutCreatingCheckpoint();

	[Fact]
	public Task Bind_DifferentDefinition_ShouldRefuseWithoutChangingCheckpoint_Test() =>
		Bind_DifferentDefinition_ShouldRefuseWithoutChangingCheckpoint();

	[Fact]
	public Task ConcurrentBindings_WithDifferentDefinitions_ShouldAcceptExactlyOne_Test() =>
		ConcurrentBindings_WithDifferentDefinitions_ShouldAcceptExactlyOne();

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
	public Task Enumerate_ShouldReportStoredCheckpoints_Test() =>
		Enumerate_ShouldReportStoredCheckpoints();

	/// <summary>
	/// Asserts this suite wraps EVERY arm the kit declares, so an arm added later cannot silently never
	/// run here. An unwired arm is indistinguishable from a passing one.
	/// </summary>
	[Fact]
	public Task ConformanceSuite_ShouldWireEveryArm_Test() => ConformanceSuite_ShouldWireEveryArm();
}
