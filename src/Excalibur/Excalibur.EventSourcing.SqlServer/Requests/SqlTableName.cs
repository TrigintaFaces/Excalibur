// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Validation;

namespace Excalibur.EventSourcing.SqlServer.Requests;

/// <summary>
/// Formats validated SQL Server schema and table names into bracket-escaped qualified identifiers.
/// </summary>
internal static class SqlTableName
{
	/// <summary>
	/// Formats a schema and table name into a bracket-escaped qualified identifier.
	/// Both identifiers are validated via <see cref="SqlIdentifierValidator"/> before formatting.
	/// </summary>
	/// <param name="schema">The schema name (e.g., "dbo").</param>
	/// <param name="table">The table name (e.g., "EventStoreEvents").</param>
	/// <returns>A qualified identifier such as <c>[dbo].[EventStoreEvents]</c>.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="schema"/> or <paramref name="table"/> contains invalid characters.
	/// </exception>
	internal static string Format(string schema, string table)
	{
		SqlIdentifierValidator.ThrowIfInvalid(schema, nameof(schema));
		SqlIdentifierValidator.ThrowIfInvalid(table, nameof(table));
		return $"[{schema}].[{table}]";
	}
}
