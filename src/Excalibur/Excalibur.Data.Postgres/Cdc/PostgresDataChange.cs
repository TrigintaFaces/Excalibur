// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.Postgres.Cdc;

/// <summary>
/// Represents a single column change in a Postgres CDC event.
/// </summary>
public sealed class PostgresDataChange
{
	/// <summary>
	/// Gets the name of the column that changed.
	/// </summary>
	public string ColumnName { get; init; } = string.Empty;

	/// <summary>
	/// Gets the Postgres data type of the column.
	/// </summary>
	public string DataType { get; init; } = string.Empty;

	/// <summary>
	/// Gets the value before the change (null for inserts).
	/// </summary>
	public object? OldValue { get; init; }

	/// <summary>
	/// Gets the value after the change (null for deletes).
	/// </summary>
	public object? NewValue { get; init; }

	/// <summary>
	/// Gets a value indicating whether this column is part of the primary key.
	/// </summary>
	public bool IsPrimaryKey { get; init; }

	/// <summary>
	/// Gets a value indicating whether the value was actually changed (for updates).
	/// </summary>
	public bool HasChanged => !Equals(OldValue, NewValue);
}
