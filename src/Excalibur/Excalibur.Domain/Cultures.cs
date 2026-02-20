// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Globalization;

namespace Excalibur.Domain;

/// <summary>
/// Provides utilities for handling and validating cultures.
/// </summary>
public static class Cultures
{
	/// <summary>
	/// The default culture name.
	/// </summary>
	public const string DefaultCultureName = "en-US";

	private static readonly Dictionary<string, CultureInfo> ValidCultures =
		CultureInfo.GetCultures(CultureTypes.SpecificCultures).ToDictionary(static c => c.Name, static c => c, StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// Gets the names of all valid cultures.
	/// </summary>
	/// <remarks> This property provides a collection of valid culture names as defined by <see cref="CultureInfo.GetCultures" />. </remarks>
	/// <value>
	/// The names of all valid cultures.
	/// </value>
	public static IEnumerable<string> Names => ValidCultures.Keys;

	/// <summary>
	/// Checks if the given culture name is valid.
	/// </summary>
	/// <param name="cultureName"> The culture name to validate. </param>
	/// <returns> <c> true </c> if the culture name is valid; otherwise, <c> false </c>. </returns>
	public static bool IsValidCultureName(string cultureName) =>
		ValidCultures.ContainsKey(cultureName);

	/// <summary>
	/// Gets the <see cref="CultureInfo" /> for the given culture name.
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
