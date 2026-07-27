// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data;

namespace Excalibur.Outbox.Postgres;

/// <summary>
/// The single-row result of a fenced outbox mutation: the effective high-water token after the
/// fence compare-and-swap and the number of rows the guarded mutation affected.
/// </summary>
internal sealed class FenceMutationResult
{
	/// <summary>
	/// Gets or sets the effective (monotonically advanced) high-water token for the scope after the CAS.
	/// Equal to the presented token when the token was accepted; strictly greater when a successor leader
	/// has already advanced past it (stale).
	/// </summary>
	public long HighWaterToken { get; set; }

	/// <summary>
	/// Gets or sets the number of rows the guarded mutation deleted (0 or 1).
	/// </summary>
	public long DeletedCount { get; set; }
}

/// <summary>
/// Represents a data request that atomically enforces the leadership-fencing high-water mark AND deletes
/// (marks-sent) the target outbox row in a <em>single</em> statement, closing the check-then-act window
/// between the fence compare-and-swap and the mutation.
/// </summary>
/// <remarks>
/// <para>
/// The fence common-table-expression runs first: its <c>INSERT … ON CONFLICT DO UPDATE … RETURNING</c>
/// takes the per-scope row lock and advances the stored high-water to <c>GREATEST(existing, presented)</c>.
/// The delete CTE is gated on <c>(SELECT high_water_token FROM fence) = @Token</c>, so the row is removed
/// only when the freshly-advanced high-water equals the presented token (i.e. the token was accepted). A
/// superseded (stale) leader observes a returned high-water strictly greater than its token and deletes
/// nothing. Because both effects execute in one statement under the fence row lock, no fresher leader can
/// interleave between the fence check and the delete — the TOCTOU hole the separate fence-then-delete path
/// exposed is closed by construction.
/// </para>
/// <para>
/// The single returned row carries the effective <c>HighWaterToken</c> and the <c>DeletedCount</c>, which
/// the caller disambiguates: <c>HighWaterToken != presented</c> is a stale (fail-closed) rejection;
/// <c>HighWaterToken == presented &amp;&amp; DeletedCount == 0</c> is a not-found/already-sent condition;
/// <c>HighWaterToken == presented &amp;&amp; DeletedCount == 1</c> is success.
/// </para>
/// <para>
/// The delete targets the globally-unique outbox <c>message_id</c>, which addresses exactly one row, so no
/// tenant predicate is applied: the drain is cross-tenant infrastructure and must always be able to remove
/// the row it claimed, regardless of any ambient tenant context.
/// </para>
/// </remarks>
internal sealed class FencedDeleteOutboxMessage : DataRequest<FenceMutationResult>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="FencedDeleteOutboxMessage"/> class.
	/// </summary>
	/// <param name="messageId">The unique identifier of the message to delete (mark sent).</param>
	/// <param name="outboxTableName">The qualified name of the outbox table.</param>
	/// <param name="fencingToken">The fencing token for the caller's current leadership tenure.</param>
	/// <param name="fenceTableName">The qualified name of the fence control table.</param>
	/// <param name="sqlTimeOutSeconds">The SQL command timeout in seconds.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public FencedDeleteOutboxMessage(
		string messageId,
		string outboxTableName,
		long fencingToken,
		string fenceTableName,
		int sqlTimeOutSeconds,
		CancellationToken cancellationToken)
	{
		var sql = $"""
		   WITH fence AS (
		           INSERT INTO {fenceTableName} AS f (scope_key, high_water_token)
		               VALUES (@ScopeKey, @Token)
		               ON CONFLICT (scope_key) DO UPDATE
		                   SET high_water_token = GREATEST(f.high_water_token, @Token)
		               RETURNING high_water_token
		           ),
		           del AS (
		               DELETE FROM {outboxTableName}
		               WHERE message_id = @MessageId
		                 AND (SELECT high_water_token FROM fence) = @Token
		               RETURNING message_id
		           )
		           SELECT (SELECT high_water_token FROM fence) AS HighWaterToken,
		                  (SELECT COUNT(*) FROM del) AS DeletedCount;
		   """;

		var parameters = new DynamicParameters();
		parameters.Add("ScopeKey", outboxTableName, direction: ParameterDirection.Input);
		parameters.Add("Token", fencingToken, direction: ParameterDirection.Input);
		parameters.Add("MessageId", messageId, direction: ParameterDirection.Input);

		Command = CreateCommand(sql, (DynamicParameters?)parameters, commandTimeout: sqlTimeOutSeconds,
			cancellationToken: cancellationToken);
		ResolveAsync = async conn => await conn.QuerySingleAsync<FenceMutationResult>(Command).ConfigureAwait(false);
	}
}
