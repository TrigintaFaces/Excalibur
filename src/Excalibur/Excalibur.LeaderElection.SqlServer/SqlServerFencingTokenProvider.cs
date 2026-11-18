// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using Excalibur.Dispatch.LeaderElection.Fencing;

using Microsoft.Data.SqlClient;

namespace Excalibur.LeaderElection.SqlServer;

/// <summary>
/// SQL Server-backed <see cref="IFencingTokenProvider"/> implementation.
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
/// Martin Kleppmann, mirroring the Redis reference.
/// </para>
/// <para>
/// <b>Injection safety:</b> the per-resource sequence name is <c>fencing_</c> + the hex SHA-256 of the
/// resource id — never raw caller input — so it is both a valid SQL identifier (independent of dots/spaces in
/// the resource id) and immune to SQL injection; <c>QUOTENAME</c> at each dynamic-SQL use site is
/// defense-in-depth. The provider opens its own short-lived pooled connections (separate from the leader
/// election's dedicated, pooling-disabled lock connection).
/// </para>
/// </remarks>
internal sealed class SqlServerFencingTokenProvider : IFencingTokenIssuer
{
	private readonly string _connectionString;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerFencingTokenProvider"/> class.
	/// </summary>
	/// <param name="connectionString">The SQL Server connection string (same database as the leader election).</param>
	public SqlServerFencingTokenProvider(string connectionString)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		// Fencing is durable infrastructure state, independent of the caller's transaction.
		// In particular, first-use sequence creation must survive business rollback.
		_connectionString = new SqlConnectionStringBuilder(connectionString) { Enlist = false }.ConnectionString;
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
		// call like QUOTENAME (server-side parse error,). The whole batch is one round-trip.
		const string sql = @"
SET NOCOUNT ON;
IF NOT EXISTS (SELECT 1 FROM sys.sequences WHERE schema_id = SCHEMA_ID(N'dbo') AND name = @name)
BEGIN
    BEGIN TRY
        DECLARE @create nvarchar(max) = N'CREATE SEQUENCE [dbo].' + QUOTENAME(@name) + N' AS bigint START WITH 1 INCREMENT BY 1';
        EXEC sp_executesql @create;
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() <> 2714 THROW; -- 2714 = object already exists (concurrent create) -> ignore
    END CATCH
END
DECLARE @token bigint;
DECLARE @draw nvarchar(max) = N'SELECT @t = NEXT VALUE FOR [dbo].' + QUOTENAME(@name);
EXEC sp_executesql @draw, N'@t bigint OUTPUT', @t = @token OUTPUT;
SELECT @token;";

		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		await using var command = new SqlCommand(sql, connection);
		_ = command.Parameters.AddWithValue("@name", sequenceName);

