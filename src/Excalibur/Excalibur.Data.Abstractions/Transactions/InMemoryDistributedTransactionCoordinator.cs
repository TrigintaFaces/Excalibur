// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.Abstractions.Transactions;

/// <summary>
/// In-memory implementation of <see cref="IDistributedTransactionCoordinator"/> using the two-phase commit protocol.
/// </summary>
/// <remarks>
/// <para>
/// This implementation maintains transaction state in a <see cref="ConcurrentDictionary{TKey, TValue}"/>
/// and is intended for testing, development, and single-process deployment scenarios.
/// Data is not persisted across process restarts.
/// </para>
/// <para>
/// Thread-safe: all state mutations are protected by per-transaction locks.
/// Follows the <c>volatile _disposed</c> guard pattern established in the codebase.
/// </para>
/// </remarks>
public sealed partial class InMemoryDistributedTransactionCoordinator
	: IDistributedTransactionCoordinator, IAsyncDisposable
{
	private readonly ConcurrentDictionary<string, TransactionState> _transactions = new();
	private readonly DistributedTransactionOptions _options;
	private readonly ILogger<InMemoryDistributedTransactionCoordinator> _logger;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="InMemoryDistributedTransactionCoordinator"/> class.
	/// </summary>
	/// <param name="options">The distributed transaction options.</param>
	/// <param name="logger">The logger instance.</param>
	public InMemoryDistributedTransactionCoordinator(
		IOptions<DistributedTransactionOptions> options,
		ILogger<InMemoryDistributedTransactionCoordinator> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_logger = logger;
	}

	/// <inheritdoc />
	public Task<string> BeginAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		var transactionId = Guid.NewGuid().ToString("N");
		var state = new TransactionState(transactionId, _options.Timeout);

		if (!_transactions.TryAdd(transactionId, state))
		{
			throw new DistributedTransactionException("Failed to create transaction: duplicate transaction ID.")
			{
				TransactionId = transactionId,
			};
		}

		LogTransactionStarted(transactionId);
		return Task.FromResult(transactionId);
	}

	/// <inheritdoc />
	public Task EnlistAsync(ITransactionParticipant participant, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentNullException.ThrowIfNull(participant);

		var state = GetActiveTransactionOrThrow();

		lock (state.Lock)
		{
			if (state.Phase != TransactionPhase.Active)
			{
				throw new DistributedTransactionException(
					$"Cannot enlist participant '{participant.ParticipantId}': transaction '{state.TransactionId}' is in phase '{state.Phase}'.")
				{
					TransactionId = state.TransactionId,
				};
			}

			if (state.Participants.Count >= _options.MaxParticipants)
			{
				throw new DistributedTransactionException(
					$"Cannot enlist participant '{participant.ParticipantId}': maximum participant count ({_options.MaxParticipants}) reached.")
				{
					TransactionId = state.TransactionId,
				};
			}

			state.Participants.Add(participant);
		}

		LogParticipantEnlisted(state.TransactionId, participant.ParticipantId, state.Participants.Count);
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public async Task CommitAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		var state = GetActiveTransactionOrThrow();

		List<ITransactionParticipant> participants;
		lock (state.Lock)
		{
			if (state.Phase != TransactionPhase.Active)
			{
				throw new DistributedTransactionException(
					$"Cannot commit transaction '{state.TransactionId}': transaction is in phase '{state.Phase}'.")
				{
					TransactionId = state.TransactionId,
				};
			}

			state.Phase = TransactionPhase.Preparing;
			participants = [.. state.Participants];
		}

		if (participants.Count == 0)
		{
			lock (state.Lock)
			{
				state.Phase = TransactionPhase.Committed;
			}

			RemoveTransaction(state.TransactionId);
			LogTransactionCommitted(state.TransactionId, 0);
			return;
		}

		// Phase 1: Prepare
		var failedParticipants = new List<string>();
		foreach (var participant in participants)
		{
			try
			{
				using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
				timeoutCts.CancelAfter(state.Timeout);

				var prepared = await participant.PrepareAsync(timeoutCts.Token).ConfigureAwait(false);
				if (!prepared)
				{
					failedParticipants.Add(participant.ParticipantId);
					LogParticipantVotedNo(state.TransactionId, participant.ParticipantId);
				}
			}
#pragma warning disable CA1031 // Do not catch general exception types -- 2PC must capture all prepare failures
			catch (Exception ex)
#pragma warning restore CA1031
			{
				failedParticipants.Add(participant.ParticipantId);
				LogParticipantPrepareFailed(state.TransactionId, participant.ParticipantId, ex);
			}
		}

		if (failedParticipants.Count > 0)
		{
			lock (state.Lock)
			{
				state.Phase = TransactionPhase.Aborting;
			}

			if (_options.AutoRollbackOnPrepareFailure)
			{
				await RollbackParticipantsAsync(state, participants, cancellationToken).ConfigureAwait(false);
			}

			RemoveTransaction(state.TransactionId);

			throw new DistributedTransactionException(
				$"Transaction '{state.TransactionId}' aborted: {failedParticipants.Count} participant(s) failed to prepare.")
			{
				TransactionId = state.TransactionId,
				FailedParticipantIds = failedParticipants,
			};
		}

		// Phase 2: Commit
		lock (state.Lock)
		{
			state.Phase = TransactionPhase.Committing;
		}

		var commitFailures = new List<string>();
		foreach (var participant in participants)
		{
			try
			{
				using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
				timeoutCts.CancelAfter(state.Timeout);

				await participant.CommitAsync(timeoutCts.Token).ConfigureAwait(false);
			}
#pragma warning disable CA1031 // Do not catch general exception types -- 2PC must capture all commit failures
			catch (Exception ex)
#pragma warning restore CA1031
			{
				commitFailures.Add(participant.ParticipantId);
				LogParticipantCommitFailed(state.TransactionId, participant.ParticipantId, ex);
			}
		}

		lock (state.Lock)
		{
			state.Phase = commitFailures.Count > 0 ? TransactionPhase.PartiallyCommitted : TransactionPhase.Committed;
		}

		RemoveTransaction(state.TransactionId);

		if (commitFailures.Count > 0)
		{
			throw new DistributedTransactionException(
				$"Transaction '{state.TransactionId}' partially committed: {commitFailures.Count} participant(s) failed during commit phase.")
			{
				TransactionId = state.TransactionId,
				FailedParticipantIds = commitFailures,
			};
		}

		LogTransactionCommitted(state.TransactionId, participants.Count);
	}

	/// <inheritdoc />
	public async Task RollbackAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		var state = GetActiveTransactionOrThrow();

		List<ITransactionParticipant> participants;
		lock (state.Lock)
		{
			if (state.Phase == TransactionPhase.Committed)
			{
				throw new DistributedTransactionException(
					$"Cannot rollback transaction '{state.TransactionId}': transaction is already committed.")
				{
					TransactionId = state.TransactionId,
				};
			}

			state.Phase = TransactionPhase.Aborting;
			participants = [.. state.Participants];
		}

		await RollbackParticipantsAsync(state, participants, cancellationToken).ConfigureAwait(false);
		RemoveTransaction(state.TransactionId);
		LogTransactionRolledBack(state.TransactionId, participants.Count);
	}

	/// <inheritdoc />
	public ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return ValueTask.CompletedTask;
		}

		_disposed = true;
		_transactions.Clear();
		return ValueTask.CompletedTask;
	}

	/// <summary>
	/// Gets the count of active transactions. Useful for testing and diagnostics.
	/// </summary>
	/// <value>The number of transactions currently tracked.</value>
	public int ActiveTransactionCount => _transactions.Count;

	private TransactionState GetActiveTransactionOrThrow()
	{
		// For the in-memory coordinator, we track the most recently created transaction
		// that is still active. In a real implementation, the transaction ID would be
		// passed via ambient context (e.g., AsyncLocal or TransactionScope).
		TransactionState? latest = null;
		foreach (var kvp in _transactions)
		{
			if (latest is null || string.CompareOrdinal(kvp.Key, latest.TransactionId) > 0)
			{
				latest = kvp.Value;
			}
		}

		if (latest is null)
		{
			throw new DistributedTransactionException("No active transaction found. Call BeginAsync first.");
		}

		return latest;
	}

	private void RemoveTransaction(string transactionId) =>
		_transactions.TryRemove(transactionId, out _);

	private async Task RollbackParticipantsAsync(
		TransactionState state,
		List<ITransactionParticipant> participants,
		CancellationToken cancellationToken)
	{
		foreach (var participant in participants)
		{
			try
			{
				using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
				timeoutCts.CancelAfter(state.Timeout);

				await participant.RollbackAsync(timeoutCts.Token).ConfigureAwait(false);
			}
#pragma warning disable CA1031 // Do not catch general exception types -- rollback must attempt all participants
			catch (Exception ex)
#pragma warning restore CA1031
			{
				LogParticipantRollbackFailed(state.TransactionId, participant.ParticipantId, ex);
			}
		}
	}

	// --- LoggerMessage source-generated methods (Event ID range 3600-3619 from Excalibur.* reserved range) ---

	[LoggerMessage(
		EventId = 3600,
		Level = LogLevel.Information,
		Message = "Distributed transaction '{TransactionId}' started.")]
	private partial void LogTransactionStarted(string transactionId);

	[LoggerMessage(
		EventId = 3601,
		Level = LogLevel.Debug,
		Message = "Participant '{ParticipantId}' enlisted in transaction '{TransactionId}' (total: {ParticipantCount}).")]
	private partial void LogParticipantEnlisted(string transactionId, string participantId, int participantCount);

	[LoggerMessage(
		EventId = 3602,
		Level = LogLevel.Warning,
		Message = "Participant '{ParticipantId}' voted 'no' during prepare phase of transaction '{TransactionId}'.")]
	private partial void LogParticipantVotedNo(string transactionId, string participantId);

	[LoggerMessage(
		EventId = 3603,
		Level = LogLevel.Error,
		Message = "Participant '{ParticipantId}' failed during prepare phase of transaction '{TransactionId}'.")]
	private partial void LogParticipantPrepareFailed(string transactionId, string participantId, Exception exception);

	[LoggerMessage(
		EventId = 3604,
		Level = LogLevel.Error,
		Message = "Participant '{ParticipantId}' failed during commit phase of transaction '{TransactionId}'.")]
	private partial void LogParticipantCommitFailed(string transactionId, string participantId, Exception exception);

	[LoggerMessage(
		EventId = 3605,
		Level = LogLevel.Error,
		Message = "Participant '{ParticipantId}' failed during rollback of transaction '{TransactionId}'.")]
	private partial void LogParticipantRollbackFailed(string transactionId, string participantId, Exception exception);

	[LoggerMessage(
		EventId = 3606,
		Level = LogLevel.Information,
		Message = "Distributed transaction '{TransactionId}' committed successfully with {ParticipantCount} participant(s).")]
	private partial void LogTransactionCommitted(string transactionId, int participantCount);

	[LoggerMessage(
		EventId = 3607,
		Level = LogLevel.Information,
		Message = "Distributed transaction '{TransactionId}' rolled back with {ParticipantCount} participant(s).")]
	private partial void LogTransactionRolledBack(string transactionId, int participantCount);

	private enum TransactionPhase
	{
		Active,
		Preparing,
		Committing,
		Aborting,
		Committed,
		PartiallyCommitted,
	}

	private sealed class TransactionState
	{
		public TransactionState(string transactionId, TimeSpan timeout)
		{
			TransactionId = transactionId;
			Timeout = timeout;
		}

		public string TransactionId { get; }

		public TimeSpan Timeout { get; }

		public TransactionPhase Phase { get; set; } = TransactionPhase.Active;

		public List<ITransactionParticipant> Participants { get; } = [];

		public object Lock { get; } = new();
	}
}
