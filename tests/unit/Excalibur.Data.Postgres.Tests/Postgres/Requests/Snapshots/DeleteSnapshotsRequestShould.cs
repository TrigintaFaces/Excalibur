// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Postgres.Snapshots;

namespace Excalibur.Data.Tests.Postgres.Requests.Snapshots;

/// <summary>
/// Unit tests for <see cref="DeleteSnapshotsRequest"/>.
/// Covers constructor validation, SQL structure, and parameter setup.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Postgres")]
public sealed class DeleteSnapshotsRequestShould
{
	private const string AggregateId = "aggregate-123";
	private const string AggregateType = "OrderAggregate";
	private const string SchemaName = "event_store";
	private const string TableName = "snapshots";

	[Fact]
	public void CreateValidRequest_WithAllParameters()
	{
		// Arrange & Act
		var request = new DeleteSnapshotsRequest(
			AggregateId, AggregateType, SchemaName, TableName,
			CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
		request.ResolveAsync.ShouldNotBeNull();
		request.RequestType.ShouldBe(nameof(DeleteSnapshotsRequest));
	}

	[Fact]
	public void GenerateSql_WithDeleteFromStatement()
	{
		// Arrange & Act
		var request = new DeleteSnapshotsRequest(
			AggregateId, AggregateType, SchemaName, TableName,
			CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain($"DELETE FROM {SchemaName}.{TableName}");
	}

	[Fact]
	public void GenerateSql_WithWhereClauseForAggregateIdAndType()
	{
		// Arrange & Act
		var request = new DeleteSnapshotsRequest(
			AggregateId, AggregateType, SchemaName, TableName,
			CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain("aggregate_id = @AggregateId");
		sql.ShouldContain("aggregate_type = @AggregateType");
	}

	[Fact]
	public void SetParameters_ForAggregateIdAndType()
	{
		// Arrange & Act
		var request = new DeleteSnapshotsRequest(
			AggregateId, AggregateType, SchemaName, TableName,
			CancellationToken.None);

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
		Should.Throw<ArgumentException>(() => new DeleteSnapshotsRequest(
			aggregateId, AggregateType, SchemaName, TableName,
			CancellationToken.None));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ThrowArgumentException_WhenAggregateTypeIsInvalid(string? aggregateType)
	{
		Should.Throw<ArgumentException>(() => new DeleteSnapshotsRequest(
			AggregateId, aggregateType, SchemaName, TableName,
			CancellationToken.None));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ThrowArgumentException_WhenSchemaNameIsInvalid(string? schemaName)
	{
		Should.Throw<ArgumentException>(() => new DeleteSnapshotsRequest(
			AggregateId, AggregateType, schemaName, TableName,
			CancellationToken.None));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ThrowArgumentException_WhenTableNameIsInvalid(string? tableName)
	{
		Should.Throw<ArgumentException>(() => new DeleteSnapshotsRequest(
			AggregateId, AggregateType, SchemaName, tableName,
			CancellationToken.None));
	}
}
