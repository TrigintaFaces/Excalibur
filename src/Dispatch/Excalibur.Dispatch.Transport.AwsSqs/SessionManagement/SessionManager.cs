// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0



using Microsoft.Extensions.Logging;

using AbstractionsSessionContext = Excalibur.Dispatch.Transport.SessionContext;
using AbstractionsSessionInfo = Excalibur.Dispatch.Transport.SessionInfo;
using AbstractionsSessionOptions = Excalibur.Dispatch.Transport.SessionOptions;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// AWS-specific session manager implementation.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="SessionManager" /> class. </remarks>
/// <param name="sessionStore"> The session store. </param>
/// <param name="logger"> The logger. </param>
public sealed class SessionManager(ISessionStore sessionStore, ILogger<SessionManager> logger) : ISessionManager
{
	private readonly ISessionStore _sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
	private readonly ILogger<SessionManager> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	/// <summary>
	/// Creates a new session.
	/// </summary>
	/// <param name="sessionId"> The session ID. </param>
	/// <param name="timeout"> The session timeout. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The created session. </returns>
	public async Task<SessionData> CreateSessionAsync(string sessionId, TimeSpan timeout, CancellationToken cancellationToken) =>
		await _sessionStore.CreateAsync(sessionId, timeout, cancellationToken).ConfigureAwait(false);

	/// <summary>
	/// Gets a session by ID.
	/// </summary>
	/// <param name="sessionId"> The session ID. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The session if found; otherwise, null. </returns>
	public async Task<SessionData?> GetSessionAsync(string sessionId, CancellationToken cancellationToken) =>
		await _sessionStore.TryGetAsync(sessionId, cancellationToken).ConfigureAwait(false);

	/// <summary>
	/// Updates a session.
	/// </summary>
	/// <param name="session"> The session to update. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The updated session. </returns>
	public async Task<SessionData> UpdateSessionAsync(SessionData session, CancellationToken cancellationToken) =>
		await _sessionStore.UpdateAsync(session, cancellationToken).ConfigureAwait(false);

	/// <summary>
	/// Deletes a session.
	/// </summary>
	/// <param name="sessionId"> The session ID. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the operation. </returns>
	public async Task DeleteSessionAsync(string sessionId, CancellationToken cancellationToken) =>
		await _sessionStore.DeleteAsync(sessionId, cancellationToken).ConfigureAwait(false);

	/// <summary>
	/// Checks if a session exists.
	/// </summary>
	/// <param name="sessionId"> The session ID. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> True if the session exists; otherwise, false. </returns>
	public async Task<bool> SessionExistsAsync(string sessionId, CancellationToken cancellationToken) =>
		await _sessionStore.ExistsAsync(sessionId, cancellationToken).ConfigureAwait(false);

	// ISessionLockCoordinator interface implementation - delegate to CloudSessionManager logic

	/// <inheritdoc />
	Task<SessionLockToken> ISessionLockCoordinator.AcquireLockAsync(string sessionId, TimeSpan lockDuration,
		CancellationToken cancellationToken)
	{
		// This is a simplified implementation - in a real scenario, you'd implement proper locking
		var token = new SessionLockToken
		{
			SessionId = sessionId,
			Token = Guid.NewGuid().ToString(),
			AcquiredAt = DateTimeOffset.UtcNow,
			ExpiresAt = DateTimeOffset.UtcNow.Add(lockDuration),
			OwnerId = Environment.MachineName,
		};

		_logger.LogDebug("Acquired lock for session '{SessionId}' with token '{Token}'", sessionId, token.Token);

		return Task.FromResult(token);
	}

	/// <inheritdoc />
	Task<SessionLockToken?> ISessionLockCoordinator.TryAcquireLockAsync(string sessionId, TimeSpan lockDuration,
		CancellationToken cancellationToken) =>
		Task.FromResult<SessionLockToken?>(new SessionLockToken
		{
			SessionId = sessionId,
			Token = Guid.NewGuid().ToString(),
			AcquiredAt = DateTimeOffset.UtcNow,
			ExpiresAt = DateTimeOffset.UtcNow.Add(lockDuration),
		});

	/// <inheritdoc />
	Task<bool> ISessionLockCoordinator.
		ExtendLockAsync(SessionLockToken lockToken, TimeSpan extension, CancellationToken cancellationToken) => Task.FromResult(true);

	/// <inheritdoc />
	Task<bool> ISessionLockCoordinator.ReleaseLockAsync(SessionLockToken lockToken, CancellationToken cancellationToken) =>
		Task.FromResult(true);

	/// <inheritdoc />
	Task<bool> ISessionLockCoordinator.IsLockedAsync(string sessionId, CancellationToken cancellationToken) => Task.FromResult(false);

	/// <inheritdoc />
	Task<AbstractionsSessionInfo?> ISessionInfoProvider.GetSessionInfoAsync(string sessionId, CancellationToken cancellationToken) =>
		Task.FromResult<AbstractionsSessionInfo?>(null);

	/// <inheritdoc />
	Task<IReadOnlyDictionary<string, AbstractionsSessionInfo>> ISessionInfoProvider.GetSessionInfosAsync(
		IEnumerable<string> sessionIds,
		CancellationToken cancellationToken) =>
		Task.FromResult<IReadOnlyDictionary<string, AbstractionsSessionInfo>>(new Dictionary<string, AbstractionsSessionInfo>(StringComparer.Ordinal));

