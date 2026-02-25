// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.Postgres.Cdc;

/// <summary>
/// Represents a data change event captured from Postgres logical replication.
/// </summary>
public sealed class PostgresDataChangeEvent
{
	/// <summary>
	/// Gets the LSN (Log Sequence Number) position of this change.
	/// </summary>
	public PostgresCdcPosition Position { get; init; }

	/// <summary>
	/// Gets the transaction ID that produced this change.
	/// </summary>
	public uint TransactionId { get; init; }

	/// <summary>
	/// Gets the commit timestamp of the transaction.
	/// </summary>
	public DateTimeOffset CommitTime { get; init; }

	/// <summary>
	/// Gets the schema name of the affected table.
	/// </summary>
	public string SchemaName { get; init; } = "public";

	/// <summary>
	/// Gets the name of the affected table.
	/// </summary>
	public string TableName { get; init; } = string.Empty;

	/// <summary>
	/// Gets the fully qualified table name (schema.table).
	/// </summary>
	public string FullTableName => $"{SchemaName}.{TableName}";

	/// <summary>
	/// Gets the type of change (Insert, Update, Delete, Truncate).
	/// </summary>
	public PostgresDataChangeType ChangeType { get; init; }

	/// <summary>
	/// Gets the collection of column changes for this event.
	/// </summary>
	public IReadOnlyList<PostgresDataChange> Changes { get; init; } = [];

	/// <summary>
	/// Gets the columns that identify the row (primary key or replica identity columns).
	/// </summary>
	public IReadOnlyList<PostgresDataChange> KeyColumns { get; init; } = [];

	/// <summary>
	/// Creates a new instance for an insert operation.
	/// </summary>
	/// <param name="position">The LSN position.</param>
	/// <param name="schemaName">The schema name.</param>
	/// <param name="tableName">The table name.</param>
	/// <param name="transactionId">The transaction ID.</param>
	/// <param name="commitTime">The commit timestamp.</param>
	/// <param name="newValues">The inserted column values.</param>
	/// <returns>A new <see cref="PostgresDataChangeEvent"/> for the insert.</returns>
	public static PostgresDataChangeEvent CreateInsert(
		PostgresCdcPosition position,
		string schemaName,
		string tableName,
		uint transactionId,
		DateTimeOffset commitTime,
		IReadOnlyList<PostgresDataChange> newValues)
	{
		return new PostgresDataChangeEvent
		{
			Position = position,
			SchemaName = schemaName,
			TableName = tableName,
			TransactionId = transactionId,
			CommitTime = commitTime,
			ChangeType = PostgresDataChangeType.Insert,
			Changes = newValues,
			KeyColumns = newValues.Where(c => c.IsPrimaryKey).ToList(),
		};
	}

	/// <summary>
	/// Creates a new instance for an update operation.
	/// </summary>
	/// <param name="position">The LSN position.</param>
	/// <param name="schemaName">The schema name.</param>
	/// <param name="tableName">The table name.</param>
	/// <param name="transactionId">The transaction ID.</param>
	/// <param name="commitTime">The commit timestamp.</param>
	/// <param name="changes">The changed column values with old and new values.</param>
	/// <param name="keyColumns">The key columns identifying the row.</param>
	/// <returns>A new <see cref="PostgresDataChangeEvent"/> for the update.</returns>
	public static PostgresDataChangeEvent CreateUpdate(
		PostgresCdcPosition position,
		string schemaName,
		string tableName,
		uint transactionId,
		DateTimeOffset commitTime,
		IReadOnlyList<PostgresDataChange> changes,
		IReadOnlyList<PostgresDataChange> keyColumns)
	{
		return new PostgresDataChangeEvent
		{
			Position = position,
			SchemaName = schemaName,
			TableName = tableName,
			TransactionId = transactionId,
			CommitTime = commitTime,
			ChangeType = PostgresDataChangeType.Update,
			Changes = changes,
			KeyColumns = keyColumns,
		};
	}

	/// <summary>
	/// Creates a new instance for a delete operation.
	/// </summary>
	/// <param name="position">The LSN position.</param>
	/// <param name="schemaName">The schema name.</param>
	/// <param name="tableName">The table name.</param>
	/// <param name="transactionId">The transaction ID.</param>
	/// <param name="commitTime">The commit timestamp.</param>
	/// <param name="keyColumns">The key columns identifying the deleted row.</param>
	/// <returns>A new <see cref="PostgresDataChangeEvent"/> for the delete.</returns>
	public static PostgresDataChangeEvent CreateDelete(
		PostgresCdcPosition position,
		string schemaName,
		string tableName,
		uint transactionId,
		DateTimeOffset commitTime,
		IReadOnlyList<PostgresDataChange> keyColumns)
	{
		return new PostgresDataChangeEvent
		{
			Position = position,
			SchemaName = schemaName,
			TableName = tableName,
			TransactionId = transactionId,
			CommitTime = commitTime,
			ChangeType = PostgresDataChangeType.Delete,
			Changes = keyColumns, // For deletes, changes contains the old key values
			KeyColumns = keyColumns,
		};
	}

	/// <summary>
	/// Creates a new instance for a truncate operation.
	/// </summary>
	/// <param name="position">The LSN position.</param>
	/// <param name="schemaName">The schema name.</param>
	/// <param name="tableName">The table name.</param>
	/// <param name="transactionId">The transaction ID.</param>
	/// <param name="commitTime">The commit timestamp.</param>
	/// <returns>A new <see cref="PostgresDataChangeEvent"/> for the truncate.</returns>
	public static PostgresDataChangeEvent CreateTruncate(
		PostgresCdcPosition position,
		string schemaName,
		string tableName,
		uint transactionId,
		DateTimeOffset commitTime)
	{
		return new PostgresDataChangeEvent
		{
			Position = position,
			SchemaName = schemaName,
			TableName = tableName,
			TransactionId = transactionId,
			CommitTime = commitTime,
			ChangeType = PostgresDataChangeType.Truncate,
			Changes = [],
			KeyColumns = [],
		};
	}
}
