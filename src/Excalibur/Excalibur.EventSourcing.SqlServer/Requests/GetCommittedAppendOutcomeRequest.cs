// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch;

namespace Excalibur.EventSourcing.SqlServer.Requests;

/// <summary>
/// What the store observed about its OWN events after an append failed partway.
/// </summary>
/// <param name="CommittedCount">How many of this append's events are present in the table.</param>
/// <param name="FirstPosition">
/// The lowest global position among them, or <see langword="null"/> when none are present.
/// </param>
/// <param name="LastVersion">
/// The highest stream version among them, or <see langword="null"/> when none are present.
/// </param>
internal readonly record struct CommittedAppendOutcome(int CommittedCount, long? FirstPosition, long? LastVersion);

/// <summary>
/// Asks whether THIS append's events are in the table, after its transaction failed to report success.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists: the version cannot answer the question.</b> When a commit throws, the two
/// possibilities are that the transaction rolled back, or that the server committed and the
/// acknowledgement was lost — a connection reset, a command timeout, a pause past the client timeout.
/// Both leave the same observable trace in the stream version: it moved. Classifying on the version
/// therefore reports a durably committed append as a concurrency conflict, and the documented response
/// to a conflict is reload-and-retry, which appends the same business event a second time at the next
/// version. The stream uniqueness key does not stop that, because the version differs.
/// </para>
/// <para>
/// <b>So the store asks a question only its own append can answer:</b> are the event identifiers this
/// call generated present? That is total where the version comparison is ambiguous — present means the
/// commit landed, absent means it rolled back — and it removes the guess rather than improving it.
/// </para>
/// <para>
/// Runs on the failure path only, outside any transaction, and never throws in a way that replaces the
/// original fault: a caller that cannot complete this read is no worse off than before it existed.
/// </para>
/// </remarks>
internal sealed class GetCommittedAppendOutcomeRequest : DataRequestBase<IDbConnection, CommittedAppendOutcome>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="GetCommittedAppendOutcomeRequest"/> class.
	/// </summary>
	/// <param name="eventIds">The event identifiers this append generated.</param>
	/// <param name="scope">
	/// The tenant scope. The event store is a <strong>keyed</strong> tenant table, so this read is
	/// always partitioned by a non-null tenant term, exactly as its siblings are.
	/// </param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="schema">The schema name for the event store table. Default: "dbo".</param>
	/// <param name="table">The event store table name. Default: "EventStoreEvents".</param>
	public GetCommittedAppendOutcomeRequest(
		IReadOnlyCollection<string> eventIds,
		TenantScope scope,
		CancellationToken cancellationToken,
		string schema = "dbo",
		string table = "EventStoreEvents")
	{
		ArgumentNullException.ThrowIfNull(eventIds);

		var qualifiedTable = SqlTableName.Format(schema, table);
		var partition = KeyedTenantPartition.FromScope(scope);

		// Same keyed-tenant partition as every sibling read: the resolved tenant when scoped, the
		// reserved sentinel when unscoped, and COALESCE folding a legacy NULL tenant to the sentinel so
		// a pre-migration row is not silently invisible to this classification.
		const string TenantPredicate = " AND COALESCE(TenantId, @UntenantedSentinel) = @TenantId";

		// No lock hint, deliberately. This runs after the transaction is gone, and it is a question
		// about durably committed rows: a row either survived the commit or it did not.
#pragma warning disable CA2100 // Table name is validated by SqlTableName.Format; every value is bound
		var sql =
			$"SELECT COUNT(*) AS CommittedCount, MIN(Position) AS FirstPosition, MAX(Version) AS LastVersion "
			+ $"FROM {qualifiedTable} WHERE EventId IN @EventIds{TenantPredicate};";
#pragma warning restore CA2100

		var parameters = new DynamicParameters();
		parameters.Add("@EventIds", eventIds);
		parameters.Add("@TenantId", partition.TenantId);
		parameters.Add("@UntenantedSentinel", TenantScope.UntenantedSentinel);

		Command = CreateCommand(sql, parameters, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
		{
			// An append with no events cannot have committed anything, and asking the database would
			// emit an empty IN list, which is not valid SQL.
			if (eventIds.Count == 0)
			{
				return new CommittedAppendOutcome(0, null, null);
			}

			return await connection.QuerySingleAsync<CommittedAppendOutcome>(Command).ConfigureAwait(false);
		};
	}
}
