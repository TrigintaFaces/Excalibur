// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Postgres.Snapshots;
using Excalibur.Domain.Model;

namespace Excalibur.Data.Tests.Postgres.Requests.Snapshots;

/// <summary>
/// Unit tests for <see cref="SaveSnapshotRequest"/>.
/// Covers constructor validation, SQL structure, and parameter setup.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Postgres")]
public sealed class SaveSnapshotRequestShould
{
	private const string SchemaName = "event_store";
	private const string TableName = "snapshots";

	private static Snapshot CreateTestSnapshot() => new()
	{
		SnapshotId = Guid.NewGuid().ToString(),
		AggregateId = "aggregate-123",
		AggregateType = "OrderAggregate",
		Version = 5,
		Data = [0x01, 0x02, 0x03],
		CreatedAt = DateTimeOffset.UtcNow
	};

	[Fact]
	public void CreateValidRequest_WithAllParameters()
	{
		// Arrange
		var snapshot = CreateTestSnapshot();

		// Act
		var request = new SaveSnapshotRequest(
			snapshot, SchemaName, TableName, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
		request.ResolveAsync.ShouldNotBeNull();
		request.RequestType.ShouldBe(nameof(SaveSnapshotRequest));
	}

	[Fact]
	public void GenerateSql_WithInsertIntoStatement()
	{
		// Arrange
		var snapshot = CreateTestSnapshot();

		// Act
		var request = new SaveSnapshotRequest(
			snapshot, SchemaName, TableName, CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain($"INSERT INTO {SchemaName}.{TableName}");
	}

	[Fact]
	public void GenerateSql_WithUpsertOnConflict()
	{
		// Arrange
		var snapshot = CreateTestSnapshot();

		// Act
		var request = new SaveSnapshotRequest(
			snapshot, SchemaName, TableName, CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain("ON CONFLICT (aggregate_id, aggregate_type)");
		sql.ShouldContain("DO UPDATE SET");
	}

	[Fact]
	public void GenerateSql_WithVersionGuardClause()
	{
		// Arrange
		var snapshot = CreateTestSnapshot();

		// Act
		var request = new SaveSnapshotRequest(
			snapshot, SchemaName, TableName, CancellationToken.None);

		// Assert - Prevents older snapshots from overwriting newer ones
		var sql = request.Command.CommandText;
		sql.ShouldContain($"WHERE {TableName}.version < EXCLUDED.version");
	}

	[Fact]
	public void GenerateSql_WithAllSnapshotColumnNames()
	{
		// Arrange
		var snapshot = CreateTestSnapshot();

		// Act
		var request = new SaveSnapshotRequest(
			snapshot, SchemaName, TableName, CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain("snapshot_id");
		sql.ShouldContain("aggregate_id");
		sql.ShouldContain("aggregate_type");
		sql.ShouldContain("version");
		sql.ShouldContain("snapshot_type");
		sql.ShouldContain("data");
		sql.ShouldContain("metadata");
		sql.ShouldContain("created_at");
	}

	[Fact]
	public void SetParameters_ForAllSnapshotFields()
	{
		// Arrange
		var snapshot = CreateTestSnapshot();

		// Act
		var request = new SaveSnapshotRequest(
			snapshot, SchemaName, TableName, CancellationToken.None);

		// Assert
		var paramNames = request.Parameters.ParameterNames.ToList();
		paramNames.ShouldContain("SnapshotId");
		paramNames.ShouldContain("AggregateId");
		paramNames.ShouldContain("AggregateType");
		paramNames.ShouldContain("Version");
		paramNames.ShouldContain("SnapshotType");
		paramNames.ShouldContain("Data");
		paramNames.ShouldContain("Metadata");
		paramNames.ShouldContain("CreatedAt");
		paramNames.Count.ShouldBe(8);
	}

	[Fact]
	public void ThrowArgumentNullException_WhenSnapshotIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new SaveSnapshotRequest(
			null!, SchemaName, TableName, CancellationToken.None));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ThrowArgumentException_WhenSchemaNameIsInvalid(string? schemaName)
	{
		var snapshot = CreateTestSnapshot();
		Should.Throw<ArgumentException>(() => new SaveSnapshotRequest(
			snapshot, schemaName, TableName, CancellationToken.None));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ThrowArgumentException_WhenTableNameIsInvalid(string? tableName)
	{
		var snapshot = CreateTestSnapshot();
		Should.Throw<ArgumentException>(() => new SaveSnapshotRequest(
			snapshot, SchemaName, tableName, CancellationToken.None));
	}

	[Fact]
	public void HandleNonGuidSnapshotId_ByGeneratingNewGuid()
	{
		// Arrange - Use a non-GUID snapshot ID
		var snapshot = new Snapshot
		{
			SnapshotId = "not-a-guid",
			AggregateId = "aggregate-123",
			AggregateType = "OrderAggregate",
			Version = 5,
			Data = [0x01],
			CreatedAt = DateTimeOffset.UtcNow
		};

		// Act - Should not throw
		var request = new SaveSnapshotRequest(
			snapshot, SchemaName, TableName, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
		request.Parameters.ParameterNames.ShouldContain("SnapshotId");
	}
}
