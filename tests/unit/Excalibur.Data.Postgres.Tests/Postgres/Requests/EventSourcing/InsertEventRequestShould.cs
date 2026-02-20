// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Excalibur.Data.Postgres.EventSourcing;

namespace Excalibur.Data.Tests.Postgres.Requests.EventSourcing;

/// <summary>
/// Unit tests for <see cref="InsertEventRequest"/>.
/// Covers constructor validation, SQL structure, and parameter setup.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Postgres")]
public sealed class InsertEventRequestShould
{
	private const string EventId = "evt-001";
	private const string AggregateId = "aggregate-123";
	private const string AggregateType = "OrderAggregate";
	private const string EventType = "OrderCreated";
	private const string SchemaName = "event_store";
	private const string TableName = "events";
	private const long Version = 1;
	private static readonly byte[] EventData = [0x01, 0x02, 0x03];
	private static readonly byte[] Metadata = [0x04, 0x05];
	private static readonly DateTimeOffset Timestamp = DateTimeOffset.UtcNow;

	[Fact]
	public void CreateValidRequest_WithAllParameters()
	{
		// Arrange & Act
		var request = new InsertEventRequest(
			EventId, AggregateId, AggregateType, EventType, EventData, Metadata,
			Version, Timestamp, SchemaName, TableName,
			transaction: null, cancellationToken: CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
		request.ResolveAsync.ShouldNotBeNull();
		request.RequestType.ShouldBe(nameof(InsertEventRequest));
	}

	[Fact]
	public void GenerateSql_WithInsertIntoStatement()
	{
		// Arrange & Act
		var request = new InsertEventRequest(
			EventId, AggregateId, AggregateType, EventType, EventData, Metadata,
			Version, Timestamp, SchemaName, TableName,
			transaction: null, cancellationToken: CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain($"INSERT INTO {SchemaName}.{TableName}");
	}

	[Fact]
	public void GenerateSql_WithReturningGlobalSequence()
	{
		// Arrange & Act
		var request = new InsertEventRequest(
			EventId, AggregateId, AggregateType, EventType, EventData, Metadata,
			Version, Timestamp, SchemaName, TableName,
			transaction: null, cancellationToken: CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain("RETURNING global_sequence");
	}

	[Fact]
	public void GenerateSql_WithAllColumnNames()
	{
		// Arrange & Act
		var request = new InsertEventRequest(
			EventId, AggregateId, AggregateType, EventType, EventData, Metadata,
			Version, Timestamp, SchemaName, TableName,
			transaction: null, cancellationToken: CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain("event_id");
		sql.ShouldContain("aggregate_id");
		sql.ShouldContain("aggregate_type");
		sql.ShouldContain("event_type");
		sql.ShouldContain("event_data");
		sql.ShouldContain("metadata");
		sql.ShouldContain("version");
		sql.ShouldContain("timestamp");
		sql.ShouldContain("is_dispatched");
	}

	[Fact]
	public void SetParameters_ForAllFields()
	{
		// Arrange & Act
		var request = new InsertEventRequest(
			EventId, AggregateId, AggregateType, EventType, EventData, Metadata,
			Version, Timestamp, SchemaName, TableName,
			transaction: null, cancellationToken: CancellationToken.None);

		// Assert
		var paramNames = request.Parameters.ParameterNames.ToList();
		paramNames.ShouldContain("EventId");
		paramNames.ShouldContain("AggregateId");
		paramNames.ShouldContain("AggregateType");
		paramNames.ShouldContain("EventType");
		paramNames.ShouldContain("EventData");
		paramNames.ShouldContain("Metadata");
		paramNames.ShouldContain("Version");
		paramNames.ShouldContain("Timestamp");
		paramNames.Count.ShouldBe(8);
	}

	[Fact]
	public void AcceptNullMetadata()
	{
		// Act
		var request = new InsertEventRequest(
			EventId, AggregateId, AggregateType, EventType, EventData, metadata: null,
			Version, Timestamp, SchemaName, TableName,
			transaction: null, cancellationToken: CancellationToken.None);

		// Assert - Should not throw and should have Metadata param
		var paramNames = request.Parameters.ParameterNames.ToList();
		paramNames.ShouldContain("Metadata");
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ThrowArgumentException_WhenEventIdIsInvalid(string? eventId)
	{
		Should.Throw<ArgumentException>(() => new InsertEventRequest(
			eventId, AggregateId, AggregateType, EventType, EventData, Metadata,
			Version, Timestamp, SchemaName, TableName,
			transaction: null, cancellationToken: CancellationToken.None));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ThrowArgumentException_WhenAggregateIdIsInvalid(string? aggregateId)
	{
		Should.Throw<ArgumentException>(() => new InsertEventRequest(
			EventId, aggregateId, AggregateType, EventType, EventData, Metadata,
			Version, Timestamp, SchemaName, TableName,
			transaction: null, cancellationToken: CancellationToken.None));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ThrowArgumentException_WhenAggregateTypeIsInvalid(string? aggregateType)
	{
		Should.Throw<ArgumentException>(() => new InsertEventRequest(
			EventId, AggregateId, aggregateType, EventType, EventData, Metadata,
			Version, Timestamp, SchemaName, TableName,
			transaction: null, cancellationToken: CancellationToken.None));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ThrowArgumentException_WhenEventTypeIsInvalid(string? eventType)
	{
		Should.Throw<ArgumentException>(() => new InsertEventRequest(
			EventId, AggregateId, AggregateType, eventType, EventData, Metadata,
			Version, Timestamp, SchemaName, TableName,
			transaction: null, cancellationToken: CancellationToken.None));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenEventDataIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new InsertEventRequest(
			EventId, AggregateId, AggregateType, EventType, eventData: null!, Metadata,
			Version, Timestamp, SchemaName, TableName,
			transaction: null, cancellationToken: CancellationToken.None));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ThrowArgumentException_WhenSchemaNameIsInvalid(string? schemaName)
	{
		Should.Throw<ArgumentException>(() => new InsertEventRequest(
			EventId, AggregateId, AggregateType, EventType, EventData, Metadata,
			Version, Timestamp, schemaName, TableName,
			transaction: null, cancellationToken: CancellationToken.None));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ThrowArgumentException_WhenTableNameIsInvalid(string? tableName)
	{
		Should.Throw<ArgumentException>(() => new InsertEventRequest(
			EventId, AggregateId, AggregateType, EventType, EventData, Metadata,
			Version, Timestamp, SchemaName, tableName,
			transaction: null, cancellationToken: CancellationToken.None));
	}

	[Fact]
	public void AcceptTransaction_WhenProvided()
	{
		// Arrange
		var transaction = A.Fake<IDbTransaction>();

		// Act
		var request = new InsertEventRequest(
			EventId, AggregateId, AggregateType, EventType, EventData, Metadata,
			Version, Timestamp, SchemaName, TableName,
			transaction, cancellationToken: CancellationToken.None);

		// Assert
		request.Command.Transaction.ShouldBeSameAs(transaction);
	}
}
