// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.RegularExpressions;

namespace Excalibur.Saga.Postgres;

/// <summary>
/// Defense-in-depth SQL identifier validation for Postgres saga request types.
/// Validates that a qualified table name matches the expected <c>"schema"."table"</c> format.
/// </summary>
/// <remarks>
/// The Postgres analogue of <c>Excalibur.Saga.SqlServer.SagaSqlValidator</c> (bd-r5r7fe parity). The
/// qualified name is composed from config-sourced <c>Schema</c>/<c>TableName</c>
/// (<see cref="PostgresSagaOptions.QualifiedTableName"/>) and interpolated into request SQL, so this guard
/// is a defense-in-depth backstop ensuring those identifiers are well-formed word characters only.
/// Postgres quotes identifiers with double quotes (<c>"schema"."table"</c>), unlike SQL Server's
/// bracket form (<c>[schema].[table]</c>) — hence a provider-specific validator rather than a shared one.
/// </remarks>
internal static partial class SagaSqlValidator
{
	/// <summary>
	/// Throws <see cref="ArgumentException"/> if the qualified table name does not match
	/// the expected <c>"schema"."table"</c> format with safe identifiers.
	/// </summary>
	/// <param name="qualifiedTableName">The qualified table name to validate.</param>
	internal static void ThrowIfInvalidQualifiedName(string qualifiedTableName)
	{
		if (!QualifiedNamePattern().IsMatch(qualifiedTableName))
		{
			throw new ArgumentException(
				$"Qualified table name does not match expected \"schema\".\"table\" format: '{qualifiedTableName}'",
				nameof(qualifiedTableName));
		}
	}

	/// <summary>
	/// Matches <c>"word"."word"</c> where word is alphanumeric + underscore.
	/// </summary>
	[GeneratedRegex("""^"\w+"\."\w+"$""")]
	private static partial Regex QualifiedNamePattern();
}