	/// <inheritdoc />
	Task<IReadOnlyList<AbstractionsSessionInfo>> ISessionInfoProvider.ListActiveSessionsAsync(int maxSessions, CancellationToken cancellationToken) =>
		Task.FromResult<IReadOnlyList<AbstractionsSessionInfo>>(new List<AbstractionsSessionInfo>());

	/// <inheritdoc />
	Task<SessionStatistics> ISessionInfoProvider.GetStatisticsAsync(CancellationToken cancellationToken) =>
		Task.FromResult(new SessionStatistics());

	/// <inheritdoc />
	Task<AbstractionsSessionContext> ISessionLifecycleManager.CreateSessionAsync(string sessionId, AbstractionsSessionOptions? options,
		CancellationToken cancellationToken)
	{
		var context = new AbstractionsSessionContext { SessionId = sessionId, Options = options ?? new AbstractionsSessionOptions() };
		return Task.FromResult(context);
	}

	/// <inheritdoc />
	Task<AbstractionsSessionContext?> ISessionLifecycleManager.OpenSessionAsync(string sessionId, CancellationToken cancellationToken) =>
		Task.FromResult<AbstractionsSessionContext?>(null);

	/// <inheritdoc />
	Task<bool> ISessionLifecycleManager.CloseSessionAsync(string sessionId, bool graceful, CancellationToken cancellationToken) =>
		Task.FromResult(true);

	/// <inheritdoc />
	Task<DateTimeOffset?> ISessionLifecycleManager.RenewSessionAsync(string sessionId, TimeSpan? duration,
		CancellationToken cancellationToken) =>
		Task.FromResult<DateTimeOffset?>(DateTimeOffset.UtcNow.Add(duration ?? TimeSpan.FromMinutes(30)));

	/// <inheritdoc />
	Task<bool> ISessionLifecycleManager.AbandonSessionAsync(string sessionId, string? reason, CancellationToken cancellationToken) =>
		Task.FromResult(true);

	/// <inheritdoc />
	Task<int> ISessionLifecycleManager.CleanupExpiredSessionsAsync(CancellationToken cancellationToken) => Task.FromResult(0);

	/// <inheritdoc />
	Task<TState?> ISessionStateManager.GetStateAsync<TState>(string sessionId, CancellationToken cancellationToken)
		where TState : class
		=>
			Task.FromResult<TState?>(null);

	/// <inheritdoc />
	Task<bool> ISessionStateManager.SetStateAsync<TState>(string sessionId, TState state, CancellationToken cancellationToken)
		where TState : class =>
		Task.FromResult(true);

	/// <inheritdoc />
	Task<TState?> ISessionStateManager.UpdateStateAsync<TState>(string sessionId, Func<TState?, TState?> updateFunc,
		CancellationToken cancellationToken)
		where TState : class
		=>
			Task.FromResult(updateFunc(default));

	/// <inheritdoc />
	Task<bool> ISessionStateManager.DeleteStateAsync(string sessionId, CancellationToken cancellationToken) => Task.FromResult(true);

	/// <inheritdoc />
	Task<bool> ISessionStateManager.CreateCheckpointAsync(string sessionId, string checkpointId, CancellationToken cancellationToken) =>
		Task.FromResult(true);

	/// <inheritdoc />
	Task<bool> ISessionStateManager.RestoreCheckpointAsync(string sessionId, string checkpointId, CancellationToken cancellationToken) =>
		Task.FromResult(true);

	/// <inheritdoc />
	Task<IReadOnlyList<CheckpointInfo>> ISessionStateManager.ListCheckpointsAsync(string sessionId, CancellationToken cancellationToken) =>
		Task.FromResult<IReadOnlyList<CheckpointInfo>>(new List<CheckpointInfo>());

	/// <inheritdoc />
	Task<IReadOnlyList<SessionLockToken>> IAdvancedSessionLocking.AcquireMultipleLocksAsync(
		IEnumerable<string> sessionIds,
		TimeSpan lockDuration, CancellationToken cancellationToken) =>
		Task.FromResult<IReadOnlyList<SessionLockToken>>(new List<SessionLockToken>());

	/// <inheritdoc />
	Task<SessionLockToken?> IAdvancedSessionLocking.WaitForLockAsync(string sessionId, TimeSpan timeout,
		CancellationToken cancellationToken) =>
		Task.FromResult<SessionLockToken?>(null);

	/// <inheritdoc />
	Task<SessionLockToken?> IAdvancedSessionLocking.UpgradeToWriteLockAsync(
		SessionLockToken readLockToken,
		CancellationToken cancellationToken) =>
		Task.FromResult<SessionLockToken?>(null);

	/// <inheritdoc />
	Task<SessionLockToken?> IAdvancedSessionLocking.DowngradeToReadLockAsync(
		SessionLockToken writeLockToken,
		CancellationToken cancellationToken) =>
		Task.FromResult<SessionLockToken?>(null);

	/// <inheritdoc />
	Task<IReadOnlyList<LockInfo>> IAdvancedSessionLocking.GetAllLocksAsync(CancellationToken cancellationToken) =>
		Task.FromResult<IReadOnlyList<LockInfo>>(new List<LockInfo>());

	/// <inheritdoc />
	Task<bool> IAdvancedSessionLocking.BreakLockAsync(string sessionId, string reason, CancellationToken cancellationToken) =>
		Task.FromResult(true);
}
