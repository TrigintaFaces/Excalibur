// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;

using Dapper;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.SqlServer.Erasure;

/// <summary>
/// The SQL Server record of which retention declarations some process in this deployment has witnessed.
/// </summary>
/// <remarks>
/// <para>
/// <b>A type of its own rather than another facet of the erasure store, and the analyzer is what said so
/// first.</b> Adding this and the withdrawal authority to <see cref="SqlServerErasureStore"/> pushed that
/// class past its class-coupling limit, which was the correct signal: the store's job is erasure requests
/// and certificates, and this record answers a different question about a different table.
/// </para>
/// <para>
/// <b>Separating it is also what makes the withdrawal contract's own purpose real.</b> The withdrawal
/// authority is a separate contract so that withdrawing a declaration is not expressible by the code that
/// deploys one. Had both lived on the erasure store, anything holding the store could withdraw, and the
/// separation would have been nominal.
/// </para>
/// <para>
/// It provisions nothing. The table is created and verified by the erasure store's own provisioning
/// funnel, so a deployment has one answer to "is the compliance schema present" rather than two that can
/// disagree.
/// </para>
/// </remarks>
public sealed class SqlServerErasureRetentionDeclarationLedger : IErasureRetentionDeclarationLedger
{
	/// <summary>
	/// Violation of a PRIMARY KEY or UNIQUE constraint.
	/// </summary>
	/// <remarks>
	/// The number the engine raises when the table's own declared primary key refuses a duplicate, which
	/// is how this table is provisioned by both the in-code path and the shipped script.
	/// </remarks>
	private const int ConstraintViolation = 2627;

	/// <summary>
	/// Violation of a unique INDEX, as opposed to a constraint.
	/// </summary>
	/// <remarks>
	/// Carried as well as the constraint number because the two are distinct in SQL Server and the number
	/// depends on how the uniqueness was declared, not on what it protects. A database provisioned with a
	/// unique index in place of the constraint is still correctly unique, and must not fail its advance
	/// because the engine chose a different number to say so.
	/// </remarks>
	private const int UniqueIndexViolation = 2601;

	private readonly SqlServerErasureStoreOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerErasureRetentionDeclarationLedger"/> class.
	/// </summary>
	/// <param name="options">The erasure store options, whose table name and timeout this shares.</param>
	/// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
	public SqlServerErasureRetentionDeclarationLedger(IOptions<SqlServerErasureStoreOptions> options)
	{
		ArgumentNullException.ThrowIfNull(options);
		_options = options.Value;
	}

	/// <inheritdoc />
	/// <remarks>
	/// <para>
	/// <b>The PRIMARY KEY is the arbiter, and the duplicate it raises is caught precisely.</b> SQL Server
	/// has no single-statement equivalent of an insert-if-absent that reports nothing on a conflict, so
	/// the insert is plain and the constraint decides. A lost race is a SUCCESS: several processes
	/// starting together each contribute, exactly one insert wins per pair, and every other raises a
	/// duplicate that is swallowed. Only those two error numbers are swallowed — a blanket catch would
	/// turn a transient fault into a silent pass, and a silently skipped advance is a declaration this
	/// deployment never records.
	/// </para>
	/// <para>
	/// <b>ONE STATEMENT PER PAIR, and that is load-bearing rather than tidy.</b> Batching the pairs into
	/// one command would abort the remaining pairs the moment any one of them was already recorded — and
	/// a pair already recorded is the NORMAL case for every instance but the first. The batch would then
	/// silently skip every declaration after the first collision, which is precisely the failure the
	/// precise catch exists to prevent, reintroduced by the batching.
	/// </para>
	/// <para>
	/// A test-then-insert under a range lock was the alternative considered and rejected: it adds a
	/// deadlock surface on a path that every instance of a deployment hits at the same moment, to avoid
	/// an exception that is expected, cheap and precisely identifiable.
	/// </para>
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

