// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch;

namespace Excalibur.EventSourcing.Oracle.Requests;

/// <summary>
/// What a read of the stream says about an append whose outcome the caller did not learn.
/// </summary>
/// <param name="CommittedCount">How many rows carrying the queried identifier are present.</param>
/// <param name="FirstPosition">The global position of the earliest of them, if any.</param>
/// <param name="LastVersion">The stream version of the latest of them, if any.</param>
/// <param name="LastPosition">
/// The global position of the LATEST of them, if any -- the value a reader waits for to observe the
/// whole recognised append. Measured rather than derived from a count, because a probe answers about
/// rows that are already durable and must report what is there.
/// </param>
internal readonly record struct CommittedAppendOutcome(
	int CommittedCount, long? FirstPosition, long? LastPosition, long? LastVersion);

/// <summary>
/// Asks whether the row carrying a specific event identifier is already committed.
/// </summary>
/// <remarks>
/// <para>
/// Keyed on IDENTITY, never on a version slot. "Another writer took my version" and "I took it myself and
/// lost the acknowledgement" move the stream identically, so a classifier reading the version must get one
/// of the two wrong -- and getting the second wrong turns a recoverable retry into a permanent duplicate,
/// because the remedy for a conflict appends the same business event again at the NEXT version where the
/// stream uniqueness key cannot catch it.
/// </para>
/// <para>
/// ONE identifier is asked for, not the whole batch, and that rests on the append being all-or-nothing:
/// this provider commits a batch in a single transaction, so if the first event is present then every
/// event of that batch is. THAT ATOMICITY IS LOAD-BEARING FOR CORRECTNESS here. A provider with a
/// non-atomic write path must ask for every identifier instead, because a torn prefix would otherwise be
/// reported as a whole committed batch.
/// </para>
/// <para>
/// The tenant predicate stays. It is not redundant defence: this statement is keyed on EVENTID, which is
/// NOT the primary key, so it is not a tenant term on a statement already addressed by one.
/// </para>
/// </remarks>
internal sealed class GetCommittedAppendOutcomeRequest : DataRequestBase<IDbConnection, CommittedAppendOutcome>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="GetCommittedAppendOutcomeRequest"/> class.
	/// </summary>
	/// <param name="eventIds">
	/// Every event identifier this append attempted to write. All of them, because the reported
	/// boundaries are aggregates: probing one identity would take a minimum and a maximum over a single
	/// row and report the first event's position as the batch's last.
	/// </param>
	/// <param name="scope">The tenant scope the append ran under.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <param name="schema">The schema holding the events table.</param>
	/// <param name="table">The events table.</param>
	public GetCommittedAppendOutcomeRequest(
		IReadOnlyList<string> eventIds,
		TenantScope scope,
		CancellationToken cancellationToken,
		string schema = "EXCALIBUR",
		string table = "EVENTSTOREEVENTS")
	{
		ArgumentNullException.ThrowIfNull(eventIds);

		if (eventIds.Count == 0)
		{
			throw new ArgumentException(
				"At least one event identifier is required: the probe reports the committed batch's "
				+ "boundaries, and an empty set has none.",
				nameof(eventIds));
		}

		var qualifiedTable = OracleTableName.Format(schema, table);
		var partition = KeyedTenantPartition.FromScope(scope);
		const string tenantPredicate = " AND COALESCE(TENANTID, :UntenantedSentinel) = :TenantId";

		// EVERY identity in the batch, not just the first. Atomicity settles EXISTENCE -- if one row is
		// there they all are -- but the aggregates below are a MINIMUM and a MAXIMUM, and restricting the
		// predicate to one identity made them run over a single row. A recognised retry of a three-event
		// batch at positions 10,11,12 then reported LastPosition = 10, so a session read could report
		// Fresh with 11 and 12 still unfolded: the token named a prefix the caller had not committed.
		//
		// The parameter list is built explicitly rather than relying on provider-specific list expansion,
		// because the bound names must match the ':' prefix this request already uses.
		var names = new string[eventIds.Count];
		for (var index = 0; index < eventIds.Count; index++)
		{
			names[index] = $":EventId{index.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
		}

		var inClause = string.Join(", ", names);

#pragma warning disable CA2100 // Schema and table validated by SqlIdentifierValidator in OracleTableName.Format; the IN list is generated parameter names
		var sql = $"""
			SELECT COUNT(*) AS "CommittedCount", MIN(POSITION) AS "FirstPosition", MAX(POSITION) AS "LastPosition", MAX(VERSION) AS "LastVersion"
			FROM {qualifiedTable}
			WHERE EVENTID IN ({inClause}){tenantPredicate}
			""";
#pragma warning restore CA2100

		var parameters = new DynamicParameters();
		for (var index = 0; index < eventIds.Count; index++)
		{
			parameters.Add(names[index], eventIds[index]);
		}

		parameters.Add(":TenantId", partition.TenantId);
		parameters.Add(":UntenantedSentinel", KeyedTenantPartition.Untenanted.TenantId);

		Command = CreateCommand(sql, parameters, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
			await connection.QuerySingleAsync<CommittedAppendOutcome>(Command).ConfigureAwait(false);
	}
}
