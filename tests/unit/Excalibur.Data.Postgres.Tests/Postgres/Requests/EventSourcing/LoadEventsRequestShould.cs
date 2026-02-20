// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Postgres.EventSourcing;

namespace Excalibur.Data.Tests.Postgres.Requests.EventSourcing;

/// <summary>
/// Unit tests for <see cref="LoadEventsRequest"/>.
/// Covers constructor validation, SQL structure, and parameter setup.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Postgres")]
public sealed class LoadEventsRequestShould
{
	private const string AggregateId = "aggregate-123";
	private const string AggregateType = "OrderAggregate";
	private const string SchemaName = "event_store";
	private const string TableName = "events";
	private const long FromVersion = 0;

	[Fact]
	public void CreateValidRequest_WithAllParameters()
	{
		// Arrange & Act
		var request = new LoadEventsRequest(
			AggregateId, AggregateType, SchemaName, TableName,
			FromVersion, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
		request.ResolveAsync.ShouldNotBeNull();
		request.RequestType.ShouldBe(nameof(LoadEventsRequest));
	}

	[Fact]
	public void GenerateSql_WithSelectFromSchemaTable()
	{
		// Arrange & Act
		var request = new LoadEventsRequest(
			AggregateId, AggregateType, SchemaName, TableName,
			FromVersion, CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain($"FROM {SchemaName}.{TableName}");
	}

	[Fact]
	public void GenerateSql_WithColumnAliases()
	{
		// Arrange & Act
		var request = new LoadEventsRequest(
			AggregateId, AggregateType, SchemaName, TableName,
			FromVersion, CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain("event_id AS EventId");
		sql.ShouldContain("aggregate_id AS AggregateId");
		sql.ShouldContain("aggregate_type AS AggregateType");
		sql.ShouldContain("event_type AS EventType");
		sql.ShouldContain("event_data AS EventData");
		sql.ShouldContain("metadata AS Metadata");
		sql.ShouldContain("version AS Version");
		sql.ShouldContain("timestamp AS Timestamp");
		sql.ShouldContain("is_dispatched AS IsDispatched");
	}

	[Fact]
	public void GenerateSql_WithVersionFilterAndOrdering()
	{
		// Arrange & Act
		var request = new LoadEventsRequest(
			AggregateId, AggregateType, SchemaName, TableName,
			FromVersion, CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain("version > @FromVersion");
		sql.ShouldContain("ORDER BY version ASC");
	}

	[Fact]
	public void SetParameters_ForAggregateIdTypeAndFromVersion()
	{
		// Arrange & Act
		var request = new LoadEventsRequest(
			AggregateId, AggregateType, SchemaName, TableName,
			FromVersion, CancellationToken.None);

		// Assert
		var paramNames = request.Parameters.ParameterNames.ToList();
		paramNames.ShouldContain("AggregateId");
		paramNames.ShouldContain("AggregateType");
		paramNames.ShouldContain("FromVersion");
		paramNames.Count.ShouldBe(3);
	}

	[Fact]
	public void AcceptNegativeFromVersion_ForLoadingAllEvents()
	{
		// Act - Should not throw for -1 (convention for "all events")
		var request = new LoadEventsRequest(
			AggregateId, AggregateType, SchemaName, TableName,
			fromVersion: -1, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ThrowArgumentException_WhenAggregateIdIsInvalid(string? aggregateId)
	{
		Should.Throw<ArgumentException>(() => new LoadEventsRequest(
			aggregateId, AggregateType, SchemaName, TableName,
			FromVersion, CancellationToken.None));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ThrowArgumentException_WhenAggregateTypeIsInvalid(string? aggregateType)
	{
		Should.Throw<ArgumentException>(() => new LoadEventsRequest(
			AggregateId, aggregateType, SchemaName, TableName,
			FromVersion, CancellationToken.None));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ThrowArgumentException_WhenSchemaNameIsInvalid(string? schemaName)
	{
		Should.Throw<ArgumentException>(() => new LoadEventsRequest(
			AggregateId, AggregateType, schemaName, TableName,
			FromVersion, CancellationToken.None));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ThrowArgumentException_WhenTableNameIsInvalid(string? tableName)
	{
		Should.Throw<ArgumentException>(() => new LoadEventsRequest(
			AggregateId, AggregateType, SchemaName, tableName,
			FromVersion, CancellationToken.None));
	}
}
