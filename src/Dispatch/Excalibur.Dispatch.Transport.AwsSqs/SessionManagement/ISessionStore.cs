// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Temporary interface to match what tests expect.
/// </summary>
public interface ISessionStore
{
	/// <summary>
	/// Creates a session.
	/// </summary>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	Task<SessionData> CreateAsync(string sessionId, TimeSpan timeout, CancellationToken cancellationToken);

	/// <summary>
	/// Tries to get a session.
	/// </summary>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	Task<SessionData?> TryGetAsync(string sessionId, CancellationToken cancellationToken);

	/// <summary>
	/// Updates a session.
	/// </summary>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	Task<SessionData> UpdateAsync(SessionData session, CancellationToken cancellationToken);

	/// <summary>
	/// Creates or updates a session.
	/// </summary>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	Task CreateOrUpdateAsync(SessionData session, CancellationToken cancellationToken);

	/// <summary>
	/// Deletes a session.
	/// </summary>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	Task DeleteAsync(string sessionId, CancellationToken cancellationToken);

	/// <summary>
	/// Checks if a session exists.
	/// </summary>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	Task<bool> ExistsAsync(string sessionId, CancellationToken cancellationToken);

	/// <summary>
	/// Gets the count of sessions.
	/// </summary>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	Task<int> GetCountAsync(CancellationToken cancellationToken);
}
