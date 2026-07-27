// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Validation;

namespace Excalibur.EventSourcing.Oracle.Requests;

/// <summary>
/// Formats validated Oracle schema and table names into double-quote-escaped qualified identifiers.
/// </summary>
/// <remarks>
/// Oracle folds unquoted identifiers to upper case; the stores create their objects unquoted, so a
/// double-quoted upper-case identifier here binds to the same object. Both parts are validated via
/// <see cref="SqlIdentifierValidator"/> before formatting so no untrusted text reaches the SQL text.
/// </remarks>
internal static class OracleTableName
{
	/// <summary>
	/// Formats a schema and table name into a double-quoted qualified identifier such as
	/// <c>"EXCALIBUR"."EVENTSTOREEVENTS"</c>.
	/// </summary>
	/// <param name="schema">The schema name.</param>
	/// <param name="table">The table name.</param>
	/// <returns>A quoted, qualified Oracle identifier.</returns>
	internal static string Format(string schema, string table)
	{
		SqlIdentifierValidator.ThrowIfInvalid(schema, nameof(schema));
		SqlIdentifierValidator.ThrowIfInvalid(table, nameof(table));
		return $"\"{schema.ToUpperInvariant()}\".\"{table.ToUpperInvariant()}\"";
	}
}
