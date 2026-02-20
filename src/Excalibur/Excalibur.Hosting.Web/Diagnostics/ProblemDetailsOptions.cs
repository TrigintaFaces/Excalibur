// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Hosting.Web.Diagnostics;

/// <summary>
/// Configuration options for customizing problem details response generation in web applications.
/// </summary>
public sealed class ProblemDetailsOptions
{
	/// <summary>
	/// Gets or sets the base URL used for generating status type URIs in problem details responses.
	/// </summary>
	/// <value> The base URL for status type references. Defaults to "https://developer.mozilla.org". </value>
	public string StatusTypeBaseUrl { get; set; } = "https://developer.mozilla.org";

	/// <summary>
	/// Gets the collection of supported locale identifiers for localized problem details responses.
	/// </summary>
	/// <value> A case-insensitive hash set containing supported locale codes (e.g., "en-US", "fr", "zh-CN"). </value>
	public HashSet<string> SupportedLocales { get; } = new(StringComparer.OrdinalIgnoreCase)
	{
		"ar",
		"ca",
		"cs",
		"de",
		"el",
		"en-US",
		"es",
		"fa",
		"fr",
		"he",
		"hi",
		"hr",
		"hu",
		"id",
		"it",
		"ja",
		"ko",
		"ms",
		"nl",
		"pl",
		"pt-BR",
		"pt-PT",
		"ro",
		"ru",
		"sl",
		"sr",
		"sv",
		"th",
		"tr",
		"uk",
		"vi",
		"zh-CN",
		"zh-TW",
	};
}
