// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;

using Dapper;

using Microsoft.Extensions.Options;

using Npgsql;

namespace Excalibur.Compliance.Postgres.Erasure;

/// <summary>
/// The PostgreSQL record of which retention declarations some process in this deployment has witnessed.
/// </summary>
/// <remarks>
/// <para>
/// <b>A type of its own rather than another facet of the erasure store, and the analyzer is what said so
/// first.</b> Adding this and the withdrawal authority to <see cref="PostgresErasureStore"/> pushed that
/// class past its class-coupling limit, which was the correct signal: the store's job is erasure requests
/// and certificates, and this record answers a different question about a different table.
/// </para>
/// <para>
/// <b>Separating it is also what makes the withdrawal contract's own purpose real.</b> The withdrawal
/// authority is a separate contract so that withdrawing a declaration is not expressible by the code that
/// deploys one. Had both lived on the erasure store, anything holding the store could withdraw, and the
/// separation would have been nominal — two interfaces over one object that every erasure path already
/// has a reference to.
/// </para>
/// <para>
/// It provisions nothing. The table is created and verified by the erasure store's own provisioning
/// funnel, so a deployment has one answer to "is the compliance schema present" rather than two that can
/// disagree.
/// </para>
/// </remarks>
public sealed class PostgresErasureRetentionDeclarationLedger : IErasureRetentionDeclarationLedger
{
	private readonly PostgresErasureStoreOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresErasureRetentionDeclarationLedger"/> class.
	/// </summary>
	/// <param name="options">The erasure store options, whose table name and timeout this shares.</param>
	/// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
	public PostgresErasureRetentionDeclarationLedger(IOptions<PostgresErasureStoreOptions> options)
	{
		ArgumentNullException.ThrowIfNull(options);
		_options = options.Value;
	}

	/// <inheritdoc />
	/// <remarks>
	/// N independent insert-if-absent statements, never an upsert and never a set-valued row.
	/// <c>ON CONFLICT DO NOTHING</c> makes the test and the write one indivisible operation per pair, so a
	/// lost race is a SUCCESS rather than an error to surface: several processes starting together each
	/// contribute, exactly one insert wins per pair, and every other is a no-op. Because the statement
	/// itself swallows the conflict there is no duplicate-key exception to catch, and therefore no risk of
	/// a blanket catch turning a transient fault into a silently skipped advance.
	/// </remarks>
	public async Task RecordDeclaredRetentionsAsync(
		IReadOnlyCollection<DeclaredRetentionKey> declarations,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(declarations);

		if (declarations.Count == 0)
		{
			return;
		}

		// DO NOTHING rather than DO UPDATE: this path contains no update at all, which is the whole
		// mechanism that stops a withdrawal being resurrected. A tombstone OCCUPIES the key, so an
		// instance whose code still declares a withdrawn retention re-runs this statement and changes
		// nothing — leaving the tombstone for the comparison on the erasure path to find.
		var sql = $@"
			INSERT INTO {_options.FullRetentionDeclarationsTableName} (tenant_id, aggregate_type)
			VALUES (@TenantId, @AggregateType)
			ON CONFLICT (tenant_id, aggregate_type) DO NOTHING";

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		// ONE STATEMENT PER PAIR, and the failures collected rather than thrown at the first one.
		//
		// A SINGLE BATCHED COMMAND IS NOT ATOMIC HERE, which is the mistake this loop replaces. Handing
		// Dapper an array executes the statement once per element and propagates the FIRST failure, so
		// pairs before it are recorded and pairs after it are never attempted -- a prefix, chosen by
		// input order rather than by anything meaningful. A conformance arm against a real server caught
		// exactly that: PostgreSQL recorded the pair before an unrecordable one and skipped the pair
		// after it, while SQL Server recorded both.
		//
		// Each pair recorded is protection that now EXISTS -- an unrecorded pair is one for which no
		// stale instance can detect its own staleness -- so every pair is attempted. What must never
		// happen is reporting success, because the caller latches on success and a latched prefix is a
		// hole with no tell.
		//
		// ON CONFLICT DO NOTHING means a pair another process already recorded raises nothing at all, so
		// anything collected here is a genuine fault rather than the specified no-op. The affected count
		// is deliberately NOT a verdict for the same reason.
		List<string>? unrecorded = null;

		foreach (var declaration in declarations)
		{
			try
			{
				_ = await connection.ExecuteAsync(new CommandDefinition(
					sql,
					new { declaration.TenantId, declaration.AggregateType },
					cancellationToken: cancellationToken,
					commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				throw;
			}
#pragma warning disable CA1031 // Collected and rethrown below, named, rather than losing the other pairs.
			catch (Exception ex)
#pragma warning restore CA1031
			{
				(unrecorded ??= []).Add(
					$"tenant [{declaration.TenantId}] type [{declaration.AggregateType}]: {ex.Message}");
			}
		}

		if (unrecorded is not null)
		{
			throw new InvalidOperationException(
				"The deployment-wide retention record is INCOMPLETE: "
				+ $"{unrecorded.Count} declaration(s) could not be recorded. Until every one of this "
				+ "instance's declarations is recorded, other instances cannot detect that they are "
				+ "behind it, so this instance must not be treated as having advanced. Unrecorded: "
				+ string.Join("; ", unrecorded));
		}
	}

	/// <inheritdoc />
	/// <remarks>
	/// Filtered to the requesting tenant by a KEY LOOKUP rather than by an added predicate — the tenant is
	/// the leading column of the primary key, so this is the key addressing the rows it owns. Withdrawn
	/// rows are returned FLAGGED, because a caller that cannot see a tombstone cannot detect the mirror
	/// case: an instance still declaring a retention the deployment has withdrawn.
	/// </remarks>
	public async Task<IReadOnlyCollection<RecordedRetentionDeclaration>> ListDeclaredRetentionsAsync(
		string tenantId,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(tenantId);

		var sql = $@"
			SELECT aggregate_type, (withdrawn_at IS NOT NULL) AS is_withdrawn
			FROM {_options.FullRetentionDeclarationsTableName}
			WHERE tenant_id = @TenantId
			ORDER BY aggregate_type";

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var rows = await connection.QueryAsync<RecordedRetentionRow>(new CommandDefinition(
			sql,
			new { TenantId = tenantId },
			cancellationToken: cancellationToken,
			commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false);

		return rows
			.Select(row => new RecordedRetentionDeclaration(
				new DeclaredRetentionKey(tenantId, row.aggregate_type),
				row.is_withdrawn))
			.ToList();
	}

	/// <summary>One recorded retention declaration as the row reports it.</summary>
	[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Dapper materializes this type.")]
	private sealed class RecordedRetentionRow
	{
		// ReSharper disable InconsistentNaming - Column names use snake_case
		public string aggregate_type { get; init; } = string.Empty;

		public bool is_withdrawn { get; init; }

		// ReSharper restore InconsistentNaming
	}
}
