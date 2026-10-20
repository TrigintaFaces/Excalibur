// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Buffers;
using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Transactions;

using Dapper;

using Excalibur.A3.Authorization;

using Excalibur.Dispatch;

using Microsoft.Data.SqlClient;

namespace Excalibur.Data.SqlServer.Authorization;

/// <summary>
/// SQL Server implementation of <see cref="IGrantStore"/> using inline Dapper queries.
/// </summary>
/// <remarks>
/// Implements both <see cref="IGrantStore"/> and <see cref="IGrantQueryStore"/> (via <see cref="GetService"/>)
/// plus <see cref="IActivityGroupGrantStore"/> for activity-group grant operations and
/// <see cref="IActivityGroupGrantReplacement"/> for replacing a set of them atomically. The replace members
/// read their snapshot with <c>OPENJSON</c>, so the database's compatibility level must be 130 or higher.
/// <see cref="IAtomicGrantRevocationStore"/> supplies all-type revocation, coherent snapshots and durable
/// user revisions. Every writer takes transaction-owned database locks and advances the affected user's
/// revision in the same commit. Apply the shipped grant schema, including its revision and operation tables.
/// Authority operations own their commit and refuse ambient transactions. An interrupted commit can have an
/// unknown outcome; resolve revocation by retrying the identical request with its original operation identifier.
/// Estate-wide replacements briefly exclude all authority readers and writers; ordinary operations lock only
/// their user after taking the shared estate lock. All grant mutations must pass through this store: direct SQL
/// writes that bypass the revision protocol invalidate its cache-fencing guarantee.
/// Authoritative reads refuse read-only database replicas; connect to the writable authority rather than
/// a read-scale endpoint. Cancellation while acquiring the authority lock is surfaced as
/// <see cref="OperationCanceledException"/>; interrupted commits still have an unknown outcome.
/// </remarks>
public sealed class SqlServerGrantStore
	: IGrantStore, IDurableGrantStore, IGrantQueryStore, IActivityGroupGrantStore, IActivityGroupGrantReplacement, IAtomicGrantRevocationStore
{
	/// <summary>The widest user id the shipped schema's key column holds, in characters.</summary>
	internal const int MaxUserIdLength = 128;

	/// <summary>The widest grant type the shipped schema's key column holds, in characters.</summary>
	internal const int MaxGrantTypeLength = 64;

	/// <summary>The widest qualifier the shipped schema's key column holds, in characters.</summary>
	internal const int MaxQualifierLength = 192;

	// The table the replace members address, and the name the replace lock is derived from so that two
	// applications on two schemas never wait on each other.
	//
	// The object name is DELIMITED, and that is not a style choice: GRANT is a T-SQL key word, so an
	// undelimited authz.Grant is a syntax error in every statement position -- SELECT, INSERT, UPDATE and
	// DELETE alike. The schema name is lower-case to match the DDL that creates it (CREATE SCHEMA authz, in
	// Scripts/003_CreateGrantSchema.sql), which a case-sensitive database collation would otherwise not
	// resolve.
	private const string GrantTable = "authz.[Grant]";

	private const string ReplaceLockResource = "Excalibur.A3.ActivityGroupGrants:" + GrantTable;

	// How long a replace waits for another replace to finish before it refuses. Shorter than the command
	// timeout, so a lock that is never granted is reported as such rather than as a generic timeout.
	private const int ReplaceLockTimeoutMilliseconds = DbTimeouts.RegularTimeoutSeconds * 1000;

	private readonly IDbConnection _connection;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerGrantStore"/> class.
	/// </summary>
	/// <param name="domainDb">The domain database connection provider.</param>
	public SqlServerGrantStore(IDomainDb domainDb)
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
		                   	FROM authz.[Grant]
		                   	WHERE UserId = @UserId COLLATE Latin1_General_BIN2
		                   	AND TenantId = @TenantId COLLATE Latin1_General_BIN2
		                   	AND GrantType = @GrantType COLLATE Latin1_General_BIN2
		                   AND Qualifier = @Qualifier COLLATE Latin1_General_BIN2;
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
		// against the DB clock (GETUTCDATE) — same precedent as GrantExistsAsync.
		const string sql = """
		                        SELECT *
		                        FROM authz.[Grant]
		                        WHERE UserId = @UserId COLLATE Latin1_General_BIN2
		                        AND (@IncludeExpired = 1 OR ISNULL(ExpiresOn, '9999-12-31') > GETUTCDATE());
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
		ValidateUserId(grant.UserId);
		ThrowIfLongerThan(grant.TenantId, Excalibur.Dispatch.TenantId.MaxLength, nameof(grant.TenantId));
		ThrowIfLongerThan(grant.GrantType, MaxGrantTypeLength, nameof(grant.GrantType));
		ThrowIfLongerThan(grant.Qualifier, MaxQualifierLength, nameof(grant.Qualifier));

		// An upsert, as IGrantStore.SaveGrantAsync promises: re-granting an existing grant replaces its details
		// instead of failing on the primary key. UPDLOCK + SERIALIZABLE inside one transaction hold the key range
		// between the update and the insert, so two concurrent saves of one grant cannot both miss the update and
		// race to insert.
		const string sql = """
		                   UPDATE authz.[Grant] WITH (UPDLOCK, SERIALIZABLE)
		                   SET FullName = @FullName, ExpiresOn = @ExpiresOn, GrantedBy = @GrantedBy, GrantedOn = @GrantedOn
		                   WHERE UserId = @UserId COLLATE Latin1_General_BIN2
		                   AND TenantId = @TenantId COLLATE Latin1_General_BIN2
		                   AND GrantType = @GrantType COLLATE Latin1_General_BIN2
		                   AND Qualifier = @Qualifier COLLATE Latin1_General_BIN2;

		                   IF @@ROWCOUNT = 0
		                   BEGIN
		                   	INSERT INTO authz.[Grant] (UserId, FullName, TenantId, GrantType, Qualifier, ExpiresOn, GrantedBy, GrantedOn)
		                   	VALUES (@UserId, @FullName, @TenantId, @GrantType, @Qualifier, @ExpiresOn, @GrantedBy, @GrantedOn);
		                   END;

		                   """;

		return await MutateUserAsync(grant.UserId, (connection, transaction) => connection.ExecuteAsync(
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
				transaction: transaction,
				commandTimeout: DbTimeouts.RegularTimeoutSeconds,
				cancellationToken: cancellationToken)), cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<int> DeleteGrantAsync(string userId, string tenantId, string grantType,
		string qualifier, string? revokedBy, DateTimeOffset? revokedOn,
		CancellationToken cancellationToken)
	{
		ValidateUserId(userId);
		const string sql = """
		                   INSERT INTO authz.GrantHistory (
		                   	UserId,
		                   	FullName,
		                   	TenantId,
		                   	GrantType,
		                   	Qualifier,
		                   	ExpiresOn,
		                   	GrantedBy,
		                   	GrantedOn,
		                   	RevokedBy,
		                   	RevokedOn
		                   )
		                   SELECT
		                   	UserId,
		                   	FullName,
		                   	TenantId,
		                   	GrantType,
		                   	Qualifier,
		                   	ExpiresOn,
		                   	GrantedBy,
		                   	GrantedOn,
		                   	@RevokedBy AS RevokedBy,
		                   	@RevokedOn AS RevokedOn
		                   FROM authz.[Grant]
		                   WHERE UserId = @UserId COLLATE Latin1_General_BIN2 AND TenantId = @TenantId COLLATE Latin1_General_BIN2 AND GrantType = @GrantType COLLATE Latin1_General_BIN2 AND Qualifier = @Qualifier COLLATE Latin1_General_BIN2;

		                   DELETE FROM authz.[Grant]
		                   WHERE UserId = @UserId COLLATE Latin1_General_BIN2 AND TenantId = @TenantId COLLATE Latin1_General_BIN2 AND GrantType = @GrantType COLLATE Latin1_General_BIN2 AND Qualifier = @Qualifier COLLATE Latin1_General_BIN2;
		                   """;

		return await MutateUserAsync(userId, (connection, transaction) => connection.ExecuteAsync(
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
				transaction: transaction,
				commandTimeout: DbTimeouts.RegularTimeoutSeconds,
				cancellationToken: cancellationToken)), cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<bool> GrantExistsAsync(string userId, string tenantId, string grantType,
		string qualifier, CancellationToken cancellationToken)
	{
		const string sql = """
		                         SELECT
		                         CASE
		                            WHEN EXISTS (
		                             SELECT 1
		                             FROM authz.[Grant]
		                             WHERE UserId = @UserId COLLATE Latin1_General_BIN2
		                             AND TenantId = @TenantId COLLATE Latin1_General_BIN2
		                             AND GrantType = @GrantType COLLATE Latin1_General_BIN2
		                             AND Qualifier = @Qualifier COLLATE Latin1_General_BIN2
		                             AND ISNULL(ExpiresOn, '9999-12-31') > GETUTCDATE()
		                         )
		                         THEN CAST(1 AS BIT)
		                         ELSE CAST(0 AS BIT)
		                         END;
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

		if (serviceType == typeof(IActivityGroupGrantReplacement))
		{
			return this;
		}

		if (serviceType == typeof(IAtomicGrantRevocationStore))
		{
			return this;
		}

		if (serviceType == typeof(IDurableGrantStore))
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
		                        SELECT TenantId, GrantType, Qualifier, ExpiresOn
		                        FROM authz.[Grant]
		                        WHERE UserId = @UserId COLLATE Latin1_General_BIN2;
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
		ValidateUserId(userId);
		const string sql = """
		                        DELETE FROM authz.[Grant]
		                        WHERE UserId = @UserId COLLATE Latin1_General_BIN2
		                        AND GrantType = @GrantType COLLATE Latin1_General_BIN2
		                   """;

		return await MutateUserAsync(userId, (connection, transaction) => connection.ExecuteAsync(
			new CommandDefinition(sql,
				new { UserId = userId, GrantType = grantType },
				transaction: transaction,
				commandTimeout: DbTimeouts.RegularTimeoutSeconds,
				cancellationToken: cancellationToken)), cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<int> DeleteAllActivityGroupGrantsAsync(string grantType,
		CancellationToken cancellationToken)
	{
		return await InTransactionAsync(async (connection, transaction) =>
		{
			await AcquireLockAsync(connection, transaction, ReplaceLockResource, exclusive: true, cancellationToken)
				.ConfigureAwait(false);
			const string sql = """
			                   DECLARE @removed TABLE (UserId NVARCHAR(128) COLLATE Latin1_General_BIN2);
			                   DELETE FROM authz.[Grant] WITH (TABLOCKX)
			                   OUTPUT deleted.UserId INTO @removed
			                   WHERE GrantType = @GrantType COLLATE Latin1_General_BIN2;
			                   SELECT UserId FROM @removed;
			                   """;
			var removed = (await connection.QueryAsync<string>(new CommandDefinition(sql,
				new { GrantType = grantType }, transaction, commandTimeout: DbTimeouts.RegularTimeoutSeconds,
				cancellationToken: cancellationToken)).ConfigureAwait(false)).ToArray();
			foreach (var user in removed.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
			{
				await AcquireLockAsync(connection, transaction, UserLock(user), exclusive: true, cancellationToken)
					.ConfigureAwait(false);
				_ = await AdvanceRevisionAsync(connection, transaction, user, cancellationToken).ConfigureAwait(false);
			}
			return removed.Length;
		}, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<int> InsertActivityGroupGrantAsync(string userId, string fullName,
		string tenantId, string grantType, string qualifier,
		DateTimeOffset? expiresOn, string grantedBy, CancellationToken cancellationToken)
	{
		ValidateUserId(userId);
		ThrowIfLongerThan(userId, MaxUserIdLength, nameof(userId));
		ThrowIfLongerThan(tenantId, Excalibur.Dispatch.TenantId.MaxLength, nameof(tenantId));
		ThrowIfLongerThan(grantType, MaxGrantTypeLength, nameof(grantType));
		ThrowIfLongerThan(qualifier, MaxQualifierLength, nameof(qualifier));

		const string sql = """
		                        INSERT INTO authz.[Grant] (UserId, FullName, TenantId, GrantType, Qualifier, ExpiresOn, GrantedBy, GrantedOn)
		                        VALUES (@UserId, @FullName, @TenantId, @GrantType, @Qualifier, @ExpiresOn, @GrantedBy, GETUTCDATE())
		                   """;

		return await MutateUserAsync(userId, (connection, transaction) => connection.ExecuteAsync(
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
				transaction: transaction,
				commandTimeout: DbTimeouts.RegularTimeoutSeconds,
				cancellationToken: cancellationToken)), cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<string>> GetDistinctActivityGroupGrantUserIdsAsync(
		string grantType, CancellationToken cancellationToken)
	{
		const string sql = """
		                        SELECT DISTINCT UserId
		                        FROM authz.[Grant]
		                        WHERE GrantType = @GrantType COLLATE Latin1_General_BIN2
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
		if (userId is not null)
		{
			ValidateUserId(userId);
		}
		foreach (var entry in snapshot.Entries)
		{
			ValidateUserId(entry.UserId);
			ThrowIfLongerThan(entry.GrantType, MaxGrantTypeLength, nameof(entry.GrantType));
			ThrowIfLongerThan(entry.Qualifier, MaxQualifierLength, nameof(entry.Qualifier));
		}
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

		// One batch, one lock, one set-based insert.
		//   sp_getapplock does not raise when it is not granted -- it returns a negative code -- so the code is
		//   checked, and a replace that did not get the lock changes nothing.
		//   The lock names the TABLE, not the scope, so an estate-wide replace and a per-user one cannot
		//   interleave: without that, the estate-wide delete would run between a per-user delete and its
		//   inserts and the user's new grants would survive an estate refresh that never saw them.
		//   TABLOCKX, and it is what makes the replace atomic FOR READERS rather than merely for writers. Row
		//   locks are not enough and the arm that proved it is
		//   NeverShowAConcurrentReaderAPartialGrantSet: under READ COMMITTED a reader takes a shared lock per
		//   row and releases it immediately, so a scan can read part of the old set, block on the delete's row
		//   locks, and resume after the commit into the new set -- returning a mixture that never existed. An
		//   exclusive TABLE lock conflicts with the intent-shared lock a reader holds for its WHOLE statement,
		//   so the reader either finishes before the replace starts or waits for it. The cost is that readers
		//   of grants of OTHER types wait too, for the length of one delete and one insert.
		//   The previous users are read from the delete itself, so they are exactly the ones removed.
		// XACT_ABORT is deliberately not set: it is a session setting and would outlive this call on the
		// caller's shared connection. Any error reaches the client as an exception before the commit below, and
		// the uncommitted transaction is rolled back when it is disposed.
		var scope = userId is null ? string.Empty : " AND UserId = @UserId";

		var sql = $"""
		           DECLARE @lock INT;
		           EXEC @lock = sp_getapplock @Resource = @LockResource, @LockMode = 'Exclusive',
		                @LockOwner = 'Transaction', @LockTimeout = @LockTimeoutMilliseconds;
		           IF @lock < 0
		               THROW 51001, 'The activity-group grant lock was not granted; nothing was changed.', 1;

		           DECLARE @previous TABLE (UserId NVARCHAR(MAX) COLLATE Latin1_General_BIN2 NOT NULL);

		           DELETE FROM {GrantTable} WITH (TABLOCKX)
		           OUTPUT deleted.UserId INTO @previous
		           WHERE GrantType = @GrantType{scope};

		           INSERT INTO {GrantTable} (UserId, FullName, TenantId, GrantType, Qualifier, ExpiresOn, GrantedBy, GrantedOn)
		           SELECT UserId, FullName, Tenant, GrantType, Qualifier, ExpiresOn, GrantedBy, GETUTCDATE()
		           FROM OPENJSON(@Snapshot)
		           WITH (UserId NVARCHAR(450) '$.u', FullName NVARCHAR(MAX) '$.f', Tenant NVARCHAR(64) '$.t',
		                 GrantType NVARCHAR(64) '$.g', Qualifier NVARCHAR(MAX) '$.q',
		                 ExpiresOn DATETIMEOFFSET '$.e', GrantedBy NVARCHAR(MAX) '$.b');

		           SELECT DISTINCT UserId FROM @previous;
		           """;

		var openedHere = connection.State != ConnectionState.Open;

		if (openedHere)
		{
			await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
		}

		try
		{
			await using var transaction = await connection
				.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken)
				.ConfigureAwait(false);

			var previous = await connection.QueryAsync<string>(
				new CommandDefinition(sql,
					new
					{
						LockResource = ReplaceLockResource,
						LockTimeoutMilliseconds = ReplaceLockTimeoutMilliseconds,
						GrantType = grantType,
						UserId = userId,
						Snapshot = ToJson(snapshot),
					},
					transaction,
					commandTimeout: DbTimeouts.LongRunningTimeoutSeconds,
					cancellationToken: cancellationToken)).ConfigureAwait(false);

			var users = previous.ToArray();
			// The estate lock excludes every ordinary writer and authoritative reader before this
			// transaction touches rows. Advance both removed and newly granted users before commit.
			foreach (var affected in users.Concat(snapshot.UserIds).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
			{
				await AcquireLockAsync(connection, transaction, UserLock(affected), exclusive: true, cancellationToken)
					.ConfigureAwait(false);
				_ = await AdvanceRevisionAsync(connection, transaction, affected, cancellationToken).ConfigureAwait(false);
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

	/// <inheritdoc />
	public async Task<GrantRevocationResult> RevokeAllAsync(GrantRevocationRequest request,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);
		ValidateUserId(request.UserId);
		ThrowIfLongerThan(request.RevokedBy, 128, nameof(request.RevokedBy));
		return await InTransactionAsync(async (connection, transaction) =>
		{
			await LockUserAsync(connection, transaction, request.UserId, exclusive: true, cancellationToken)
				.ConfigureAwait(false);
			var parameters = new
			{
				request.UserId, request.OperationId, ScopeTenantId = request.Scope.TenantId, request.RevokedBy,
			};
			const string ledgerRead = """
			                          SELECT ScopeTenantId, RevokedBy, Revision, RevokedCount
			                          FROM authz.GrantRevocation
			                          WHERE UserId = @UserId COLLATE Latin1_General_BIN2 AND OperationId = @OperationId;
			                          """;
			var committed = await connection.QuerySingleOrDefaultAsync<RevocationRow>(new CommandDefinition(
				ledgerRead, parameters, transaction, commandTimeout: DbTimeouts.RegularTimeoutSeconds,
				cancellationToken: cancellationToken)).ConfigureAwait(false);
			if (committed is not null)
			{
				if (!string.Equals(committed.ScopeTenantId, request.Scope.TenantId, StringComparison.Ordinal)
					|| !string.Equals(committed.RevokedBy, request.RevokedBy, StringComparison.Ordinal))
				{
					throw new InvalidOperationException("The revocation operation identifier was reused with a different payload.");
				}
				return new GrantRevocationResult(request.OperationId, committed.Revision, committed.RevokedCount);
			}

			// Audit, deletion, revision and retry receipt share this native transaction. No grant type
			// filter: activity-group and expired grants are revoked alongside direct activity grants.
			const string revokeSql = """
			                         DELETE FROM authz.[Grant] WITH (TABLOCKX)
			                         OUTPUT deleted.UserId, deleted.FullName, deleted.TenantId, deleted.GrantType,
			                                deleted.Qualifier, deleted.ExpiresOn, deleted.GrantedBy, deleted.GrantedOn,
			                                @RevokedBy, SYSUTCDATETIME()
			                         INTO authz.GrantHistory
			                              (UserId, FullName, TenantId, GrantType, Qualifier, ExpiresOn,
			                               GrantedBy, GrantedOn, RevokedBy, RevokedOn)
			                         WHERE UserId = @UserId COLLATE Latin1_General_BIN2
			                         AND (@ScopeTenantId IS NULL OR TenantId = @ScopeTenantId COLLATE Latin1_General_BIN2);
			                         SELECT CONVERT(INT, @@ROWCOUNT);
			                         """;
			var count = await connection.QuerySingleAsync<int>(new CommandDefinition(revokeSql, parameters,
				transaction, commandTimeout: DbTimeouts.RegularTimeoutSeconds,
				cancellationToken: cancellationToken)).ConfigureAwait(false);
			var revision = await AdvanceRevisionAsync(connection, transaction, request.UserId, cancellationToken)
				.ConfigureAwait(false);
			const string ledgerWrite = """
			                           INSERT INTO authz.GrantRevocation (UserId, OperationId, ScopeTenantId, RevokedBy, Revision, RevokedCount)
			                           VALUES (@UserId, @OperationId, @ScopeTenantId, @RevokedBy, @Revision, @RevokedCount);
			                           """;
			_ = await connection.ExecuteAsync(new CommandDefinition(ledgerWrite,
				new { request.UserId, request.OperationId, ScopeTenantId = request.Scope.TenantId, request.RevokedBy,
					Revision = revision, RevokedCount = count }, transaction,
				commandTimeout: DbTimeouts.RegularTimeoutSeconds, cancellationToken: cancellationToken)).ConfigureAwait(false);
			return new GrantRevocationResult(request.OperationId, revision, count);
		}, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<long> ReadUserRevisionAsync(string userId, CancellationToken cancellationToken)
	{
		ValidateUserId(userId);
		return await InTransactionAsync(async (connection, transaction) =>
		{
			await LockUserAsync(connection, transaction, userId, exclusive: false, cancellationToken).ConfigureAwait(false);
			return await ReadRevisionAsync(connection, transaction, userId, cancellationToken).ConfigureAwait(false);
		}, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<UserGrantSnapshot> ReadUserSnapshotAsync(string userId, bool includeExpired,
		CancellationToken cancellationToken)
	{
		ValidateUserId(userId);
		return await InTransactionAsync(async (connection, transaction) =>
		{
			await LockUserAsync(connection, transaction, userId, exclusive: false, cancellationToken).ConfigureAwait(false);
			var revision = await ReadRevisionAsync(connection, transaction, userId, cancellationToken).ConfigureAwait(false);
			const string sql = """
			                   SELECT * FROM authz.[Grant]
			                   WHERE UserId = @UserId COLLATE Latin1_General_BIN2
			                   AND (@IncludeExpired = 1 OR ISNULL(ExpiresOn, '9999-12-31') > GETUTCDATE());
			                   """;
			var rows = await connection.QueryAsync<GrantRow>(new CommandDefinition(sql,
				new { UserId = userId, IncludeExpired = includeExpired }, transaction,
				commandTimeout: DbTimeouts.RegularTimeoutSeconds, cancellationToken: cancellationToken)).ConfigureAwait(false);
			return new UserGrantSnapshot(userId, revision, rows.Select(g => new Grant(g.UserId, g.FullName,
				g.TenantId, g.GrantType, g.Qualifier, g.ExpiresOn, g.GrantedBy, g.GrantedOn!.Value)));
		}, cancellationToken).ConfigureAwait(false);
	}

	private async Task<int> MutateUserAsync(string userId,
		Func<DbConnection, DbTransaction, Task<int>> mutation, CancellationToken cancellationToken) =>
		await InTransactionAsync(async (connection, transaction) =>
		{
			await LockUserAsync(connection, transaction, userId, exclusive: true, cancellationToken).ConfigureAwait(false);
			var affected = await mutation(connection, transaction).ConfigureAwait(false);
			_ = await AdvanceRevisionAsync(connection, transaction, userId, cancellationToken).ConfigureAwait(false);
			return affected;
		}, cancellationToken).ConfigureAwait(false);

	private async Task<T> InTransactionAsync<T>(Func<DbConnection, DbTransaction, Task<T>> action,
		CancellationToken cancellationToken)
	{
		if (Transaction.Current is not null)
		{
			throw new InvalidOperationException("Grant authority operations own their commit and cannot run inside an ambient transaction.");
		}
		var connection = _connection as DbConnection
			?? throw new InvalidOperationException("Grant authority operations require a DbConnection.");
		var openedHere = connection.State != ConnectionState.Open;
		if (openedHere)
		{
			await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
		}
		try
		{
			await using var transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted,
				cancellationToken).ConfigureAwait(false);
			var result = await action(connection, transaction).ConfigureAwait(false);
			await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
			return result;
		}
		finally
		{
			if (openedHere)
			{
				await connection.CloseAsync().ConfigureAwait(false);
			}
		}
	}

	private static async Task LockUserAsync(DbConnection connection, DbTransaction transaction, string userId,
		bool exclusive, CancellationToken cancellationToken)
	{
		// Estate first, then user: bulk replacement takes the estate lock exclusively. Never invert
		// this order, or a bulk writer and an ordinary writer can deadlock before either changes rows.
		await AcquireLockAsync(connection, transaction, ReplaceLockResource, exclusive: false, cancellationToken)
			.ConfigureAwait(false);
		await AcquireLockAsync(connection, transaction, UserLock(userId), exclusive, cancellationToken).ConfigureAwait(false);
	}

	private static async Task AcquireLockAsync(DbConnection connection, DbTransaction transaction, string resource,
		bool exclusive, CancellationToken cancellationToken)
	{
		const string sql = """
		                   IF ISNULL(CONVERT(NVARCHAR(128), DATABASEPROPERTYEX(DB_NAME(), 'Updateability')), N'') <> N'READ_WRITE'
		                       THROW 51003, 'Grant authority operations require the writable primary database.', 1;
		                   DECLARE @result INT;
		                   EXEC @result = sp_getapplock @Resource = @Resource, @LockMode = @Mode,
		                        @LockOwner = 'Transaction', @LockTimeout = @Timeout;
		                   SELECT @result;
		                   """;
		int result;
		try
		{
			result = await connection.QuerySingleAsync<int>(new CommandDefinition(sql,
				new { Resource = resource, Mode = exclusive ? "Exclusive" : "Shared", Timeout = ReplaceLockTimeoutMilliseconds },
				transaction, commandTimeout: DbTimeouts.LongRunningTimeoutSeconds, cancellationToken: cancellationToken))
				.ConfigureAwait(false);
		}
		catch (SqlException exception) when (cancellationToken.IsCancellationRequested
			&& exception.Errors.Count > 0
			&& exception.Errors.Cast<SqlError>().All(static error => error.Number == 0 && error.State == 0 && error.Class == 11))
		{
			// SqlClient 7's TDS attention path reports cancellation as SqlError(0, 0, 11),
			// rather than OCE. Normalize only that signature while acquiring locks, before
			// commit. Preserve unrelated SQL errors and do not infer rollback after commit.
			throw new OperationCanceledException("Grant authority lock acquisition was canceled.", exception, cancellationToken);
		}
		if (result < 0)
		{
			throw new InvalidOperationException($"The grant authority lock was not granted (result {result}); nothing was committed.");
		}
	}

	private static string UserLock(string userId) =>
		"Excalibur.A3.GrantUser:" + GrantTable + ":" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(userId)));

	private static Task<long> ReadRevisionAsync(DbConnection connection, DbTransaction transaction, string userId,
		CancellationToken cancellationToken) => connection.QuerySingleAsync<long>(new CommandDefinition(
		"SELECT COALESCE((SELECT Revision FROM authz.GrantRevision WHERE UserId = @UserId COLLATE Latin1_General_BIN2), CONVERT(BIGINT, 0));",
		new { UserId = userId }, transaction, commandTimeout: DbTimeouts.RegularTimeoutSeconds,
		cancellationToken: cancellationToken));

	private static Task<long> AdvanceRevisionAsync(DbConnection connection, DbTransaction transaction, string userId,
		CancellationToken cancellationToken)
	{
		const string sql = """
		                   UPDATE authz.GrantRevision SET Revision = Revision + 1
		                   WHERE UserId = @UserId COLLATE Latin1_General_BIN2;
		                   IF @@ROWCOUNT = 0 INSERT INTO authz.GrantRevision (UserId, Revision) VALUES (@UserId, 1);
		                   SELECT Revision FROM authz.GrantRevision WHERE UserId = @UserId COLLATE Latin1_General_BIN2;
		                   """;
		return connection.QuerySingleAsync<long>(new CommandDefinition(sql, new { UserId = userId }, transaction,
			commandTimeout: DbTimeouts.RegularTimeoutSeconds, cancellationToken: cancellationToken));
	}

	private static void ValidateUserId(string userId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(userId);
		ThrowIfLongerThan(userId, MaxUserIdLength, nameof(userId));
		if (!string.Equals(userId, userId.Trim(), StringComparison.Ordinal))
		{
			throw new ArgumentException("Grant user identifiers must not have leading or trailing whitespace.", nameof(userId));
		}
	}

	private sealed record RevocationRow
	{
		public string? ScopeTenantId { get; init; }
		public required string RevokedBy { get; init; }
		public long Revision { get; init; }
		public int RevokedCount { get; init; }
	}

	private static string ToJson(ActivityGroupGrantSnapshot snapshot)
	{
		var buffer = new ArrayBufferWriter<byte>();

		using (var writer = new Utf8JsonWriter(buffer))
		{
			writer.WriteStartArray();

			foreach (var entry in snapshot.Entries)
			{
				writer.WriteStartObject();
				writer.WriteString("u", entry.UserId);
				writer.WriteString("f", entry.FullName);
				writer.WriteString("t", entry.TenantId);
				writer.WriteString("g", entry.GrantType);
				writer.WriteString("q", entry.Qualifier);

				if (entry.ExpiresOn is { } expiresOn)
				{
					writer.WriteString("e", expiresOn);
				}
				else
				{
					writer.WriteNull("e");
				}

				writer.WriteString("b", entry.GrantedBy);
				writer.WriteEndObject();
			}

			writer.WriteEndArray();
		}

		return Encoding.UTF8.GetString(buffer.WrittenSpan);
	}

	/// <summary>
	/// Dapper row-mapping DTO for grant data from SQL Server.
	/// </summary>
	// The match query: ordinal equality on every supplied filter; a null filter adds no constraint, and a null
	// tenant (the estate-wide read) adds none either. COLLATE is on every term because '=' otherwise inherits the
	// column's collation, and a table a consumer created by hand is commonly case-insensitive -- where 'Admin'
	// would match 'admin'. OPTION (RECOMPILE) lets the optimizer drop the terms whose parameter is null.
	private async Task<IReadOnlyList<Grant>> QueryMatchingAsync(string? tenantId, string? userId,
		string? grantType, string? qualifier, CancellationToken cancellationToken)
	{
		const string sql = """
		                        SELECT *
		                        FROM authz.[Grant]
		                        WHERE (@TenantId IS NULL OR TenantId = @TenantId COLLATE Latin1_General_BIN2)
		                        AND (@UserId IS NULL OR UserId = @UserId COLLATE Latin1_General_BIN2)
		                        AND (@GrantType IS NULL OR GrantType = @GrantType COLLATE Latin1_General_BIN2)
		                        AND (@Qualifier IS NULL OR Qualifier = @Qualifier COLLATE Latin1_General_BIN2)
		                        OPTION (RECOMPILE);
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
