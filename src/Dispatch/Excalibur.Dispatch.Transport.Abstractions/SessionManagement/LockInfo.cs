// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Information about a lock.
/// </summary>
public sealed class LockInfo
{
	/// <summary>
	/// Gets or sets the session identifier.
	/// </summary>
	/// <value>The current <see cref="SessionId"/> value.</value>
	public string SessionId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the lock token.
	/// </summary>
	/// <value>The current <see cref="LockToken"/> value.</value>
	public string LockToken { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the lock type.
	/// </summary>
	/// <value>The current <see cref="Type"/> value.</value>
	public LockType Type { get; set; }

	/// <summary>
	/// Gets or sets the owner identifier.
	/// </summary>
	/// <value>The current <see cref="OwnerId"/> value.</value>
	public string OwnerId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets when the lock was acquired.
	/// </summary>
	/// <value>The current <see cref="AcquiredAt"/> value.</value>
	public DateTimeOffset AcquiredAt { get; set; }

	/// <summary>
	/// Gets or sets when the lock expires.
	/// </summary>
	/// <value>The current <see cref="ExpiresAt"/> value.</value>
	public DateTimeOffset ExpiresAt { get; set; }

	/// <summary>
	/// Gets or sets the number of times the lock has been extended.
	/// </summary>
	/// <value>The current <see cref="ExtensionCount"/> value.</value>
	public int ExtensionCount { get; set; }

	/// <summary>
	/// Gets lock metadata.
	/// </summary>
	/// <value>The current <see cref="Metadata"/> value.</value>
	public Dictionary<string, string> Metadata { get; init; } = [];
}
