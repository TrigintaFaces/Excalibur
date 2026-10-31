// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.ExceptionServices;

namespace Excalibur.EventSourcing.Projections;

/// <summary>
/// The positioned-write half of the shared relational destination, for SQLite and Oracle.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists at all.</b> These two providers shipped implementing only the blind
/// <see cref="IProjectionStore{T}"/> surface, so <c>PositionedProjectionWriter.Resolve</c> found no
/// positioned capability on them and the apply path fell back to the unconditional
/// <see cref="IProjectionStore{T}.UpsertAsync"/>. That fallback is not a slower answer, it is a
/// different and weaker one: an event redelivered after a restart is folded a second time and an
/// accumulating projection double-counts, with nothing downstream able to observe it. Their PostgreSQL
/// and SQL Server siblings both implement the positioned contract, so the gap was provider parity
/// rather than a capability a relational store cannot carry.
/// </para>
/// <para>
/// <b>One implementation for three stores, deliberately.</b> Both provider stores delegate their whole
/// surface here, so the compare-and-set protocol is written once. A copy per provider would give each
/// its own chance to get the ordering subtly wrong, and every copy would pass its own tests.
/// </para>
/// <para>
/// <b>The invariant.</b> For projection id <c>x</c> with stored position <c>P(x)</c>:
/// <c>state(x) = fold(apply, init, { e : pos(e) &lt;= P(x) })</c>. The UPDATE arm of an advance carries
/// both conjuncts that keep it true — <c>LastAppliedPosition = @Expected</c> orders concurrent writers
/// against each other, and <c>@New &gt; LastAppliedPosition</c> orders a writer against its own past,
/// which is what a redelivery is. The second is not redundant: a caller obtains its expected position by
/// reading it, so on a redelivery the first conjunct is satisfied by construction and only the
/// forward-only rule refuses the re-application.
/// </para>
/// <para>
/// <b>Why a refusal is classified from a SECOND observation.</b> When no row is written the statement
/// cannot also report what the row holds, so the position is read afterwards and a concurrent writer can
/// change it in between. That is why the outcome describes what the WRITE did and never what the row
/// CONTAINS — a caller that needs the latter reads it through <see cref="GetWithPositionAsync"/>, where
/// state and position are one observation.
/// </para>
/// </remarks>
/// <typeparam name="T">The projection type.</typeparam>
internal sealed partial class RelationalTransactionalProjectionStore<T>
    where T : class
{
    private const string PositionedColumns = "TenantId, Id, Data, LastAppliedPosition, TransactionalSource, TransactionalAggregateType";

    private static readonly string[] PositionedInsertParameters = ["Tenant", "Id", "Data", "State"];

    /// <summary>
    /// A positioned write takes ownership of the row away from the transactional-inline path.
    /// </summary>
    /// <remarks>
    /// Clearing the two transactional columns is what makes a destination shared between the two apply
    /// paths fail LOUDLY rather than silently: the transactional guard requires the unnumbered sentinel
    /// and a matching source, so once a positioned writer has advanced the row the transactional write
    /// throws instead of interleaving with it.
    /// </remarks>
    private string PositionedAssignments =>
        $"Data = {Parameter("Data")}, LastAppliedPosition = {Parameter("State")}, TransactionalSource = NULL, TransactionalAggregateType = NULL";

    private string RowPredicate =>
        $"{Binary("TenantId")} = {Parameter("Tenant")} AND {Binary("Id")} = {Parameter("Id")}";

    /// <inheritdoc />
    [RequiresUnreferencedCode("Projection serialization uses the configured JSON metadata.")]
    [RequiresDynamicCode("Projection serialization uses the configured JSON metadata.")]
    public async Task<(T? Projection, ProjectionPosition Position)> GetWithPositionAsync(
        string id, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        // ONE statement. Reading state and position separately admits a writer between them, after which
        // the position the caller holds certifies a prefix its state does not contain -- and the
        // conditional write then ACCEPTS, losing every event in the gap. No caller can be correct on a
        // split read, however atomic the write is. Note this read deliberately does NOT filter out the
        // retirement marker the way GetByIdAsync does: reporting Retired is the whole point.
        await using var connection = createConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = createCommand(connection, null,
            $"SELECT Data, LastAppliedPosition FROM {table} WHERE {RowPredicate}",
            [("Tenant", Tenant), ("Id", id)]);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            // An absent row is a fold over no prefix, so Unplaceable is the true reading rather than a
            // convenient one. Unnumbered would assert a complete fold over state that does not exist.
            return (null, ProjectionPosition.Unplaceable);
        }

        var json = await reader.IsDBNullAsync(0, cancellationToken).ConfigureAwait(false) ? null : reader.GetString(0);
        var position = ProjectionPosition.FromStored(
            await reader.IsDBNullAsync(1, cancellationToken).ConfigureAwait(false)
                ? null
                : Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture));

        // Through the store's single deserialization site, not a second options bag here: a read path
        // with its own options drifts from the wire shape the write path produced, silently.
        return position.Kind == ProjectionPositionKind.Retired
            ? (null, position)
            : (Deserialize(json), position);
    }

    /// <inheritdoc />
    [RequiresUnreferencedCode("Projection serialization uses the configured JSON metadata.")]
    [RequiresDynamicCode("Projection serialization uses the configured JSON metadata.")]
    public Task UpsertUnnumberedAsync(string id, T projection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(projection);

        // A complete fold whose global coordinate is unknown. This is a STRICTLY WEAKER claim than
        // UpsertAsync's, which records Unplaceable -- state related to no prefix at all -- and that is
        // the whole reason a caller with no position number reaches for this instead of the blind write.
        return IndependentWriteAsync(id, Serialize(projection), ProjectionPosition.UnnumberedSentinel,
            UpsertGuard.NotRetired, cancellationToken);
    }

    /// <inheritdoc />
    [RequiresUnreferencedCode("Projection serialization uses the configured JSON metadata.")]
    [RequiresDynamicCode("Projection serialization uses the configured JSON metadata.")]
    public async Task<ProjectionAdvanceResult> UpsertAtPositionAsync(
        string id, T projection, long? expectedPosition, long newPosition, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(projection);
        ArgumentOutOfRangeException.ThrowIfNegative(newPosition);

        // A NEGATIVE EXPECTATION IS THE ADOPT LICENCE THROUGH A DIFFERENT DOOR. The negatives are the
        // sentinel space for the two states that carry no number, so a caller naming one as "the position
        // I read" would be matching a row this store refuses by design.
        // ProjectionPosition.ExpectedPositionOrNull is the only legal source for this argument and never
        // yields a negative.
        if (expectedPosition is { } claimed)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(claimed, nameof(expectedPosition));
        }

        var json = Serialize(projection);
        await using var connection = createConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        int affected;
        DbException? conflict = null;
        if (expectedPosition is { } expected)
        {
            // UPDATE-ONLY, and the absence of an insert arm here is the contract rather than an
            // oversight. A caller that read a position may not create a row: an absent row was DELETED,
            // deletion is how erasure removes personal data, and re-folding the stream would reinstate
            // it. The engine reports that as Vanished below instead of silently recreating the subject.
            affected = await ExecuteAsync(connection, transaction,
                $"UPDATE {table} SET {PositionedAssignments} WHERE {RowPredicate} "
                    + $"AND LastAppliedPosition = {Parameter("Expected")} "
                    + $"AND {Parameter("State")} > LastAppliedPosition",
                [("Tenant", Tenant), ("Id", id), ("Data", json), ("State", newPosition), ("Expected", expected)],
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // INSERT-IF-ABSENT AND NOTHING ELSE. A caller that read no position knows nothing about which
            // prefix the stored state covers, so claiming one over an existing row would be a guess --
            // whatever that row's own position reads. An existing row is refused and classified below.
            try
            {
                affected = await ExecuteAsync(connection, transaction, InsertIfAbsentSql(),
                    [("Tenant", Tenant), ("Id", id), ("Data", json), ("State", newPosition)],
                    cancellationToken).ConfigureAwait(false);
            }
            catch (DbException error) when (dialect.IsDuplicateKey(error))
            {
                conflict = error;
                // A ROW EXISTS, which is exactly what zero-rows-affected means on an engine whose
                // insert-if-absent is atomic. Oracle cannot take that decision atomically, so its
                // unique index takes it and reports by raising; this translates the raise back into the
                // refusal the contract declares, and the classification below names which refusal.
                // Caught ONLY around this statement: the re-fold and rebuild paths cannot insert, so a
                // constraint violation there is a real fault and must not be read as a lost race.
                affected = 0;
            }
        }

        if (affected == 1)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new ProjectionAdvanceResult(ProjectionAdvanceOutcome.Applied, newPosition);
        }

        var current = await ReadStoredPositionAsync(connection, transaction, id, cancellationToken).ConfigureAwait(false);

        // A DUPLICATE KEY WITH NO ROW BEHIND IT IS NOT THIS OPERATION'S REFUSAL. The conflict above is
        // read as "a row exists", which is what zero-rows-affected means on an atomic engine -- but that
        // reading is only true if a row actually does exist. A unique index a consumer added over their
        // own business key raises the same violation while this row is absent, and classifying that as
        // Vanished would hand back a declared outcome for an undeclared fault, discarding the engine's
        // diagnosis. Looking is what settles it, which is why no constraint NAME is load-bearing here.
        if (conflict is not null && current is null)
        {
            ExceptionDispatchInfo.Capture(conflict).Throw();
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return ClassifyAdvance(current);
    }

    /// <inheritdoc />
    [RequiresUnreferencedCode("Projection serialization uses the configured JSON metadata.")]
    [RequiresDynamicCode("Projection serialization uses the configured JSON metadata.")]
    public async Task<ProjectionRefoldResult> RefoldAtPositionAsync(
        string id, T projection, long atPosition, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(projection);
        ArgumentOutOfRangeException.ThrowIfNegative(atPosition);

        var json = Serialize(projection);
        await using var connection = createConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        // The position is matched and left UNCHANGED. A re-fold exists because the events at or below it
        // were rewritten in place -- erasure does that -- so the state no longer matches the prefix the
        // position names while the position itself stays correct. Advancing here would be wrong, and so
        // would creating a row: an absent row was erased.
        var affected = await ExecuteAsync(connection, transaction,
            $"UPDATE {table} SET Data = {Parameter("Data")} WHERE {RowPredicate} "
                + $"AND LastAppliedPosition = {Parameter("State")}",
            [("Tenant", Tenant), ("Id", id), ("Data", json), ("State", atPosition)],
            cancellationToken).ConfigureAwait(false);

        if (affected > 0)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new ProjectionRefoldResult(ProjectionRefoldOutcome.Applied, atPosition);
        }

        var current = await ReadStoredPositionAsync(connection, transaction, id, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return ClassifyRefold(current);
    }

    /// <inheritdoc />
    [RequiresUnreferencedCode("Projection serialization uses the configured JSON metadata.")]
    [RequiresDynamicCode("Projection serialization uses the configured JSON metadata.")]
    public async Task<ProjectionRebuildResult> RebuildAtPositionAsync(
        string id, T projection, long newPosition, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(projection);
        ArgumentOutOfRangeException.ThrowIfNegative(newPosition);

        var json = Serialize(projection);
        await using var connection = createConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        // UPDATE-ONLY and unconditional on position. It cannot detect a concurrent advance -- those are
        // the same check, and giving it up is the deliberate price of overwriting a row whose stored
        // number is unusable, which is why a rebuild requires a stopped processor. It still must not
        // CREATE the row: a whole-stream replay is precisely the write that could reconstruct an erased
        // subject. The rows the statement changed IS the existence answer, taken from the write itself;
        // a separate SELECT would be a second observation the row could be deleted between.
        var affected = await ExecuteAsync(connection, null,
            $"UPDATE {table} SET {PositionedAssignments} WHERE {RowPredicate} "
                + $"AND LastAppliedPosition <> {ProjectionPosition.RetiredSentinel.ToString(CultureInfo.InvariantCulture)}",
            [("Tenant", Tenant), ("Id", id), ("Data", json), ("State", newPosition)],
            cancellationToken).ConfigureAwait(false);

        return new ProjectionRebuildResult(
            affected > 0 ? ProjectionRebuildOutcome.Applied : ProjectionRebuildOutcome.Vanished);
    }

    /// <summary>Insert-if-absent, expressed in each engine's idiom.</summary>
    /// <remarks>
    /// <para>
    /// SQLite decides match-or-insert inside one index operation, so the conflict clause is enough and a
    /// losing writer is reported as zero rows affected.
    /// </para>
    /// <para>
    /// <b>Oracle's MERGE is NOT race-free here, and the caller compensates.</b> It evaluates
    /// <c>WHEN NOT MATCHED</c> against the state its own session can see, so two sessions racing a key
    /// that does not exist yet both take the insert arm and the loser raises a primary-key violation.
    /// That violation means precisely "a row exists", which is the fact zero-rows-affected carries on an
    /// atomic engine, so the caller catches it and classifies from the stored position rather than
    /// letting a provider exception escape an operation whose contract declares none.
    /// </para>
    /// <para>
    /// Superseded wording, quoted so a reader who inherited it recognises it: "Oracle uses a MERGE with
    /// only a NOT MATCHED arm so the probe and the insert take their locks as one statement; a NOT
    /// EXISTS subquery would leave a window a concurrent insert turns into a constraint violation." The
    /// first clause is false -- the locks are NOT taken as one statement -- and it was disproved by
    /// running the conformance arm that races a deletion against a first insert against a real Oracle
    /// database.
    /// </para>
    /// </remarks>
    private string InsertIfAbsentSql() =>
        dialect.InsertIfAbsent(table, PositionedColumns,
            string.Join(", ", PositionedInsertParameters.Select(Parameter)) + ", NULL, NULL");

    private async Task<int> ExecuteAsync(DbConnection connection, DbTransaction? transaction, string sql,
        IReadOnlyList<(string Name, object? Value)> parameters, CancellationToken token)
    {
        await using var command = createCommand(connection, transaction, sql, parameters);
        return await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
    }

    private async Task<long?> ReadStoredPositionAsync(DbConnection connection, DbTransaction? transaction,
        string id, CancellationToken token)
    {
        await using var command = createCommand(connection, transaction,
            $"SELECT LastAppliedPosition FROM {table} WHERE {RowPredicate}",
            [("Tenant", Tenant), ("Id", id)]);
        var value = await command.ExecuteScalarAsync(token).ConfigureAwait(false);
        return value is null or DBNull ? null : Convert.ToInt64(value, CultureInfo.InvariantCulture);
    }

    /// <summary>Classifies an advance that wrote nothing, from the position the row holds.</summary>
    /// <remarks>
    /// BOTH no-number states report <see cref="ProjectionAdvanceOutcome.Unplaceable"/>, deliberately, and
    /// it is TERMINAL rather than a supersede: there is no number to advance from, so re-reading yields
    /// the same row and the same refusal and a caller that retried would redeliver forever. The repair is
    /// a rebuild, which writes state and position together and needs no prior prefix.
    /// </remarks>
    private static ProjectionAdvanceResult ClassifyAdvance(long? current)
    {
        if (current is not { } stored || stored == ProjectionPosition.RetiredSentinel)
        {
            // Gone, or a retained deletion marker. Either way the caller stops; it must not fall back to
            // a create, because deletion is how erasure removes personal data.
            return new ProjectionAdvanceResult(ProjectionAdvanceOutcome.Vanished, null);
        }

        var held = ProjectionPosition.FromStored(stored);
        return held.Kind == ProjectionPositionKind.Positioned
            ? new ProjectionAdvanceResult(ProjectionAdvanceOutcome.Superseded, held.Value)
            : new ProjectionAdvanceResult(ProjectionAdvanceOutcome.Unplaceable, null);
    }

    /// <summary>Classifies a re-fold that wrote nothing, from the position the row holds.</summary>
    private static ProjectionRefoldResult ClassifyRefold(long? current)
    {
        if (current is not { } stored || stored == ProjectionPosition.RetiredSentinel)
        {
            return new ProjectionRefoldResult(ProjectionRefoldOutcome.Vanished, null);
        }

        // A row carrying no established position has nothing to match, and retrying is futile because
        // nothing about it will change on its own. Only a rebuild can place it.
        return stored < 0
            ? new ProjectionRefoldResult(ProjectionRefoldOutcome.RequiresRebuild, null)
            : new ProjectionRefoldResult(ProjectionRefoldOutcome.Superseded, stored);
    }
}
