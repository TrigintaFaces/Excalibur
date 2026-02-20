// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Postgres.Snapshots;

namespace Excalibur.Data.Tests.Postgres.Requests.Snapshots;

/// <summary>
/// Unit tests for <see cref="DeleteSnapshotsOlderThanRequest"/>.
/// Covers constructor validation, SQL structure, and parameter setup.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Postgres")]
public sealed class DeleteSnapshotsOlderThanRequestShould
{
	private const string AggregateId = "aggregate-123";
	private const string AggregateType = "OrderAggregate";
	private const long OlderThanVersion = 10;
	private const string SchemaName = "event_store";
	private const string TableName = "snapshots";

	[Fact]
	public void CreateValidRequest_WithAllParameters()
	{
		// Arrange & Act
		var request = new DeleteSnapshotsOlderThanRequest(
			AggregateId, AggregateType, OlderThanVersion, SchemaName, TableName,
			CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
		request.ResolveAsync.ShouldNotBeNull();
		request.RequestType.ShouldBe(nameof(DeleteSnapshotsOlderThanRequest));
	}

	[Fact]
	public void GenerateSql_WithDeleteFromStatement()
	{
		// Arrange & Act
		var request = new DeleteSnapshotsOlderThanRequest(
			AggregateId, AggregateType, OlderThanVersion, SchemaName, TableName,
			CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain($"DELETE FROM {SchemaName}.{TableName}");
	}

	[Fact]
	public void GenerateSql_WithVersionComparisonInWhere()
	{
		// Arrange & Act
		var request = new DeleteSnapshotsOlderThanRequest(
			AggregateId, AggregateType, OlderThanVersion, SchemaName, TableName,
			CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain("aggregate_id = @AggregateId");
		sql.ShouldContain("aggregate_type = @AggregateType");
		sql.ShouldContain("version < @Version");
	}

	[Fact]
	public void SetParameters_ForAggregateIdTypeAndVersion()
	{
		// Arrange & Act
		var request = new DeleteSnapshotsOlderThanRequest(
			AggregateId, AggregateType, OlderThanVersion, SchemaName, TableName,
			CancellationToken.None);

		// Assert
		var paramNames = request.Parameters.ParameterNames.ToList();
		paramNames.ShouldContain("AggregateId");
		paramNames.ShouldContain("AggregateType");
		paramNames.ShouldContain("Version");
		paramNames.Count.ShouldBe(3);
	}

	[Fact]
	public void AcceptZeroVersion()
	{
		// Act - version 0 should be valid
		var request = new DeleteSnapshotsOlderThanRequest(
			AggregateId, AggregateType, olderThanVersion: 0, SchemaName, TableName,
			CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ThrowArgumentException_WhenAggregateIdIsInvalid(string? aggregateId)
	{
		Should.Throw<ArgumentException>(() => new DeleteSnapshotsOlderThanRequest(
			aggregateId, AggregateType, OlderThanVersion, SchemaName, TableName,
			CancellationToken.None));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ThrowArgumentException_WhenAggregateTypeIsInvalid(string? aggregateType)
	{
		Should.Throw<ArgumentException>(() => new DeleteSnapshotsOlderThanRequest(
			AggregateId, aggregateType, OlderThanVersion, SchemaName, TableName,
			CancellationToken.None));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ThrowArgumentException_WhenSchemaNameIsInvalid(string? schemaName)
	{
		Should.Throw<ArgumentException>(() => new DeleteSnapshotsOlderThanRequest(
			AggregateId, AggregateType, OlderThanVersion, schemaName, TableName,
			CancellationToken.None));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ThrowArgumentException_WhenTableNameIsInvalid(string? tableName)
	{
		Should.Throw<ArgumentException>(() => new DeleteSnapshotsOlderThanRequest(
			AggregateId, AggregateType, OlderThanVersion, SchemaName, tableName,
			CancellationToken.None));
	}
}
