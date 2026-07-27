// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Postgres;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Serialization;

namespace Excalibur.Data.Tests.Postgres.Requests.Saga;

/// <summary>
/// Unit tests for <see cref="SaveSagaRequest{TSagaState}"/>.
/// Covers constructor validation, SQL structure, and parameter setup.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Postgres")]
public sealed class SaveSagaRequestShould : IDisposable
{
	private readonly DispatchJsonSerializer _serializer = new();

	public void Dispose() => _serializer.Dispose();

	private static PostgresSagaOptions CreateOptions() => new()
	{
		ConnectionString = "Host=localhost;Database=test;",
		Schema = "dispatch",
		TableName = "sagas",
		CommandTimeoutSeconds = 30
	};

	private static TestSagaState CreateTestState() => new()
	{
		SagaId = Guid.NewGuid(),
		Completed = false,
		OrderId = "order-123"
	};

	[Fact]
	public void CreateValidRequest_WithAllParameters()
	{
		// Arrange
		var state = CreateTestState();
		var options = CreateOptions();

		// Act
		var request = new SaveSagaRequest<TestSagaState>(
			state, options, _serializer, TenantScope.None, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
		request.ResolveAsync.ShouldNotBeNull();
	}

	[Fact]
	public void GenerateSql_WithInsertIntoStatement()
	{
		// Arrange
		var state = CreateTestState();
		var options = CreateOptions();

		// Act
		var request = new SaveSagaRequest<TestSagaState>(
			state, options, _serializer, TenantScope.None, CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain($"INSERT INTO {options.QualifiedTableName}");
	}

	[Fact]
	public void GenerateSql_NewSaga_InsertWithNoResurrectGuard()
	{
		// skl8r7 / e1tsq2 (SA two-branch no-resurrect, store-owns-increment): a NEW saga (Version 0) →
		// INSERT ... ON CONFLICT (saga_id) DO NOTHING. A pre-existing row (concurrent create / already-advanced
		// saga) yields 0 rows → ConcurrencyException; there is NO blind DO-UPDATE that could silently overwrite.
		// RED on a single unconditional upsert / DO-UPDATE-SET on the new-saga path.
		var state = CreateTestState(); // Version 0 (new saga)
		var options = CreateOptions();

		var sql = new SaveSagaRequest<TestSagaState>(state, options, _serializer, TenantScope.None, CancellationToken.None).Command.CommandText;

		sql.ShouldContain($"INSERT INTO {options.QualifiedTableName}");

		// The conflict target is the (tenant_id, saga_id) PAIR, not saga_id alone. That widened when the
		// tenant predicate became unconditional: the same saga_id legitimately exists once PER TENANT, so a
		// single-column target would make one tenant's create collide with another's and DO NOTHING —
		// silently dropping a saga that was never a duplicate. This arm asserted the narrower target and
		// went red on the fix; it is rebound rather than relaxed, because the no-resurrect property it
		// guards is unchanged and still worth holding.
		sql.ShouldContain("ON CONFLICT (tenant_id, saga_id) DO NOTHING");
		sql.ShouldNotContain("DO UPDATE"); // no blind upsert on the new-saga path
	}

	[Fact]
	public void GenerateSql_ExistingSaga_VersionGatedUpdate_NoInsertResurrection()
	{
		// skl8r7 / e1tsq2: an UPDATE (Version > 0) → version-gated UPDATE ... WHERE version = @ExpectedVersion,
		// and CRUCIALLY no INSERT branch — a stale version OR a deleted/missing row matches 0 rows →
		// ConcurrencyException (no lost update, no zombie resurrection). RED on a blind upsert / unguarded insert.
		var state = CreateTestState();
		state.Version = 3; // existing saga loaded at v3
		var options = CreateOptions();

		var sql = new SaveSagaRequest<TestSagaState>(state, options, _serializer, TenantScope.None, CancellationToken.None).Command.CommandText;

		sql.ShouldContain($"UPDATE {options.QualifiedTableName}");
		sql.ShouldContain("version = @ExpectedVersion");
		sql.ShouldNotContain("INSERT INTO"); // no resurrection path for an existing/stale saga
	}

	[Fact]
	public void GenerateSql_WithAllSagaColumnNames()
	{
		// Arrange
		var state = CreateTestState();
		var options = CreateOptions();

		// Act
		var request = new SaveSagaRequest<TestSagaState>(
			state, options, _serializer, TenantScope.None, CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain("saga_id");
		sql.ShouldContain("saga_type");
		sql.ShouldContain("state_json");
		sql.ShouldContain("is_completed");
		sql.ShouldContain("version"); // skl8r7: optimistic-concurrency version column (was absent pre-fix)
		sql.ShouldContain("created_utc");
		sql.ShouldContain("updated_utc");
	}

	[Fact]
	public void GenerateSql_WithJsonbCast()
	{
		// Arrange
		var state = CreateTestState();
		var options = CreateOptions();

		// Act
		var request = new SaveSagaRequest<TestSagaState>(
			state, options, _serializer, TenantScope.None, CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain("@StateJson::jsonb");
	}

	[Fact]
	public void SetParameters_NewSaga_BindsNewVersion_NoExpectedVersion()
	{
		// skl8r7 / e1tsq2: a NEW saga (Version 0) takes the INSERT branch, which binds the store-owned
		// increment (@NewVersion) only. @ExpectedVersion is referenced solely by the UPDATE branch, so it is
		// NOT bound here (no unreferenced parameter on the insert path).
		var state = CreateTestState(); // Version 0
		var options = CreateOptions();

		var request = new SaveSagaRequest<TestSagaState>(state, options, _serializer, TenantScope.None, CancellationToken.None);

		var paramNames = request.Parameters.ParameterNames.ToList();
		paramNames.ShouldContain("SagaId");
		paramNames.ShouldContain("SagaType");
		paramNames.ShouldContain("StateJson");
		paramNames.ShouldContain("IsCompleted");
		paramNames.ShouldContain("CompletedAt"); // completion instant bound on every save (w8aqq3 saga-purge key)
		paramNames.ShouldContain("TenantId"); // tenant scope bound on every save (bd-952rbe)
		paramNames.ShouldContain("NewVersion");
		paramNames.ShouldNotContain("ExpectedVersion");
		paramNames.Count.ShouldBe(7);
	}

	[Fact]
	public void SetParameters_ExistingSaga_BindsExpectedVersionGate()
	{
		// skl8r7 / e1tsq2: an existing saga (Version > 0) takes the UPDATE branch, which binds BOTH the
		// optimistic-concurrency token (@ExpectedVersion, the version-gate) and the store-owned increment
		// (@NewVersion). RED on a blind update with no version-gate parameter.
		var state = CreateTestState();
		state.Version = 3;
		var options = CreateOptions();

		var request = new SaveSagaRequest<TestSagaState>(state, options, _serializer, TenantScope.None, CancellationToken.None);

		var paramNames = request.Parameters.ParameterNames.ToList();
		paramNames.ShouldContain("ExpectedVersion");
		paramNames.ShouldContain("NewVersion");
		paramNames.ShouldContain("CompletedAt"); // completion instant bound on every save (w8aqq3 saga-purge key)
		paramNames.ShouldContain("TenantId"); // tenant scope bound on every save (bd-952rbe)
		paramNames.Count.ShouldBe(8);
	}

	[Fact]
	public void SetCommandTimeout_FromOptions()
	{
		// Arrange
		var state = CreateTestState();
		var options = CreateOptions();
		options.CommandTimeoutSeconds = 45;

		// Act
		var request = new SaveSagaRequest<TestSagaState>(
			state, options, _serializer, TenantScope.None, CancellationToken.None);

		// Assert
		request.Command.CommandTimeout.ShouldBe(45);
	}

	[Fact]
	public void CallSerializer_WithSagaState()
	{
		// Arrange
		var state = CreateTestState();
		state.OrderId = "order-123";
		var options = CreateOptions();

		// Act
		var request = new SaveSagaRequest<TestSagaState>(
			state, options, _serializer, TenantScope.None, CancellationToken.None);

		// Assert — verify serializer produced valid JSON containing the saga state
		var stateJson = request.Parameters.Get<string>("StateJson");
		stateJson.ShouldNotBeNullOrWhiteSpace();
		stateJson.ShouldContain("order-123");
	}

	[Fact]
	public void ThrowArgumentNullException_WhenSagaStateIsNull()
	{
		var options = CreateOptions();
		Should.Throw<ArgumentNullException>(() => new SaveSagaRequest<TestSagaState>(
			null!, options, _serializer, TenantScope.None, CancellationToken.None));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenOptionsIsNull()
	{
		var state = CreateTestState();
		Should.Throw<ArgumentNullException>(() => new SaveSagaRequest<TestSagaState>(
			state, null!, _serializer, TenantScope.None, CancellationToken.None));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenSerializerIsNull()
	{
		var state = CreateTestState();
		var options = CreateOptions();
		Should.Throw<ArgumentNullException>(() => new SaveSagaRequest<TestSagaState>(
			state, options, null!, TenantScope.None, CancellationToken.None));
	}

	/// <summary>
	/// Test saga state for unit testing.
	/// </summary>
	private sealed class TestSagaState : SagaState
	{
		public string OrderId { get; set; } = string.Empty;
	}
}
