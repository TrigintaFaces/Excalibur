// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

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

	/// <summary>
	/// Concurrent creates of one saga key resolve as exactly one winner, with every loser seeing the
	/// contract's concurrency conflict rather than a raw provider failure.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>What this arm does NOT prove, stated first so it is not mistaken for more.</b> It does not
	/// reproduce the MERGE conversion deadlock (error 1205) and it is not the lock for the hint pair that
	/// prevents it. That was attempted and measured, not assumed: with the shared-lock-only form of the
	/// upsert restored, this shape produced zero deadlocks over 200 real races (8 writers x 25 fresh keys)
	/// and again over 7,200 (48 x 150), against real SQL Server both times. It is GREEN on the broken code
	/// and cannot discriminate. The conversion window inside a single autocommit MERGE is too narrow to hit
	/// reliably from one client process -- production load finds it, a test loop does not. So the hint pair
	/// is locked structurally instead, next to the reasoning for it, in the unit tier
	/// (<c>SagaUpsertLockHintShould</c>), and this arm claims only what it can show.
	/// </para>
	/// <para>
	/// <b>What it does prove</b> is the property the hints exist to protect, observed through the store's own
	/// contract: several writers arriving together on a key that does not yet exist resolve into exactly one
	/// creation and N-1 concurrency conflicts. Zero would mean the race resolved by losing every write; more
	/// than one would mean the version gate did not hold; anything other than a concurrency conflict -- a key
	/// violation, a deadlock victim -- would mean a raw provider failure reached the caller where the
	/// contract promises a typed one. The winner count is what keeps the failure count honest, since a race
	/// that never actually raced would also report zero failures.
	/// </para>
	/// <para>
	/// Real SQL Server, never skipped: the outcome under contention is the engine's own behaviour, and a
	/// mocked or absent database would certify it either way.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task ResolveConcurrentCreatesOfOneSagaKey_AsExactlyOneWinner()
	{
		_sqlFixture.DockerAvailable.ShouldBeTrue(
			"the outcome of concurrent upserts is the SQL Server engine's own behaviour -- it cannot be "
			+ "observed without it, so this arm is never skipped.");

		await InitializeSagaTableAsync();
		var store = CreateSagaStore();

		// Many short races rather than one long one. Each round is a fresh key, so every round is a genuine
		// create-vs-create contention on a key no session has seen.
		const int WritersPerKey = 8;
		const int Rounds = 25;

		var unexpected = new ConcurrentBag<string>();
		var winners = 0;

		for (var round = 0; round < Rounds; round++)
		{
			var sagaId = Guid.NewGuid();

			// Released once, after every writer is already parked on it, so the MERGEs are issued together
			// instead of drifting apart as the tasks start.
			var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

			var writers = Enumerable.Range(0, WritersPerKey).Select(async _ =>
			{
				await gate.Task;

				var state = TestSagaState.Create(sagaId);
				state.Status = "Started";

				try
				{
					await store.SaveAsync(state, TestCancellationToken);
					_ = Interlocked.Increment(ref winners);
				}
				catch (ConcurrencyException)
				{
					// The contract's own answer for a loser: another writer created the saga first, so this
					// one's expected version no longer matches. Expected, and not a failure.
				}
				catch (Exception ex)
				{
					// Anything else -- a deadlock victim, a primary-key violation -- is the defect. Recorded
					// as text so the assertion names what actually happened instead of a bare count.
					unexpected.Add($"{ex.GetType().Name}: {ex.Message}");
				}
			}).ToArray();

			gate.SetResult();
			await Task.WhenAll(writers);
		}

		unexpected.ShouldBeEmpty(
			"a losing writer must see the contract's concurrency conflict, never a raw provider failure such "
			+ "as a key violation or a deadlock victim");

		winners.ShouldBe(
			Rounds,
			"exactly one writer per key must create the saga -- zero would mean the race resolved by losing "
			+ "every write, and more than one would mean the version gate did not hold");
	}

	private SqlServerSagaStore CreateSagaStore()
	{
		var logger = NullLogger<SqlServerSagaStore>.Instance;
		var serializer = new DispatchJsonSerializer();
		return new SqlServerSagaStore(_sqlFixture.ConnectionString, logger, serializer, tenantContext: new TestTenantContext());
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
			        SagaId UNIQUEIDENTIFIER NOT NULL,
			        SagaType NVARCHAR(500) NOT NULL,
			        StateJson NVARCHAR(MAX) NOT NULL,
			        IsCompleted BIT NOT NULL DEFAULT 0,
			        -- DATETIMEOFFSET(7), not DATETIME2: CompletedAt is a consumer-supplied DateTimeOffset
			        -- (SagaState.CompletedAt) and the retention purge keys on it; DATETIME2 would discard
			        -- the offset. Mirrors the shipped Scripts/01-SagaSchema.sql column that production
			        -- SaveSaga writes, QuerySagaSummaries reads, and PurgeCompletedSagas filters on.
			        CompletedAt DATETIMEOFFSET(7) NULL,
			        -- Mirrors the shipped schema exactly: NOT NULL with the reserved untenanted sentinel, and
			        -- BIN2 so in-engine equality on the tenant term is case-sensitive like .NET's Ordinal.
			        TenantId NVARCHAR(200) COLLATE Latin1_General_BIN2 NOT NULL DEFAULT '__untenanted__',
			        Version BIGINT NOT NULL DEFAULT 0,
			        CreatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
			        UpdatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
			        -- The tenant term is part of the key, matching Scripts/01-SagaSchema.sql. This is not
			        -- cosmetic for a fixture: with SagaId as the sole key, two tenants CANNOT hold the same
			        -- SagaId, so the cross-tenant-overwrite case is not merely untested but INEXPRESSIBLE --
			        -- a safety arm written against the old fixture would pass by being unable to set up.
			        CONSTRAINT PK_dispatch_sagas PRIMARY KEY CLUSTERED (TenantId, SagaId)
			    );
			END
			""";

		await using var connection = new SqlConnection(_sqlFixture.ConnectionString);
		await connection.OpenAsync(TestCancellationToken);

		_ = await connection.ExecuteAsync(createSchemaSql);
		_ = await connection.ExecuteAsync(createTableSql);
	}
}
