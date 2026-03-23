// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.RegularExpressions;

namespace Excalibur.Saga.SqlServer;

/// <summary>
/// Defense-in-depth SQL identifier validation for saga request types.
/// Validates that a qualified table name matches the expected <c>[schema].[table]</c> format.
/// </summary>
internal static partial class SagaSqlValidator
{
	/// <summary>
	/// Throws <see cref="ArgumentException"/> if the qualified table name does not match
	/// the expected <c>[schema].[table]</c> format with safe identifiers.
	/// </summary>
	/// <param name="qualifiedTableName">The qualified table name to validate.</param>
	internal static void ThrowIfInvalidQualifiedName(string qualifiedTableName)
	{
		if (!QualifiedNamePattern().IsMatch(qualifiedTableName))
		{
			throw new ArgumentException(
				$"Qualified table name does not match expected [schema].[table] format: '{qualifiedTableName}'",
				nameof(qualifiedTableName));
		}
	}

	/// <summary>
	/// Matches <c>[word].[word]</c> where word is alphanumeric + underscore.
	/// </summary>
	[GeneratedRegex(@"^\[\w+\]\.\[\w+\]$")]
	private static partial Regex QualifiedNamePattern();
}
