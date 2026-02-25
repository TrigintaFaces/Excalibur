// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Data.Abstractions.Validation;
using Excalibur.Data.SqlServer.Cdc;

namespace Excalibur.Data.Tests.SqlServer.Cdc;

/// <summary>
/// Tests verifying SQL identifier validation used by CdcRepository (S543.8, bd-4y6t3).
/// Validates that the centralized SqlIdentifierValidator rejects malicious input and accepts valid names.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data.SqlServer")]
[Trait("Feature", "CDC")]
public sealed class CdcRepositoryValidationShould : UnitTestBase
{
	private static readonly MethodInfo NormalizeCaptureInstanceForSqlMethod = typeof(CdcRepository)
		.GetMethod("NormalizeCaptureInstanceForSql", BindingFlags.NonPublic | BindingFlags.Static)
		?? throw new InvalidOperationException("Expected private normalization helper on CdcRepository.");

	#region Whitelist Regex Tests

	[Theory]
	[InlineData("ValidTable")]
	[InlineData("valid_table")]
	[InlineData("Table123")]
	[InlineData("_underscore")]
	[InlineData("a")]
	[InlineData("ABC")]
	public void AcceptValidIdentifiers(string identifier)
	{
		// Assert — centralized validator accepts valid CDC identifiers
		SqlIdentifierValidator.IsValid(identifier).ShouldBeTrue(
			$"'{identifier}' should be a valid CDC identifier");
	}

	[Theory]
	[InlineData(";DROP TABLE Users")]
	[InlineData("'OR 1=1--")]
	[InlineData("table; DELETE FROM")]
	[InlineData("name WITH (NOLOCK)")]
	[InlineData("table\nname")]
	[InlineData("table name")]
	[InlineData("table-name")]
	[InlineData("table.name")]
	[InlineData("[brackets]")]
	[InlineData("")]
	[InlineData("schema.table")]
	public void RejectInvalidIdentifiers(string identifier)
	{
		// Assert — centralized validator rejects invalid CDC identifiers
		SqlIdentifierValidator.IsValid(identifier).ShouldBeFalse(
			$"'{identifier}' should be rejected as a CDC identifier");
	}

	#endregion

	#region ThrowIfInvalid Tests

	[Theory]
	[InlineData(";DROP TABLE Users")]
	[InlineData("' OR 1=1 --")]
	[InlineData("table\t\nname")]
	[InlineData("schema.table")]
	[InlineData("[dbo]")]
	public void ThrowIfInvalid_RejectsInvalidInput(string maliciousInput)
	{
		// Act & Assert — centralized validator throws ArgumentException for invalid identifiers
		var ex = Should.Throw<ArgumentException>(
			() => SqlIdentifierValidator.ThrowIfInvalid(maliciousInput, "testParam"));
		ex.Message.ShouldContain("invalid characters");
		ex.ParamName.ShouldBe("testParam");
	}

	[Theory]
	[InlineData("ValidCapture")]
	[InlineData("capture_instance_1")]
	[InlineData("dbo_Orders")]
	public void ThrowIfInvalid_AcceptsValidInput(string validInput)
	{
		// Act & Assert — should not throw for valid identifiers
		Should.NotThrow(() => SqlIdentifierValidator.ThrowIfInvalid(validInput, "testParam"));
	}

	#endregion

	#region Capture Instance Normalization

	[Theory]
	[InlineData("dbo.Orders", "dbo_Orders")]
	[InlineData("sales.InvoiceItems", "sales_InvoiceItems")]
	[InlineData("dbo_Orders", "dbo_Orders")]
	public void NormalizeCaptureInstanceForSql_ConvertsValidDottedTableNames(string input, string expected)
	{
		NormalizeCaptureInstanceForSql(input).ShouldBe(expected);
	}

	[Theory]
	[InlineData("dbo..Orders")]
	[InlineData(".Orders")]
	[InlineData("dbo.")]
	[InlineData("dbo.Order-Items")]
	[InlineData("dbo.Order Items")]
	[InlineData("dbo.orders.extra")]
	public void NormalizeCaptureInstanceForSql_LeavesInvalidDottedNamesUnchanged(string input)
	{
		var normalized = NormalizeCaptureInstanceForSql(input);
		normalized.ShouldBe(input);
		Should.Throw<ArgumentException>(() => SqlIdentifierValidator.ThrowIfInvalid(normalized, "captureInstance"));
	}

	private static string NormalizeCaptureInstanceForSql(string input) =>
		(string?)NormalizeCaptureInstanceForSqlMethod.Invoke(null, [input]) ?? string.Empty;

	#endregion
}
