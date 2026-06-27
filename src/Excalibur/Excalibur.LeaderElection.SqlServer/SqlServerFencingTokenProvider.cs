// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using Excalibur.Dispatch.LeaderElection.Fencing;

using Microsoft.Data.SqlClient;

namespace Excalibur.LeaderElection.SqlServer;

/// <summary>
/// SQL Server-backed <see cref="IFencingTokenProvider"/> implementation (ADR-339, bd-nxmjpm).
/// </summary>
/// <remarks>
/// <para>
/// Uses a dedicated per-resource SQL Server <c>SEQUENCE</c> object as the atomic monotonic mint:
/// <c>NEXT VALUE FOR</c> is the purpose-built, gap-tolerant, strictly-increasing global counter (preferred
/// over <c>ROWVERSION</c>). The first leader receives <c>1</c> and every subsequent acquisition is strictly
/// greater, so the monotonic invariant holds without a read-modify-write race that could mint two equal tokens.
/// </para>
/// <para>
/// Validation is fail-closed against the current high-water mark: a token is accepted only when it is at or
/// above the stored sequence value, so a stale leader whose lease was taken over by a new leader (which
/// advanced the sequence) is rejected. This is the distributed-systems fencing-token pattern described by
/// Martin Kleppmann, mirroring the Redis reference (<c>RedisFencingTokenProvider</c>, bd-umemwa).
/// </para>
/// <para>
/// <b>Injection safety:</b> the per-resource sequence name is <c>fencing_</c> + the hex SHA-256 of the
/// resource id — never raw caller input — so it is both a valid SQL identifier (independent of dots/spaces in
/// the resource id) and immune to SQL injection; <c>QUOTENAME</c> at each dynamic-SQL use site is
/// defense-in-depth. The provider opens its own short-lived pooled connections (separate from the leader
/// election's dedicated, pooling-disabled lock connection).
/// </para>
/// </remarks>
internal sealed class SqlServerFencingTokenProvider : IFencingTokenProvider
{
	private readonly string _connectionString;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerFencingTokenProvider"/> class.
	/// </summary>
	/// <param name="connectionString">The SQL Server connection string (same database as the leader election).</param>
	public SqlServerFencingTokenProvider(string connectionString)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		_connectionString = connectionString;
	}

	/// <inheritdoc />
	public async ValueTask<long> IssueTokenAsync(string resourceId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(resourceId);
		cancellationToken.ThrowIfCancellationRequested();

		var sequenceName = SequenceName(resourceId);

		// Idempotent, concurrency-safe create (TRY/CATCH swallows 2714 "already exists" from a racing creator),
		// then atomically draw the next strictly-monotonic value. Both the CREATE and the NEXT VALUE FOR draw
		// require a literal identifier, so each materializes its QUOTENAME-bracketed, hash-derived
		// (injection-proof) name into an nvarchar variable and runs through sp_executesql — NOT EXEC(<concat>),
		// because EXEC(string) only accepts literals + @variables concatenated and rejects an inline function
		// call like QUOTENAME (server-side parse error, bd-397kyu). The whole batch is one round-trip.
		const string sql = @"
SET NOCOUNT ON;
IF NOT EXISTS (SELECT 1 FROM sys.sequences WHERE name = @name)
BEGIN
    BEGIN TRY
        DECLARE @create nvarchar(max) = N'CREATE SEQUENCE ' + QUOTENAME(@name) + N' AS bigint START WITH 1 INCREMENT BY 1';
        EXEC sp_executesql @create;
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() <> 2714 THROW; -- 2714 = object already exists (concurrent create) -> ignore
    END CATCH
END
DECLARE @token bigint;
DECLARE @draw nvarchar(max) = N'SELECT @t = NEXT VALUE FOR ' + QUOTENAME(@name);
EXEC sp_executesql @draw, N'@t bigint OUTPUT', @t = @token OUTPUT;
SELECT @token;";

		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		await using var command = new SqlCommand(sql, connection);
		_ = command.Parameters.AddWithValue("@name", sequenceName);

		var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
		return Convert.ToInt64(result, CultureInfo.InvariantCulture);
	}

	/// <inheritdoc />
	public async ValueTask<long?> GetTokenAsync(string resourceId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(resourceId);
		cancellationToken.ThrowIfCancellationRequested();

		var sequenceName = SequenceName(resourceId);

		// null = the sequence has never been created -> no token ever issued -> no active leader. Never a
		// fabricated/sentinel value (the idiomatic "no value" signal, ADR-339 Decision 2). After at least one
		// IssueToken, current_value is the last drawn token (the high-water mark).
		const string sql = "SELECT CAST(current_value AS bigint) FROM sys.sequences WHERE name = @name;";

		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		await using var command = new SqlCommand(sql, connection);
		_ = command.Parameters.AddWithValue("@name", sequenceName);

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
	/// Derives a deterministic, injection-proof, valid SQL-identifier sequence name for a resource:
	/// <c>fencing_</c> + the hex SHA-256 of the resource id. Independent of the resource id's characters
	/// (dots, spaces, etc.) and never embeds raw caller input.
	/// </summary>
	/// <param name="resourceId">The protected resource identifier.</param>
	/// <returns>A 72-character sequence name well within SQL Server's 128-character identifier limit.</returns>
	private static string SequenceName(string resourceId)
	{
		var hash = SHA256.HashData(Encoding.UTF8.GetBytes(resourceId));
		return "fencing_" + Convert.ToHexString(hash);
	}
}
