// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Cdc.InMemory;

/// <summary>
/// Represents a simulated CDC change record for testing scenarios.
/// </summary>
/// <remarks>
/// <para>
/// Use this class to simulate database changes in unit and integration tests
/// without requiring a real database connection.
/// </para>
/// </remarks>
public sealed class InMemoryCdcChange
{
	/// <summary>
	/// Gets or sets the table name where the change occurred.
	/// </summary>
	/// <value>The fully qualified table name (e.g., "dbo.Orders").</value>
	public string TableName { get; init; } = string.Empty;

	/// <summary>
	/// Gets or sets the type of change.
	/// </summary>
	/// <value>The change type (Insert, Update, or Delete).</value>
	public CdcChangeType ChangeType { get; init; }

	/// <summary>
	/// Gets or sets the timestamp when the change occurred.
	/// </summary>
	/// <value>The change timestamp. Default is current UTC time.</value>
	public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets or sets the column changes in this CDC record.
	/// </summary>
	/// <value>The collection of column changes.</value>
	public IReadOnlyList<CdcDataChange> Changes { get; init; } = [];

	/// <summary>
	/// Gets or sets optional metadata associated with the change.
	/// </summary>
	/// <value>The metadata dictionary.</value>
	public IReadOnlyDictionary<string, object?>? Metadata { get; init; }

	/// <summary>
	/// Creates a new INSERT change record.
	/// </summary>
	/// <param name="tableName">The table name.</param>
	/// <param name="changes">The column values being inserted.</param>
	/// <returns>A new <see cref="InMemoryCdcChange"/> representing an insert.</returns>
	public static InMemoryCdcChange Insert(string tableName, params CdcDataChange[] changes) =>
		new()
		{
			TableName = tableName,
			ChangeType = CdcChangeType.Insert,
			Changes = changes
		};

	/// <summary>
	/// Creates a new UPDATE change record.
	/// </summary>
	/// <param name="tableName">The table name.</param>
	/// <param name="changes">The column changes (old and new values).</param>
	/// <returns>A new <see cref="InMemoryCdcChange"/> representing an update.</returns>
	public static InMemoryCdcChange Update(string tableName, params CdcDataChange[] changes) =>
		new()
		{
			TableName = tableName,
			ChangeType = CdcChangeType.Update,
			Changes = changes
		};

	/// <summary>
	/// Creates a new DELETE change record.
	/// </summary>
	/// <param name="tableName">The table name.</param>
	/// <param name="changes">The column values being deleted.</param>
	/// <returns>A new <see cref="InMemoryCdcChange"/> representing a delete.</returns>
	public static InMemoryCdcChange Delete(string tableName, params CdcDataChange[] changes) =>
		new()
		{
			TableName = tableName,
			ChangeType = CdcChangeType.Delete,
			Changes = changes
		};

	/// <inheritdoc/>
	public override string ToString() =>
		$"{ChangeType} on {TableName} at {Timestamp:O} ({Changes.Count} columns)";
}
