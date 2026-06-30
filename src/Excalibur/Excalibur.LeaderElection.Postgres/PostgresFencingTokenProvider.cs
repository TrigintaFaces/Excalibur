// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using Excalibur.Dispatch.LeaderElection.Fencing;

using Npgsql;

namespace Excalibur.LeaderElection.Postgres;

/// <summary>
/// Postgres-backed <see cref="IFencingTokenProvider"/> implementation.
/// </summary>
/// <remarks>
/// <para>
/// Uses a dedicated per-resource Postgres <c>SEQUENCE</c> as the atomic monotonic mint: <c>nextval</c> is the
/// purpose-built, strictly-increasing counter (gaps on rollback are fine — the value only ever increases).
/// The first leader receives <c>1</c> and every subsequent acquisition is strictly greater, so the monotonic
/// invariant holds without a read-modify-write race that could mint two equal tokens. Mirrors the Redis
/// reference and the SqlServer sibling.
/// </para>
/// <para>
/// Validation is fail-closed against the current high-water mark: a token is accepted only when it is at or
/// above the stored sequence value, so a stale leader whose lease was taken over by a new leader (which
/// advanced the sequence) is rejected (Martin Kleppmann's fencing-token pattern).
/// </para>
/// <para>
/// <b>Injection safety:</b> the per-resource sequence name is <c>fencing_</c> + the lowercase hex SHA-256 of
/// the resource id — never raw caller input — so it is a valid, lowercase (unquoted) Postgres identifier and
/// immune to SQL injection. <c>GetTokenAsync</c> reads <c>pg_sequences.last_value</c> via a parameterized
/// query (NULL until the first <c>nextval</c> — the idiomatic "no token issued yet" signal). The provider
/// opens its own short-lived pooled connections (separate from the election's dedicated, pooling-disabled lock
/// connection).
/// </para>
/// </remarks>
internal sealed class PostgresFencingTokenProvider : IFencingTokenProvider
{
	private readonly string _connectionString;

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresFencingTokenProvider"/> class.
	/// </summary>
	/// <param name="connectionString">The Postgres connection string (same database as the leader election).</param>
	public PostgresFencingTokenProvider(string connectionString)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		_connectionString = connectionString;
	}

	/// <inheritdoc />
	[SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities",
		Justification = "The only interpolated token is the sequence name from SequenceName(): 'fencing_' + " +
			"lowercase-hex SHA-256 of the resource id, i.e. exclusively [0-9a-f] and never raw caller input, so it " +
			"cannot carry SQL injection. Postgres DDL identifiers (CREATE SEQUENCE / nextval) cannot be " +
			"parameterized, so the provably-safe name is necessarily inlined.")]
	public async ValueTask<long> IssueTokenAsync(string resourceId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(resourceId);
		cancellationToken.ThrowIfCancellationRequested();

		var sequenceName = SequenceName(resourceId);

		// CREATE SEQUENCE IF NOT EXISTS is idempotent + concurrency-safe; nextval is atomic + strictly
		// monotonic. The sequence name is hash-derived lowercase hex (injection-proof) so inlining it as an
		// identifier is safe. Both statements run in one round-trip; the SELECT returns the new token.
		var sql =
			$"CREATE SEQUENCE IF NOT EXISTS {sequenceName}; SELECT nextval('{sequenceName}');";

		await using var connection = new NpgsqlConnection(_connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		await using var command = new NpgsqlCommand(sql, connection);
		var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
		return Convert.ToInt64(result, CultureInfo.InvariantCulture);
	}

	/// <inheritdoc />
	public async ValueTask<long?> GetTokenAsync(string resourceId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(resourceId);
		cancellationToken.ThrowIfCancellationRequested();

		var sequenceName = SequenceName(resourceId);

		// pg_sequences.last_value is NULL until the first nextval (and the row is absent if the sequence was
		// never created) -> null = no token ever issued = no active leader (the idiomatic "no value" signal,
		// ADR-339 Decision 2). Parameterized, so no inline identifier here.
		const string sql =
			"SELECT last_value FROM pg_sequences WHERE schemaname = current_schema() AND sequencename = @name;";

		await using var connection = new NpgsqlConnection(_connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		await using var command = new NpgsqlCommand(sql, connection);
		command.Parameters.AddWithValue("name", sequenceName);

		var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
		return result is null or DBNull ? null : Convert.ToInt64(result, CultureInfo.InvariantCulture);
	}

	/// <inheritdoc />
	public async ValueTask<bool> ValidateTokenAsync(string resourceId, long token, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(resourceId);

		// Fail-closed high-water-mark check: with no issued token nothing is valid; otherwise accept only
		// tokens at or above the current sequence value, rejecting a stale leader's lower token.
		var current = await GetTokenAsync(resourceId, cancellationToken).ConfigureAwait(false);
		return current.HasValue && token >= current.Value;
	}

	/// <summary>
	/// Derives a deterministic, injection-proof, valid lowercase Postgres-identifier sequence name for a
	/// resource: <c>fencing_</c> + the lowercase hex SHA-256 of the resource id. Independent of the resource
	/// id's characters and never embeds raw caller input.
	/// </summary>
	/// <param name="resourceId">The protected resource identifier.</param>
	/// <returns>A 72-character lowercase sequence name (within Postgres's 63-byte identifier limit? no — see remarks).</returns>
	/// <remarks>
	/// SHA-256 hex is 64 chars + the 8-char <c>fencing_</c> prefix = 72 chars, which exceeds Postgres's 63-byte
	/// identifier limit; Postgres silently truncates to 63 bytes (NAMEDATALEN-1). Truncation is deterministic
	/// and collision-resistant for our purposes (the first 55 hex chars of a SHA-256 remain effectively unique
	/// per resource), so the same resource always maps to the same (truncated) sequence. Use the truncated form
	/// explicitly to avoid relying on silent truncation.
	/// </remarks>
	private static string SequenceName(string resourceId)
	{
		var hash = SHA256.HashData(Encoding.UTF8.GetBytes(resourceId));
		// 55 hex chars + "fencing_" (8) = 63 = Postgres NAMEDATALEN-1, the max identifier length.
		var hex = Convert.ToHexStringLower(hash)[..55];
		return "fencing_" + hex;
	}
}
