// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data;

namespace Excalibur.Outbox.SqlServer.Requests;

/// <summary>
/// Data request to mark a message as sent in the outbox.
/// </summary>
public sealed class MarkMessageSentRequest : DataRequestBase<IDbConnection, int>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="MarkMessageSentRequest"/> class.
	/// </summary>
	/// <param name="tableName">The qualified outbox table name.</param>
	/// <param name="messageId">The message ID to mark as sent.</param>
	/// <param name="commandTimeout">Command timeout in seconds.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="fencingToken">
	/// The fencing token for the caller's current leadership tenure, or <see langword="null"/> when no
	/// fencing applies. When non-null, the update is applied only if <paramref name="fencingToken"/> is
	/// greater than or equal to the recorded fencing high-water mark; the row's <c>FencingToken</c> column
	/// is atomically advanced to this value as part of the same update. A zero-rows-affected result with a
	/// stale token indicates a superseded leader; the caller is responsible for distinguishing that from a
	/// not-found row and throwing <c>StaleOutboxFencingTokenException</c>.
	/// <para>
	/// The high-water mark is read from the durable <c>OutboxFence</c> control table (keyed by
	/// <paramref name="fenceScope"/>), not from <c>MAX(FencingToken)</c> over the message rows. Because
	/// cleanup never deletes that control row, the high-water outlives the purge of sent, token-bearing
	/// rows, so a superseded leader's stale token stays rejected across a cleanup. The presented token is
	/// compared inside the update statement itself, so the guard and the mutation are one atomic step.
	/// </para>
	/// </param>
	/// <param name="fenceTableName">The qualified durable fence control table name.</param>
	/// <param name="fenceScope">The fence scope key — the qualified outbox table name this fence guards.</param>
	/// <remarks>
	/// The mark targets the globally-unique outbox <c>Id</c>, which addresses exactly one row, so no tenant
	/// predicate is applied: the drain is cross-tenant infrastructure and must always be able to mark the row
	/// it claimed, regardless of any ambient tenant context. Tenant isolation lives on the write/stage path
	/// (<c>TenantId</c> stamping) and on tenant-facing queries.
	/// </remarks>
	public MarkMessageSentRequest(
		string tableName,
		string messageId,
		int commandTimeout,
		long? fencingToken,
		string fenceTableName,
		string fenceScope,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(fenceTableName);
		ArgumentException.ThrowIfNullOrWhiteSpace(fenceScope);

		var sql = $"""
			UPDATE {tableName}
			SET Status = 2, SentAt = @SentAt, LastError = NULL,
				-- Release the lease on completion. A sent message is terminal and holds no lease, but
				-- these columns were left populated, and GetStatistics counts "Sending" as
				-- (LeasedAt IS NOT NULL) with no status predicate -- so every message ever sent stayed
				-- in that count forever and SendingMessageCount grew without bound for the life of the
				-- table, reporting long-completed messages as still in flight.
				LeasedAt = NULL, LeasedBy = NULL,
				FencingToken = CASE WHEN @FencingToken IS NULL THEN FencingToken ELSE @FencingToken END
			WHERE Id = @MessageId
				AND Status <> 2
				AND (@FencingToken IS NULL OR @FencingToken >= ISNULL((SELECT HighWaterToken FROM {fenceTableName} WHERE OutboxTable = @FenceScope), 0))
			""";

		var parameters = new DynamicParameters();
		parameters.Add("@MessageId", messageId);
		parameters.Add("@SentAt", DateTimeOffset.UtcNow);
		parameters.Add("@FencingToken", fencingToken);
		parameters.Add("@FenceScope", fenceScope);

		Command = CreateCommand(sql, parameters, commandTimeout: commandTimeout, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
			await connection.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
