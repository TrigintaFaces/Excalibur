// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Security.Tests.Compliance.Encryption;

/// <summary>
/// Unit tests for <see cref="DecryptionExportFormat"/> enum.
/// </summary>
/// <remarks>
/// Per AD-255-3, these tests verify the export format enum values for bulk decryption.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Compliance")]
public sealed class DecryptionExportFormatShould
{
	#region Enum Value Tests

	[Fact]
	public void HaveJsonAsDefaultValue()
	{
		// Arrange
		var defaultFormat = default(DecryptionExportFormat);

		// Assert
		defaultFormat.ShouldBe(DecryptionExportFormat.Json);
	}

	[Fact]
	public void HaveJsonValueZero()
	{
		// Arrange & Act
		var value = (int)DecryptionExportFormat.Json;

		// Assert
		value.ShouldBe(0);
	}

	[Fact]
	public void HaveCsvValueOne()
	{
		// Arrange & Act
		var value = (int)DecryptionExportFormat.Csv;

		// Assert
		value.ShouldBe(1);
	}

	[Fact]
	public void HavePlaintextValueTwo()
	{
		// Arrange & Act
		var value = (int)DecryptionExportFormat.Plaintext;

		// Assert
		value.ShouldBe(2);
	}

	[Fact]
	public void HaveExactlyThreeValues()
	{
		// Arrange & Act
		var values = Enum.GetValues<DecryptionExportFormat>();

		// Assert
		values.Length.ShouldBe(3);
	}

	#endregion Enum Value Tests

	#region Enum Name Tests

	[Theory]
	[InlineData(DecryptionExportFormat.Json, "Json")]
	[InlineData(DecryptionExportFormat.Csv, "Csv")]
	[InlineData(DecryptionExportFormat.Plaintext, "Plaintext")]
	public void HaveCorrectNameForValue(DecryptionExportFormat format, string expectedName)
	{
		// Arrange & Act
		var name = format.ToString();

		// Assert
		name.ShouldBe(expectedName);
	}

	#endregion Enum Name Tests

	#region Parse Tests

	[Theory]
	[InlineData("Json", DecryptionExportFormat.Json)]
	[InlineData("Csv", DecryptionExportFormat.Csv)]
	[InlineData("Plaintext", DecryptionExportFormat.Plaintext)]
	public void ParseFromString(string name, DecryptionExportFormat expected)
	{
		// Arrange & Act
		var parsed = Enum.Parse<DecryptionExportFormat>(name);

		// Assert
		parsed.ShouldBe(expected);
	}

	[Theory]
	[InlineData("0", DecryptionExportFormat.Json)]
	[InlineData("1", DecryptionExportFormat.Csv)]
	[InlineData("2", DecryptionExportFormat.Plaintext)]
	public void ParseFromNumericString(string numericValue, DecryptionExportFormat expected)
	{
		// Arrange & Act
		var parsed = Enum.Parse<DecryptionExportFormat>(numericValue);

		// Assert
		parsed.ShouldBe(expected);
	}

	[Fact]
	public void FailToParseInvalidName()
	{
		// Arrange & Act & Assert
		_ = Should.Throw<ArgumentException>(() => Enum.Parse<DecryptionExportFormat>("InvalidFormat"));
	}

	#endregion Parse Tests

	#region TryParse Tests

	[Theory]
	[InlineData("Json", true)]
	[InlineData("Csv", true)]
	[InlineData("Plaintext", true)]
	[InlineData("XML", false)]
	[InlineData("", false)]
	public void TryParseReturnsExpectedResult(string input, bool expectedResult)
	{
		// Arrange & Act
		var result = Enum.TryParse<DecryptionExportFormat>(input, out _);

		// Assert
		result.ShouldBe(expectedResult);
	}

	#endregion TryParse Tests

	#region IsDefined Tests

	[Theory]
	[InlineData(0, true)]
	[InlineData(1, true)]
	[InlineData(2, true)]
	[InlineData(3, false)]
	[InlineData(-1, false)]
	[InlineData(100, false)]
	public void IsDefinedForValue(int value, bool expectedDefined)
	{
		// Arrange & Act
		var isDefined = Enum.IsDefined(typeof(DecryptionExportFormat), value);

		// Assert
		isDefined.ShouldBe(expectedDefined);
	}

	#endregion IsDefined Tests

	#region Format Semantics Tests

	[Fact]
	public void Json_IsDefaultForStructuredData()
	{
		// Per AD-255-3: JSON is the default for GDPR exports
		var format = DecryptionExportFormat.Json;

		format.ShouldBe(default(DecryptionExportFormat));
	}

	[Fact]
	public void Csv_IsForSpreadsheetCompatibility()
	{
		// Per AD-255-3: CSV for spreadsheet/legacy system exports
		var format = DecryptionExportFormat.Csv;

		format.ShouldNotBe(DecryptionExportFormat.Json);
		format.ShouldNotBe(DecryptionExportFormat.Plaintext);
	}

	[Fact]
	public void Plaintext_IsForSimpleOutput()
	{
		// Per AD-255-3: Plaintext for simple one-record-per-line output
		var format = DecryptionExportFormat.Plaintext;

		format.ShouldNotBe(DecryptionExportFormat.Json);
		format.ShouldNotBe(DecryptionExportFormat.Csv);
	}

	#endregion Format Semantics Tests
}
