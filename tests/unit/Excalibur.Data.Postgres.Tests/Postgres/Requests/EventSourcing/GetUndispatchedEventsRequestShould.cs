// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Postgres.EventSourcing;

namespace Excalibur.Data.Tests.Postgres.Requests.EventSourcing;

/// <summary>
/// Unit tests for <see cref="GetUndispatchedEventsRequest"/>.
/// Covers constructor validation, SQL structure, and parameter setup.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Postgres")]
public sealed class GetUndispatchedEventsRequestShould
{
	private const int BatchSize = 100;
	private const string SchemaName = "event_store";
	private const string TableName = "events";

	[Fact]
	public void CreateValidRequest_WithAllParameters()
	{
		// Arrange & Act
		var request = new GetUndispatchedEventsRequest(
			BatchSize, SchemaName, TableName, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
		request.ResolveAsync.ShouldNotBeNull();
		request.RequestType.ShouldBe(nameof(GetUndispatchedEventsRequest));
	}

	[Fact]
	public void GenerateSql_WithSelectFromSchemaTable()
	{
		// Arrange & Act
		var request = new GetUndispatchedEventsRequest(
			BatchSize, SchemaName, TableName, CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain($"FROM {SchemaName}.{TableName}");
	}

	[Fact]
	public void GenerateSql_FilteringUndispatchedEventsOnly()
	{
		// Arrange & Act
		var request = new GetUndispatchedEventsRequest(
			BatchSize, SchemaName, TableName, CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain("is_dispatched = false");
	}

	[Fact]
	public void GenerateSql_OrderedByGlobalSequence()
	{
		// Arrange & Act
		var request = new GetUndispatchedEventsRequest(
			BatchSize, SchemaName, TableName, CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain("ORDER BY global_sequence ASC");
	}

	[Fact]
	public void GenerateSql_WithLimitClause()
	{
		// Arrange & Act
		var request = new GetUndispatchedEventsRequest(
			BatchSize, SchemaName, TableName, CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain("LIMIT @BatchSize");
	}

	[Fact]
	public void SetParameters_ForBatchSize()
	{
		// Arrange & Act
		var request = new GetUndispatchedEventsRequest(
			BatchSize, SchemaName, TableName, CancellationToken.None);

		// Assert
		var paramNames = request.Parameters.ParameterNames.ToList();
		paramNames.ShouldContain("BatchSize");
		paramNames.Count.ShouldBe(1);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(-100)]
	public void ThrowArgumentOutOfRangeException_WhenBatchSizeIsInvalid(int batchSize)
	{
		Should.Throw<ArgumentOutOfRangeException>(() => new GetUndispatchedEventsRequest(
			batchSize, SchemaName, TableName, CancellationToken.None));
	}

	[Fact]
	public void AcceptBatchSizeOfOne()
	{
		// Act - minimum valid batch size
		var request = new GetUndispatchedEventsRequest(
			1, SchemaName, TableName, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ThrowArgumentException_WhenSchemaNameIsInvalid(string? schemaName)
	{
		Should.Throw<ArgumentException>(() => new GetUndispatchedEventsRequest(
			BatchSize, schemaName, TableName, CancellationToken.None));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ThrowArgumentException_WhenTableNameIsInvalid(string? tableName)
	{
		Should.Throw<ArgumentException>(() => new GetUndispatchedEventsRequest(
			BatchSize, SchemaName, tableName, CancellationToken.None));
	}

	[Fact]
	public void GenerateSql_WithColumnAliases()
	{
		// Arrange & Act
		var request = new GetUndispatchedEventsRequest(
			BatchSize, SchemaName, TableName, CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain("event_id AS EventId");
		sql.ShouldContain("aggregate_id AS AggregateId");
		sql.ShouldContain("aggregate_type AS AggregateType");
		sql.ShouldContain("event_type AS EventType");
		sql.ShouldContain("event_data AS EventData");
	}
}
