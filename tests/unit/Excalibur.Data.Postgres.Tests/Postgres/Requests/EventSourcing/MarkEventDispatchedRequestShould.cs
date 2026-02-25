// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Postgres.EventSourcing;

namespace Excalibur.Data.Tests.Postgres.Requests.EventSourcing;

/// <summary>
/// Unit tests for <see cref="MarkEventDispatchedRequest"/>.
/// Covers constructor validation, SQL structure, and parameter setup.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Postgres")]
public sealed class MarkEventDispatchedRequestShould
{
	private const string EventId = "evt-001";
	private const string SchemaName = "event_store";
	private const string TableName = "events";

	[Fact]
	public void CreateValidRequest_WithAllParameters()
	{
		// Arrange & Act
		var request = new MarkEventDispatchedRequest(
			EventId, SchemaName, TableName, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
		request.ResolveAsync.ShouldNotBeNull();
		request.RequestType.ShouldBe(nameof(MarkEventDispatchedRequest));
	}

	[Fact]
	public void GenerateSql_WithUpdateStatement()
	{
		// Arrange & Act
		var request = new MarkEventDispatchedRequest(
			EventId, SchemaName, TableName, CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain($"UPDATE {SchemaName}.{TableName}");
	}

	[Fact]
	public void GenerateSql_SettingIsDispatchedToTrue()
	{
		// Arrange & Act
		var request = new MarkEventDispatchedRequest(
			EventId, SchemaName, TableName, CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain("SET is_dispatched = true");
	}

	[Fact]
	public void GenerateSql_WithWhereClauseForEventId()
	{
		// Arrange & Act
		var request = new MarkEventDispatchedRequest(
			EventId, SchemaName, TableName, CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain("WHERE event_id = @EventId");
	}

	[Fact]
	public void SetParameters_ForEventId()
	{
		// Arrange & Act
		var request = new MarkEventDispatchedRequest(
			EventId, SchemaName, TableName, CancellationToken.None);

		// Assert
		var paramNames = request.Parameters.ParameterNames.ToList();
		paramNames.ShouldContain("EventId");
		paramNames.Count.ShouldBe(1);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ThrowArgumentException_WhenEventIdIsInvalid(string? eventId)
	{
		Should.Throw<ArgumentException>(() => new MarkEventDispatchedRequest(
			eventId, SchemaName, TableName, CancellationToken.None));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ThrowArgumentException_WhenSchemaNameIsInvalid(string? schemaName)
	{
		Should.Throw<ArgumentException>(() => new MarkEventDispatchedRequest(
			EventId, schemaName, TableName, CancellationToken.None));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ThrowArgumentException_WhenTableNameIsInvalid(string? tableName)
	{
		Should.Throw<ArgumentException>(() => new MarkEventDispatchedRequest(
			EventId, SchemaName, tableName, CancellationToken.None));
	}
}
