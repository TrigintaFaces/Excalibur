// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Cdc.SqlServer;

/// <summary>
/// Identifies the consumer a deduplication record belongs to, as the same tuple the checkpoint store
/// advances under.
/// </summary>
/// <param name="ConnectionIdentifier">
/// The configured connection identifier — a connection-string NAME, which is what the job path passes and
/// what the checkpoint store records.
/// </param>
/// <param name="DatabaseName">The configured source database name.</param>
/// <remarks>
/// <para>
/// <b>This type exists because the two identities had drifted apart, in the direction that suppresses.</b>
/// The checkpoint store matches on <c>(DatabaseConnectionIdentifier, DatabaseName, TableName)</c> — three
/// axes. The deduplication key used two of them, omitting the database name, so the dedupe namespace was
/// strictly COARSER than the namespace of the position it guards. Two configured sources sharing a
/// connection identifier but differing in database name therefore kept separate checkpoints while sharing
/// one dedupe namespace, and the first to process a change at a given position marked it done for the
/// other — a change skipped that was never processed, silently.
/// </para>
/// <para>
/// A comment at the call site asserted the opposite: that sourcing both from one field meant the filter
/// and the position it guards "cannot disagree about who is asking". Sourcing them from one field is
/// exactly what made them disagree, because one of the two identities had a third axis.
/// </para>
/// <para>
/// <b>Passing the pair as one value is what keeps them from drifting again.</b> A sixth string parameter
/// alongside the table name and position would have allowed a caller to supply the connection identifier
/// and forget the database — which is the bug, re-expressible. It would also have put the method over the
/// five-parameter threshold. With this type, "identify the consumer by connection alone" cannot be
/// written.
/// </para>
/// <para>
/// Nothing establishes that a connection identifier is unique across whatever shares one dedupe table:
/// the options validator checks only that the collection is non-empty, the fan-out's <c>Distinct()</c> is
/// reference equality over a class with no equality members, and the dedupe table's database is chosen
/// independently of any source. So the invariant is not that identifiers happen to differ — it is that
/// this key carries every axis the checkpoint carries.
/// </para>
/// </remarks>
internal readonly record struct CdcConsumerIdentity(string ConnectionIdentifier, string DatabaseName)
{
	/// <summary>
	/// Gets a single string form for logging and for the in-memory key, with a separator that cannot occur
	/// in a SQL Server identifier, so two different pairs cannot collapse to one value.
	/// </summary>
	/// <returns>The composed identity.</returns>
	public override string ToString() => $"{ConnectionIdentifier}\u001f{DatabaseName}";
}
