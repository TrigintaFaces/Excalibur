// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Cdc;

/// <summary>
/// Specifies the type of change captured by CDC.
/// </summary>
/// <remarks>
/// <para>
/// This is the canonical change type enum shared by all CDC providers.
/// Common operations (Insert, Update, Delete) use the same numeric values
/// across all providers. Provider-specific operations (Replace, Truncate,
/// Invalidate, Drop, etc.) are included for completeness.
/// </para>
/// </remarks>
public enum CdcChangeType
{
	/// <summary>
	/// No change type specified, or the change type is unknown.
	/// </summary>
	None = 0,

	/// <summary>
	/// A new row/document was inserted or added.
	/// </summary>
	Insert = 1,

	/// <summary>
	/// An existing row/document was updated or modified.
	/// </summary>
	Update = 2,

	/// <summary>
	/// An existing row/document was deleted or removed.
	/// </summary>
	Delete = 3,

	/// <summary>
	/// A document was replaced (full document update, distinct from partial update).
	/// </summary>
	/// <remarks>Used by MongoDB and CosmosDB change feeds.</remarks>
	Replace = 4,

	/// <summary>
	/// A table was truncated (all rows removed).
	/// </summary>
	/// <remarks>Used by Postgres logical replication.</remarks>
	Truncate = 5,

	/// <summary>
	/// The change stream was invalidated (e.g., collection dropped or renamed).
	/// </summary>
	/// <remarks>Used by MongoDB change streams.</remarks>
	Invalidate = 6,

	/// <summary>
	/// A collection or table was dropped.
	/// </summary>
	/// <remarks>Used by MongoDB change streams.</remarks>
	Drop = 7,

	/// <summary>
	/// A database was dropped.
	/// </summary>
	/// <remarks>Used by MongoDB change streams.</remarks>
	DropDatabase = 8,

	/// <summary>
	/// A collection was renamed.
	/// </summary>
	/// <remarks>Used by MongoDB change streams.</remarks>
	Rename = 9,
}
