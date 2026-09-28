// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Data;

using Dapper;

using Excalibur.Data;

namespace Excalibur.EventSourcing.Oracle.Requests;

/// <summary>
/// Data request that reserves a contiguous block of global stream positions from the event store's
/// position counter, inside the caller's append transaction.
/// </summary>
/// <remarks>
/// <para>
/// This request is the mechanism behind the event store's global ordering guarantee, and the reason the
/// position column is a plain <c>NUMBER(19)</c> rather than <c>GENERATED ALWAYS AS IDENTITY</c>.
/// </para>
/// <para>
/// An Oracle identity column is a sequence, and a sequence hands its number out when the row is inserted
/// and lets that number escape the transaction: the value is allocated before COMMIT and is not returned
/// to the sequence if the transaction rolls back. Two further Oracle-specific properties make this worse
/// here than elsewhere. A sequence defaults to <c>CACHE 20</c>, so each session draws a BLOCK of values
/// and hands them out from that block -- a pooled session can therefore issue a LOW position long after a
/// different session has committed a HIGHER one, which breaks monotonic allocation outright rather than
/// merely leaving holes. And cached values are discarded when the instance restarts.
/// </para>
/// <para>
/// A subscriber that has already read the higher position then never sees the lower one: the event is
/// committed, durable, and permanently invisible to every projection, with nothing downstream able to
/// detect it.
/// </para>
/// <para>
/// Updating a single counter row instead makes the allocation part of the transaction. The row lock taken
/// here is released only at COMMIT, so no other append can allocate while this one is in flight; and the
/// increment rolls back with the transaction, so an aborted append burns no value. The invariant is that
/// the set of committed positions is, at every instant, a contiguous prefix starting at 1 -- with no
/// dependency on sequence cache settings, instance restarts, or session pooling.
/// </para>
/// <para>
/// The cost is that appends serialize on this row for the remainder of the transaction, which bounds
/// sustained append throughput at roughly one commit. That is the intrinsic price of a single global
/// total order over concurrent writers rather than an artifact of this implementation. Note that
/// <c>NOCACHE</c> on a sequence would pay a comparable serialization cost on <c>SEQ$</c> and STILL leave
/// holes on rollback, so it is not a cheaper alternative -- it is the same cost with none of the benefit.
/// </para>
/// </remarks>
internal sealed class AllocateGlobalPositionsRequest : DataRequestBase<IDbConnection, long>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="AllocateGlobalPositionsRequest"/> class.
	/// </summary>
	/// <param name="count">How many consecutive positions to reserve. Must be greater than zero.</param>
	/// <param name="transaction">
	/// The append transaction. This is <strong>required</strong>: the counter row's lock is what orders
	/// concurrent appends, and a lock taken outside the append's transaction is released before the events
	/// are committed, which restores exactly the defect this request exists to remove.
	/// </param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="schema">The schema holding the position counter. Default: "EXCALIBUR".</param>
	/// <param name="table">The position counter table name. Default: "EVENTSTOREEVENTSPOSITION".</param>
	public AllocateGlobalPositionsRequest(
		int count,
		IDbTransaction transaction,
		CancellationToken cancellationToken,
		string schema = "EXCALIBUR",
		string table = "EVENTSTOREEVENTSPOSITION")
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
		ArgumentNullException.ThrowIfNull(transaction);

		var qualifiedTable = OracleTableName.Format(schema, table);

		// Oracle has no UPDATE ... RETURNING usable as a scalar result set, so the block below returns the
		// allocated value through an OUT bind.
		//
		// THE BLOCK COUNT IS BOUND TWICE, UNDER DISTINCT NAMES, ON PURPOSE. ODP.NET binds by POSITION
		// unless BindByName is set, and this request goes through Dapper rather than a hand-built
		// OracleCommand, so it does not own the command object and cannot set that flag. Under positional
		// binding every placeholder OCCURRENCE consumes a parameter, so a single `Count` reused in two
		// places would leave the third placeholder — the OUT bind — unbound.
		//
		// Two named parameters added in the exact left-to-right order the placeholders appear is therefore
		// correct under BOTH binding modes, which is the property to preserve: positional binding matches
		// them by order, named binding matches them by name, and neither depends on how the driver
		// happens to treat a repeated name. A previous comment here asserted the command ran with
		// BindByName; nothing on this path sets it, and relying on the driver's handling of a duplicate
		// name was an unpinned dependency on undocumented behaviour rather than a stated contract.
		//
		// This mirrors the sibling requests in this package (SaveSnapshotRequest's _k/_u/_i suffixes for a
		// MERGE that reuses each value in both branches).
		var parameters = new DynamicParameters();
		parameters.Add("CountApply", count, DbType.Int64);
		parameters.Add("CountOffset", count, DbType.Int64);
		parameters.Add("OutFirst", dbType: DbType.Int64, direction: ParameterDirection.Output);

#pragma warning disable CA2100 // Schema and table validated by SqlIdentifierValidator in OracleTableName.Format
		var sql = $"""
			BEGIN
			  UPDATE {qualifiedTable} SET VALUE = VALUE + :CountApply WHERE ID = 1
			  RETURNING VALUE - :CountOffset + 1 INTO :OutFirst;
			  IF SQL%ROWCOUNT = 0 THEN
			    RAISE_APPLICATION_ERROR(-20001, 'Event store position counter row is missing.');
			  END IF;
			END;
			""";
#pragma warning restore CA2100

		Command = CreateCommand(sql, parameters, transaction, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
		{
			// The counter row is created and seeded by the event store schema script. If it is absent the
			// store cannot allocate a position at all, and inserting one lazily here would race other
			// appenders and hand out duplicate positions -- so the block above raises rather than repairs.
			_ = await connection.ExecuteAsync(Command).ConfigureAwait(false);

			return parameters.Get<long>("OutFirst");
		};
	}
}
