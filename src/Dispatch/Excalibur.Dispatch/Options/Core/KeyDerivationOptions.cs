// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Options.Core;

/// <summary>
/// Configuration options for key derivation.
/// </summary>
public sealed class KeyDerivationOptions
{
	/// <summary>
	/// Gets or sets the password for key derivation.
	/// </summary>
	/// <value> The secret used as the basis for derived keys. </value>
	public string? Password { get; set; }

	/// <summary>
	/// Gets or sets the salt for key derivation.
	/// </summary>
	/// <value> The salt bytes applied during key derivation. </value>
	public byte[]? Salt { get; set; }

	/// <summary>
	/// Gets or sets the number of iterations for key derivation.
	/// </summary>
	/// <value> The iteration count applied by the key derivation function. </value>
	[Range(1, int.MaxValue)]
	public int Iterations { get; set; } = 100_000;
}
