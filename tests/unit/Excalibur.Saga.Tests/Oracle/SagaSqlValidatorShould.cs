// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Saga.Oracle;

namespace Excalibur.Saga.Tests.Oracle;

/// <summary>
/// Security regression lock for the Oracle saga <see cref="SagaSqlValidator"/>: a defense-in-depth
/// SQL-injection guard over the config-sourced <c>schema.table</c> identifier that is interpolated into
/// saga request SQL. The Oracle analogue of the Postgres and SqlServer locks, which this provider lacked.
/// </summary>
/// <remarks>
/// <b>Non-vacuity:</b> the ASCII arm below is RED against a Unicode-aware <c>\w</c> pattern — .NET's
/// <c>\w</c> matches any Unicode letter, so a Cyrillic or full-width identifier passes a validator whose
/// documented contract is "alphanumeric + underscore". Both arms are required: an over-tightened pattern
/// that rejects a normal ASCII identifier is a worse defect than the one being closed.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SagaSqlValidatorShould
{
	[Theory]
	[InlineData("PUBLIC.SAGAS")]
	[InlineData("schema_1.table_2")]
	[InlineData("MySchema.Saga_State")]
	public void AcceptWellFormedAsciiQualifiedNames(string qualifiedName) =>
		Should.NotThrow(() => SagaSqlValidator.ThrowIfInvalidQualifiedName(qualifiedName));

	[Theory]
	// SQL-injection payloads
	[InlineData("schema;DROP TABLE sagas;--")]
	[InlineData("schema.table; DROP TABLE sagas")]
	// Malformed: embedded whitespace/specials, missing parts, quoting forms Oracle unquoted names disallow
	[InlineData("schema.ta ble")]
	[InlineData("schema")]
	[InlineData("schema.table.extra")]
	[InlineData("\"schema\".\"table\"")]
	[InlineData("[schema].[table]")]
	[InlineData("")]
	[InlineData(".")]
	// Non-ASCII identifiers: the contract is an ASCII allow-list, so a Unicode letter must NOT pass
	[InlineData("схема.таблица")]
	[InlineData("schema.таблица")]
	[InlineData("ｓｃｈｅｍａ.ｔａｂｌｅ")]
	public void RejectMaliciousMalformedOrNonAsciiQualifiedNames(string qualifiedName) =>
		_ = Should.Throw<ArgumentException>(() => SagaSqlValidator.ThrowIfInvalidQualifiedName(qualifiedName));
}
