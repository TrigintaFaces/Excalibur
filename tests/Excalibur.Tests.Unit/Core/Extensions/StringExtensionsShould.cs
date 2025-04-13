using Excalibur.Core.Extensions;

using Shouldly;

namespace Excalibur.Tests.Unit.Core.Extensions;

public class StringExtensionsShould
{
	[Theory]
	[InlineData("Hello World", "helloWorld")]
	[InlineData("hello world", "helloWorld")]
	[InlineData("  handle   spaces  here  ", "handleSpacesHere")]
	[InlineData("SingleWord", "singleWord")]
	[InlineData("  TrailingSpaces  ", "trailingSpaces")]
	[InlineData("", "")]
	[InlineData("   ", "")]
	public void ConvertToCamelCaseHandlesSpacing(string input, string expected)
	{
		input.ToCamelCase().ShouldBe(expected);
	}

	[Theory]
	[InlineData("Hello@#World!!", "helloWorld")]
	[InlineData("  handle!@#spaces   here  ", "handleSpacesHere")]
	[InlineData("Special_Characters$%^", "specialCharacters")]
	[InlineData("This&That", "thisThat")]
	public void ConvertToCamelCaseWithCleaning(string input, string expected)
	{
		input.ToCamelCase(true).ShouldBe(expected);
	}

	[Theory]
	[InlineData("Hello World", "hello_world")]
	[InlineData("hello world", "hello_world")]
	[InlineData("  handle   spaces  here  ", "handle_spaces_here")]
	[InlineData("SingleWord", "single_word")]
	[InlineData("  TrailingSpaces  ", "trailing_spaces")]
	[InlineData("helloWorld", "hello_world")]
	[InlineData("", "")]
	[InlineData("   ", "")]
	public void ConvertToSnakeCaseLowerHandlesSpacing(string input, string expected)
	{
		input.ToSnakeCaseLower().ShouldBe(expected);
	}

	[Theory]
	[InlineData("Hello@#World!!", "hello_world")]
	[InlineData("  handle!@#spaces   here  ", "handle_spaces_here")]
	public void ConvertToSnakeCaseLowerWithCleaning(string input, string expected)
	{
		input.ToSnakeCaseLower(true).ShouldBe(expected);
	}

	[Theory]
	[InlineData("Hello World", "HELLO_WORLD")]
	[InlineData("hello world", "HELLO_WORLD")]
	[InlineData("helloWorld", "HELLO_WORLD")]
	public void ConvertToSnakeCaseUpper(string input, string expected)
	{
		input.ToSnakeCaseUpper().ShouldBe(expected);
	}

	[Theory]
	[InlineData("Hello@#World!!", "HELLO_WORLD")]
	[InlineData("  handle!@#spaces   here  ", "HANDLE_SPACES_HERE")]
	public void ConvertToSnakeCaseUpperWithCleaning(string input, string expected)
	{
		input.ToSnakeCaseUpper(true).ShouldBe(expected);
	}

	[Theory]
	[InlineData("Hello World", "hello-world")]
	[InlineData("hello world", "hello-world")]
	[InlineData("  handle   spaces  here  ", "handle-spaces-here")]
	[InlineData("SingleWord", "single-word")]
	[InlineData("  TrailingSpaces  ", "trailing-spaces")]
	[InlineData("helloWorld", "hello-world")]
	[InlineData("", "")]
	[InlineData("   ", "")]
	public void ConvertToKebabCaseLowerHandlesSpacing(string input, string expected)
	{
		input.ToKebabCaseLower().ShouldBe(expected);
	}

	[Theory]
	[InlineData("Hello@#World!!", "hello-world")]
	[InlineData("  handle!@#spaces   here  ", "handle-spaces-here")]
	public void ConvertToKebabCaseLowerWithCleaning(string input, string expected)
	{
		input.ToKebabCaseLower(true).ShouldBe(expected);
	}

	[Theory]
	[InlineData("Hello World", "HELLO-WORLD")]
	[InlineData("hello world", "HELLO-WORLD")]
	[InlineData("helloWorld", "HELLO-WORLD")]
	public void ConvertToKebabCaseUpper(string input, string expected)
	{
		input.ToKebabCaseUpper().ShouldBe(expected);
	}

	[Theory]
	[InlineData("Hello@#World!!", "HELLO-WORLD")]
	[InlineData("  handle!@#spaces   here  ", "HANDLE-SPACES-HERE")]
	public void ConvertToKebabCaseUpperWithCleaning(string input, string expected)
	{
		input.ToKebabCaseUpper(true).ShouldBe(expected);
	}

	[Theory]
	[InlineData(null, "")]
	[InlineData("", "")]
	[InlineData("   ", "")]
	public void ReturnEmptyStringIfInputIsNullOrWhitespace(string input, string expected)
	{
		input.ToCamelCase().ShouldBe(expected);
	}

	[Theory]
	[InlineData("hello", "hello", "hello", "HELLO", "hello", "HELLO")]
	[InlineData("API Response", "apiResponse", "api_response", "API_RESPONSE", "api-response", "API-RESPONSE")]
	public void PreserveAcronymsAndHandleSingleWords(
		string input, string expectedCamel, string expectedSnakeLower, string expectedSnakeUpper,
		string expectedKebabLower, string expectedKebabUpper)
	{
		input.ToCamelCase().ShouldBe(expectedCamel);
		input.ToSnakeCaseLower().ShouldBe(expectedSnakeLower);
		input.ToSnakeCaseUpper().ShouldBe(expectedSnakeUpper);
		input.ToKebabCaseLower().ShouldBe(expectedKebabLower);
		input.ToKebabCaseUpper().ShouldBe(expectedKebabUpper);
	}
}
