// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.SqlServer;

namespace Excalibur.Data.Tests.Saga;

/// <summary>
/// Author≠impl security regression lock for the SqlServer saga <see cref="SagaSqlValidator"/>: a
/// defense-in-depth SQL-injection guard over the config-sourced <c>[schema].[table]</c> identifier that is
/// interpolated into saga request SQL. It MUST reject any qualified name that is not a pair of
/// bracket-quoted word-character identifiers, and accept a well-formed one.
/// </summary>
/// <remarks>
/// <para>
/// The SqlServer analogue of the Postgres <c>SagaSqlValidatorShould</c> lock. SQL Server quotes
/// identifiers with brackets (<c>[schema].[table]</c>), unlike Postgres' double-quote form
/// (<c>"schema"."table"</c>) — hence a provider-specific validator and lock. Backstops the
/// <c>Schema</c>/<c>TableName</c> options that flow into <c>LoadSagaRequest</c>/<c>SaveSagaRequest</c> SQL.
/// </para>
/// <para>
/// <b>Non-vacuity:</b> RED-proven by mutating the validator regex (<c>^\[\w+\]\.\[\w+\]$</c>) to
/// accept-all (cp-backup) → the reject cases fail; GREEN on the shipped pattern.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class SagaSqlValidatorShould
{
	[Theory]
	[InlineData("[public].[sagas]")]
	[InlineData("[schema_1].[table_2]")]
	[InlineData("[MySchema].[Saga_State]")]
	public void AcceptWellFormedBracketQualifiedNames(string qualifiedName) =>
		Should.NotThrow(() => SagaSqlValidator.ThrowIfInvalidQualifiedName(qualifiedName));

	[Theory]
	// SQL-injection payloads
	[InlineData("[schema];DROP TABLE sagas;--")]
	[InlineData("[schema].[table]; DROP TABLE sagas")]
	[InlineData("[; DROP TABLE sagas--].[x]")]
	// Unquoted / partially-bracketed identifiers (bare/quoted forms must not pass)
	[InlineData("public.sagas")]
	[InlineData("[schema].table")]
	[InlineData("schema.[table]")]
	[InlineData("\"schema\".\"table\"")]
	// Malformed: unbalanced brackets, embedded whitespace/specials, missing parts
	[InlineData("[schema].[table")]
	[InlineData("[schema].[ta ble]")]
	[InlineData("[schema]")]
	[InlineData("[schema].[table].[extra]")]
	[InlineData("")]
	[InlineData("[].[]")]
	public void RejectMaliciousOrMalformedQualifiedNames(string qualifiedName) =>
		_ = Should.Throw<ArgumentException>(() => SagaSqlValidator.ThrowIfInvalidQualifiedName(qualifiedName));
}
