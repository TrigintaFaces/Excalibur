// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Security;

/// <summary>
/// Extends <see cref="SigningOptions"/> with middleware-specific properties.
/// </summary>
public partial class SigningOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether to require valid signatures on incoming messages.
	/// </summary>
	/// <value>
	/// <see langword="true"/> to require valid signatures on incoming messages; otherwise, <see langword="false"/>. The default is <see langword="true"/>.
	/// </value>
	public bool RequireValidSignature { get; set; } = true;
}
