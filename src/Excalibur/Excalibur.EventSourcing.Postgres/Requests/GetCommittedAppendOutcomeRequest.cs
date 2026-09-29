// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch;

namespace Excalibur.EventSourcing.Postgres.Requests;

/// <summary>
/// What a read of the stream says about an append whose outcome the caller did not learn.
/// </summary>
/// <param name="CommittedCount">How many of the queried event identifiers are durably present.</param>
/// <param name="FirstPosition">The global position of the earliest of them, if any.</param>
/// <param name="LastVersion">The stream version of the latest of them, if any.</param>
internal readonly record struct CommittedAppendOutcome(int CommittedCount, long? FirstPosition, long? LastVersion);

/// <summary>
/// Asks whether the rows carrying a specific set of event identifiers are already committed.
/// </summary>
/// <remarks>
/// <para>
/// Keyed on IDENTITY, never on a version slot. "Another writer took my version" and "I took it myself and
/// lost the acknowledgement" move the stream identically, so a classifier reading the version must get one
/// of the two wrong -- and getting the second one wrong is what turns a recoverable retry into a permanent
/// duplicate, because the documented remedy for a conflict appends the same business event again at the
/// NEXT version, where the stream uniqueness key cannot catch it.
/// </para>
/// <para>
/// A slot-keyed variant ("is our event at the version we expected") is deliberately not used: it is blind
/// to an append that committed at some other version, reads it as absent, and reopens the same hole.
/// </para>
/// <para>
/// The tenant predicate stays. It is not redundant defence: this statement is keyed on event_id, which is
/// NOT the primary key, so it is not a tenant term on a statement already addressed by one.
/// </para>
/// </remarks>
internal sealed class GetCommittedAppendOutcomeRequest : DataRequestBase<IDbConnection, CommittedAppendOutcome>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="GetCommittedAppendOutcomeRequest"/> class.
	/// </summary>
	/// <param name="eventIds">The event identifiers this append attempted to write.</param>
	/// <param name="scope">The tenant scope the append ran under.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <param name="schema">The schema holding the events table.</param>
	/// <param name="table">The events table.</param>
	public GetCommittedAppendOutcomeRequest(
		IReadOnlyCollection<string> eventIds,
		TenantScope scope,
		CancellationToken cancellationToken,
		string schema = "public",
		string table = "events")
	{
		ArgumentNullException.ThrowIfNull(eventIds);

		var qualifiedTable = PgTableName.Format(schema, table);
		var partition = KeyedTenantPartition.FromScope(scope);
		const string tenantPredicate = " AND COALESCE(tenant_id, @UntenantedSentinel) = @TenantId";

#pragma warning disable CA2100 // Schema and table validated by SqlIdentifierValidator in PgTableName.Format
		var sql = $"""
			SELECT COUNT(*) AS "CommittedCount", MIN(position) AS "FirstPosition", MAX(version) AS "LastVersion"
			FROM {qualifiedTable}
			WHERE event_id = ANY(@EventIds){tenantPredicate}
			""";
#pragma warning restore CA2100

		var parameters = new DynamicParameters();
		parameters.Add("@EventIds", eventIds as string[] ?? [.. eventIds]);
		parameters.Add("@TenantId", partition.TenantId);
		parameters.Add("@UntenantedSentinel", KeyedTenantPartition.Untenanted.TenantId);

		Command = CreateCommand(sql, parameters, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
			await connection.QuerySingleAsync<CommittedAppendOutcome>(Command).ConfigureAwait(false);
	}
}
