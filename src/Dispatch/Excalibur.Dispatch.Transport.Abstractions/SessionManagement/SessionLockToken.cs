// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Represents a session lock token.
/// </summary>
public sealed class SessionLockToken
{
	/// <summary>
	/// Gets the session identifier.
	/// </summary>
	/// <value>The current <see cref="SessionId"/> value.</value>
	public string SessionId { get; init; } = string.Empty;

	/// <summary>
	/// Gets the lock token value.
	/// </summary>
	/// <value>The current <see cref="Token"/> value.</value>
	public string Token { get; init; } = string.Empty;

	/// <summary>
	/// Gets the lock acquisition time.
	/// </summary>
	/// <value>The current <see cref="AcquiredAt"/> value.</value>
	public DateTimeOffset AcquiredAt { get; init; }

	/// <summary>
	/// Gets the lock expiration time.
	/// </summary>
	/// <value>The current <see cref="ExpiresAt"/> value.</value>
	public DateTimeOffset ExpiresAt { get; init; }

	/// <summary>
	/// Gets the owner identifier of the lock.
	/// </summary>
	/// <value>The current <see cref="OwnerId"/> value.</value>
	public string OwnerId { get; init; } = string.Empty;

	/// <summary>
	/// Gets a value indicating whether the lock is still valid.
	/// </summary>
	/// <value>The current <see cref="IsValid"/> value.</value>
	public bool IsValid => DateTimeOffset.UtcNow < ExpiresAt;
}