		try
		{
			var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
			return Convert.ToInt64(result, CultureInfo.InvariantCulture);
		}
		catch (SqlException ex) when (ex.Number == SequenceExhaustedErrorNumber)
		{
			// A NO CYCLE bigint SEQUENCE raises error 11728 ("has reached its minimum or maximum value.
			// Restart the sequence object to allow new values to be generated") at its ceiling rather than
			// wrapping; translate to the contract's FencingTokenExhaustedException so a consumer's
			// fail-closed catch relinquishes rather than seeing a raw SqlException (a wrapped/reused
			// fencing token would be a split-brain catastrophe).
			throw new FencingTokenExhaustedException(
				string.Format(
					CultureInfo.InvariantCulture,
					"SQL Server fencing token domain is exhausted for resource '{0}'.",
					resourceId),
				ex)
			{
				ResourceId = resourceId,
			};
		}
	}

	/// <summary>
	/// SQL Server error number raised when a NO CYCLE sequence reaches its min/max value. Measured against
	/// a real SQL Server: 11728, not 11732 -- an earlier draft of this provider checked the wrong
	/// number, which would have let a real overflow escape untranslated as a raw <see cref="SqlException"/>.
	/// </summary>
	private const int SequenceExhaustedErrorNumber = 11728;

	/// <inheritdoc />
	public async ValueTask<long?> GetTokenAsync(string resourceId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(resourceId);
		cancellationToken.ThrowIfCancellationRequested();

		var sequenceName = SequenceName(resourceId);

		// null = NO TOKEN WAS EVER ISSUED for this resource, which covers two distinct states that must
		// answer identically: the sequence was never created, and the sequence EXISTS BUT WAS NEVER DRAWN.
		// Never a fabricated or sentinel value -- the idiomatic "no value" signal.
		//
		// READ last_used_value, NOT current_value, AND THE REASON IS THE COLUMN'S OWN TYPE. Microsoft
		// declares current_value as sql_variant NOT NULL and documents it as "the last value obligated...
		// Returns the START WITH value if the sequence has never been used", so that column CANNOT EXPRESS
		// never-issued: it answers 1 for a sequence nobody ever drew from. last_used_value is sql_variant
		// NULL, documented as "the last value generated by the NEXT VALUE FOR function", so the
		// never-issued state is representable.
		//
		// The consequence of reading the wrong one is an INVERSION rather than an off-by-one, which is why
		// the column choice is load-bearing. The issue path creates the sequence and draws from it in one
		// batch with no enclosing transaction, so an interruption between the two (a cancelled token being
		// the likeliest route) leaves a created, never-drawn sequence. Against current_value the fence then
		// reads a high-water mark of 1 and admits 1 and every value above it -- an unfenced resource that
		// reports itself fenced, logging each admission as a success. Against last_used_value it reads null,
		// the has-value test in ValidateTokenAsync fails, and the fence refuses EVERY token: fail-closed,
		// which is the stated guarantee.
		//
		// The two columns also diverge for a sequence driven through sp_sequence_get_range, where
		// current_value is the top of the reserved range rather than a generated value. This provider never
		// uses that procedure, so that divergence does not arise here -- but it is a second reason the
		// documented meaning of current_value is not the high-water mark a fence needs.
		//
		// VERSION REQUIREMENT, and it is this package's floor rather than an inherited one.
		// last_used_value is documented "Applies to SQL Server 2017 and later", so reading it sets a hard
		// 2017 minimum for this provider. current_value needs only 2012, so this is a NARROWING, and it is
		// taken deliberately: the alternative is a fence that admits every token for a resource whose token
		// was never issued, which is not a supported-version question.
		//
		// The previous wording here cited "the published compatibility floor for this framework's SQL
		// Server packages is 2019". That was imprecise in the direction that hid the narrowing: 2019 is
		// published for the event-store package, and the compatibility matrix states NO floor for leader
		// election on SQL Server. Nothing inherited a floor to this provider; this comment is where it
		// starts.
		const string sql = @"
DECLARE @schemaId int = SCHEMA_ID(N'dbo');
DECLARE @visibleCount int = (SELECT COUNT(*) FROM sys.sequences WHERE schema_id = @schemaId AND name = @name);
DECLARE @canProveAbsent bit = 0;
IF @visibleCount = 0 AND @schemaId IS NOT NULL
BEGIN
    IF USER_ID() = 1
        SET @canProveAbsent = 1;
    ELSE IF (HAS_PERMS_BY_NAME(N'dbo', N'SCHEMA', N'VIEW DEFINITION') = 1
             OR HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'VIEW DEFINITION') = 1)
        AND NOT EXISTS
        (
            SELECT 1 FROM sys.database_permissions AS p
            INNER JOIN sys.user_token AS t ON t.principal_id = p.grantee_principal_id
            WHERE p.state = N'D' AND p.permission_name IN (N'VIEW DEFINITION', N'CONTROL')
              AND (p.class = 0 OR (p.class = 3 AND p.major_id = @schemaId)
                   OR (p.class = 1 AND (OBJECT_SCHEMA_NAME(p.major_id) = N'dbo'
                                       OR OBJECT_SCHEMA_NAME(p.major_id) IS NULL)))
        )
        SET @canProveAbsent = 1;
END;
SELECT CAST(DATABASEPROPERTYEX(DB_NAME(), N'Updateability') AS nvarchar(128)),
       @schemaId, @visibleCount, @canProveAbsent,
       (SELECT CAST(last_used_value AS bigint) FROM sys.sequences WHERE schema_id = @schemaId AND name = @name);";

		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
		await using var command = new SqlCommand(sql, connection);
		_ = command.Parameters.AddWithValue("@name", sequenceName);
		await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false) || await reader.IsDBNullAsync(0, cancellationToken).ConfigureAwait(false) ||
			!string.Equals(reader.GetString(0), "READ_WRITE", StringComparison.Ordinal) || await reader.IsDBNullAsync(1, cancellationToken).ConfigureAwait(false))
		{
			throw new InvalidOperationException("Fencing marks require the selected writable SQL Server authority and dbo schema.");
		}

		var count = reader.GetInt32(2);
		if (count == 0)
		{
			if (!reader.GetBoolean(3))
			{
				throw new InvalidOperationException("Fencing sequence metadata visibility cannot distinguish absence from a hidden mark. Grant effective dbo-schema metadata visibility without overriding DENY permissions.");
			}
			return null;
		}
		if (count != 1)
		{
			throw new InvalidOperationException("The selected dbo fencing sequence is ambiguous.");
		}
		return await reader.IsDBNullAsync(4, cancellationToken).ConfigureAwait(false) ? null : reader.GetInt64(4);
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
