// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Options for session management.
/// </summary>
public sealed class SessionOptions
{
	/// <summary>
	/// Gets or sets the session timeout.
	/// </summary>
	/// <value>
	/// The session timeout.
	/// </value>
	public TimeSpan SessionTimeout { get; set; } = TimeSpan.FromMinutes(5);

	/// <summary>
	/// Gets or sets the lock timeout.
	/// </summary>
	/// <value>
	/// The lock timeout.
	/// </value>
	public TimeSpan LockTimeout { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets a value indicating whether to auto-renew the session.
	/// </summary>
	/// <value>The current <see cref="AutoRenew"/> value.</value>
	public bool AutoRenew { get; set; } = true;

	/// <summary>
	/// Gets or sets the auto-renew interval.
	/// </summary>
	/// <value>
	/// The auto-renew interval.
	/// </value>
	public TimeSpan AutoRenewInterval { get; set; } = TimeSpan.FromMinutes(1);

	/// <summary>
	/// Gets or sets the maximum number of messages per session.
	/// </summary>
	/// <value>The current <see cref="MaxMessagesPerSession"/> value.</value>
	[Range(1, int.MaxValue)]
	public int? MaxMessagesPerSession { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to preserve message order within the session.
	/// </summary>
	/// <value>The current <see cref="PreserveOrder"/> value.</value>
	public bool PreserveOrder { get; set; } = true;

	/// <summary>
	/// Gets the session metadata.
	/// </summary>
	/// <value>The current <see cref="Metadata"/> value.</value>
	public Dictionary<string, string> Metadata { get; } = [];
}
