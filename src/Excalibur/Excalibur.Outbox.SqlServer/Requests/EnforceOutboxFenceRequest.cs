// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data;

namespace Excalibur.Outbox.SqlServer.Requests;

/// <summary>
/// Atomic leadership-fence compare-and-advance against the durable <c>OutboxFence</c> control table.
/// </summary>
/// <remarks>
/// <para>
/// A single serializable <c>MERGE</c> both reads the recorded high-water and advances it monotonically to
/// the presented token — never lowering it. Because the whole compare-and-advance is one statement under
/// <c>UPDLOCK, HOLDLOCK</c>, two concurrent leaders cannot both advance the high-water: the second observes the
/// first's write and is ordered after it. The statement always emits exactly one row carrying the
/// <em>resulting</em> high-water:
/// </para>
/// <list type="bullet">
/// <item>presented token &gt;= recorded high-water → the high-water advances to (or stays at) the token,
/// and the result equals the token (accepted);</item>
/// <item>presented token &lt; recorded high-water → the high-water is left unchanged, and the result is the
/// recorded (higher) high-water (rejected — the caller was superseded).</item>
/// </list>
/// <para>
/// The caller therefore decides staleness by comparing the returned high-water to the presented token:
/// a returned value strictly greater than the token means the token was rejected, and that returned value
/// is the recorded high-water to report on the fencing diagnostic. The high-water lives in a table cleanup
/// never touches, so it survives the deletion of the token-bearing message rows.
/// </para>
/// </remarks>
[NoTenantTerm(
	TenantConfinement.EstateWide,
	"the fence is keyed on the outbox table, not on a tenant: a fencing token orders competing processors of one table and has no tenant dimension to scope to")]
public sealed class EnforceOutboxFenceRequest : DataRequestBase<IDbConnection, long>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="EnforceOutboxFenceRequest"/> class.
	/// </summary>
	/// <param name="fenceTableName">The qualified fence control table name.</param>
	/// <param name="scope">The fence scope key — the qualified outbox table name this fence guards.</param>
	/// <param name="fencingToken">The leadership fencing token presented by the caller.</param>
	/// <param name="commandTimeout">Command timeout in seconds.</param>
	/// <param name="transaction">Optional transaction to participate in.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public EnforceOutboxFenceRequest(
		string fenceTableName,
		string scope,
		long fencingToken,
		int commandTimeout,
		IDbTransaction? transaction,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(fenceTableName);
		ArgumentException.ThrowIfNullOrWhiteSpace(scope);

		// WHEN MATCHED THEN UPDATE always fires (so the OUTPUT always yields a row), but only raises the
		// high-water when the token is at least the current value — a monotonic advance that never regresses.
		// HOLDLOCK (serializable) on the target range makes the compare-and-advance atomic across concurrent
		// leaders, and UPDLOCK makes that range lock an UPDATE lock rather than a SHARED one. Both hints are
		// required: with HOLDLOCK alone two leaders enforcing the same scope each hold a shared range lock and
		// each need to convert it to exclusive for the write, so the engine breaks the cycle by killing one as
		// a deadlock victim. UPDATE locks are not mutually compatible, so the second leader blocks instead.
		// OUTPUT returns the resulting high-water for both the matched and the inserted branch.
		var sql = $"""
			MERGE {fenceTableName} WITH (UPDLOCK, HOLDLOCK) AS f
			USING (SELECT @Scope AS OutboxTable) AS s ON (f.OutboxTable = s.OutboxTable)
			WHEN MATCHED THEN
				UPDATE SET HighWaterToken =
					CASE WHEN @FencingToken >= f.HighWaterToken THEN @FencingToken ELSE f.HighWaterToken END
			WHEN NOT MATCHED THEN
				INSERT (OutboxTable, HighWaterToken) VALUES (@Scope, @FencingToken)
			OUTPUT INSERTED.HighWaterToken;
			""";

		var parameters = new DynamicParameters();
		parameters.Add("@Scope", scope);
		parameters.Add("@FencingToken", fencingToken);

		Command = CreateCommand(sql, parameters, transaction, commandTimeout, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
			await connection.ExecuteScalarAsync<long>(Command).ConfigureAwait(false);
	}
}
