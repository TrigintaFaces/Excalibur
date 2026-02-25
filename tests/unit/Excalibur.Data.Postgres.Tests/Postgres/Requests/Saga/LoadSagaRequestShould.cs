// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Postgres.Saga;
using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Dispatch.Abstractions.Serialization;

namespace Excalibur.Data.Tests.Postgres.Requests.Saga;

/// <summary>
/// Unit tests for <see cref="LoadSagaRequest{TSagaState}"/>.
/// Covers constructor validation, SQL structure, and parameter setup.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Postgres")]
public sealed class LoadSagaRequestShould
{
	private static readonly Guid SagaId = Guid.NewGuid();

	private static PostgresSagaOptions CreateOptions() => new()
	{
		ConnectionString = "Host=localhost;Database=test;",
		Schema = "dispatch",
		TableName = "sagas",
		CommandTimeoutSeconds = 30
	};

	[Fact]
	public void CreateValidRequest_WithAllParameters()
	{
		// Arrange
		var options = CreateOptions();
		var serializer = A.Fake<IJsonSerializer>();

		// Act
		var request = new LoadSagaRequest<TestSagaState>(
			SagaId, options, serializer, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
		request.ResolveAsync.ShouldNotBeNull();
	}

	[Fact]
	public void GenerateSql_SelectingStateJsonAndIsCompleted()
	{
		// Arrange
		var options = CreateOptions();
		var serializer = A.Fake<IJsonSerializer>();

		// Act
		var request = new LoadSagaRequest<TestSagaState>(
			SagaId, options, serializer, CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain("state_json");
		sql.ShouldContain("is_completed");
	}

	[Fact]
	public void GenerateSql_WithQualifiedTableName()
	{
		// Arrange
		var options = CreateOptions();
		var serializer = A.Fake<IJsonSerializer>();

		// Act
		var request = new LoadSagaRequest<TestSagaState>(
			SagaId, options, serializer, CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain(options.QualifiedTableName);
	}

	[Fact]
	public void GenerateSql_WithWhereClauseForSagaId()
	{
		// Arrange
		var options = CreateOptions();
		var serializer = A.Fake<IJsonSerializer>();

		// Act
		var request = new LoadSagaRequest<TestSagaState>(
			SagaId, options, serializer, CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain("saga_id = @SagaId");
	}

	[Fact]
	public void SetParameters_ForSagaId()
	{
		// Arrange
		var options = CreateOptions();
		var serializer = A.Fake<IJsonSerializer>();

		// Act
		var request = new LoadSagaRequest<TestSagaState>(
			SagaId, options, serializer, CancellationToken.None);

		// Assert
		var paramNames = request.Parameters.ParameterNames.ToList();
		paramNames.ShouldContain("SagaId");
	}

	[Fact]
	public void SetCommandTimeout_FromOptions()
	{
		// Arrange
		var options = CreateOptions();
		options.CommandTimeoutSeconds = 60;
		var serializer = A.Fake<IJsonSerializer>();

		// Act
		var request = new LoadSagaRequest<TestSagaState>(
			SagaId, options, serializer, CancellationToken.None);

		// Assert
		request.Command.CommandTimeout.ShouldBe(60);
	}

	[Fact]
	public void ThrowArgumentNullException_WhenOptionsIsNull()
	{
		var serializer = A.Fake<IJsonSerializer>();
		Should.Throw<ArgumentNullException>(() => new LoadSagaRequest<TestSagaState>(
			SagaId, null!, serializer, CancellationToken.None));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenSerializerIsNull()
	{
		var options = CreateOptions();
		Should.Throw<ArgumentNullException>(() => new LoadSagaRequest<TestSagaState>(
			SagaId, options, null!, CancellationToken.None));
	}

	/// <summary>
	/// Test saga state for unit testing.
	/// </summary>
	private sealed class TestSagaState : SagaState
	{
		public string OrderId { get; set; } = string.Empty;
	}
}
