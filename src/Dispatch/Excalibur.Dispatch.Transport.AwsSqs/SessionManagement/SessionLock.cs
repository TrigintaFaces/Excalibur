// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Represents a session lock that ensures exclusive access to a session.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="SessionLock" /> class. </remarks>
/// <param name="sessionId"> The session identifier. </param>
/// <param name="lockId"> The unique lock identifier. </param>
/// <param name="expiresAt"> When the lock expires. </param>
/// <param name="releaseAction"> Action to execute when releasing the lock. </param>
public sealed class SessionLock(string sessionId, string lockId, DateTimeOffset expiresAt, Func<ValueTask>? releaseAction = null)
	: IAsyncDisposable
{
	private volatile bool _disposed;

	/// <summary>
	/// Gets the session identifier.
	/// </summary>
	/// <value> The session identifier. </value>
	public string SessionId { get; } = sessionId ?? throw new ArgumentNullException(nameof(sessionId));

	/// <summary>
	/// Gets the unique lock identifier.
	/// </summary>
	/// <value> The unique lock identifier. </value>
	public string LockId { get; } = lockId ?? throw new ArgumentNullException(nameof(lockId));

	/// <summary>
	/// Gets when the lock was acquired.
	/// </summary>
	/// <value> When the lock was acquired. </value>
	public DateTimeOffset AcquiredAt { get; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets when the lock expires.
	/// </summary>
	/// <value> When the lock expires. </value>
	public DateTimeOffset ExpiresAt { get; } = expiresAt;

	/// <summary>
	/// Gets a value indicating whether gets whether the lock has expired.
	/// </summary>
	/// <value> A value indicating whether gets whether the lock has expired. </value>
	public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;

	/// <summary>
	/// Gets the remaining time before the lock expires.
	/// </summary>
	/// <value> The remaining time before the lock expires. </value>
	public TimeSpan RemainingTime => IsExpired ? TimeSpan.Zero : ExpiresAt - DateTimeOffset.UtcNow;

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		if (releaseAction != null)
		{
			await releaseAction().ConfigureAwait(false);
		}
	}
}
