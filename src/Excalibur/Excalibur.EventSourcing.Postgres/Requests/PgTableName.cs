// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Validation;

namespace Excalibur.EventSourcing.Postgres.Requests;

/// <summary>
/// Formats validated Postgres schema and table names into double-quoted qualified identifiers.
/// </summary>
internal static class PgTableName
{
	/// <summary>
	/// Formats a schema and table name into a double-quoted qualified identifier.
	/// Both identifiers are validated via <see cref="SqlIdentifierValidator"/> before formatting.
	/// </summary>
	/// <param name="schema">The schema name (e.g., "public").</param>
	/// <param name="table">The table name (e.g., "events").</param>
	/// <returns>A qualified identifier such as <c>"public"."events"</c>.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="schema"/> or <paramref name="table"/> contains invalid characters.
	/// </exception>
	internal static string Format(string schema, string table)
	{
		SqlIdentifierValidator.ThrowIfInvalid(schema, nameof(schema));
		SqlIdentifierValidator.ThrowIfInvalid(table, nameof(table));
		return $"\"{schema}\".\"{table}\"";
	}
}
