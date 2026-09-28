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
/// </remarks>
	/// <remarks>
	/// <b>This type does not serialize.</b> It receives the read-model JSON already encoded and returns it
	/// still encoded, so the projection's wire shape is decided in exactly ONE place per provider -- the
	/// owning store, which sources the canonical read-model options. A second serializer here would be a
	/// second chance to write a document the canonical read path cannot load back, which is a defect class
	/// this framework has already paid for once.
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
			return (null, ProjectionPosition.Unnumbered);
		}

		// The stored value decodes to one of THREE states, and the two carrying no number need opposite
		// treatment: the unnumbered one is adoptable, the unplaceable one is not. The decoding lives in
		// ProjectionPosition so every provider agrees on it; comparing against a locally-written
		// sentinel here would be a cross-provider divergence no single-provider test could see.
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

		// Both conjuncts of the contract are in the DO UPDATE predicate:
		//   t.last_applied_position = COALESCE(@ExpectedPosition, -1)  -- orders concurrent writers
		//   @NewPosition > t.last_applied_position                     -- orders this writer against
		//                                                                 its own past, i.e. redelivery
		// The COALESCE is what lets a legacy row (sentinel -1, read back as "unknown") be claimed by a
		// caller passing null, instead of livelocking against an insert the primary key refuses.
		//
		// The feeding SELECT carries `WHERE @ExpectedPosition IS NULL` so the INSERT arm is reachable
		// ONLY when the caller claimed absence. Without it, a non-null expected against a deleted row
		// inserts and reports success for a false precondition.
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
			    ON CONFLICT (id, tenant_id) DO UPDATE
			        SET data = EXCLUDED.data,
			            updated_at = EXCLUDED.updated_at,
			            last_applied_position = EXCLUDED.last_applied_position
			        WHERE "{_tableName}".last_applied_position = COALESCE(@ExpectedPosition::bigint, -1)
			          AND EXCLUDED.last_applied_position > "{_tableName}".last_applied_position
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

		// An UNPLACEABLE row is not a supersede and must not be reported as one. A superseded caller
		// re-reads and retries; re-reading this row yields the same value and the same refusal, so
		// reporting Superseded is an unbounded redelivery loop. No retry can make the write correct --
		// the state is not a fold over any prefix, so the projection has to be rebuilt.
		static ProjectionAdvanceResult Refused(long current)
		{
			var held = ProjectionPosition.FromStored(current);
			return held.Kind == ProjectionPositionKind.Unplaceable
				? new ProjectionAdvanceResult(ProjectionAdvanceOutcome.Unplaceable, null)
				: new ProjectionAdvanceResult(ProjectionAdvanceOutcome.Superseded, held.ExpectedPositionOrNull);
		}
	}

	/// <summary>
	/// Writes a state that is a complete fold over a prefix carrying no global position number.
	/// </summary>
	/// <remarks>
	/// The position is written rather than left to a default: a row created here is adoptable by a later
	/// positioned writer, which is the whole difference between this and the blind upsert.
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
