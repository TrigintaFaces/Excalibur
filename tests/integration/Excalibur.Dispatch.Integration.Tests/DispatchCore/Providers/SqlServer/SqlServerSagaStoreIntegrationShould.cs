// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Serialization;

using Excalibur.Saga.SqlServer;
using Excalibur.Testing.Conformance;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Tests.Shared;
using Tests.Shared.Categories;
using Tests.Shared.Fixtures;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Providers.SqlServer;

/// <summary>
/// Integration tests for <see cref="SqlServerSagaStore"/> using TestContainers.
/// Tests real SQL Server database operations for saga state persistence.
/// </summary>
/// <remarks>
/// <para>
/// Sprint 176 - Provider Testing Epic Phase 2.
/// bd-huirr: SqlServer SagaStore Tests (10 tests).
/// </para>
/// <para>
/// These tests verify the SqlServerSagaStore implementation against a real SQL Server
/// database using TestContainers. Tests cover save, load, update, and isolation behavior.
/// </para>
/// </remarks>
[IntegrationTest]
[Collection(ContainerCollections.SqlServer)]
[Trait("Component", "SagaStore")]
[Trait("Database", "SqlServer")]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class SqlServerSagaStoreIntegrationShould : IntegrationTestBase
{
	private readonly SqlServerFixture _sqlFixture;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerSagaStoreIntegrationShould"/> class.
	/// </summary>
	/// <param name="sqlFixture">The SQL Server container fixture.</param>
	public SqlServerSagaStoreIntegrationShould(SqlServerFixture sqlFixture)
	{
		_sqlFixture = sqlFixture;
	}

	/// <summary>
	/// Tests that a new saga can be saved and loaded.
	/// </summary>
	[Fact]
	public async Task SaveAndLoadNewSaga()
	{
		// Arrange
		await InitializeSagaTableAsync();
		var store = CreateSagaStore();
		var sagaId = Guid.NewGuid();
		var state = TestSagaState.Create(sagaId);
		state.Status = "Started";

		// Act
		await store.SaveAsync(state, TestCancellationToken);
		var loaded = await store.LoadAsync<TestSagaState>(sagaId, TestCancellationToken);

		// Assert
		_ = loaded.ShouldNotBeNull();
		loaded.SagaId.ShouldBe(sagaId);
		loaded.Status.ShouldBe("Started");
	}

	/// <summary>
	/// Tests that an existing saga can be updated.
	/// </summary>
	[Fact]
	public async Task UpdateExistingSaga()
	{
		// Arrange
		await InitializeSagaTableAsync();
		var store = CreateSagaStore();
		var sagaId = Guid.NewGuid();
		var state = TestSagaState.Create(sagaId);
		state.Status = "Initial";
		state.Counter = 1;

		await store.SaveAsync(state, TestCancellationToken);

		// Act - Update the saga
		state.Status = "Updated";
		state.Counter = 42;
		await store.SaveAsync(state, TestCancellationToken);

		var loaded = await store.LoadAsync<TestSagaState>(sagaId, TestCancellationToken);

		// Assert
		_ = loaded.ShouldNotBeNull();
		loaded.Status.ShouldBe("Updated");
		loaded.Counter.ShouldBe(42);
	}

	/// <summary>
	/// Tests that loading a non-existent saga returns null.
	/// </summary>
	[Fact]
	public async Task ReturnNullForNonExistentSaga()
	{
		// Arrange
		await InitializeSagaTableAsync();
		var store = CreateSagaStore();
		var sagaId = Guid.NewGuid();

		// Act
		var loaded = await store.LoadAsync<TestSagaState>(sagaId, TestCancellationToken);

		// Assert
		loaded.ShouldBeNull();
	}

	/// <summary>
	/// Tests that the Completed flag is persisted correctly.
	/// </summary>
	[Fact]
	public async Task PersistCompletedFlag()
	{
		// Arrange
		await InitializeSagaTableAsync();
		var store = CreateSagaStore();
		var sagaId = Guid.NewGuid();
		var state = TestSagaState.Create(sagaId);
		state.Completed = true;
		state.CompletedUtc = DateTime.UtcNow;

		// Act
		await store.SaveAsync(state, TestCancellationToken);
		var loaded = await store.LoadAsync<TestSagaState>(sagaId, TestCancellationToken);

		// Assert
		_ = loaded.ShouldNotBeNull();
		loaded.Completed.ShouldBeTrue();
	}

	/// <summary>
	/// Tests that multiple updates preserve the latest state.
	/// </summary>
	[Fact]
	public async Task PreserveLatestStateAfterMultipleUpdates()
	{
		// Arrange
		await InitializeSagaTableAsync();
		var store = CreateSagaStore();
		var sagaId = Guid.NewGuid();
		var state = TestSagaState.Create(sagaId);

		// Act - Multiple updates
		state.Counter = 1;
		await store.SaveAsync(state, TestCancellationToken);

		state.Counter = 2;
		await store.SaveAsync(state, TestCancellationToken);

		state.Counter = 3;
		await store.SaveAsync(state, TestCancellationToken);

		var loaded = await store.LoadAsync<TestSagaState>(sagaId, TestCancellationToken);

		// Assert
		_ = loaded.ShouldNotBeNull();
		loaded.Counter.ShouldBe(3);
	}

	/// <summary>
	/// Tests that all properties are preserved through save/load cycle.
	/// </summary>
	[Fact]
	public async Task PreserveAllPropertiesThroughRoundTrip()
	{
		// Arrange
		await InitializeSagaTableAsync();
		var store = CreateSagaStore();
		var sagaId = Guid.NewGuid();
		var state = TestSagaState.Create(sagaId);
		var createdUtc = new DateTime(2025, 1, 15, 10, 30, 0, DateTimeKind.Utc);
		state.Status = "Complete";
		state.Counter = 100;
		state.CreatedUtc = createdUtc;
		state.Completed = true;
		state.CompletedUtc = new DateTime(2025, 1, 16, 14, 45, 0, DateTimeKind.Utc);
		state.Data["key1"] = "value1";
		state.Data["key2"] = "value2";

		// Act
		await store.SaveAsync(state, TestCancellationToken);
		var loaded = await store.LoadAsync<TestSagaState>(sagaId, TestCancellationToken);

		// Assert
		_ = loaded.ShouldNotBeNull();
		loaded.SagaId.ShouldBe(sagaId);
		loaded.Status.ShouldBe("Complete");
		loaded.Counter.ShouldBe(100);
		loaded.Completed.ShouldBeTrue();
		_ = loaded.Data.ShouldNotBeNull();
		loaded.Data.Count.ShouldBe(2);
		loaded.Data["key1"].ShouldBe("value1");
		loaded.Data["key2"].ShouldBe("value2");
	}

	/// <summary>
	/// Tests that sagas are isolated by saga ID.
	/// </summary>
	[Fact]
	public async Task IsolateSagasBySagaId()
	{
		// Arrange
		await InitializeSagaTableAsync();
		var store = CreateSagaStore();
		var sagaId1 = Guid.NewGuid();
		var sagaId2 = Guid.NewGuid();

		var state1 = TestSagaState.Create(sagaId1);
		state1.Counter = 111;
		var state2 = TestSagaState.Create(sagaId2);
		state2.Counter = 222;

		// Act
		await store.SaveAsync(state1, TestCancellationToken);
		await store.SaveAsync(state2, TestCancellationToken);

		var loaded1 = await store.LoadAsync<TestSagaState>(sagaId1, TestCancellationToken);
		var loaded2 = await store.LoadAsync<TestSagaState>(sagaId2, TestCancellationToken);

		// Assert
		_ = loaded1.ShouldNotBeNull();
		loaded1.Counter.ShouldBe(111);
		_ = loaded2.ShouldNotBeNull();
		loaded2.Counter.ShouldBe(222);
	}

	/// <summary>
	/// Tests that updating one saga doesn't affect others.
	/// </summary>
	[Fact]
	public async Task NotAffectOtherSagasWhenUpdating()
	{
		// Arrange
		await InitializeSagaTableAsync();
		var store = CreateSagaStore();
		var sagaId1 = Guid.NewGuid();
		var sagaId2 = Guid.NewGuid();

		var state1 = TestSagaState.Create(sagaId1);
		state1.Status = "First";
		var state2 = TestSagaState.Create(sagaId2);
		state2.Status = "Second";

		await store.SaveAsync(state1, TestCancellationToken);
		await store.SaveAsync(state2, TestCancellationToken);

		// Act - Update only state1
		state1.Status = "Updated";
		await store.SaveAsync(state1, TestCancellationToken);

		var loaded2 = await store.LoadAsync<TestSagaState>(sagaId2, TestCancellationToken);

		// Assert - state2 should be unchanged
		_ = loaded2.ShouldNotBeNull();
		loaded2.Status.ShouldBe("Second");
	}

	/// <summary>
	/// Tests that saving a saga with default values succeeds.
	/// </summary>
	[Fact]
	public async Task SaveSagaWithDefaultValues()
	{
		// Arrange
		await InitializeSagaTableAsync();
		var store = CreateSagaStore();
		var sagaId = Guid.NewGuid();
		var state = new TestSagaState { SagaId = sagaId };

		// Act
		await store.SaveAsync(state, TestCancellationToken);
		var loaded = await store.LoadAsync<TestSagaState>(sagaId, TestCancellationToken);

		// Assert
		_ = loaded.ShouldNotBeNull();
		loaded.SagaId.ShouldBe(sagaId);
		loaded.Status.ShouldBe("Pending"); // Default value
	}

	/// <summary>
	/// Tests that DateTime values are preserved correctly.
	/// </summary>
	[Fact]
	public async Task PreserveDateTimeValues()
	{
		// Arrange
		await InitializeSagaTableAsync();
		var store = CreateSagaStore();
		var sagaId = Guid.NewGuid();
		var state = TestSagaState.Create(sagaId);
		var createdUtc = new DateTime(2025, 6, 15, 12, 30, 45, DateTimeKind.Utc);
		state.CreatedUtc = createdUtc;

		// Act
		await store.SaveAsync(state, TestCancellationToken);
		var loaded = await store.LoadAsync<TestSagaState>(sagaId, TestCancellationToken);

		// Assert
		_ = loaded.ShouldNotBeNull();
		// Allow for minor precision differences
		var timeDiff = Math.Abs((loaded.CreatedUtc - createdUtc).TotalSeconds);
		timeDiff.ShouldBeLessThanOrEqualTo(1);
	}

	/// <summary>
	/// bd-eszc06 (S840, AC-11) — independent regression lock (author≠impl, TestsDeveloper).
	/// The SHIPPED SQL saga store must enforce optimistic concurrency under the store-owns-increment
	/// convention (SA 13980): two parties load a saga at version N and save with NO caller arithmetic →
	/// exactly one succeeds (store bumps to N+1); the other (stale, still carrying N) throws
	/// <see cref="ConcurrencyException"/> with no lost update. RED on the pre-fix store (unchecked
	/// last-writer-wins MERGE — the stale save would silently overwrite).
	/// </summary>
	[Fact]
	public async Task EnforceOptimisticConcurrency_StaleSaveThrowsConcurrencyException()
	{
		// Arrange — persist a saga (store bumps 0 -> 1), then load two copies, both at version 1.
		await InitializeSagaTableAsync();
		var store = CreateSagaStore();
		var sagaId = Guid.NewGuid();
		var initial = TestSagaState.Create(sagaId);
		initial.Status = "v1";
		await store.SaveAsync(initial, TestCancellationToken); // Version 0 -> store inserts at 1

		var copy1 = await store.LoadAsync<TestSagaState>(sagaId, TestCancellationToken);
		var copy2 = await store.LoadAsync<TestSagaState>(sagaId, TestCancellationToken);
		_ = copy1.ShouldNotBeNull();
		_ = copy2.ShouldNotBeNull();
		copy1!.Version.ShouldBe(1L);
		copy2!.Version.ShouldBe(1L);

		// Act — copy1 saves first (NO arithmetic): store CASes on the loaded version 1, succeeds, bumps to 2.
		copy1.Status = "winner";
		await store.SaveAsync(copy1, TestCancellationToken);

		// copy2 still carries the loaded version 1, but the row is now 2 → stale. No caller arithmetic.
		copy2.Status = "loser";

		// Assert — the stale save is rejected (no lost update). RED on the pre-fix last-writer-wins MERGE.
		_ = await Should.ThrowAsync<ConcurrencyException>(
			() => store.SaveAsync(copy2, TestCancellationToken)).ConfigureAwait(false);

		// The winner's write survived; the loser did NOT overwrite it.
		var persisted = await store.LoadAsync<TestSagaState>(sagaId, TestCancellationToken);
		_ = persisted.ShouldNotBeNull();
		persisted!.Status.ShouldBe("winner");
		persisted.Version.ShouldBe(2L);
	}

	/// <summary>
	/// bd-eszc06 (engage-test, SA 13980): a brand-new saga at the natural default <c>Version = 0</c>
	/// persists with zero caller arithmetic — the store owns the increment (0 -> 1 on insert).
	/// </summary>
	[Fact]
	public async Task PersistNewSagaAtDefaultVersionZero()
	{
		// Arrange
		await InitializeSagaTableAsync();
		var store = CreateSagaStore();
		var sagaId = Guid.NewGuid();
		var state = TestSagaState.Create(sagaId);
		state.Status = "created";
		state.Version.ShouldBe(0L); // natural new saga, no caller math

		// Act — store owns the increment.
		await store.SaveAsync(state, TestCancellationToken);

		// Assert
		var loaded = await store.LoadAsync<TestSagaState>(sagaId, TestCancellationToken);
		_ = loaded.ShouldNotBeNull();
		loaded!.Status.ShouldBe("created");
		loaded.Version.ShouldBe(1L); // store bumped 0 -> 1
	}

	/// <summary>
	/// bd-eszc06 (engage-test for the EF-style write-back, SA 13980): create -> save -> mutate -> save on
	/// the SAME object, with no caller arithmetic, must succeed. The store writes the new version back onto
	/// the saved instance, so the second save carries the freshly-bumped version (not the stale loaded one).
	/// Without the write-back the second save would re-conflict → <see cref="ConcurrencyException"/>.
	/// </summary>
	[Fact]
	public async Task AllowConsecutiveSavesOnSameObjectViaWriteBack()
	{
		// Arrange
		await InitializeSagaTableAsync();
		var store = CreateSagaStore();
		var sagaId = Guid.NewGuid();
		var state = TestSagaState.Create(sagaId);
		state.Status = "first";

		// Act — first save: 0 -> 1, write-back sets state.Version = 1.
		await store.SaveAsync(state, TestCancellationToken);
		state.Version.ShouldBe(1L); // EF-style write-back (the subtle bit that makes this work)

		// Mutate the SAME object and save again — no arithmetic.
		state.Status = "second";
		await store.SaveAsync(state, TestCancellationToken); // 1 -> 2

		// Assert
		var loaded = await store.LoadAsync<TestSagaState>(sagaId, TestCancellationToken);
		_ = loaded.ShouldNotBeNull();
		loaded!.Status.ShouldBe("second");
		loaded.Version.ShouldBe(2L);
	}

	/// <summary>
	/// 1f5om2 (S853, DATA-CORRUPTION) — author≠impl TYPE-ISOLATION regression lock (TestsDeveloper).
	/// SqlServer has no <c>SagaStoreConformanceTestBase</c> subclass (it uses this TestContainers harness),
	/// so the uniform type-isolation contract is covered here against a real SQL Server: a typed
	/// <c>LoadAsync&lt;TSagaState&gt;(id)</c> MUST return <see langword="null"/> when the saga stored at
	/// <c>id</c> is a DIFFERENT type that merely shares the Guid — never mis-deserialize the wrong-typed blob.
	/// </summary>
	/// <remarks>
	/// The fix (<c>LoadSagaRequest&lt;TSagaState&gt;</c>) scopes the SELECT to
	/// <c>WHERE SagaId = @SagaId AND SagaType = @SagaType</c>, with the discriminator
	/// <c>typeof(TSagaState).Name</c> matching what <c>SaveSagaRequest</c> persists. RED on the pre-fix
	/// load-by-<c>SagaId</c>-only path (which deserialized the stored "TestSagaState" blob into
	/// <see cref="TypeIsolationOtherSagaState"/>); GREEN on the type-scoped load. NON-SKIPPED real infra —
	/// runs against the SQL Server container like its 13 sibling integration facts (no mock).
	/// Production RED-proof deferred post-commit (FrontendDeveloper's src is reserved; do not modify src/).
	/// </remarks>
	[Fact]
	public async Task ReturnNull_WhenLoadingDifferentSagaTypeAtSameSagaId()
	{
		// Arrange — persist a TestSagaState at the id (the store records SagaType = "TestSagaState").
		await InitializeSagaTableAsync();
		var store = CreateSagaStore();
		var sagaId = Guid.NewGuid();
		var state = TestSagaState.Create(sagaId);
		state.Status = "Started";
		await store.SaveAsync(state, TestCancellationToken);

		// Sanity — the correct type loads (guards against a vacuous always-null result).
		var sameType = await store.LoadAsync<TestSagaState>(sagaId, TestCancellationToken);
		_ = sameType.ShouldNotBeNull();

		// Act — load the SAME id as a DIFFERENT saga type (SagaType "TypeIsolationOtherSagaState").
		var loaded = await store.LoadAsync<TypeIsolationOtherSagaState>(sagaId, TestCancellationToken);

		// Assert — the wrong-typed saga must NOT be returned (no mis-deserialization).
		loaded.ShouldBeNull(
			"LoadAsync<TSagaState>(id) must return null when the saga at id is a different type (1f5om2)");
	}

	private SqlServerSagaStore CreateSagaStore()
	{
		var logger = NullLogger<SqlServerSagaStore>.Instance;
		var serializer = new DispatchJsonSerializer();
		return new SqlServerSagaStore(_sqlFixture.ConnectionString, logger, serializer);
	}

	/// <summary>
	/// A distinct saga-state type used only to drive the 1f5om2 type-isolation lock. Its simple type name
	/// ("TypeIsolationOtherSagaState") differs from the persisted "TestSagaState", so the type-scoped load
	/// filter excludes the stored row.
	/// </summary>
	private sealed class TypeIsolationOtherSagaState : SagaState
	{
		public string OtherData { get; set; } = string.Empty;
	}

	private async Task InitializeSagaTableAsync()
	{
		const string createSchemaSql = """
			IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'dispatch')
			BEGIN
			    EXEC('CREATE SCHEMA dispatch');
			END
			""";

		const string createTableSql = """
			IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dispatch].[sagas]') AND type in (N'U'))
			BEGIN
			    CREATE TABLE dispatch.sagas (
			        SagaId UNIQUEIDENTIFIER PRIMARY KEY,
			        SagaType NVARCHAR(500) NOT NULL,
			        StateJson NVARCHAR(MAX) NOT NULL,
			        IsCompleted BIT NOT NULL DEFAULT 0,
			        Version BIGINT NOT NULL DEFAULT 0,
			        CreatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
			        UpdatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
			    );
			END
			""";

		await using var connection = new SqlConnection(_sqlFixture.ConnectionString);
		await connection.OpenAsync(TestCancellationToken);

		_ = await connection.ExecuteAsync(createSchemaSql);
		_ = await connection.ExecuteAsync(createTableSql);
	}
}
