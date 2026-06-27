// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Postgres;

namespace Excalibur.Data.Postgres.Tests.Saga;

/// <summary>
/// Author≠impl security regression lock for bead <c>r5r7fe</c> nit 5 (sprint 855): the Postgres saga
/// <see cref="SagaSqlValidator"/> is a defense-in-depth SQL-injection guard over the config-sourced
/// <c>"schema"."table"</c> identifier that is interpolated into saga request SQL. It MUST reject any
/// qualified name that is not a pair of double-quoted word-character identifiers, and accept a
/// well-formed one.
/// </summary>
/// <remarks>
/// <para>
/// Authored independently of the fix (<c>issue-remediation-protocol</c>). The validator
/// (<c>src/Excalibur/Excalibur.Saga.Postgres/SagaSqlValidator.cs</c>) is the Postgres analogue of the
/// SqlServer guard (bd-r5r7fe parity) and backstops the <c>Schema</c>/<c>TableName</c> options that flow
/// into <c>LoadSagaRequest</c>/<c>SaveSagaRequest</c> SQL.
/// </para>
/// <para>
/// <b>Non-vacuity:</b> the guard did not exist on the pre-fix surface (newly added by r5r7fe) — without
/// it a malicious/malformed qualified name flowed unvalidated into interpolated SQL. RED-proven by
/// mutating the validator regex to accept-all (cp-backup) → the reject cases fail; GREEN on the shipped
/// pattern <c>^"\w+"\."\w+"$</c>.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class SagaSqlValidatorShould
{
	[Theory]
	[InlineData("\"public\".\"sagas\"")]
	[InlineData("\"schema_1\".\"table_2\"")]
	[InlineData("\"MySchema\".\"Saga_State\"")]
	public void AcceptWellFormedQuotedQualifiedNames(string qualifiedName) =>
		Should.NotThrow(() => SagaSqlValidator.ThrowIfInvalidQualifiedName(qualifiedName));

	[Theory]
	// SQL-injection payloads
	[InlineData("\"schema\";DROP TABLE sagas;--")]
	[InlineData("\"schema\".\"table\"; DROP TABLE sagas")]
	[InlineData("\"; DROP TABLE sagas--\".\"x\"")]
	// Unquoted / partially-quoted identifiers (bracket/bare forms must not pass)
	[InlineData("public.sagas")]
	[InlineData("\"schema\".table")]
	[InlineData("schema.\"table\"")]
	[InlineData("[schema].[table]")]
	// Malformed: unbalanced quotes, embedded whitespace/specials, missing parts
	[InlineData("\"schema\".\"table")]
	[InlineData("\"schema\".\"ta ble\"")]
	[InlineData("\"schema\"")]
	[InlineData("\"schema\".\"table\".\"extra\"")]
	[InlineData("")]
	[InlineData("\"\".\"\"")]
	public void RejectMaliciousOrMalformedQualifiedNames(string qualifiedName) =>
		_ = Should.Throw<ArgumentException>(() => SagaSqlValidator.ThrowIfInvalidQualifiedName(qualifiedName));
}
