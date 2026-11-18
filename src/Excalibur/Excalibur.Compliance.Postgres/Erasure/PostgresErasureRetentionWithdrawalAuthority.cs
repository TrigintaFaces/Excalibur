// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;

using Dapper;

using Microsoft.Extensions.Options;

using Npgsql;

namespace Excalibur.Compliance.Postgres.Erasure;

/// <summary>
/// Withdraws a PostgreSQL-recorded retention declaration, tombstoning it rather than deleting it.
/// </summary>
/// <remarks>
/// <para>
/// <b>A type of its own, and that is the whole point of the contract being separate.</b> Withdrawing a
/// declaration is an administrative act with an accountable actor; declaring one is a deployment. Keeping
/// them on separate types means the code that deploys cannot withdraw, because it holds no reference that
/// can. Putting both on one object would have left two interfaces over something every erasure path
/// already has, which is a separation in name only.
/// </para>
/// <para>
/// It is deliberately NOT registered by the erasure store's own registration. A consumer that needs to
/// withdraw a declaration registers this itself, which keeps the capability out of the default graph.
/// </para>
/// </remarks>
public sealed class PostgresErasureRetentionWithdrawalAuthority : IErasureRetentionWithdrawalAuthority
{
	private readonly PostgresErasureStoreOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresErasureRetentionWithdrawalAuthority"/> class.
	/// </summary>
	/// <param name="options">The erasure store options, whose table name and timeout this shares.</param>
	/// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
	public PostgresErasureRetentionWithdrawalAuthority(IOptions<PostgresErasureStoreOptions> options)
	{
		ArgumentNullException.ThrowIfNull(options);
		_options = options.Value;
	}

	/// <inheritdoc />
	/// <remarks>
	/// <para>
	/// A withdrawal TOMBSTONES the row rather than deleting it, so the pair stays distinguishable from one
	/// this deployment never recorded, and the advance path — which contains no update — cannot re-create
	/// it.
	/// </para>
	/// <para>
	/// <b>The <c>withdrawn_at IS NULL</c> term is what makes a repeat withdrawal a no-op that PRESERVES the
	/// first withdrawal's actor, reason and instant.</b> Without it a second call would overwrite the audit
	/// facts of the first, and the record an auditor reads would name whoever withdrew it last rather than
	/// who decided.
	/// </para>
	/// <para>
	/// One statement decides all three outcomes, so there is no window between asking and acting. The
	/// update reports whether it tombstoned the row; a count in the same statement reports whether the row
	/// exists at all, which is what separates "already withdrawn" from "never recorded".
	/// </para>
	/// </remarks>
	public async Task<RetentionWithdrawalOutcome> WithdrawDeclaredRetentionAsync(
		DeclaredRetentionKey declaration,
		string actor,
		string reason,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(declaration.TenantId);
		ArgumentException.ThrowIfNullOrWhiteSpace(declaration.AggregateType);
		ArgumentException.ThrowIfNullOrWhiteSpace(actor);
		ArgumentException.ThrowIfNullOrWhiteSpace(reason);

		var sql = $@"
			WITH updated AS (
				UPDATE {_options.FullRetentionDeclarationsTableName}
				SET withdrawn_at = @Now, withdrawn_by = @Actor, withdrawal_reason = @Reason
				WHERE tenant_id = @TenantId AND aggregate_type = @AggregateType AND withdrawn_at IS NULL
				RETURNING 1
			)
			SELECT
				(SELECT count(*) FROM updated) AS withdrawn_now,
				(SELECT count(*) FROM {_options.FullRetentionDeclarationsTableName}
					WHERE tenant_id = @TenantId AND aggregate_type = @AggregateType) AS recorded_rows";

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var result = await connection.QuerySingleAsync<WithdrawalOutcomeRow>(new CommandDefinition(
			sql,
			new
			{
				declaration.TenantId,
				declaration.AggregateType,
				Actor = actor,
				Reason = reason,
				Now = DateTimeOffset.UtcNow
			},
			cancellationToken: cancellationToken,
			commandTimeout: _options.CommandTimeoutSeconds)).ConfigureAwait(false);

		return result.withdrawn_now > 0
			? RetentionWithdrawalOutcome.Withdrawn
			: result.recorded_rows > 0
				? RetentionWithdrawalOutcome.AlreadyWithdrawn
				: RetentionWithdrawalOutcome.NotDeclared;
	}

	/// <summary>The two counts that together decide a withdrawal's outcome.</summary>
	[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Dapper materializes this type.")]
	private sealed class WithdrawalOutcomeRow
	{
		// ReSharper disable InconsistentNaming - Column names use snake_case
		public long withdrawn_now { get; init; }

		public long recorded_rows { get; init; }

		// ReSharper restore InconsistentNaming
	}
}