		var sql = $@"
			INSERT INTO {_options.FullRetentionDeclarationsTableName} ([TenantId], [AggregateType])
			VALUES (@TenantId, @AggregateType)";

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		// EVERY PAIR IS ATTEMPTED, and the failures are collected rather than thrown at the first one.
		// Each pair recorded is protection that now EXISTS -- an unrecorded pair is one for which no stale
		// instance can ever detect its own staleness -- so aborting early throws away recordings that
		// could have been made. What must never happen is reporting success: the caller latches on
		// success, and a latched prefix is a hole with no tell.
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
			catch (SqlException ex) when (IsDuplicateKey(ex))
			{
				// Another process recorded this pair first. That is the specified no-op, not a fault.
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				throw;
			}
#pragma warning disable CA1031 // Collected and rethrown below, named, rather than losing the other pairs.
			catch (Exception ex)
#pragma warning restore CA1031
			{
				(unrecorded ??= []).Add($"tenant [{declaration.TenantId}] type [{declaration.AggregateType}]: {ex.Message}");
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
	/// the leading column of the clustered primary key, so this is the key addressing the rows it owns.
	/// Withdrawn rows are returned FLAGGED, because a caller that cannot see a tombstone cannot detect the
	/// mirror case: an instance still declaring a retention the deployment has withdrawn.
	/// </remarks>
	public async Task<IReadOnlyCollection<RecordedRetentionDeclaration>> ListDeclaredRetentionsAsync(
		string tenantId,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(tenantId);

		var sql = $@"
			SELECT [AggregateType] AS aggregate_type,
				CAST(CASE WHEN [WithdrawnAt] IS NULL THEN 0 ELSE 1 END AS BIT) AS is_withdrawn
			FROM {_options.FullRetentionDeclarationsTableName}
			WHERE [TenantId] = @TenantId
			ORDER BY [AggregateType]";

		await using var connection = new SqlConnection(_options.ConnectionString);
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

	/// <summary>
	/// Whether the engine reported a duplicate-key violation among the errors it raised.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>CONTAINS a duplicate, not CONSISTS ONLY of one, and the real engine is what settled that.</b>
	/// This first required every error in the collection to be a duplicate, on the reasoning that a
	/// collection carrying a duplicate AND a real fault must not read as merely a duplicate. SQL Server
	/// raises the violation together with a second, informational error saying the statement was
	/// terminated — so that predicate rejected its own catch, the exception escaped, and the conformance
	/// arms failed on the very first duplicate. Measured, not reasoned: the arms ran against a real
	/// server before this comment was written.
	/// </para>
	/// <para>
	/// <b>Why the looser predicate is still precise enough here.</b> The statement is a single-row
	/// INSERT, and a statement reports at most one constraint violation, so the only shape this can
	/// swallow is the expected duplicate and its own termination notice. An exception carrying NO
	/// duplicate-key error at all is rethrown, which is the direction that matters: a transient fault
	/// must never be read as a specified no-op, because a silently skipped advance is a declaration this
	/// deployment never records.
	/// </para>
	/// <para>
	/// The companion error's number is deliberately NOT named here. Its behaviour is observable and
	/// consistent, but it is not something this code should assert a documented meaning for; keying on
	/// the presence of the violation needs no claim about what accompanies it.
	/// </para>
	/// </remarks>
	/// <param name="exception">The exception the insert raised.</param>
	/// <returns><see langword="true"/> when the insert lost a race for this pair.</returns>
	private static bool IsDuplicateKey(SqlException exception) =>
		exception.Errors.Cast<SqlError>()
			.Any(static error => error.Number is ConstraintViolation or UniqueIndexViolation);

	/// <summary>One recorded retention declaration as the row reports it.</summary>
	[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Dapper materializes this type.")]
	private sealed class RecordedRetentionRow
	{
		// ReSharper disable InconsistentNaming - Column aliases use snake_case to match the shared shape
		public string aggregate_type { get; init; } = string.Empty;

		public bool is_withdrawn { get; init; }

		// ReSharper restore InconsistentNaming
	}
}
