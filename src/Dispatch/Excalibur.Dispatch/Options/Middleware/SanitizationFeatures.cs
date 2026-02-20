// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Options.Middleware;

/// <summary>
/// Configures which sanitization features are active in <see cref="InputSanitizationOptions" />.
/// </summary>
public sealed class SanitizationFeatures
{
	/// <summary>
	/// Gets or sets a value indicating whether to prevent XSS attacks.
	/// </summary>
	/// <value> Default is true. </value>
	public bool PreventXss { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to remove HTML tags or encode them.
	/// </summary>
	/// <value> Default is true (remove tags). </value>
	public bool RemoveHtmlTags { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to prevent SQL injection.
	/// </summary>
	/// <value> Default is true. </value>
	public bool PreventSqlInjection { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to prevent path traversal attacks.
	/// </summary>
	/// <value> Default is true. </value>
	public bool PreventPathTraversal { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to remove null bytes.
	/// </summary>
	/// <value> Default is true. </value>
	public bool RemoveNullBytes { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to normalize Unicode characters.
	/// </summary>
	/// <value> Default is true. </value>
	public bool NormalizeUnicode { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to trim whitespace from strings.
	/// </summary>
	/// <value> Default is true. </value>
	public bool TrimWhitespace { get; set; } = true;
}
