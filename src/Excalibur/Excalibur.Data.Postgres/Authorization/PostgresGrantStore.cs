// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Buffers.Binary;
using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Transactions;

using Dapper;

using Excalibur.A3.Authorization;

using Excalibur.Dispatch;

namespace Excalibur.Data.Postgres.Authorization;

/// <summary>
/// PostgreSQL implementation of <see cref="IGrantStore"/> using inline Dapper queries.
/// </summary>
/// <remarks>
/// Implements both <see cref="IGrantStore"/> and <see cref="IGrantQueryStore"/> (via <see cref="GetService"/>)
/// plus <see cref="IActivityGroupGrantStore"/> for activity-group grant operations and
/// <see cref="IActivityGroupGrantReplacement"/> for replacing a set of them atomically.
/// </remarks>
public sealed class PostgresGrantStore
	: IGrantStore, IDurableGrantStore, IGrantQueryStore, IActivityGroupGrantStore, IActivityGroupGrantReplacement
{
	/// <summary>The widest user id the shipped schema's key column holds, in characters.</summary>
	internal const int MaxUserIdLength = 128;

	/// <summary>The widest grant type the shipped schema's key column holds, in characters.</summary>
	internal const int MaxGrantTypeLength = 64;

	/// <summary>The widest qualifier the shipped schema's key column holds, in characters.</summary>
	internal const int MaxQualifierLength = 192;

	// Quoted, because "grant" is a SQL key word and an unquoted occurrence is only accepted in some
	// positions -- and quoted-lowercase is the form the unquoted spellings elsewhere in this file fold to,
	// so both address one table. A quoted "Authz"."Grant" would name a DIFFERENT table, because quoting
	// makes an identifier case-sensitive while the unquoted spellings fold to lower case.
	private const string GrantTable = "\"authz\".\"grant\"";

	// The first key of the two-key advisory lock: a namespace, so an unrelated application's advisory lock on
	// the same second key cannot order against this one. ASCII "EXA3".
	private const int AdvisoryLockNamespace = 0x45584133;

	// Derived from the qualified table with a stable hash -- never string.GetHashCode, which differs per
	// process -- so every instance orders replaces of the same table, and replaces of a different table do
	// not wait on them.
	private static readonly int ReplaceLockKey = BinaryPrimitives.ReadInt32BigEndian(
		SHA256.HashData(Encoding.UTF8.GetBytes($"Excalibur.A3.ActivityGroupGrants:{GrantTable}")));

	private readonly IDbConnection _connection;

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresGrantStore"/> class.
	/// </summary>
	/// <param name="domainDb">The domain database connection provider.</param>
	public PostgresGrantStore(IDomainDb domainDb)
	{
		ArgumentNullException.ThrowIfNull(domainDb);
		_connection = domainDb.Connection;
	}

	/// <inheritdoc />
	public async Task<Grant?> GetGrantAsync(string userId, string tenantId, string grantType,
		string qualifier, CancellationToken cancellationToken)
	{
		const string sql = """
		                   SELECT *
		                   FROM authz.grant
		                   WHERE user_id = @UserId COLLATE "C"
		                   AND tenant_id = @TenantId COLLATE "C"
		                   AND grant_type = @GrantType COLLATE "C"
		                   AND qualifier = @Qualifier COLLATE "C";
		                   """;

		var grant = await _connection.QuerySingleOrDefaultAsync<GrantRow>(
			new CommandDefinition(sql,
				new { UserId = userId, TenantId = tenantId, GrantType = grantType, Qualifier = qualifier },
				commandTimeout: DbTimeouts.RegularTimeoutSeconds,
				cancellationToken: cancellationToken)).ConfigureAwait(false);

		return grant != null
			? new Grant(grant.UserId, grant.FullName, grant.TenantId, grant.GrantType, grant.Qualifier, grant.ExpiresOn,
				grant.GrantedBy, grant.GrantedOn!.Value)
			: null;
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<Grant>> GetAllGrantsAsync(string userId, CancellationToken cancellationToken) =>
		GetAllGrantsAsync(userId, includeExpired: false, cancellationToken);

	/// <inheritdoc />
	public async Task<IReadOnlyList<Grant>> GetAllGrantsAsync(string userId, bool includeExpired,
		CancellationToken cancellationToken)
	{
		// Default-secure: exclude expired grants unless explicitly requested. Expiry is evaluated
		// against the DB clock — same precedent as GrantExistsAsync.
		const string sql = """
		                        SELECT *
		                        FROM authz.grant
		                        WHERE user_id = @UserId COLLATE "C"
		                        AND (@IncludeExpired OR COALESCE(expires_on, 'infinity') > now());
		                   """;

		var grants = await _connection.QueryAsync<GrantRow>(
			new CommandDefinition(sql,
				new { UserId = userId, IncludeExpired = includeExpired },
				commandTimeout: DbTimeouts.RegularTimeoutSeconds,
				cancellationToken: cancellationToken)).ConfigureAwait(false);

		return grants.Select(g => new Grant(
			g.UserId, g.FullName, g.TenantId, g.GrantType, g.Qualifier, g.ExpiresOn, g.GrantedBy, g.GrantedOn!.Value))
			.ToList().AsReadOnly();
	}

	/// <inheritdoc />
	public async Task<int> SaveGrantAsync(Grant grant, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(grant);

		ThrowIfLongerThan(grant.UserId, MaxUserIdLength, nameof(grant.UserId));
		ThrowIfLongerThan(grant.TenantId, Excalibur.Dispatch.TenantId.MaxLength, nameof(grant.TenantId));
		ThrowIfLongerThan(grant.GrantType, MaxGrantTypeLength, nameof(grant.GrantType));
		ThrowIfLongerThan(grant.Qualifier, MaxQualifierLength, nameof(grant.Qualifier));

		// An upsert, as IGrantStore.SaveGrantAsync promises: re-granting an existing grant replaces its details
		// instead of failing on the primary key.
		const string sql = """
		                   INSERT INTO authz."grant" (
		                   user_id, full_name, tenant_id, grant_type, qualifier, expires_on, granted_by, granted_on
		                   ) VALUES (
		                   @UserId, @FullName, @TenantId, @GrantType, @Qualifier, @ExpiresOn::timestamptz, @GrantedBy, @GrantedOn::timestamptz
		                   )
		                   ON CONFLICT (user_id, tenant_id, grant_type, qualifier) DO UPDATE SET
		                   full_name = EXCLUDED.full_name,
		                   expires_on = EXCLUDED.expires_on,
		                   granted_by = EXCLUDED.granted_by,
		                   granted_on = EXCLUDED.granted_on;
		                   """;

		return await _connection.ExecuteAsync(
			new CommandDefinition(sql,
				new
				{
					grant.UserId,
					grant.FullName,
					grant.TenantId,
					grant.GrantType,
					grant.Qualifier,
					grant.ExpiresOn,
					grant.GrantedBy,
					grant.GrantedOn,
				},
				commandTimeout: DbTimeouts.RegularTimeoutSeconds,
				cancellationToken: cancellationToken)).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<int> DeleteGrantAsync(string userId, string tenantId, string grantType,
		string qualifier, string? revokedBy, DateTimeOffset? revokedOn,
		CancellationToken cancellationToken)
	{
		const string sql = """
		                   INSERT INTO authz.grant_history (
		                   	user_id,
		                   	full_name,
		                   	tenant_id,
		                   	grant_type,
		                   	qualifier,
		                   	expires_on,
		                   	granted_by,
		                   	granted_on,
		                   	revoked_by,
		                   	revoked_on
		                   )
		                   SELECT
		                   	user_id,
		                   	full_name,
		                   	tenant_id,
		                   	grant_type,
		                   	qualifier,
		                   	expires_on,
		                   	granted_by,
		                   	granted_on,
		                   	@RevokedBy AS revoked_by,
		                   	@RevokedOn::timestamptz AS revoked_on
		                   FROM authz.grant
		                   WHERE user_id = @UserId COLLATE "C" AND tenant_id = @TenantId COLLATE "C" AND grant_type = @GrantType COLLATE "C" AND qualifier = @Qualifier COLLATE "C";

		                   DELETE FROM authz.grant WHERE user_id = @UserId COLLATE "C" AND tenant_id = @TenantId COLLATE "C" AND grant_type = @GrantType COLLATE "C" AND qualifier = @Qualifier COLLATE "C";
		                   """;

		return await _connection.ExecuteAsync(
			new CommandDefinition(sql,
				new
				{
					UserId = userId,
					TenantId = tenantId,
					GrantType = grantType,
					Qualifier = qualifier,
					RevokedBy = revokedBy,
					RevokedOn = revokedOn,
				},
				commandTimeout: DbTimeouts.RegularTimeoutSeconds,
				cancellationToken: cancellationToken)).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<bool> GrantExistsAsync(string userId, string tenantId, string grantType,
		string qualifier, CancellationToken cancellationToken)
	{
		const string sql = """
		                   SELECT EXISTS (
		                   SELECT 1
		                   FROM authz.grant
		                   WHERE user_id = @UserId COLLATE "C"
		                   AND tenant_id = @TenantId COLLATE "C"
		                   AND grant_type = @GrantType COLLATE "C"
		                   AND qualifier = @Qualifier COLLATE "C"
		                   AND COALESCE(expires_on, 'infinity') > now()
		                   );
		                   """;

		return await _connection.ExecuteScalarAsync<bool>(
			new CommandDefinition(sql,
				new { UserId = userId, TenantId = tenantId, GrantType = grantType, Qualifier = qualifier },
				commandTimeout: DbTimeouts.RegularTimeoutSeconds,
				cancellationToken: cancellationToken)).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);

		if (serviceType == typeof(IDurableGrantStore))
		{
			return this;
		}

		if (serviceType == typeof(IGrantQueryStore))
		{
			return this;
		}

		if (serviceType == typeof(IActivityGroupGrantStore))
		{
			return this;
		}

		if (serviceType == typeof(IActivityGroupGrantReplacement))
		{
			return this;
		}

		return null;
	}

	// IGrantQueryStore

	/// <inheritdoc />
	public Task<IReadOnlyList<Grant>> GetMatchingGrantsAsync(string tenantId, string? userId,
		string? grantType, string? qualifier, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
		ThrowIfEmptyFilter(userId, grantType, qualifier);

		return QueryMatchingAsync(tenantId, userId, grantType, qualifier, cancellationToken);
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<Grant>> GetMatchingGrantsAcrossTenantsAsync(string? userId, string? grantType,
		string? qualifier, CancellationToken cancellationToken)
	{
		ThrowIfEmptyFilter(userId, grantType, qualifier);

		return QueryMatchingAsync(tenantId: null, userId, grantType, qualifier, cancellationToken);
	}

	/// <inheritdoc />
	public async Task<IReadOnlyDictionary<string, object>> FindUserGrantsAsync(string userId,
		CancellationToken cancellationToken)
	{
		const string sql = """
		                        SELECT tenant_id, grant_type, qualifier, expires_on::timestamptz
		                        from authz.grant
		                        WHERE user_id = @userId COLLATE "C"
		                   """;

		var grants = await _connection
			.QueryAsync<(string TenantId, string GrantType, string Qualifier, DateTimeOffset? ExpiresOn)>(
				new CommandDefinition(sql,
					new { UserId = userId },
					commandTimeout: DbTimeouts.RegularTimeoutSeconds,
					cancellationToken: cancellationToken)).ConfigureAwait(false);

		return grants.ToDictionary(
			grant => SegmentedKey.Compose(grant.TenantId, grant.GrantType, grant.Qualifier),
			object (grant) => new UserGrantData(grant.ExpiresOn),
			StringComparer.Ordinal);
	}

	// IActivityGroupGrantStore

	/// <inheritdoc />
	public async Task<int> DeleteActivityGroupGrantsByUserIdAsync(string userId, string grantType,
		CancellationToken cancellationToken)
	{
		const string sql = """
		                        DELETE FROM Authz.grant
		                        WHERE user_id = @UserId COLLATE "C"
		                        AND grant_type = @GrantType COLLATE "C"
		                   """;

		return await _connection.ExecuteAsync(
			new CommandDefinition(sql,
				new { UserId = userId, GrantType = grantType },
				commandTimeout: DbTimeouts.RegularTimeoutSeconds,
				cancellationToken: cancellationToken)).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<int> DeleteAllActivityGroupGrantsAsync(string grantType,
		CancellationToken cancellationToken)
	{
		const string sql = """
		                         DELETE FROM Authz.grant
		                         WHERE grant_type = @GrantType COLLATE "C"
		                   """;

		return await _connection.ExecuteAsync(
			new CommandDefinition(sql,
				new { GrantType = grantType },
				commandTimeout: DbTimeouts.RegularTimeoutSeconds,
				cancellationToken: cancellationToken)).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<int> InsertActivityGroupGrantAsync(string userId, string fullName,
		string tenantId, string grantType, string qualifier,
		DateTimeOffset? expiresOn, string grantedBy, CancellationToken cancellationToken)
	{
		ThrowIfLongerThan(userId, MaxUserIdLength, nameof(userId));
		ThrowIfLongerThan(tenantId, Excalibur.Dispatch.TenantId.MaxLength, nameof(tenantId));
		ThrowIfLongerThan(grantType, MaxGrantTypeLength, nameof(grantType));
		ThrowIfLongerThan(qualifier, MaxQualifierLength, nameof(qualifier));

		const string sql = """
		                        INSERT INTO authz."grant" (user_id, full_name, tenant_id, grant_type, qualifier, expires_on, granted_by, granted_on)
		                        VALUES (@UserId, @FullName, @TenantId, @GrantType, @Qualifier, @ExpiresOn::timestamptz, @GrantedBy, now())
		                   """;

		return await _connection.ExecuteAsync(
			new CommandDefinition(sql,
				new
				{
					UserId = userId,
					FullName = fullName,
					TenantId = tenantId,
					GrantType = grantType,
					Qualifier = qualifier,
					ExpiresOn = expiresOn,
					GrantedBy = grantedBy,
				},
				commandTimeout: DbTimeouts.RegularTimeoutSeconds,
				cancellationToken: cancellationToken)).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<string>> GetDistinctActivityGroupGrantUserIdsAsync(
		string grantType, CancellationToken cancellationToken)
	{
		const string sql = """
		                        SELECT DISTINCT user_id
		                        FROM Authz.grant
		                        WHERE grant_type = @GrantType COLLATE "C"
		                   """;

		var result = await _connection.QueryAsync<string>(
			new CommandDefinition(sql,
				new { GrantType = grantType },
				commandTimeout: DbTimeouts.RegularTimeoutSeconds,
				cancellationToken: cancellationToken)).ConfigureAwait(false);

		return result.ToList().AsReadOnly();
	}

	// IActivityGroupGrantReplacement

	/// <inheritdoc />
	public Task<IReadOnlyCollection<string>> ReplaceActivityGroupGrantsAsync(
		string grantType,
		ActivityGroupGrantSnapshot snapshot,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(snapshot);

		// Refused before the transaction opens: an empty estate-wide snapshot, or one mixing grant types,
		// cannot be applied whole, so it is not applied at all.
		ActivityGroupGrantSnapshot.ValidateAsEstateReplacement(snapshot, grantType);

		return ReplaceAsync(grantType, userId: null, snapshot, cancellationToken);
	}

	/// <inheritdoc />
	public Task<IReadOnlyCollection<string>> ReplaceActivityGroupGrantsForUserAsync(
		string userId,
		string grantType,
		ActivityGroupGrantSnapshot snapshot,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(snapshot);

		// An EMPTY snapshot is accepted here, unlike the estate-wide member: that user now holds no grants of
		// this type and every one of theirs is revoked.
		ActivityGroupGrantSnapshot.ValidateAsUserReplacement(snapshot, userId, grantType);

		return ReplaceAsync(grantType, userId, snapshot, cancellationToken);
	}

	/// <summary>
	/// Removes the grants in scope and writes <paramref name="snapshot"/> in their place, in one transaction,
	/// reporting the users whose grants were removed.
	/// </summary>
	/// <param name="grantType">The grant type in scope.</param>
	/// <param name="userId">The single user in scope, or <see langword="null"/> for every user.</param>
	/// <param name="snapshot">The grants to write. Already validated against the scope.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	private async Task<IReadOnlyCollection<string>> ReplaceAsync(
		string grantType,
		string? userId,
		ActivityGroupGrantSnapshot snapshot,
		CancellationToken cancellationToken)
	{
		// An ambient transaction would decide this commit together with work the store cannot see, so the
		// replace could be rolled back after it reported success, or kept after the caller's own work failed.
		if (Transaction.Current is not null)
		{
			throw new InvalidOperationException(
				"The activity-group grant replace runs in its own transaction and cannot run inside an ambient "
				+ "transaction. Call it outside the TransactionScope.");
		}

		var connection = _connection as DbConnection
			?? throw new InvalidOperationException(
				$"The activity-group grant replace needs a {nameof(DbConnection)}; the configured connection is "
				+ $"{_connection.GetType().FullName}.");

		var entries = snapshot.Entries;
		var openedHere = connection.State != ConnectionState.Open;

		if (openedHere)
		{
			await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
		}

		try
		{
			// READ COMMITTED, stated rather than inherited: a reader sees the committed grants as of its own
			// statement, which is the whole old set until this commits and the whole new one after. A stronger
			// level would make a concurrent replace fail with a serialization error instead of waiting.
			await using var transaction = await connection
				.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken)
				.ConfigureAwait(false);

			// Held until this transaction ends, and keyed on the TABLE rather than on the scope, so an
			// estate-wide replace and a per-user one cannot interleave: without that, the estate-wide delete
			// would run between a per-user delete and its inserts and the user's new grants would survive an
			// estate refresh that never saw them. SET LOCAL bounds the wait for it to this transaction too,
			// so the caller's shared connection keeps its own lock_timeout afterwards.
			_ = await connection.ExecuteAsync(
				new CommandDefinition(
					$"SET LOCAL lock_timeout = '{DbTimeouts.RegularTimeoutSeconds}s'; "
					+ "SELECT pg_advisory_xact_lock(@Namespace, @Key);",
					new { Namespace = AdvisoryLockNamespace, Key = ReplaceLockKey },
					transaction,
					commandTimeout: DbTimeouts.LongRunningTimeoutSeconds,
					cancellationToken: cancellationToken)).ConfigureAwait(false);

			// Read under the lock, from the delete itself, so the previous users are exactly the ones removed.
			var scope = userId is null ? string.Empty : " AND user_id = @UserId";

			var previous = await connection.QueryAsync<string>(
				new CommandDefinition(
					$"DELETE FROM {GrantTable} WHERE grant_type = @GrantType{scope} RETURNING user_id",
					new { GrantType = grantType, UserId = userId },
					transaction,
					commandTimeout: DbTimeouts.LongRunningTimeoutSeconds,
					cancellationToken: cancellationToken)).ConfigureAwait(false);

			var users = previous.Distinct(StringComparer.Ordinal).ToArray();

			if (entries.Count > 0)
			{
				_ = await connection.ExecuteAsync(
					new CommandDefinition(
						$"""
						 INSERT INTO {GrantTable}
						   (user_id, full_name, tenant_id, grant_type, qualifier, expires_on, granted_by, granted_on)
						 SELECT u, f, t, g, q, e, b, NOW() AT TIME ZONE 'UTC'
						 FROM unnest(@Users::text[], @FullNames::text[], @Tenants::text[], @GrantTypes::text[],
						             @Qualifiers::text[], @ExpiresOn::timestamptz[], @GrantedBy::text[])
						      AS s(u, f, t, g, q, e, b)
						 """,
						new
						{
							Users = entries.Select(static e => e.UserId).ToArray(),
							FullNames = entries.Select(static e => e.FullName).ToArray(),
							Tenants = entries.Select(static e => e.TenantId).ToArray(),
							GrantTypes = entries.Select(static e => e.GrantType).ToArray(),
							Qualifiers = entries.Select(static e => e.Qualifier).ToArray(),

							// Normalized to UTC because Npgsql refuses to write a DateTimeOffset with a
							// non-zero offset to timestamptz, and the authority's payload carries whatever
							// offset it was serialized with.
							ExpiresOn = entries.Select(static e => e.ExpiresOn?.ToUniversalTime()).ToArray(),
							GrantedBy = entries.Select(static e => e.GrantedBy).ToArray(),
						},
						transaction,
						commandTimeout: DbTimeouts.LongRunningTimeoutSeconds,
						cancellationToken: cancellationToken)).ConfigureAwait(false);
			}

			await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

			return users;
		}
		finally
		{
			if (openedHere)
			{
				await connection.CloseAsync().ConfigureAwait(false);
			}
		}
	}

	/// <summary>
	/// Dapper row-mapping DTO for grant data from PostgreSQL.
	/// </summary>
	// The match query: ordinal equality on every supplied filter; a null filter adds no constraint, and a null
	// tenant (the estate-wide read) adds none either. COLLATE "C" makes each comparison byte-wise whatever the
	// database's default collation is. The ::text casts give a null parameter a type Postgres can compare.
	private async Task<IReadOnlyList<Grant>> QueryMatchingAsync(string? tenantId, string? userId,
		string? grantType, string? qualifier, CancellationToken cancellationToken)
	{
		const string sql = """
		                        SELECT *
		                        FROM authz."grant"
		                        WHERE (@TenantId::text IS NULL OR tenant_id = @TenantId::text COLLATE "C")
		                        AND (@UserId::text IS NULL OR user_id = @UserId::text COLLATE "C")
		                        AND (@GrantType::text IS NULL OR grant_type = @GrantType::text COLLATE "C")
		                        AND (@Qualifier::text IS NULL OR qualifier = @Qualifier::text COLLATE "C");
		                   """;

		var grants = await _connection.QueryAsync<GrantRow>(
			new CommandDefinition(sql,
				new
				{
					TenantId = tenantId,
					UserId = userId,
					GrantType = grantType,
					Qualifier = qualifier,
				},
				commandTimeout: DbTimeouts.RegularTimeoutSeconds,
				cancellationToken: cancellationToken)).ConfigureAwait(false);

		return grants.Select(g => new Grant(
			g.UserId, g.FullName, g.TenantId, g.GrantType, g.Qualifier, g.ExpiresOn, g.GrantedBy, g.GrantedOn!.Value))
			.ToList().AsReadOnly();
	}

	private static void ThrowIfEmptyFilter(string? userId, string? grantType, string? qualifier)
	{
		if (userId is not null)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(userId);
		}

		if (grantType is not null)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(grantType);
		}

		if (qualifier is not null)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(qualifier);
		}
	}

	// Refused before the database sees it, naming the limit, so every provider fails the same way.
	private static void ThrowIfLongerThan(string value, int maxLength, string paramName)
	{
		if (value is not null && value.Length > maxLength)
		{
			throw new ArgumentException(
				$"'{paramName}' is {value.Length} characters; the shipped grant schema holds at most {maxLength}.",
				paramName);
		}
	}

	private sealed record GrantRow
	{
		public required string UserId { get; init; }
		public required string FullName { get; init; }
		public required string TenantId { get; init; }
		public required string GrantType { get; init; }
		public required string Qualifier { get; init; }
		public DateTimeOffset? ExpiresOn { get; init; }
		public required string GrantedBy { get; init; }
		public DateTimeOffset? GrantedOn { get; init; }
	}

	/// <summary>
	/// Data associated with a user grant for <see cref="FindUserGrantsAsync"/> results.
	/// </summary>
	private sealed record UserGrantData
	{
		public UserGrantData(DateTimeOffset? expiresOn) => ExpiresOn = expiresOn?.ToUniversalTime().Ticks;

		public long? ExpiresOn { get; }
	}
}
