// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Postgres;
using Excalibur.Dispatch.Abstractions.Messaging;
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
			state, options, _serializer, CancellationToken.None);

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
			state, options, _serializer, CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain($"INSERT INTO {options.QualifiedTableName}");
	}

	[Fact]
	public void GenerateSql_WithUpsertOnConflict()
	{
		// Arrange
		var state = CreateTestState();
		var options = CreateOptions();

		// Act
		var request = new SaveSagaRequest<TestSagaState>(
			state, options, _serializer, CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain("ON CONFLICT (saga_id) DO UPDATE SET");
	}

	[Fact]
	public void GenerateSql_WithAllSagaColumnNames()
	{
		// Arrange
		var state = CreateTestState();
		var options = CreateOptions();

		// Act
		var request = new SaveSagaRequest<TestSagaState>(
			state, options, _serializer, CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain("saga_id");
		sql.ShouldContain("saga_type");
		sql.ShouldContain("state_json");
		sql.ShouldContain("is_completed");
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
			state, options, _serializer, CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain("@StateJson::jsonb");
	}

	[Fact]
	public void SetParameters_ForAllSagaFields()
	{
		// Arrange
		var state = CreateTestState();
		var options = CreateOptions();

		// Act
		var request = new SaveSagaRequest<TestSagaState>(
			state, options, _serializer, CancellationToken.None);

		// Assert
		var paramNames = request.Parameters.ParameterNames.ToList();
		paramNames.ShouldContain("SagaId");
		paramNames.ShouldContain("SagaType");
		paramNames.ShouldContain("StateJson");
		paramNames.ShouldContain("IsCompleted");
		paramNames.Count.ShouldBe(4);
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
			state, options, _serializer, CancellationToken.None);

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
			state, options, _serializer, CancellationToken.None);

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
			null!, options, _serializer, CancellationToken.None));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenOptionsIsNull()
	{
		var state = CreateTestState();
		Should.Throw<ArgumentNullException>(() => new SaveSagaRequest<TestSagaState>(
			state, null!, _serializer, CancellationToken.None));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenSerializerIsNull()
	{
		var state = CreateTestState();
		var options = CreateOptions();
		Should.Throw<ArgumentNullException>(() => new SaveSagaRequest<TestSagaState>(
			state, options, null!, CancellationToken.None));
	}

	/// <summary>
	/// Test saga state for unit testing.
	/// </summary>
	private sealed class TestSagaState : SagaState
	{
		public string OrderId { get; set; } = string.Empty;
	}
}
