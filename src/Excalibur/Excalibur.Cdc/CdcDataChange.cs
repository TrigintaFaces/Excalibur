// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Cdc;

/// <summary>
/// Represents a change to a specific column in a database row, capturing the old and new values.
/// </summary>
/// <remarks>
/// <para>
/// This is the core abstraction for CDC data changes that is independent of
/// database provider. Provider-specific types may extend this with additional metadata.
/// </para>
/// </remarks>
public sealed class CdcDataChange
{
	/// <summary>
	/// Gets or sets the name of the column where the change occurred.
	/// </summary>
	/// <value>The column name.</value>
	public string ColumnName { get; init; } = string.Empty;

	/// <summary>
	/// Gets or sets the value of the column before the change occurred.
	/// </summary>
	/// <value>
	/// The old value, or <see langword="null"/> for INSERT operations.
	/// </value>
	public object? OldValue { get; init; }

	/// <summary>
	/// Gets or sets the value of the column after the change occurred.
	/// </summary>
	/// <value>
	/// The new value, or <see langword="null"/> for DELETE operations.
	/// </value>
	public object? NewValue { get; init; }

	/// <summary>
	/// Gets or sets the data type of the column.
	/// </summary>
	/// <value>The .NET type of the column.</value>
	public Type? DataType { get; init; }

	/// <summary>
	/// Returns a string representation of the data change.
	/// </summary>
	/// <returns>A string showing the column name and value transition.</returns>
	public override string ToString() =>
		$"{ColumnName}: {OldValue} → {NewValue} (Type: {DataType?.Name ?? "Unknown"})";
}
