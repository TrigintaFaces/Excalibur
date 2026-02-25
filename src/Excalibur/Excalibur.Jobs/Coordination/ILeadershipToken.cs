// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Jobs.Coordination;

/// <summary>
/// Represents a leadership token that grants coordination authority to an instance.
/// </summary>
public interface ILeadershipToken : IAsyncDisposable
{
	/// <summary>
	/// Gets the unique identifier for the instance that holds this leadership token.
	/// </summary>
	/// <value>
	/// The unique identifier for the instance that holds this leadership token.
	/// </value>
	string LeaderInstanceId { get; }

	/// <summary>
	/// Gets the time when this leadership was acquired.
	/// </summary>
	/// <value>
	/// The time when this leadership was acquired.
	/// </value>
	DateTimeOffset AcquiredAt { get; }

	/// <summary>
	/// Gets the time when this leadership will expire.
	/// </summary>
	/// <value>
	/// The time when this leadership will expire.
	/// </value>
	DateTimeOffset ExpiresAt { get; }

	/// <summary>
	/// Gets a value indicating whether this leadership token is still valid.
	/// </summary>
	/// <value>
	/// A value indicating whether this leadership token is still valid.
	/// </value>
	bool IsValid { get; }

	/// <summary>
	/// Extends the duration of this leadership token.
	/// </summary>
	/// <param name="additionalDuration"> The additional time to extend the leadership. </param>
	/// <param name="cancellationToken"> A token to monitor for cancellation requests. </param>
	/// <returns> A task indicating whether the leadership extension was successful. </returns>
	Task<bool> ExtendAsync(TimeSpan additionalDuration, CancellationToken cancellationToken);

	/// <summary>
	/// Releases the leadership before its expiration time.
	/// </summary>
	/// <param name="cancellationToken"> A token to monitor for cancellation requests. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	Task ReleaseAsync(CancellationToken cancellationToken);
}
