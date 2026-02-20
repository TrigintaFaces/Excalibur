// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Represents an access control rule for a destination.
/// </summary>
public sealed class AccessRule
{
	/// <summary>
	/// Gets or sets the principal (user/service) this rule applies Excalibur.Dispatch.Transport.Aws.Sqs.LongPolling.Configuration.
	/// </summary>
	/// <value>The current <see cref="Principal"/> value.</value>
	public string Principal { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the permissions granted.
	/// </summary>
	/// <value>The current <see cref="Permissions"/> value.</value>
	public AccessPermissions Permissions { get; set; }
}
