// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.A3.Governance.NonHumanIdentity;

/// <summary>
/// Configuration options for API key management.
/// </summary>
public sealed class ApiKeyOptions
{
	/// <summary>
	/// Gets or sets the maximum number of active keys per principal.
	/// </summary>
	/// <value>Defaults to 10.</value>
	[Range(1, 100)]
	public int MaxKeysPerPrincipal { get; set; } = 10;

	/// <summary>
	/// Gets or sets the default expiration period in days for new keys
	/// when no explicit expiration is specified.
	/// </summary>
	/// <value>Defaults to 90 days.</value>
	[Range(1, 3650)]
	public int DefaultExpirationDays { get; set; } = 90;

	/// <summary>
	/// Gets or sets the length in bytes of generated API keys.
	/// </summary>
	/// <value>Defaults to 32 bytes (256-bit).</value>
	[Range(16, 128)]
	public int KeyLengthBytes { get; set; } = 32;
}
