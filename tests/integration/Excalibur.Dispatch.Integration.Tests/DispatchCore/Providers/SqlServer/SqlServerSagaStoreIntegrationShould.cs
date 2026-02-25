// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Excalibur.Dispatch.Abstractions.Serialization;

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
[Trait("Provider", "SqlServer")]
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
		await InitializeSagaTableAsync().ConfigureAwait(true);
		var store = CreateSagaStore();
		var sagaId = Guid.NewGuid();
		var state = TestSagaState.Create(sagaId);
		state.Status = "Started";

		// Act
		await store.SaveAsync(state, TestCancellationToken).ConfigureAwait(true);
		var loaded = await store.LoadAsync<TestSagaState>(sagaId, TestCancellationToken).ConfigureAwait(true);

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
		await InitializeSagaTableAsync().ConfigureAwait(true);
		var store = CreateSagaStore();
		var sagaId = Guid.NewGuid();
		var state = TestSagaState.Create(sagaId);
		state.Status = "Initial";
		state.Counter = 1;

		await store.SaveAsync(state, TestCancellationToken).ConfigureAwait(true);

		// Act - Update the saga
		state.Status = "Updated";
		state.Counter = 42;
		await store.SaveAsync(state, TestCancellationToken).ConfigureAwait(true);

		var loaded = await store.LoadAsync<TestSagaState>(sagaId, TestCancellationToken).ConfigureAwait(true);

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
		await InitializeSagaTableAsync().ConfigureAwait(true);
		var store = CreateSagaStore();
		var sagaId = Guid.NewGuid();

		// Act
		var loaded = await store.LoadAsync<TestSagaState>(sagaId, TestCancellationToken).ConfigureAwait(true);

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
		await InitializeSagaTableAsync().ConfigureAwait(true);
		var store = CreateSagaStore();
		var sagaId = Guid.NewGuid();
		var state = TestSagaState.Create(sagaId);
		state.Completed = true;
		state.CompletedUtc = DateTime.UtcNow;

		// Act
		await store.SaveAsync(state, TestCancellationToken).ConfigureAwait(true);
		var loaded = await store.LoadAsync<TestSagaState>(sagaId, TestCancellationToken).ConfigureAwait(true);

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
		await InitializeSagaTableAsync().ConfigureAwait(true);
		var store = CreateSagaStore();
		var sagaId = Guid.NewGuid();
		var state = TestSagaState.Create(sagaId);

		// Act - Multiple updates
		state.Counter = 1;
		await store.SaveAsync(state, TestCancellationToken).ConfigureAwait(true);

		state.Counter = 2;
		await store.SaveAsync(state, TestCancellationToken).ConfigureAwait(true);

		state.Counter = 3;
		await store.SaveAsync(state, TestCancellationToken).ConfigureAwait(true);

		var loaded = await store.LoadAsync<TestSagaState>(sagaId, TestCancellationToken).ConfigureAwait(true);

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
		await InitializeSagaTableAsync().ConfigureAwait(true);
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
		await store.SaveAsync(state, TestCancellationToken).ConfigureAwait(true);
		var loaded = await store.LoadAsync<TestSagaState>(sagaId, TestCancellationToken).ConfigureAwait(true);

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
		await InitializeSagaTableAsync().ConfigureAwait(true);
		var store = CreateSagaStore();
		var sagaId1 = Guid.NewGuid();
		var sagaId2 = Guid.NewGuid();

		var state1 = TestSagaState.Create(sagaId1);
		state1.Counter = 111;
		var state2 = TestSagaState.Create(sagaId2);
		state2.Counter = 222;

		// Act
		await store.SaveAsync(state1, TestCancellationToken).ConfigureAwait(true);
		await store.SaveAsync(state2, TestCancellationToken).ConfigureAwait(true);

		var loaded1 = await store.LoadAsync<TestSagaState>(sagaId1, TestCancellationToken).ConfigureAwait(true);
		var loaded2 = await store.LoadAsync<TestSagaState>(sagaId2, TestCancellationToken).ConfigureAwait(true);

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
		await InitializeSagaTableAsync().ConfigureAwait(true);
		var store = CreateSagaStore();
		var sagaId1 = Guid.NewGuid();
		var sagaId2 = Guid.NewGuid();

		var state1 = TestSagaState.Create(sagaId1);
		state1.Status = "First";
		var state2 = TestSagaState.Create(sagaId2);
		state2.Status = "Second";

		await store.SaveAsync(state1, TestCancellationToken).ConfigureAwait(true);
		await store.SaveAsync(state2, TestCancellationToken).ConfigureAwait(true);

		// Act - Update only state1
		state1.Status = "Updated";
		await store.SaveAsync(state1, TestCancellationToken).ConfigureAwait(true);

		var loaded2 = await store.LoadAsync<TestSagaState>(sagaId2, TestCancellationToken).ConfigureAwait(true);

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
		await InitializeSagaTableAsync().ConfigureAwait(true);
		var store = CreateSagaStore();
		var sagaId = Guid.NewGuid();
		var state = new TestSagaState { SagaId = sagaId };

		// Act
		await store.SaveAsync(state, TestCancellationToken).ConfigureAwait(true);
		var loaded = await store.LoadAsync<TestSagaState>(sagaId, TestCancellationToken).ConfigureAwait(true);

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
		await InitializeSagaTableAsync().ConfigureAwait(true);
		var store = CreateSagaStore();
		var sagaId = Guid.NewGuid();
		var state = TestSagaState.Create(sagaId);
		var createdUtc = new DateTime(2025, 6, 15, 12, 30, 45, DateTimeKind.Utc);
		state.CreatedUtc = createdUtc;

		// Act
		await store.SaveAsync(state, TestCancellationToken).ConfigureAwait(true);
		var loaded = await store.LoadAsync<TestSagaState>(sagaId, TestCancellationToken).ConfigureAwait(true);

		// Assert
		_ = loaded.ShouldNotBeNull();
		// Allow for minor precision differences
		var timeDiff = Math.Abs((loaded.CreatedUtc - createdUtc).TotalSeconds);
		timeDiff.ShouldBeLessThanOrEqualTo(1);
	}

	private SqlServerSagaStore CreateSagaStore()
	{
		var logger = NullLogger<SqlServerSagaStore>.Instance;
		var serializer = new SystemTextJsonSerializer();
		return new SqlServerSagaStore(_sqlFixture.ConnectionString, logger, serializer);
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
			        CreatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
			        UpdatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
			    );
			END
			""";

		await using var connection = new SqlConnection(_sqlFixture.ConnectionString);
		await connection.OpenAsync(TestCancellationToken).ConfigureAwait(true);

		_ = await connection.ExecuteAsync(createSchemaSql).ConfigureAwait(true);
		_ = await connection.ExecuteAsync(createTableSql).ConfigureAwait(true);
	}

	/// <summary>
	/// Simple JSON serializer for testing.
	/// </summary>
	private sealed class SystemTextJsonSerializer : IJsonSerializer
	{
		private static readonly System.Text.Json.JsonSerializerOptions Options = new()
		{
			PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
		};

		public string Serialize(object value, Type type) =>
			System.Text.Json.JsonSerializer.Serialize(value, type, Options);

		public object? Deserialize(string json, Type type) =>
			System.Text.Json.JsonSerializer.Deserialize(json, type, Options);

		public Task<string> SerializeAsync(object value, Type type) =>
			Task.FromResult(Serialize(value, type));

		public Task<object?> DeserializeAsync(string json, Type type) =>
			Task.FromResult(Deserialize(json, type));
	}
}
