// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data;

namespace Excalibur.Outbox.Oracle;

/// <summary>
/// Represents a data request to reset outbox message reservations for a specific dispatcher in the Oracle database.
/// </summary>
[NoTenantTerm(
	TenantConfinement.EstateWide,
	"a dispatcher-scoped recovery sweep: it releases the reservations held by a named dispatcher so its claimed messages become re-claimable after a crash. A dispatcher serves every tenant, so the rows it holds span tenants; the statement is keyed on dispatcher identity, which is the correct key for the job, and never on tenant state")]
public sealed class ResetOutboxMessageReservation : DataRequest<int>
{

	/// <summary>
	/// Initializes a new instance of the <see cref="ResetOutboxMessageReservation"/> class.
	/// </summary>
	/// <param name="dispatcherId">The unique identifier of the dispatcher whose reservations should be reset.</param>
	/// <param name="outboxTableName">The name of the outbox table.</param>
	/// <param name="sqlTimeOutSeconds">The SQL command timeout in seconds.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <remarks>
	/// The reserve path stamps <c>dispatcher_id</c> with a per-call value (<c>"{dispatcherId}:{guid}"</c>),
	/// so this release matches the dispatcher-id <em>prefix</em> to clear every reservation that dispatcher
	/// holds, not a single exact id.
	/// </remarks>
	public ResetOutboxMessageReservation(string dispatcherId, string outboxTableName, int sqlTimeOutSeconds,
		CancellationToken cancellationToken)
	{
		var sql = $"""
		   UPDATE {outboxTableName}
		           SET dispatcher_id = NULL,
		           dispatcher_timeout = NULL
		           WHERE dispatcher_id = :DispatcherId OR SUBSTR(dispatcher_id, 1, :DispatcherPrefixLen) = :DispatcherPrefix
		   """;

		// Match every per-call claim token this dispatcher holds via an EXACT prefix — NOT a bare
		// "LIKE :dispatcherId || ':%'": DispatcherId = "dispatcher-{MachineName}-{pid}" and MachineName may
		// contain '_'/'%' (Oracle LIKE wildcards), so a raw LIKE would over-match a FOREIGN dispatcher's tokens
		// and this reset would clear its LIVE reservation (double-delivery). SUBSTR(dispatcher_id, 1, LEN) =
		// "{dispatcherId}:" is an exact prefix, no wildcards. ODP.NET binds positionally (no BindByName): add in
		// placeholder order — :DispatcherId, :DispatcherPrefixLen, :DispatcherPrefix.
		var parameters = new DynamicParameters();
		parameters.Add("DispatcherId", dispatcherId, direction: ParameterDirection.Input);
		parameters.Add("DispatcherPrefixLen", dispatcherId.Length + 1, direction: ParameterDirection.Input);
		parameters.Add("DispatcherPrefix", dispatcherId + ":", direction: ParameterDirection.Input);

		Command = CreateCommand(sql, (DynamicParameters?)parameters, commandTimeout: sqlTimeOutSeconds, cancellationToken: cancellationToken);
		ResolveAsync = async conn => await conn.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
