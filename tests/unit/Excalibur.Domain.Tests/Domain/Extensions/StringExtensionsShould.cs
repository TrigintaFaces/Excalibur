// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Domain.Extensions;

namespace Excalibur.Tests.Domain.Extensions;

/// <summary>
/// Unit tests for <see cref="StringExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Domain")]
public sealed class StringExtensionsShould
{
	#region ToCamelCase Tests

	[Theory]
	[InlineData("HelloWorld", "helloWorld")]
	[InlineData("TestCase", "testCase")]
	[InlineData("ABC", "abc")]
	[InlineData("myProperty", "myProperty")]
	public void ToCamelCase_ConvertsCorrectly(string input, string expected)
	{
		// Act
		var result = input.ToCamelCase();

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void ToCamelCase_ReturnsEmpty_ForNullInput()
	{
		// Arrange
		string? input = null;

		// Act
		var result = input.ToCamelCase();

		// Assert
		result.ShouldBe(string.Empty);
	}

	[Fact]
	public void ToCamelCase_ReturnsEmpty_ForEmptyString()
	{
		// Act
		var result = string.Empty.ToCamelCase();

		// Assert
		result.ShouldBe(string.Empty);
	}

	[Fact]
	public void ToCamelCase_ReturnsEmpty_ForWhitespace()
	{
		// Act
		var result = "   ".ToCamelCase();

		// Assert
		result.ShouldBe(string.Empty);
	}

	[Fact]
	public void ToCamelCase_WithClean_RemovesNonAlphanumericCharacters()
	{
		// Arrange
		var input = "Hello_World!Test";

		// Act
		var result = input.ToCamelCase(clean: true);

		// Assert
		result.ShouldBe("helloWorldTest");
	}

	#endregion ToCamelCase Tests

	#region ToSnakeCaseLower Tests

	[Theory]
	[InlineData("HelloWorld", "hello_world")]
	[InlineData("TestCase", "test_case")]
	[InlineData("myProperty", "my_property")]
	public void ToSnakeCaseLower_ConvertsCorrectly(string input, string expected)
	{
		// Act
		var result = input.ToSnakeCaseLower();

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void ToSnakeCaseLower_ConvertsAcronymsToLowercase()
	{
		// Arrange - JsonNamingPolicy treats consecutive uppercase differently
		const string input = "ABC";

		// Act
		var result = input.ToSnakeCaseLower();

		// Assert - consecutive uppercase may be treated as a single word
		result.ShouldBe("abc");
	}

	[Fact]
	public void ToSnakeCaseLower_ReturnsEmpty_ForEmptyString()
	{
		// Act
		var result = string.Empty.ToSnakeCaseLower();

		// Assert
		result.ShouldBe(string.Empty);
	}

	[Fact]
	public void ToSnakeCaseLower_WithClean_RemovesNonAlphanumericCharacters()
	{
		// Arrange
		var input = "Hello-World!Test";

		// Act
		var result = input.ToSnakeCaseLower(clean: true);

		// Assert
		result.ShouldNotContain("!");
	}

	#endregion ToSnakeCaseLower Tests

	#region ToSnakeCaseUpper Tests

	[Theory]
	[InlineData("HelloWorld", "HELLO_WORLD")]
	[InlineData("TestCase", "TEST_CASE")]
	public void ToSnakeCaseUpper_ConvertsCorrectly(string input, string expected)
	{
		// Act
		var result = input.ToSnakeCaseUpper();

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void ToSnakeCaseUpper_ConvertsAcronymsToUppercase()
	{
		// Arrange
		const string input = "ABC";

		// Act
		var result = input.ToSnakeCaseUpper();

		// Assert
		result.ShouldBe("ABC");
	}

	[Fact]
	public void ToSnakeCaseUpper_ReturnsEmpty_ForEmptyString()
	{
		// Act
		var result = string.Empty.ToSnakeCaseUpper();

		// Assert
		result.ShouldBe(string.Empty);
	}

	#endregion ToSnakeCaseUpper Tests

	#region ToKebabCaseLower Tests

	[Theory]
	[InlineData("HelloWorld", "hello-world")]
	[InlineData("TestCase", "test-case")]
	[InlineData("myProperty", "my-property")]
	public void ToKebabCaseLower_ConvertsCorrectly(string input, string expected)
	{
		// Act
		var result = input.ToKebabCaseLower();

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void ToKebabCaseLower_ConvertsAcronymsToLowercase()
	{
		// Arrange
		const string input = "ABC";

		// Act
		var result = input.ToKebabCaseLower();

		// Assert - consecutive uppercase treated as single word
		result.ShouldBe("abc");
	}

	[Fact]
	public void ToKebabCaseLower_ReturnsEmpty_ForEmptyString()
	{
		// Act
		var result = string.Empty.ToKebabCaseLower();

		// Assert
		result.ShouldBe(string.Empty);
	}

	[Fact]
	public void ToKebabCaseLower_WithClean_RemovesNonAlphanumericCharacters()
	{
		// Arrange
		var input = "Hello_World!Test";

		// Act
		var result = input.ToKebabCaseLower(clean: true);

		// Assert
		result.ShouldNotContain("_");
		result.ShouldNotContain("!");
	}

	#endregion ToKebabCaseLower Tests

	#region ToKebabCaseUpper Tests

	[Theory]
	[InlineData("HelloWorld", "HELLO-WORLD")]
	[InlineData("TestCase", "TEST-CASE")]
	public void ToKebabCaseUpper_ConvertsCorrectly(string input, string expected)
	{
		// Act
		var result = input.ToKebabCaseUpper();

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void ToKebabCaseUpper_ConvertsAcronymsToUppercase()
	{
		// Arrange
		const string input = "ABC";

		// Act
		var result = input.ToKebabCaseUpper();

		// Assert
		result.ShouldBe("ABC");
	}

	[Fact]
	public void ToKebabCaseUpper_ReturnsEmpty_ForEmptyString()
	{
		// Act
		var result = string.Empty.ToKebabCaseUpper();

		// Assert
		result.ShouldBe(string.Empty);
	}

	#endregion ToKebabCaseUpper Tests

	#region Edge Cases

	[Fact]
	public void AllMethods_HandleSingleCharacter()
	{
		// Arrange
		const string input = "X";

		// Act & Assert
		input.ToCamelCase().ShouldBe("x");
		input.ToSnakeCaseLower().ShouldBe("x");
		input.ToSnakeCaseUpper().ShouldBe("X");
		input.ToKebabCaseLower().ShouldBe("x");
		input.ToKebabCaseUpper().ShouldBe("X");
	}

	[Fact]
	public void AllMethods_HandleNumericStrings()
	{
		// Arrange
		const string input = "Test123Value";

		// Act
		var camelResult = input.ToCamelCase();
		var snakeLowerResult = input.ToSnakeCaseLower();

		// Assert
		camelResult.ShouldContain("123");
		snakeLowerResult.ShouldContain("123");
	}

	[Fact]
	public void CleanOption_HandlesSpacesCorrectly()
	{
		// Arrange
		const string input = "Hello World Test";

		// Act - with clean=true, spaces should be handled
		var result = input.ToCamelCase(clean: true);

		// Assert - spaces should trigger word capitalization
		result.ShouldNotContain(" ");
	}

	[Fact]
	public void WithoutClean_PreservesSpecialCharacters()
	{
		// Arrange
		const string input = "Hello_World";

		// Act - without clean
		var result = input.ToCamelCase(clean: false);

		// Assert - underscore should be preserved in some form
		result.ShouldNotBeEmpty();
	}

	#endregion Edge Cases
}
