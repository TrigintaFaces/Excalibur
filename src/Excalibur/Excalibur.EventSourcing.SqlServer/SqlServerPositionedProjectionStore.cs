// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Dapper;

using Excalibur.Dispatch;

using Microsoft.Data.SqlClient;

namespace Excalibur.EventSourcing.SqlServer;

/// <summary>
/// The positioned half of the SQL Server projection store: reads state and position as one
/// observation, and writes them as one indivisible action.
/// </summary>
/// <typeparam name="TProjection">The projection type.</typeparam>
/// <remarks>
/// <para>
/// <b>Why a MERGE and not an UPDATE.</b> The write has to express three outcomes against one row —
/// matched and advancing, matched and refused, and absent — and it must do so atomically with the state
/// write. <c>UPDLOCK, HOLDLOCK</c> is what makes the match-or-insert decision serializable against a
/// concurrent writer; without it two callers can both take the NOT MATCHED branch.
/// </para>
/// <para>
/// <b>The insert arm is guarded, and the guard is the whole point.</b> An unguarded
/// <c>WHEN NOT MATCHED THEN INSERT</c> fires when the caller supplied a NON-NULL expected position and
/// the row is absent — which happens when the projection was deleted between the caller's read and its
/// write. The store would then report success for a write whose precondition ("the stored position is
/// 12") was false, silently RECREATING a projection that erasure had removed. Guarding on
/// <c>@ExpectedPosition IS NULL</c> makes that case fall through to no action, which is reported as
/// <see cref="ProjectionAdvanceOutcome.Vanished"/>.
/// </para>
/// <para>
/// <b>Why <c>OUTPUT $action</c> rather than the row count.</b> A MERGE reports one affected row for an
/// update and one for an insert, and zero for both "matched but the condition failed" and "no row at
/// all" — so the row count cannot separate the outcomes the contract must distinguish. <c>$action</c>
/// names which branch fired, and its absence distinguishes the two zero cases when read together with
/// the position the same statement returns.
/// </para>
/// <para>
/// <b>This type does not serialize.</b> It receives the read-model JSON already encoded and returns it
/// still encoded, so the projection's wire shape is decided in exactly ONE place per provider -- the
/// owning store, which sources the canonical read-model options. A second serializer here would be a
/// second chance to write a document the canonical read path cannot load back, which is a defect class
/// this framework has already paid for once.
/// </para>
/// </remarks>
internal sealed class SqlServerPositionedProjectionStore<TProjection>
	where TProjection : class
{
	private readonly Func<SqlConnection> _connectionFactory;
	private readonly string _tableName;

	internal SqlServerPositionedProjectionStore(
		Func<SqlConnection> connectionFactory,
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

		// ONE statement, deliberately. Two reads admit a writer between them, after which the position
		// the caller holds certifies a prefix its state does not contain -- and the conditional write
		// then ACCEPTS, losing every event in the gap.
#pragma warning disable CA2100 // Table name is a validated configured identifier, bracketed below
		var sql =
			$"SELECT Data, LastAppliedPosition FROM [{_tableName}] WHERE Id = @Id AND TenantId = @TenantId;";
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

		// The stored value decodes to one of THREE states, and the two that carry no number need
		// opposite treatment from a positioned writer -- the unnumbered one is adoptable, the
		// unplaceable one is not. The decoding lives in ProjectionPosition so every provider agrees on
		// it; a provider that compared against its own sentinel here would be a silent cross-provider
		// divergence no single-provider test could see.
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

		// The UPDATE arm carries BOTH conjuncts of the contract:
		//   target.LastAppliedPosition = @ExpectedPosition  -- orders concurrent writers
		//   @NewPosition > target.LastAppliedPosition       -- orders this writer against its own past
		// The second is not redundant. A caller obtains its expected position by READING it, so on a
		// redelivery the first conjunct is satisfied by construction; only the forward-only rule refuses
		// the re-application.
		//
		// The expected-position comparison uses the sentinel rather than NULL semantics, because
		// `NULL = NULL` is unknown in SQL and would silently never match a legacy row.
#pragma warning disable CA2100 // Table name is a validated configured identifier, bracketed below
		var sql = $"""
			DECLARE @Result TABLE (Act NVARCHAR(10));

			MERGE [{_tableName}] WITH (UPDLOCK, HOLDLOCK) AS target
			USING (SELECT @Id AS Id, @TenantId AS TenantId) AS source
			ON target.Id = source.Id AND target.TenantId = source.TenantId
			WHEN MATCHED
				AND target.LastAppliedPosition = COALESCE(@ExpectedPosition, -1)
				AND @NewPosition > target.LastAppliedPosition THEN
				UPDATE SET Data = @Data, UpdatedAt = @UpdatedAt, LastAppliedPosition = @NewPosition
			WHEN NOT MATCHED AND @ExpectedPosition IS NULL THEN
				INSERT (Id, Data, CreatedAt, UpdatedAt, TenantId, LastAppliedPosition)
				VALUES (@Id, @Data, @UpdatedAt, @UpdatedAt, @TenantId, @NewPosition)
			OUTPUT $action INTO @Result;

			SELECT
				(SELECT TOP 1 Act FROM @Result) AS Act,
				(SELECT LastAppliedPosition FROM [{_tableName}]
				 WHERE Id = @Id AND TenantId = @TenantId) AS CurrentPosition;
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

		var outcome = await connection.QuerySingleAsync<MergeOutcomeRow>(
			new CommandDefinition(sql, parameters, cancellationToken: cancellationToken)).ConfigureAwait(false);

		if (outcome.Act is not null)
		{
			return new ProjectionAdvanceResult(ProjectionAdvanceOutcome.Applied, newPosition);
		}

		// No branch fired. Two different worlds, and the caller must not treat them alike: a row that is
		// still there refused the condition, and a row that is gone was deleted -- which is how erasure
		// removes personal data. Recreating it would reinstate erased data from the event stream.
		if (outcome.CurrentPosition is not { } current)
		{
			return new ProjectionAdvanceResult(ProjectionAdvanceOutcome.Vanished, null);
		}

		// An UNPLACEABLE row is not a supersede and must not be reported as one. A superseded caller
		// re-reads and retries; re-reading this row yields the same value and the same refusal, so
		// reporting Superseded here is an unbounded redelivery loop. The state is not a fold over any
		// prefix, so no retry can make this write correct -- the projection has to be rebuilt.
		var held = ProjectionPosition.FromStored(current);
		return held.Kind == ProjectionPositionKind.Unplaceable
			? new ProjectionAdvanceResult(ProjectionAdvanceOutcome.Unplaceable, null)
			: new ProjectionAdvanceResult(ProjectionAdvanceOutcome.Superseded, held.ExpectedPositionOrNull);
	}

	/// <summary>
	/// Writes a state that is a complete fold over a prefix carrying no global position number.
	/// </summary>
	/// <remarks>
	/// One statement, and the position is written rather than left to a default: a row created here is
	/// adoptable by a later positioned writer, which is the whole difference between this and the blind
	/// upsert. Writing the sentinel explicitly also means a row created by this method and a row created
	/// before positions existed decode identically, which is correct -- both hold a complete fold.
	/// </remarks>
	internal async Task UpsertUnnumberedAsync(
		string id,
		string data,
		string tenantId,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(id);
		ArgumentNullException.ThrowIfNull(data);

#pragma warning disable CA2100 // Table name is a validated configured identifier, bracketed below
		var sql = $"""
			MERGE [{_tableName}] WITH (UPDLOCK, HOLDLOCK) AS target
			USING (SELECT @Id AS Id, @TenantId AS TenantId) AS source
			ON target.Id = source.Id AND target.TenantId = source.TenantId
			WHEN MATCHED THEN
				UPDATE SET Data = @Data, UpdatedAt = @UpdatedAt, LastAppliedPosition = @Unnumbered
			WHEN NOT MATCHED THEN
				INSERT (Id, Data, CreatedAt, UpdatedAt, TenantId, LastAppliedPosition)
				VALUES (@Id, @Data, @UpdatedAt, @UpdatedAt, @TenantId, @Unnumbered);
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

	private sealed record PositionedRow(string? Data, long? LastAppliedPosition);

	/// <summary>
	/// Rewrites the state at the position the row already holds, without moving the position.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Why this is not the MERGE above with a relaxed comparison.</b> That statement requires
	/// <c>@NewPosition &gt; target.LastAppliedPosition</c>, and relaxing it to <c>&gt;=</c> does not
	/// produce this operation — it produces a general non-advancing write, which is exactly what the
	/// advancing path exists to refuse when a batch is redelivered. This statement matches the
	/// position and does not write it.
	/// </para>
	/// <para>
	/// <b>It cannot INSERT, and that is the point.</b> An absent row means the projection was deleted,
	/// deletion is how erasure removes personal data, and a re-fold that recreated it would reinstate
	/// what the erasure removed. A plain UPDATE has no not-matched arm, so creating is not expressible
	/// here rather than merely avoided.
	/// </para>
	/// <para>
	/// The <c>-1</c> sentinel is a row that carries no established position. It cannot match a real
	/// position, so it falls through to <c>RequiresRebuild</c> rather than to <c>Superseded</c>: the
	/// caller cannot fix it by re-reading, because there is nothing to re-read toward.
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

#pragma warning disable CA2100 // Table name is a validated configured identifier, bracketed below
		var sql = $"""
			UPDATE [{_tableName}] WITH (UPDLOCK, HOLDLOCK)
			SET Data = @Data, UpdatedAt = @UpdatedAt
			WHERE Id = @Id AND TenantId = @TenantId AND LastAppliedPosition = @AtPosition;

			SELECT
				@@ROWCOUNT AS Affected,
				(SELECT LastAppliedPosition FROM [{_tableName}]
				 WHERE Id = @Id AND TenantId = @TenantId) AS CurrentPosition;
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

		var outcome = await connection.QuerySingleAsync<RefoldOutcomeRow>(
			new CommandDefinition(sql, parameters, cancellationToken: cancellationToken)).ConfigureAwait(false);

		return ClassifyRefold(outcome.Affected, outcome.CurrentPosition, atPosition);
	}

	/// <summary>
	/// Turns a re-fold's row count and the position the row holds into an outcome.
	/// </summary>
	/// <remarks>
	/// Shared shape across every provider, written once here and mirrored in each because their
	/// transports differ but the four cases do not:
	/// <list type="bullet">
	/// <item>the update landed — <c>Applied</c>, and the position is unchanged by construction</item>
	/// <item>no row at all — <c>Vanished</c>. Never recreate; deletion is how erasure works</item>
	/// <item>a row carrying no established position — <c>RequiresRebuild</c>, terminal</item>
	/// <item>a row at a different real position — <c>Superseded</c></item>
	/// </list>
	/// </remarks>
	internal static ProjectionRefoldResult ClassifyRefold(int affected, long? currentPosition, long atPosition)
	{
		if (affected > 0)
		{
			return new ProjectionRefoldResult(ProjectionRefoldOutcome.Applied, atPosition);
		}

		if (currentPosition is not { } current)
		{
			return new ProjectionRefoldResult(ProjectionRefoldOutcome.Vanished, null);
		}

		return current < 0
			? new ProjectionRefoldResult(ProjectionRefoldOutcome.RequiresRebuild, null)
			: new ProjectionRefoldResult(ProjectionRefoldOutcome.Superseded, current);
	}

	private sealed record RefoldOutcomeRow(int Affected, long? CurrentPosition);

	private sealed record MergeOutcomeRow(string? Act, long? CurrentPosition);
}
