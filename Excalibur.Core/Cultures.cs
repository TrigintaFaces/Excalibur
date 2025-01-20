using System.Globalization;

namespace Excalibur.Core;

/// <summary>
///     Provides utilities for handling and validating cultures.
/// </summary>
public static class Cultures
{
	/// <summary>
	///     The default culture name.
	/// </summary>
	public static readonly string DefaultCultureName = "en-US";

	private static readonly Dictionary<string, CultureInfo> ValidCultures =
		CultureInfo.GetCultures(CultureTypes.SpecificCultures).ToDictionary(c => c.Name, c => c);

	/// <summary>
	///     Gets the names of all valid cultures.
	/// </summary>
	/// <remarks> This property provides a collection of valid culture names as defined by <see cref="CultureInfo.GetCultures" />. </remarks>
	public static IEnumerable<string> Names => ValidCultures.Keys;

	/// <summary>
	///     Checks if the given culture name is valid.
	/// </summary>
	/// <param name="cultureName"> The culture name to validate. </param>
	/// <returns> <c> true </c> if the culture name is valid; otherwise, <c> false </c>. </returns>
	public static bool IsValidCultureName(string cultureName) =>
		Names.Any(validCultureName => validCultureName.Equals(cultureName, StringComparison.OrdinalIgnoreCase));

	/// <summary>
	///     Gets the <see cref="CultureInfo" /> for the given culture name.
	/// </summary>
	/// <param name="cultureName"> The name of the culture to retrieve. </param>
	/// <returns> The <see cref="CultureInfo" /> associated with the specified culture name. </returns>
	/// <exception cref="CultureNotFoundException"> Thrown if the culture name is invalid or <c> null </c>. </exception>
	public static CultureInfo GetCultureInfo(string cultureName)
	{
		if (string.IsNullOrWhiteSpace(cultureName) || !ValidCultures.TryGetValue(cultureName, out var cultureInfo))
		{
			throw new CultureNotFoundException(
				message: $"The requested culture {cultureName} is not available.",
				invalidCultureName: cultureName,
				innerException: null);
		}

		return cultureInfo;
	}
}
