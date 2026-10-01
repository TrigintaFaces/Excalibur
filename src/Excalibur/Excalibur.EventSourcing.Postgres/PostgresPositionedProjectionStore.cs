// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Dapper;

using Npgsql;

namespace Excalibur.EventSourcing.Postgres;

/// <summary>
/// The positioned half of the PostgreSQL projection store: reads state and position as one
/// observation, and writes them as one indivisible action.
/// </summary>
/// <typeparam name="TProjection">The projection type.</typeparam>
/// <remarks>
/// <para>
/// <b>The insert arm is guarded, and an unguarded one is a real defect rather than a tidiness point.</b>
/// A plain <c>INSERT … ON CONFLICT (id, tenant_id) DO UPDATE … WHERE t.last_applied_position = @Expected</c>
/// looks like a compare-and-set and is not: when no row exists there is no conflict, so the INSERT arm
/// fires, the row is created, and the statement reports one affected row. The store would then report
/// success for a write whose precondition ("the stored position is 12") was false — silently RECREATING
/// a projection that erasure had deleted. The insert is therefore conditional on the caller having
/// claimed absence, via <c>WHERE @ExpectedPosition IS NULL</c> on the SELECT that feeds it.
/// </para>
/// <para>
/// <b>RETURNING is what separates the outcomes.</b> An affected-row count of zero covers both "the row
/// is there and refused the condition" and "there is no row", and the correct responses are opposite —
/// retry in the first case, and in the second do NOT recreate, because deletion is how personal data is
/// erased. The statement returns the position it holds so the caller can tell them apart and, on a
/// refusal, decide whether it is behind (retry) or already applied (stop).
/// </para>
/// <para>
/// <b>This type does not serialize.</b> It receives the read-model JSON already encoded and returns it
/// still encoded, so the projection's wire shape is decided in exactly ONE place per provider -- the
/// owning store, which sources the canonical read-model options. A second serializer here would be a
/// second chance to write a document the canonical read path cannot load back, which is a defect class
/// this framework has already paid for once.
/// </para>
/// </remarks>
internal sealed class PostgresPositionedProjectionStore<TProjection>
	where TProjection : class
{
	private readonly Func<NpgsqlConnection> _connectionFactory;
	private readonly string _tableName;

	internal PostgresPositionedProjectionStore(
		Func<NpgsqlConnection> connectionFactory,
		string tableName)
	{
		_connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
		_tableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
	}

	/// <summary>Reads the state and the position in one statement.</summary>
	[RequiresUnreferencedCode("Projection deserialization is reflective.")]
	[RequiresDynamicCode("Projection deserialization is reflective.")]
	internal async Task<(string? Data, ProjectionPosition Position)> GetWithPositionAsync(
		string id,
		string tenantId,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(id);

		// ONE statement. Reading state and position separately admits a writer between them, after which
		// the caller's expected position certifies a prefix its state does not contain -- and the
		// conditional write then ACCEPTS, losing every event in the gap.
#pragma warning disable CA2100 // Table name is a validated configured identifier, quoted below
		var sql =
			$"""SELECT data AS "Data", last_applied_position AS "LastAppliedPosition" """
			+ $"""FROM "{_tableName}" WHERE id = @Id AND tenant_id = @TenantId;""";
#pragma warning restore CA2100

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var row = await connection.QuerySingleOrDefaultAsync<PositionedRow>(
			new CommandDefinition(sql, new { Id = id, TenantId = tenantId }, cancellationToken: cancellationToken))
			.ConfigureAwait(false);

		if (row is null || row.Data is null)
		{
			// A row that does not exist is not a fold over any prefix, so UNPLACEABLE is the true
			// reading rather than a convenient one. Unnumbered would assert a complete fold over state
			// that does not exist.
			return (null, ProjectionPosition.Unplaceable);
		}

		// The stored value decodes to one of THREE states, and only the positioned one can be advanced
		// from: the two carrying no number are refused by the write below and repaired by a rebuild. The
		// decoding lives in ProjectionPosition so every provider agrees on it; comparing against a
		// locally-written sentinel here would be a cross-provider divergence no single-provider test
		// could see.
		var position = ProjectionPosition.FromStored(row.LastAppliedPosition);

		return (row.Data, position);
	}

	/// <summary>Writes the state and the position together, conditionally.</summary>
	[RequiresUnreferencedCode("Projection serialization is reflective.")]
	[RequiresDynamicCode("Projection serialization is reflective.")]
	internal async Task<ProjectionAdvanceResult> UpsertAtPositionAsync(
		string id,
		string data,
		long? expectedPosition,
		long newPosition,
		string tenantId,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(id);
		ArgumentOutOfRangeException.ThrowIfNegative(newPosition);
		ArgumentNullException.ThrowIfNull(data);

		// A NEGATIVE EXPECTATION IS THE ADOPT LICENCE THROUGH A DIFFERENT DOOR. The negatives are the
		// sentinel space for the two states that carry no number, so a caller naming one as "the position I
		// read" would be matching a row this store refuses by design. ExpectedPositionOrNull is the only
		// legal source for this argument and never yields a negative.
		if (expectedPosition is { } claimed)
		{
			ArgumentOutOfRangeException.ThrowIfNegative(claimed, nameof(expectedPosition));
		}

		// TWO CTEs, each conditional on exactly one thing the caller claimed.
		//
		// `attempted` is INSERT-IF-ABSENT and nothing else. Its feeding SELECT carries
		// `WHERE @ExpectedPosition IS NULL`, so it is reachable only when the caller claimed absence --
		// without that, a non-null expected against a deleted row would insert and report success for a
		// false precondition. ON CONFLICT DO NOTHING is what keeps it to insert-if-absent: the arm used to
		// be DO UPDATE with `t.last_applied_position = COALESCE(@ExpectedPosition, -1)`, which mapped "I
		// read no position" onto the unnumbered sentinel and therefore ADOPTED an existing numberless row,
		// stamping this batch's position onto a state whose prefix nobody established. With DO NOTHING
		// there is no update arm here at all, so adoption is inexpressible rather than merely avoided.
		//
		// `updated` carries both conjuncts of the contract and requires a NON-NULL expectation:
		//   t.last_applied_position = @ExpectedPosition  -- orders concurrent writers
		//   @NewPosition > t.last_applied_position       -- orders this writer against its own past,
		//                                                  i.e. a redelivery
		//
		// EVERY use of @ExpectedPosition carries an explicit ::bigint, and that is not decoration.
		// PostgreSQL infers a parameter's type from the context it appears in, and `$n IS NULL` gives it
		// none: the server refuses to plan the statement at all, with 42P08 "could not determine data
		// type of parameter". Measured against a real server -- without the casts EVERY positioned write
		// on this provider threw, and no mocked connection could have shown it.
#pragma warning disable CA2100 // Table name is a validated configured identifier, quoted below
		var sql = $"""
			WITH attempted AS (
			    INSERT INTO "{_tableName}" (id, data, created_at, updated_at, tenant_id, last_applied_position)
			    SELECT @Id, @Data::jsonb, @UpdatedAt, @UpdatedAt, @TenantId, @NewPosition::bigint
			    WHERE @ExpectedPosition::bigint IS NULL
			    ON CONFLICT (id, tenant_id) DO NOTHING
			    RETURNING last_applied_position
			), updated AS (
			    UPDATE "{_tableName}" t
			    SET data = @Data::jsonb, updated_at = @UpdatedAt, last_applied_position = @NewPosition
			    WHERE t.id = @Id AND t.tenant_id = @TenantId
			      AND @ExpectedPosition::bigint IS NOT NULL
			      AND t.last_applied_position = @ExpectedPosition::bigint
			      AND @NewPosition > t.last_applied_position
			    RETURNING last_applied_position
			)
			SELECT
			    (SELECT COUNT(*) FROM attempted) + (SELECT COUNT(*) FROM updated) AS "Wrote",
			    (SELECT last_applied_position FROM "{_tableName}"
			     WHERE id = @Id AND tenant_id = @TenantId) AS "CurrentPosition";
			""";
#pragma warning restore CA2100

		var parameters = new DynamicParameters();
		parameters.Add("@Id", id);
		parameters.Add("@Data", data);
		parameters.Add("@UpdatedAt", DateTimeOffset.UtcNow);
		parameters.Add("@TenantId", tenantId);
		// DbType is pinned, not inferred. A nullable long arrives at the ADO boundary as a boxed null
		// that carries no type, and what happens next is up to the ENGINE: PostgreSQL refuses to plan
		// the statement at all (42P08), while SQL Server silently recovers it from an adjacent literal.
		// One root cause, two outcomes, and the next provider gets whichever its engine gives -- so the
		// type is stated here rather than left to be rediscovered per provider.
		parameters.Add("@ExpectedPosition", expectedPosition, DbType.Int64);
		parameters.Add("@NewPosition", newPosition, DbType.Int64);

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var outcome = await connection.QuerySingleAsync<WriteOutcomeRow>(
			new CommandDefinition(sql, parameters, cancellationToken: cancellationToken)).ConfigureAwait(false);

		if (outcome.Wrote > 0)
		{
			return new ProjectionAdvanceResult(ProjectionAdvanceOutcome.Applied, newPosition);
		}

		// Nothing was written. A row that is still present refused the condition; an absent row was
		// deleted, and must NOT be recreated -- deletion is how erasure removes personal data, and
		// re-folding the stream would reinstate it.
		return outcome.CurrentPosition is { } current
			? Refused(current)
			: new ProjectionAdvanceResult(ProjectionAdvanceOutcome.Vanished, null);

		// Terminal, not a supersede: there is no number to advance from, and re-reading yields the
		// same value and the same refusal, so Superseded here is an unbounded redelivery loop. That is the
		// arm a careless edit reintroduces, and a numberless row landing in it retries forever.
		//
		// BOTH no-number states report the SAME outcome, deliberately. This result describes what the WRITE
		// did; it carries no state, and the position was read at a different instant from the one the write
		// was refused at, so an outcome characterising the stored STATE would attribute a property of the
		// row-at-read-time to a write refused earlier. A caller that needs to know what the row holds reads
		// its position, where ProjectionPositionKind reports it as a measured fact.
		static ProjectionAdvanceResult Refused(long current)
		{
			var held = ProjectionPosition.FromStored(current);
			return held.Kind == ProjectionPositionKind.Positioned
				? new ProjectionAdvanceResult(ProjectionAdvanceOutcome.Superseded, held.Value)
				: new ProjectionAdvanceResult(ProjectionAdvanceOutcome.Unplaceable, null);
		}
	}

	/// <summary>
	/// Writes a state that is a complete fold over a prefix carrying no global position number.
	/// </summary>
	/// <remarks>
	/// The position is written rather than left to a default. A row created here is NOT adoptable -- a
	/// positioned write refuses every row carrying no number -- but it reads back as a COMPLETE FOLD whose
	/// coordinate is unknown rather than as a state related to no prefix at all, and that is the whole
	/// difference between this and the blind upsert.
	/// </remarks>
	internal async Task UpsertUnnumberedAsync(
		string id,
		string data,
		string tenantId,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(id);
		ArgumentNullException.ThrowIfNull(data);

#pragma warning disable CA2100 // Table name is a validated configured identifier, quoted below
		var sql = $"""
		           INSERT INTO "{_tableName}" (id, data, created_at, updated_at, tenant_id, last_applied_position)
		           VALUES (@Id, @Data::jsonb, @UpdatedAt, @UpdatedAt, @TenantId, @Unnumbered)
		           ON CONFLICT (id, tenant_id)
		           DO UPDATE SET data = EXCLUDED.data, updated_at = EXCLUDED.updated_at,
		                         last_applied_position = EXCLUDED.last_applied_position
		           """;
#pragma warning restore CA2100

		var parameters = new DynamicParameters();
		parameters.Add("@Id", id);
		parameters.Add("@Data", data);
		parameters.Add("@UpdatedAt", DateTimeOffset.UtcNow);
		parameters.Add("@TenantId", tenantId);
		parameters.Add("@Unnumbered", ProjectionPosition.Unnumbered.ToStored());

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
		_ = await connection.ExecuteAsync(
			new CommandDefinition(sql, parameters, cancellationToken: cancellationToken)).ConfigureAwait(false);
	}

	/// <summary>
	/// Overwrites an existing row's state and position, for a caller that folded the whole stream.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A plain UPDATE, where the advancing write is an INSERT ... ON CONFLICT. Unconditional on POSITION
	/// -- a state folded from an empty seed has no prior prefix for a condition to be written against --
	/// but conditional on the row EXISTING, which is a different question and the one this statement
	/// still asks.
	/// </para>
	/// <para>
	/// <b>There is no insert arm, and that is the point.</b> An absent row means the projection was
	/// deleted, deletion is how erasure removes personal data, and a whole-stream replay is precisely
	/// the write that could reconstruct it. A plain UPDATE cannot insert, so creating is inexpressible
	/// here rather than merely avoided, and the affected-row count is what reports the absence.
	/// </para>
	/// </remarks>
	internal async Task<ProjectionRebuildResult> RebuildAtPositionAsync(
		string id,
		string data,
		long newPosition,
		string tenantId,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(id);
		ArgumentOutOfRangeException.ThrowIfNegative(newPosition);
		ArgumentNullException.ThrowIfNull(data);

#pragma warning disable CA2100 // Table name is a validated configured identifier, quoted below
		var sql = $"""
		           UPDATE "{_tableName}"
		           SET data = @Data::jsonb, updated_at = @UpdatedAt, last_applied_position = @NewPosition
		           WHERE id = @Id AND tenant_id = @TenantId
		           """;
#pragma warning restore CA2100

		var parameters = new DynamicParameters();
		parameters.Add("@Id", id);
		parameters.Add("@Data", data);
		parameters.Add("@UpdatedAt", DateTimeOffset.UtcNow);
		parameters.Add("@TenantId", tenantId);
		parameters.Add("@NewPosition", ProjectionPosition.At(newPosition).ToStored());

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		// The rows the statement changed IS the existence answer, taken from the write itself. A separate
		// SELECT would be a second observation, and the row can be deleted between the two.
		var affected = await connection.ExecuteAsync(
			new CommandDefinition(sql, parameters, cancellationToken: cancellationToken)).ConfigureAwait(false);

		return new ProjectionRebuildResult(
			affected > 0 ? ProjectionRebuildOutcome.Applied : ProjectionRebuildOutcome.Vanished);
	}

	/// <summary>
	/// Rewrites the state at the position the row already holds, without moving the position.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A plain UPDATE, deliberately, where the advancing write is an INSERT ... ON CONFLICT. There is
	/// no insert arm because an absent row means the projection was DELETED, deletion is how erasure
	/// removes personal data, and a re-fold that recreated it would reinstate what the erasure
	/// removed. Creating is not expressible here rather than merely avoided.
	/// </para>
	/// <para>
	/// The -1 sentinel is a row carrying no established position. It cannot equal a real position, so
	/// it falls through to RequiresRebuild rather than Superseded: re-reading cannot help, because
	/// there is nothing to re-read toward.
	/// </para>
	/// </remarks>
	[RequiresUnreferencedCode("Projection serialization is reflective.")]
	[RequiresDynamicCode("Projection serialization is reflective.")]
	internal async Task<ProjectionRefoldResult> RefoldAtPositionAsync(
		string id,
		string data,
		long atPosition,
		string tenantId,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(id);
		ArgumentOutOfRangeException.ThrowIfNegative(atPosition);
		ArgumentNullException.ThrowIfNull(data);

#pragma warning disable CA2100 // Table name is a validated configured identifier, quoted below
		var sql = $"""
			WITH refolded AS (
			    UPDATE "{_tableName}" t
			    SET data = @Data::jsonb, updated_at = @UpdatedAt
			    WHERE t.id = @Id AND t.tenant_id = @TenantId
			      AND t.last_applied_position = @AtPosition::bigint
			    RETURNING last_applied_position
			)
			SELECT
			    (SELECT COUNT(*) FROM refolded) AS "Wrote",
			    (SELECT last_applied_position FROM "{_tableName}"
			     WHERE id = @Id AND tenant_id = @TenantId) AS "CurrentPosition";
			""";
#pragma warning restore CA2100

		var parameters = new DynamicParameters();
		parameters.Add("@Id", id);
		parameters.Add("@Data", data);
		parameters.Add("@UpdatedAt", DateTimeOffset.UtcNow);
		parameters.Add("@TenantId", tenantId);
		parameters.Add("@AtPosition", atPosition, DbType.Int64);

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var outcome = await connection.QuerySingleAsync<WriteOutcomeRow>(
			new CommandDefinition(sql, parameters, cancellationToken: cancellationToken)).ConfigureAwait(false);

		if (outcome.Wrote > 0)
		{
			return new ProjectionRefoldResult(ProjectionRefoldOutcome.Applied, atPosition);
		}

		if (outcome.CurrentPosition is not { } current)
		{
			return new ProjectionRefoldResult(ProjectionRefoldOutcome.Vanished, null);
		}

		return current < 0
			? new ProjectionRefoldResult(ProjectionRefoldOutcome.RequiresRebuild, null)
			: new ProjectionRefoldResult(ProjectionRefoldOutcome.Superseded, current);
	}

	private sealed record PositionedRow(string? Data, long? LastAppliedPosition);

	private sealed record WriteOutcomeRow(long Wrote, long? CurrentPosition);
}
