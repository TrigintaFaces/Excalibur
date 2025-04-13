using System.Globalization;
using System.Text.Json;

namespace Excalibur.Core.Extensions;

/// <summary>
///     Provides extension methods for string transformations into various naming conventions.
/// </summary>
public static class StringExtensions
{
	/// <summary>
	///     Converts a string to camelCase format.
	/// </summary>
	/// <param name="str"> The input string to be converted. </param>
	/// <param name="clean">
	///     A boolean value indicating whether to remove non-alphanumeric characters before conversion. Defaults to <c> false </c>.
	/// </param>
	/// <returns> The string in camelCase format. </returns>
	public static string ToCamelCase(this string str, bool clean = false)
	{
		var processedString = GetProcessedString(str, clean);
		return JsonNamingPolicy.CamelCase.ConvertName(processedString);
	}

	/// <summary>
	///     Converts a string to snake_case format with all lowercase characters.
	/// </summary>
	/// <param name="str"> The input string to be converted. </param>
	/// <param name="clean">
	///     A boolean value indicating whether to remove non-alphanumeric characters before conversion. Defaults to <c> false </c>.
	/// </param>
	/// <returns> The string in snake_case format with lowercase characters. </returns>
	public static string ToSnakeCaseLower(this string str, bool clean = false)
	{
		var processedString = GetProcessedString(str, clean, false);
		return JsonNamingPolicy.SnakeCaseLower.ConvertName(processedString);
	}

	/// <summary>
	///     Converts a string to SNAKE_CASE format with all uppercase characters.
	/// </summary>
	/// <param name="str"> The input string to be converted. </param>
	/// <param name="clean">
	///     A boolean value indicating whether to remove non-alphanumeric characters before conversion. Defaults to <c> false </c>.
	/// </param>
	/// <returns> The string in SNAKE_CASE format with uppercase characters. </returns>
	public static string ToSnakeCaseUpper(this string str, bool clean = false)
	{
		var processedString = GetProcessedString(str, clean, false);
		return JsonNamingPolicy.SnakeCaseUpper.ConvertName(processedString);
	}

	/// <summary>
	///     Converts a string to kebab-case format with all lowercase characters.
	/// </summary>
	/// <param name="str"> The input string to be converted. </param>
	/// <param name="clean">
	///     A boolean value indicating whether to remove non-alphanumeric characters before conversion. Defaults to <c> false </c>.
	/// </param>
	/// <returns> The string in kebab-case format with lowercase characters. </returns>
	public static string ToKebabCaseLower(this string str, bool clean = false)
	{
		var processedString = GetProcessedString(str, clean, false);
		return JsonNamingPolicy.KebabCaseLower.ConvertName(processedString);
	}

	/// <summary>
	///     Converts a string to KEBAB-CASE format with all uppercase characters.
	/// </summary>
	/// <param name="str"> The input string to be converted. </param>
	/// <param name="clean">
	///     A boolean value indicating whether to remove non-alphanumeric characters before conversion. Defaults to <c> false </c>.
	/// </param>
	/// <returns> The string in KEBAB-CASE format with uppercase characters. </returns>
	public static string ToKebabCaseUpper(this string str, bool clean = false)
	{
		var processedString = GetProcessedString(str, clean, false);
		return JsonNamingPolicy.KebabCaseUpper.ConvertName(processedString);
	}

	/// <summary>
	///     Processes the input string by optionally removing non-alphanumeric characters.
	/// </summary>
	/// <param name="str"> The input string to process. </param>
	/// <param name="clean"> A boolean value indicating whether to remove non-alphanumeric characters. Defaults to <c> false </c>. </param>
	/// <param name="removeSpaces"> A boolean value indicating whether to remove spaces. Defaults to <c> false </c>. </param>
	/// <returns> The processed string with optional character cleanup applied. </returns>
	private static string GetProcessedString(string str, bool clean, bool removeSpaces = true)
	{
		if (string.IsNullOrWhiteSpace(str))
		{
			return string.Empty;
		}

		var span = str.AsSpan();
		Span<char> buffer = stackalloc char[span.Length];
		var bufferIndex = 0;
		var newWord = false;

		foreach (var c in span)
		{
			if ((!removeSpaces || !char.IsWhiteSpace(c)) && (!clean || char.IsLetterOrDigit(c)))
			{
				buffer[bufferIndex++] = newWord ? char.ToUpper(c, CultureInfo.CurrentCulture) : c;
			}

			newWord = char.IsWhiteSpace(c) || (clean && !char.IsLetterOrDigit(c));
		}

		return new string(buffer[..bufferIndex]);
	}
}
