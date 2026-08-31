// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data;

namespace Excalibur.Outbox.SqlServer.Requests;

/// <summary>
/// Data request to mark a batch of messages as failed in the outbox.
/// </summary>
/// <remarks>
/// The set-based counterpart of <see cref="MarkMessageFailedRequest"/>. Both compose their statement from
/// <see cref="OutboxFailureMark"/>, so the two paths reach the same contract: the same ownership and
/// not-already-sent guards, the same non-decreasing retry count, and the same visibility floor. The batch
/// path previously wrote its own statement and carried none of them, which meant a message's guarantees
/// depended on whether the processor happened to settle it individually or as part of a batch.
/// <para>
/// Internal: the store is its only caller, and nothing in the consumer contract requires constructing a
/// batch failure mark directly.
/// </para>
/// </remarks>
[NoTenantTerm(
	TenantConfinement.IdentityAddressed,
	"the outbox Id is the table's primary key, so this statement addresses exactly the rows named by the id set. The drain claims across tenants and hands back rows addressed by those globally-unique Ids, so the mark must be able to address the rows the claim returned; a tenant term could only subtract from that set, never redirect the statement to different rows")]
internal sealed class MarkBatchFailedRequest : DataRequestBase<IDbConnection, int>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="MarkBatchFailedRequest"/> class.
	/// </summary>
	/// <param name="tableName">The qualified outbox table name.</param>
	/// <param name="messageIds">The message IDs to mark as failed.</param>
	/// <param name="errorMessage">The error message.</param>
	/// <param name="retryCount">The current retry count.</param>
	/// <param name="leasedBy">
	/// Identifier of the processor marking the messages failed (the same value written to <c>LeasedBy</c> when
	/// the rows were claimed). The update only affects rows that are unleased or still leased by this
	/// processor, so a stale processor cannot overwrite messages a peer has since re-claimed.
	/// </param>
	/// <param name="floorSeconds">
	/// The failure-anchored visibility floor F, in seconds. <c>NextAttemptAt</c> is set to
	/// <c>SYSUTCDATETIME() + F</c> on the server clock, so a freed lease always carries a lower bound on the
	/// next claim rather than becoming immediately re-claimable.
	/// </param>
	/// <param name="commandTimeout">Command timeout in seconds.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public MarkBatchFailedRequest(
		string tableName,
		IReadOnlyList<string> messageIds,
		string errorMessage,
		int retryCount,
		string leasedBy,
		int floorSeconds,
		int commandTimeout,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
		ArgumentNullException.ThrowIfNull(messageIds);
		ArgumentNullException.ThrowIfNull(errorMessage);
		ArgumentException.ThrowIfNullOrWhiteSpace(leasedBy);

		// The batch path has no caller-computed schedule, so the floor is the whole of the assignment.
		var nextAttemptClause = OutboxFailureMark.NextAttemptClause(hasNextAttempt: false, hasFloor: true);

		var sql = $"""
			UPDATE {tableName}
			{OutboxFailureMark.SetClause}{nextAttemptClause}
			WHERE Id IN @Ids
			{OutboxFailureMark.Guards}
			""";

		var parameters = new DynamicParameters();
		parameters.Add("@Ids", messageIds);
		parameters.Add("@ErrorMessage", errorMessage);
		parameters.Add("@RetryCount", retryCount);
		parameters.Add("@LeasedBy", leasedBy);
		parameters.Add("@LastAttemptAt", DateTimeOffset.UtcNow);
		parameters.Add("@FloorSeconds", floorSeconds);

		Command = CreateCommand(sql, parameters, commandTimeout: commandTimeout, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
			await connection.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
