// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Options.Middleware;

/// <summary>
/// Configuration options for input sanitization middleware.
/// </summary>
public sealed class InputSanitizationOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether input sanitization is enabled.
	/// </summary>
	/// <value> Default is true. </value>
	public bool Enabled { get; set; } = true;

	/// <summary>
	/// Gets or sets the sanitization feature toggles.
	/// </summary>
	/// <value> A <see cref="SanitizationFeatures" /> instance with all features enabled by default. </value>
	public SanitizationFeatures Features { get; set; } = new();

	/// <summary>
	/// Gets or sets the maximum allowed string length.
	/// </summary>
	/// <value> Default is 0 (no limit). </value>
	[Range(0, int.MaxValue)]
	public int MaxStringLength { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to sanitize context items.
	/// </summary>
	/// <value> Default is true. </value>
	public bool SanitizeContextItems { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to use custom sanitization service.
	/// </summary>
	/// <value> Default is true. </value>
	public bool UseCustomSanitization { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to throw exception on sanitization error.
	/// </summary>
	/// <value> Default is false. </value>
	public bool ThrowOnSanitizationError { get; set; }

	/// <summary>
	/// Gets or sets message types that bypass sanitization.
	/// </summary>
	/// <value>The current <see cref="BypassSanitizationForTypes"/> value.</value>
	public string[]? BypassSanitizationForTypes { get; set; }

	/// <summary>
	/// Gets or sets property names to exclude from sanitization.
	/// </summary>
	/// <value>The current <see cref="ExcludeProperties"/> value.</value>
	public string[]? ExcludeProperties { get; set; }
}
