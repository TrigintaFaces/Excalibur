// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.RegularExpressions;

namespace Excalibur.Data.Abstractions.Validation;

/// <summary>
/// Provides centralized SQL identifier validation to prevent SQL injection
/// in dynamic SQL constructs such as savepoint names, CDC capture instances,
/// table names, and schema names.
/// </summary>
/// <remarks>
/// <para>
/// All SQL identifiers that are interpolated into SQL strings (not parameterized)
/// MUST be validated through this class before use.
/// </para>
/// <para>
/// The validation uses a strict allowlist pattern: only ASCII letters, digits,
/// and underscores are permitted. This matches SQL Server's rules for regular
/// identifiers without delimiters.
/// </para>
/// <para>
/// Uses <c>[GeneratedRegex]</c> for AOT compatibility (no <c>RegexOptions.Compiled</c>).
/// </para>
/// </remarks>
public static partial class SqlIdentifierValidator
{
	/// <summary>
	/// Validates that a SQL identifier contains only safe characters (ASCII letters, digits, underscores).
	/// Throws <see cref="ArgumentException"/> if the identifier is invalid.
	/// </summary>
	/// <param name="identifier">The SQL identifier to validate.</param>
	/// <param name="parameterName">
	/// The name of the parameter being validated, used in the exception message.
	/// </param>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="identifier"/> contains characters other than
	/// ASCII letters, digits, or underscores.
	/// </exception>
	/// <example>
	/// <code>
	/// SqlIdentifierValidator.ThrowIfInvalid(tableName, nameof(tableName));
	/// var sql = $"SELECT * FROM [{tableName}]";
	/// </code>
	/// </example>
	public static void ThrowIfInvalid(string identifier, string parameterName)
	{
		if (!ValidIdentifierPattern().IsMatch(identifier))
		{
			throw new ArgumentException(
				$"SQL identifier contains invalid characters. Only alphanumeric characters and underscores are allowed: '{identifier}'",
				parameterName);
		}
	}

	/// <summary>
	/// Returns whether the specified string is a valid SQL identifier
	/// (contains only ASCII letters, digits, and underscores).
	/// </summary>
	/// <param name="identifier">The string to test.</param>
	/// <returns>
	/// <see langword="true"/> if <paramref name="identifier"/> matches the allowlist pattern;
	/// otherwise, <see langword="false"/>.
	/// </returns>
	public static bool IsValid(string identifier) => ValidIdentifierPattern().IsMatch(identifier);

	/// <summary>
	/// AOT-safe generated regex matching valid SQL identifiers: one or more ASCII letters, digits, or underscores.
	/// </summary>
	[GeneratedRegex(@"^[a-zA-Z0-9_]+$")]
	private static partial Regex ValidIdentifierPattern();
}
