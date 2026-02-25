// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Options.Configuration;

/// <summary>
/// Options for Dispatch security features.
/// </summary>
public sealed class SecurityOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether payload encryption is enabled.
	/// </summary>
	/// <value><see langword="false"/> by default.</value>
	public bool EnableEncryption { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether message signing is enabled.
	/// </summary>
	/// <value><see langword="false"/> by default.</value>
	public bool EnableSigning { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether rate limiting is enabled.
	/// </summary>
	/// <value><see langword="false"/> by default.</value>
	public bool EnableRateLimiting { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether message validation is enabled.
	/// </summary>
	/// <value><see langword="true"/> by default.</value>
	public bool EnableValidation { get; set; } = true;
}
