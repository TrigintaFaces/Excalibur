// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.A3.Governance.Provisioning;

/// <summary>
/// Configuration options for just-in-time (JIT) access provisioning.
/// </summary>
public sealed class JitAccessOptions
{
	/// <summary>
	/// Gets or sets the default duration for JIT access grants.
	/// </summary>
	/// <value>Defaults to 4 hours.</value>
	[Required]
	public TimeSpan DefaultJitDuration { get; set; } = TimeSpan.FromHours(4);

	/// <summary>
	/// Gets or sets the maximum allowed duration for JIT access grants.
	/// </summary>
	/// <value>Defaults to 24 hours.</value>
	[Required]
	public TimeSpan MaxJitDuration { get; set; } = TimeSpan.FromHours(24);

	/// <summary>
	/// Gets or sets the interval at which the JIT expiry service checks
	/// for expired grants.
	/// </summary>
	/// <value>Defaults to 5 minutes.</value>
	[Required]
	public TimeSpan ExpiryCheckInterval { get; set; } = TimeSpan.FromMinutes(5);
}
