// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using System.Data.Common;
using System.Runtime.ExceptionServices;

using Excalibur.Data;
using Excalibur.Dispatch;

using Microsoft.Extensions.Logging;

using Excalibur.Outbox.Oracle.Diagnostics;

using Oracle.ManagedDataAccess.Client;

namespace Excalibur.Outbox.Oracle;

public sealed partial class OracleOutboxStore
{
	internal const string MissingWorkerConnectionMessage =
		"Oracle outbox polling workers require fresh exclusively owned worker connections. " +
		"Use ConnectionString(), ConnectionStringName(), BindConfiguration(), or configure WorkerConnectionFactory() " +
		"alongside the borrowed IDb ConnectionFactory(). Borrowed Stage and caller-transaction Stage remain available.";

	object? IServiceProvider.GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);
		if (_workerConnections is null && (serviceType == typeof(IClaimScopedOutboxStore)
			|| serviceType == typeof(IClaimScopedDeadLetterableOutboxStore)
			|| serviceType == typeof(IFencedClaimScopedOutboxStore)
			|| serviceType == typeof(IFencedDeadLetterableOutboxStore)
			|| serviceType == typeof(IFencedOutboxStore)))
		{
			return null;
		}
		return serviceType.IsInstanceOfType(this) ? this : null;
	}

	/// <inheritdoc />
	public ValueTask<OutboxCompletionOutcome> MarkSentAsync(string messageId, string claimIdentity,
		CancellationToken cancellationToken) =>
		CompleteOwnedAsync(messageId, OracleClaimTransition.Sent, null, 0, null, claimIdentity, null, cancellationToken);

	/// <inheritdoc />
	public ValueTask<OutboxCompletionOutcome> MarkSentAsync(string messageId, OutboxWriteAuthority authority,
		CancellationToken cancellationToken)
	{
		ValidateAuthority(authority);
		return CompleteOwnedAsync(messageId, OracleClaimTransition.Sent, null, 0, null,
			authority.ClaimIdentity, authority.FencingToken, cancellationToken);
	}

	/// <inheritdoc />
	public ValueTask<OutboxCompletionOutcome> MarkFailedAsync(string messageId, string errorMessage, int retryCount,
		string claimIdentity, CancellationToken cancellationToken) =>
		CompleteOwnedAsync(messageId, OracleClaimTransition.Failed, errorMessage, retryCount, null,
			claimIdentity, null, cancellationToken);

	/// <inheritdoc />
	public ValueTask<OutboxCompletionOutcome> MarkFailedWithBackoffAsync(string messageId, string errorMessage,
		int retryCount, DateTimeOffset nextAttemptAt, string claimIdentity, CancellationToken cancellationToken) =>
		CompleteOwnedAsync(messageId, OracleClaimTransition.Failed, errorMessage, retryCount, nextAttemptAt,
			claimIdentity, null, cancellationToken);

	/// <inheritdoc />
	public ValueTask<OutboxCompletionOutcome> MarkDeadLetteredAsync(string messageId, string reason,
		int attemptCount, string claimIdentity, CancellationToken cancellationToken) =>
		CompleteOwnedAsync(messageId, OracleClaimTransition.DeadLetter, reason, attemptCount, null,
			claimIdentity, null, cancellationToken);

	/// <inheritdoc />
	public ValueTask<OutboxCompletionOutcome> MarkDeadLetteredAsync(string messageId, string reason,
		int attemptCount, OutboxWriteAuthority authority, CancellationToken cancellationToken)
	{
		ValidateAuthority(authority);
		return CompleteOwnedAsync(messageId, OracleClaimTransition.DeadLetter, reason, attemptCount, null,
			authority.ClaimIdentity, authority.FencingToken, cancellationToken);
	}

	private static void ValidateAuthority(OutboxWriteAuthority authority)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(authority.FencingToken);
		ArgumentException.ThrowIfNullOrWhiteSpace(authority.ClaimIdentity);
	}

	private ValueTask<OutboxCompletionOutcome> CompleteOwnedAsync(string messageId,
		OracleClaimTransition transition, string? error, int attempts, DateTimeOffset? nextAttemptAt,
		string? claimIdentity, long? fencingToken, CancellationToken cancellationToken,
		bool requireClaim = true)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentOutOfRangeException.ThrowIfNegative(attempts);
		if (requireClaim) { ArgumentException.ThrowIfNullOrWhiteSpace(claimIdentity); }
		if (fencingToken is { } token) { ArgumentOutOfRangeException.ThrowIfNegativeOrZero(token); }
		if (transition == OracleClaimTransition.Failed) { ArgumentException.ThrowIfNullOrWhiteSpace(error); }
		if (transition == OracleClaimTransition.DeadLetter) { ArgumentNullException.ThrowIfNull(error); }
		// Both operands use the same dispatcher clock; persist only the resulting duration,
		// re-anchored once on Oracle's post-lock server clock.
		var delay = nextAttemptAt is { } at ? Math.Max(0, (at - _timeProvider.GetUtcNow()).TotalSeconds) : 0;
		return ExecuteOwnedAsync(async (connection, transaction, scope, token) =>
		{
			var request = new OracleClaimCompletionRequest(_options, scope, messageId, transition, error, attempts,
				delay, requireClaim ? claimIdentity : null, fencingToken, transaction, token);
			return await connection.ResolveAsync(request).ConfigureAwait(false);
		}, cancellationToken);
	}

	private async ValueTask<IEnumerable<OutboundMessage>> ClaimOwnedAsync(int batchSize, long? fencingToken,
		CancellationToken cancellationToken)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);
		if (fencingToken is { } token) { ArgumentOutOfRangeException.ThrowIfNegativeOrZero(token); }
		long? refusedHighWater = null;
		var result = await ExecuteOwnedAsync<IEnumerable<OutboundMessage>>(async (connection, transaction, scope, token) =>
		{
			IEnumerable<IOutboxMessage> reserved;
			if (fencingToken is { } tenure)
			{
				var request = new FencedReserveOutboxMessages(ProcessDispatcherId, batchSize, _options.ReservationTimeout,
					tenure, scope.OutboxTable, scope.FenceTable,
					DbTimeouts.RegularTimeoutSeconds, transaction, scope.Key, token);
				reserved = await connection.ResolveAsync(request).ConfigureAwait(false);
				var highWater = request.EffectiveHighWater
					?? throw new InvalidDataException("Oracle fenced claim did not return its effective high-water.");
				if (highWater > tenure)
				{
					refusedHighWater = highWater;
				}
			}
			else
			{
				var request = new ReserveOutboxMessages(ProcessDispatcherId, batchSize, _options.ReservationTimeout,
					scope.OutboxTable, DbTimeouts.RegularTimeoutSeconds, transaction, token);
				reserved = await connection.ResolveAsync(request).ConfigureAwait(false);
			}
			// Buffer AND materialize before Commit. No envelope crosses the owner boundary on commit failure.
			return ConvertReservedToOutbound(reserved);
		}, cancellationToken).ConfigureAwait(false);
		if (fencingToken is { } presentedToken && refusedHighWater is { } highWater)
		{
			ReportClaimFenceRefusal(presentedToken, highWater);
		}

		return result;
	}

	private async ValueTask<T> ExecuteOwnedAsync<T>(
		Func<OracleConnection, DbTransaction, OraclePhysicalOutboxScope, CancellationToken, Task<T>> action, CancellationToken token)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		var source = _workerConnections ?? throw new InvalidOperationException(MissingWorkerConnectionMessage);
		var lease = await source.AcquireAsync(_logger, token).ConfigureAwait(false);
		DbTransaction? transaction = null;
		Exception? primary = null;
		try
		{
			OracleWorkerConnectionSource.RejectAmbientTransaction();
			await lease.Connection.OpenAsync(token).ConfigureAwait(false);
			transaction = await lease.Connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, token).ConfigureAwait(false);
			var scope = await OraclePhysicalOutboxScope.ResolveAsync(lease.Connection, transaction, _options, token).ConfigureAwait(false);
			var result = await action(lease.Connection, transaction, scope, token).ConfigureAwait(false);
			await transaction.CommitAsync(token).ConfigureAwait(false);
			return result;
		}
		catch (Exception error)
		{
			primary = error;
			if (transaction is not null)
			{
				try
				{
					using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(30));
					await transaction.RollbackAsync(cleanup.Token).ConfigureAwait(false);
				}
				catch (Exception rollbackError) { LogWorkerCleanup(rollbackError, "rollback"); }
			}
			throw;
		}
		finally
		{
			Exception? cleanupFailure = null;
			if (transaction is not null)
			{
				try { await transaction.DisposeAsync().ConfigureAwait(false); }
				catch (Exception error) { cleanupFailure = error; LogWorkerCleanup(error, "transaction"); }
			}
			try { await lease.DisposePreservingAsync(primary ?? cleanupFailure).ConfigureAwait(false); }
			catch (Exception error) { cleanupFailure ??= error; LogWorkerCleanup(error, "connection/scope"); }
			if (primary is null && cleanupFailure is not null) { ExceptionDispatchInfo.Capture(cleanupFailure).Throw(); }
		}
	}

	[LoggerMessage(OutboxOracleEventId.OutboxWorkerTransactionCleanup, LogLevel.Warning, "Oracle worker cleanup failed during {Operation}; the primary operation error is preserved.")]
	private partial void LogWorkerCleanup(Exception error, string operation);
}
