// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


namespace Excalibur.Dispatch.Hosting.AspNetCore.Diagnostics;

/// <summary>
/// Configures the <c>type</c> link that the HTTP exception handler writes into every problem details response.
/// </summary>
/// <remarks>
/// Supplied through the registration callback rather than bound from a configuration section. The handler is
/// registered as a singleton and reads these values once when it is constructed, so a later change is not
/// observed for the remaining lifetime of the host. Nothing here affects the status code, the title, or
/// whether internal detail is disclosed; those follow the status mapping and the hosting environment.
/// </remarks>
public class ProblemDetailsOptions
{
	/// <summary>
	/// Gets or sets the documentation origin that every <c>type</c> URI is built from.
	/// </summary>
	/// <value>
	/// The scheme and host to prefix, defaulting to "https://developer.mozilla.org". The handler appends
	/// "/{locale}/docs/Web/HTTP/Status/{statusCode}", and that trailing shape is fixed, so a replacement
	/// origin must serve the same path layout to produce a resolvable link. The value is concatenated
	/// verbatim: a trailing slash yields a doubled separator.
	/// </value>
	public string StatusTypeBaseUrl { get; set; } = "https://developer.mozilla.org";

	/// <summary>
	/// Gets the cultures for which the <c>type</c> link may point at a localized copy of the documentation.
	/// </summary>
	/// <value>
	/// A case-insensitive set compared against the current UI culture's name. A match becomes the locale
	/// segment of the <c>type</c> URI; any culture not in the set falls back to "en-US". This selects a
	/// link and nothing more — no part of the response body is translated, and the title is the standard
	/// HTTP reason phrase regardless of culture. The set is mutable: add a culture the configured origin
	/// publishes, or clear it to pin every link to "en-US".
	/// </value>
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
