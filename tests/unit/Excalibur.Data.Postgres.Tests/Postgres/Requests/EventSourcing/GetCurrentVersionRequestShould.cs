// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Excalibur.Data.Postgres.EventSourcing;

namespace Excalibur.Data.Tests.Postgres.Requests.EventSourcing;

/// <summary>
/// Unit tests for <see cref="GetCurrentVersionRequest"/>.
/// Covers constructor validation, SQL structure, and parameter setup.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Postgres")]
public sealed class GetCurrentVersionRequestShould
{
	private const string AggregateId = "aggregate-123";
	private const string AggregateType = "OrderAggregate";
	private const string SchemaName = "event_store";
	private const string TableName = "events";

	[Fact]
	public void CreateValidRequest_WithAllParameters()
	{
		// Arrange & Act
		var request = new GetCurrentVersionRequest(
			AggregateId, AggregateType, SchemaName, TableName,
			transaction: null, cancellationToken: CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
		request.ResolveAsync.ShouldNotBeNull();
		request.RequestType.ShouldBe(nameof(GetCurrentVersionRequest));
	}

	[Fact]
	public void GenerateSql_ContainingSchemaAndTableName()
	{
		// Arrange & Act
		var request = new GetCurrentVersionRequest(
			AggregateId, AggregateType, SchemaName, TableName,
			transaction: null, cancellationToken: CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain($"{SchemaName}.{TableName}");
	}

	[Fact]
	public void GenerateSql_WithSelectCoalesceMaxVersion()
	{
		// Arrange & Act
		var request = new GetCurrentVersionRequest(
			AggregateId, AggregateType, SchemaName, TableName,
			transaction: null, cancellationToken: CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain("COALESCE(MAX(version), -1)");
	}

	[Fact]
	public void GenerateSql_WithWhereClauseForAggregateIdAndType()
	{
		// Arrange & Act
		var request = new GetCurrentVersionRequest(
			AggregateId, AggregateType, SchemaName, TableName,
			transaction: null, cancellationToken: CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain("aggregate_id = @AggregateId");
		sql.ShouldContain("aggregate_type = @AggregateType");
	}

	[Fact]
	public void SetParameters_ForAggregateIdAndType()
	{
		// Arrange & Act
		var request = new GetCurrentVersionRequest(
			AggregateId, AggregateType, SchemaName, TableName,
			transaction: null, cancellationToken: CancellationToken.None);

		// Assert
		var paramNames = request.Parameters.ParameterNames.ToList();
		paramNames.ShouldContain("AggregateId");
		paramNames.ShouldContain("AggregateType");
		paramNames.Count.ShouldBe(2);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ThrowArgumentException_WhenAggregateIdIsInvalid(string? aggregateId)
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() => new GetCurrentVersionRequest(
			aggregateId, AggregateType, SchemaName, TableName,
			transaction: null, cancellationToken: CancellationToken.None));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ThrowArgumentException_WhenAggregateTypeIsInvalid(string? aggregateType)
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() => new GetCurrentVersionRequest(
			AggregateId, aggregateType, SchemaName, TableName,
			transaction: null, cancellationToken: CancellationToken.None));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ThrowArgumentException_WhenSchemaNameIsInvalid(string? schemaName)
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() => new GetCurrentVersionRequest(
			AggregateId, AggregateType, schemaName, TableName,
			transaction: null, cancellationToken: CancellationToken.None));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ThrowArgumentException_WhenTableNameIsInvalid(string? tableName)
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() => new GetCurrentVersionRequest(
			AggregateId, AggregateType, SchemaName, tableName,
			transaction: null, cancellationToken: CancellationToken.None));
	}

	[Fact]
	public void AcceptTransaction_WhenProvided()
	{
		// Arrange
		var transaction = A.Fake<IDbTransaction>();

		// Act
		var request = new GetCurrentVersionRequest(
			AggregateId, AggregateType, SchemaName, TableName,
			transaction, cancellationToken: CancellationToken.None);

		// Assert
		request.Command.Transaction.ShouldBeSameAs(transaction);
	}

	[Fact]
	public void AcceptNullTransaction()
	{
		// Act
		var request = new GetCurrentVersionRequest(
			AggregateId, AggregateType, SchemaName, TableName,
			transaction: null, cancellationToken: CancellationToken.None);

		// Assert
		request.Command.Transaction.ShouldBeNull();
	}
}
