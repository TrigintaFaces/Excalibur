// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data;

namespace Excalibur.Outbox.Oracle;

/// <summary>
/// The result of a fenced outbox mutation: the effective high-water token after the fence
/// compare-and-swap and the number of rows the guarded mutation affected.
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
/// (marks-sent) the target outbox row in a <em>single</em> PL/SQL block, closing the check-then-act window
/// between the fence compare-and-swap and the mutation.
/// </summary>
/// <remarks>
/// <para>
/// The block first advances the fence via a <c>MERGE</c> (<c>GREATEST</c> — strictly monotonic), taking the
/// per-scope row lock, then reads the effective high-water. The <c>DELETE</c> executes only when that
/// high-water equals the presented token; a superseded (stale) leader skips the delete entirely. Both the
/// fence advance and the conditional delete run inside one server-side block under the fence row lock, so no
/// fresher leader can interleave between the check and the delete — the TOCTOU hole the separate
/// fence-then-delete path exposed is closed by construction. The block returns the effective high-water and
/// the deleted row count through OUT binds, which the caller disambiguates into stale / not-found / success.
/// </para>
/// <para>
/// A cold-start race — two callers both seeing "not matched" and racing the first INSERT — is caught as
/// <c>DUP_VAL_ON_INDEX</c> and resolved by the equivalent GREATEST UPDATE, so the fence never fails on a
/// concurrent first write.
/// </para>
/// <para>
/// The command binds <b>by name</b> (<see cref="OracleDynamicParameters"/> sets
/// <c>OracleCommand.BindByName = true</c>), so each named value is supplied exactly once even though the
/// block references <c>:ScopeKey</c> and <c>:Token</c> at several occurrences (the MERGE, the
/// DUP_VAL_ON_INDEX fallback UPDATE, the high-water read, and the fence comparison). The invariant rests on
/// the language, not on a hand-maintained positional insertion order.
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
	/// <param name="outboxTableName">The (qualified) name of the outbox table.</param>
	/// <param name="fencingToken">The fencing token for the caller's current leadership tenure.</param>
	/// <param name="fenceTableName">The (qualified) name of the fence control table.</param>
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
		   DECLARE
		       v_high NUMBER;
		       v_deleted NUMBER := 0;
		   BEGIN
		       BEGIN
		           MERGE INTO {fenceTableName} f
		           USING (SELECT :ScopeKey AS scope_key, :Token AS tok FROM dual) src
		           ON (f.scope_key = src.scope_key)
		           WHEN MATCHED THEN UPDATE SET f.high_water_token = GREATEST(f.high_water_token, src.tok)
		           WHEN NOT MATCHED THEN INSERT (scope_key, high_water_token) VALUES (src.scope_key, src.tok);
		       EXCEPTION
		           WHEN DUP_VAL_ON_INDEX THEN
		               UPDATE {fenceTableName} SET high_water_token = GREATEST(high_water_token, :Token) WHERE scope_key = :ScopeKey;
		       END;
		       SELECT high_water_token INTO v_high FROM {fenceTableName} WHERE scope_key = :ScopeKey;
		       IF v_high = :Token THEN
		           DELETE FROM {outboxTableName} WHERE message_id = :MessageId;
		           v_deleted := SQL%ROWCOUNT;
		       END IF;
		       :OutHigh := v_high;
		       :OutDeleted := v_deleted;
		   END;
		   """;

		// BindByName=true (OracleDynamicParameters): each named value is supplied once, no matter how many
		// times :ScopeKey / :Token appear in the block. Insertion order no longer carries meaning.
		var parameters = new DynamicParameters();
		parameters.Add("ScopeKey", outboxTableName, direction: ParameterDirection.Input);
		parameters.Add("Token", fencingToken, direction: ParameterDirection.Input);
		parameters.Add("MessageId", messageId, direction: ParameterDirection.Input);
		parameters.Add("OutHigh", dbType: DbType.Int64, direction: ParameterDirection.Output);
		parameters.Add("OutDeleted", dbType: DbType.Int64, direction: ParameterDirection.Output);

		Command = new CommandDefinition(sql, new OracleDynamicParameters(parameters),
			commandTimeout: sqlTimeOutSeconds, cancellationToken: cancellationToken);
		ResolveAsync = async conn =>
		{
			_ = await conn.ExecuteAsync(Command).ConfigureAwait(false);
			return new FenceMutationResult
			{
				HighWaterToken = parameters.Get<long>("OutHigh"),
				DeletedCount = parameters.Get<long>("OutDeleted"),
			};
		};
	}
}
