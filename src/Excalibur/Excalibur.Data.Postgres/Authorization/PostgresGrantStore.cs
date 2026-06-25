// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.A3.Authorization;

namespace Excalibur.Data.Postgres.Authorization;

/// <summary>
/// PostgreSQL implementation of <see cref="IGrantStore"/> using inline Dapper queries.
/// </summary>
/// <remarks>
/// Implements both <see cref="IGrantStore"/> and <see cref="IGrantQueryStore"/> (via <see cref="GetService"/>)
/// plus <see cref="IActivityGroupGrantStore"/> for activity-group grant operations.
/// </remarks>
public sealed class PostgresGrantStore : IGrantStore, IGrantQueryStore, IActivityGroupGrantStore
{
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
		                   WHERE user_id = @UserId
		                   AND tenant_id = @TenantId
		                   AND grant_type = @GrantType
		                   AND qualifier = @Qualifier;
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
		                        WHERE user_id = @UserId
		                        AND (@IncludeExpired OR COALESCE(expires_on, 'infinity') > now() at time zone 'utc');
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

		const string sql = """
		                   INSERT INTO authz."grant" (
		                   user_id,
		                   full_name,
		                   tenant_id,
		                   grant_type,
		                   qualifier,
		                   expires_on,
		                   granted_by,
		                   granted_on
		                   ) VALUES (
		                   @UserId,
		                   @FullName,
		                   @TenantId,
		                   @GrantType,
		                   @Qualifier,
		                   @ExpiresOn::timestamptz,
		                   @GrantedBy,
		                   @GrantedOn::timestamptz
		                   );
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
		                   WHERE user_id = @UserId AND tenant_id = @TenantId AND grant_type = @GrantType AND qualifier = @Qualifier;

		                   DELETE FROM authz.grant WHERE user_id = @UserId AND tenant_id = @TenantId AND grant_type = @GrantType AND qualifier = @Qualifier;
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
		                   WHERE user_id = @UserId
		                   AND tenant_id = @TenantId
		                   AND grant_type = @GrantType
		                   AND qualifier = @Qualifier
		                   AND COALESCE(expires_on, 'infinity') > now() at time zone 'utc'
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

		if (serviceType == typeof(IGrantQueryStore))
		{
			return this;
		}

		if (serviceType == typeof(IActivityGroupGrantStore))
		{
			return this;
		}

		return null;
	}

	// IGrantQueryStore

	/// <inheritdoc />
	public async Task<IReadOnlyList<Grant>> GetMatchingGrantsAsync(string? userId, string tenantId,
		string grantType, string qualifier, CancellationToken cancellationToken)
	{
		const string sql = """
		                        SELECT *
		                        FROM authz.grant
		                        WHERE user_id LIKE COALESCE(@UserId, '%')
		                        AND tenant_id LIKE @TenantId
		                        AND grant_type LIKE @GrantType
		                        AND qualifier LIKE @Qualifier;
		                   """;

		var grants = await _connection.QueryAsync<GrantRow>(
			new CommandDefinition(sql,
				new
				{
					UserId = userId ?? "%",
					TenantId = tenantId,
					GrantType = grantType,
					Qualifier = qualifier,
				},
				commandTimeout: DbTimeouts.RegularTimeoutSeconds,
				cancellationToken: cancellationToken)).ConfigureAwait(false);

		return grants.Select(g => new Grant(
			g.UserId, g.FullName, g.TenantId, g.GrantType, g.Qualifier, g.ExpiresOn, g.GrantedBy, g.GrantedOn!.Value))
			.ToList().AsReadOnly();
	}

	/// <inheritdoc />
	public async Task<IReadOnlyDictionary<string, object>> FindUserGrantsAsync(string userId,
		CancellationToken cancellationToken)
	{
		const string sql = """
		                        SELECT tenant_id, grant_type, qualifier, expires_on::timestamptz
		                        from authz.grant
		                        WHERE user_id = @userId
		                   """;

		var grants = await _connection
			.QueryAsync<(string TenantId, string GrantType, string Qualifier, DateTimeOffset? ExpiresOn)>(
				new CommandDefinition(sql,
					new { UserId = userId },
					commandTimeout: DbTimeouts.RegularTimeoutSeconds,
					cancellationToken: cancellationToken)).ConfigureAwait(false);

		return grants.ToDictionary(
			grant => string.Join(":", grant.TenantId, grant.GrantType, grant.Qualifier),
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
		                        WHERE user_id = @UserId
		                        AND grant_type = @GrantType
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
		                         WHERE grant_type = @GrantType
		                   """;

		return await _connection.ExecuteAsync(
			new CommandDefinition(sql,
				new { GrantType = grantType },
				commandTimeout: DbTimeouts.RegularTimeoutSeconds,
				cancellationToken: cancellationToken)).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<int> InsertActivityGroupGrantAsync(string userId, string fullName,
		string? tenantId, string grantType, string qualifier,
		DateTimeOffset? expiresOn, string grantedBy, CancellationToken cancellationToken)
	{
		const string sql = """
		                        INSERT INTO "Authz"."Grant" ("UserId", "FullName", "TenantId", "GrantType", "Qualifier", "ExpiresOn", "GrantedBy", "GrantedOn")
		                        VALUES (@UserId, @FullName, @TenantId, @GrantType, @Qualifier, @ExpiresOn, @GrantedBy, NOW() AT TIME ZONE 'UTC')
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
		                        WHERE grant_type = @GrantType
		                   """;

		var result = await _connection.QueryAsync<string>(
			new CommandDefinition(sql,
				new { GrantType = grantType },
				commandTimeout: DbTimeouts.RegularTimeoutSeconds,
				cancellationToken: cancellationToken)).ConfigureAwait(false);

		return result.ToList().AsReadOnly();
	}

	/// <summary>
	/// Dapper row-mapping DTO for grant data from PostgreSQL.
	/// </summary>
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
