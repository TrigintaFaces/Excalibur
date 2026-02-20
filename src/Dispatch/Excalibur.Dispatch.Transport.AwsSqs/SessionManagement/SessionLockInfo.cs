// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Information about a session lock.
/// </summary>
public sealed class SessionLockInfo
{
	/// <summary>
	/// Gets or sets the lock identifier.
	/// </summary>
	/// <value>
	/// The lock identifier.
	/// </value>
	public required string LockId { get; set; }

	/// <summary>
	/// Gets or sets the owner identifier.
	/// </summary>
	/// <value>
	/// The owner identifier.
	/// </value>
	public required string OwnerId { get; set; }

	/// <summary>
	/// Gets or sets when the lock was acquired.
	/// </summary>
	/// <value>
	/// When the lock was acquired.
	/// </value>
	public DateTime AcquiredAt { get; set; }

	/// <summary>
	/// Gets or sets when the lock expires.
	/// </summary>
	/// <value>
	/// When the lock expires.
	/// </value>
	public DateTime ExpiresAt { get; set; }

	/// <summary>
	/// Gets or sets the number of times the lock has been renewed.
	/// </summary>
	/// <value>
	/// The number of times the lock has been renewed.
	/// </value>
	public int RenewCount { get; set; }

	/// <summary>
	/// Gets additional metadata.
	/// </summary>
	/// <value>
	/// Additional metadata.
	/// </value>
	public Dictionary<string, object> Metadata { get; } = [];
}
