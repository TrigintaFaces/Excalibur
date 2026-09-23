// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Buffers.Binary;
using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Transactions;

using Dapper;

using Excalibur.Dispatch;

using Excalibur.A3.Authorization;

using Microsoft.Extensions.Options;

namespace Excalibur.Data.Postgres.Authorization;

/// <summary>
/// PostgreSQL implementation of <see cref="IActivityGroupStore"/> using inline Dapper queries.
/// </summary>
public sealed class PostgresActivityGroupStore : IActivityGroupStore
{
	/// <summary>The longest activity-group name the shipped schema accepts.</summary>
	internal const int MaxNameLength = ActivityGroupCatalogue.MaxNameLength;

	/// <summary>The longest activity name the shipped schema accepts.</summary>
	internal const int MaxActivityNameLength = ActivityGroupCatalogue.MaxActivityNameLength;

	// The first key of the two-key advisory lock: a namespace, so an unrelated application's advisory lock on
	// the same second key cannot order against this one. ASCII "EXA3".
	private const int AdvisoryLockNamespace = 0x45584133;

	private readonly IDbConnection _connection;
	private readonly string _table;
	private readonly int _advisoryLockKey;

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresActivityGroupStore"/> class.
	/// </summary>
	/// <param name="domainDb">The domain database connection provider.</param>
	/// <param name="options">Where the authorization tables live.</param>
	public PostgresActivityGroupStore(IDomainDb domainDb, IOptions<PostgresAuthorizationOptions> options)
	{
		ArgumentNullException.ThrowIfNull(domainDb);
		ArgumentNullException.ThrowIfNull(options);

		var schema = options.Value.SchemaName;
		Excalibur.Data.Validation.SqlIdentifierValidator.ThrowIfInvalid(schema, nameof(options.Value.SchemaName));

		_connection = domainDb.Connection;

		// Quoted exactly as the shipped script quotes it, so running the script with the schema substituted
		// creates the table this store addresses, case included.
		_table = $"\"{schema}\".\"activity_group\"";

		// Derived from the qualified table with a stable hash -- never string.GetHashCode, which differs per
		// process -- so every instance orders replaces of the same catalogue, and replaces of a different
		// catalogue do not wait on them.
		_advisoryLockKey = BinaryPrimitives.ReadInt32BigEndian(
			SHA256.HashData(Encoding.UTF8.GetBytes($"Excalibur.A3.ActivityGroupCatalogue:{_table}")));
	}

	/// <inheritdoc />
	public async Task<bool> ActivityGroupExistsAsync(string tenantId, string activityGroupName,
		CancellationToken cancellationToken)
	{
		var sql = $"""
		                                          SELECT EXISTS
		                                          (
		                                            SELECT 1
		                                            FROM {_table}
		                                            WHERE tenant_id=@tenantId AND name=@activityGroupName
		                                          );
		                   """;

		return await _connection.ExecuteScalarAsync<bool>(
			new CommandDefinition(sql,
				new { tenantId, activityGroupName },
				commandTimeout: DbTimeouts.RegularTimeoutSeconds,
				cancellationToken: cancellationToken)).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<IReadOnlyDictionary<string, IReadOnlyCollection<string>>> FindActivityGroupsAsync(
		string tenantId,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

		var sql = $"""
		                        SELECT name, tenant_id, activity_name
		                        FROM {_table}
		                        WHERE tenant_id=@tenantId
		                   """;

		var activityGroups = await _connection
			.QueryAsync<(string Name, string TenantId, string ActivityName)>(
				new CommandDefinition(sql,
					new { tenantId },
					commandTimeout: DbTimeouts.RegularTimeoutSeconds,
					cancellationToken: cancellationToken)).ConfigureAwait(false);

		// Composed with the tenant: a group name is unique only WITHIN a tenant, so grouping on the bare
		// name fused several tenants' activities into one group object, which the decision path then read
		// as a single group whose membership satisfied every one of them.
		return activityGroups
			.GroupBy(
				group => SegmentedKey.Compose(group.TenantId, group.Name),
				group => group.ActivityName,
				StringComparer.Ordinal)
			.ToDictionary(
				group => group.Key,
				IReadOnlyCollection<string> (group) => [.. group],
				StringComparer.Ordinal);
	}

	/// <inheritdoc />
	public async Task<int> DeleteActivityGroupsForTenantAsync(string tenantId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

		// The tenant term here is doing real work, unlike a tenant predicate bolted onto a statement already
		// addressed by a primary key: this DELETE is addressed by tenant and by nothing else, so the
		// predicate IS the operation's scope rather than a redundant guard on it.
		var sql = $"DELETE FROM {_table} WHERE tenant_id=@tenantId";

		return await _connection.ExecuteAsync(
			new CommandDefinition(sql,
				new { tenantId },
				commandTimeout: DbTimeouts.RegularTimeoutSeconds,
				cancellationToken: cancellationToken)).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<IReadOnlyCollection<string>> ReplaceAllActivityGroupsAsync(
		ActivityGroupCatalogue catalogue,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(catalogue);

		// An ambient transaction would decide this commit together with work the store cannot see, so the
		// replace could be rolled back after it reported success, or kept after the caller's own work failed.
		if (Transaction.Current is not null)
		{
			throw new InvalidOperationException(
				"The activity-group catalogue replace runs in its own transaction and cannot run inside an "
				+ "ambient transaction. Call it outside the TransactionScope.");
		}

		var connection = _connection as DbConnection
			?? throw new InvalidOperationException(
				$"The activity-group catalogue replace needs a {nameof(DbConnection)}; the configured connection is "
				+ $"{_connection.GetType().FullName}.");

		var entries = catalogue.Entries;
		var openedHere = connection.State != ConnectionState.Open;

		if (openedHere)
		{
			await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
		}

		try
		{
			// READ COMMITTED, stated rather than inherited: a reader sees the committed catalogue as of its own
			// statement, which is the whole old one until this commits and the whole new one after. A stronger
			// level would make a concurrent replace fail with a serialization error instead of waiting.
			await using var transaction = await connection
				.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken)
				.ConfigureAwait(false);

			// Held until this transaction ends. SET LOCAL bounds the wait for it to this transaction too, so the
			// caller's shared connection keeps its own lock_timeout afterwards.
			_ = await connection.ExecuteAsync(
				new CommandDefinition(
					$"SET LOCAL lock_timeout = '{DbTimeouts.RegularTimeoutSeconds}s'; "
					+ "SELECT pg_advisory_xact_lock(@Namespace, @Key);",
					new { Namespace = AdvisoryLockNamespace, Key = _advisoryLockKey },
					transaction,
					commandTimeout: DbTimeouts.LongRunningTimeoutSeconds,
					cancellationToken: cancellationToken)).ConfigureAwait(false);

			// Read under the lock, from the delete itself, so the previous tenants are exactly the ones removed.
			// Never TRUNCATE: it takes an ACCESS EXCLUSIVE lock that blocks every reader until commit.
			var previous = await connection.QueryAsync<string>(
				new CommandDefinition(
					$"DELETE FROM {_table} RETURNING tenant_id",
					transaction: transaction,
					commandTimeout: DbTimeouts.LongRunningTimeoutSeconds,
					cancellationToken: cancellationToken)).ConfigureAwait(false);

			var tenants = previous.Distinct(StringComparer.Ordinal).ToArray();

			// One set-based statement for the whole catalogue; DISTINCT leaves duplicate detection to the
			// database's own key comparison.
			_ = await connection.ExecuteAsync(
				new CommandDefinition(
					$"""
					 INSERT INTO {_table} (tenant_id, name, activity_name)
					 SELECT DISTINCT t, n, a FROM unnest(@Tenants::text[], @Names::text[], @Activities::text[]) AS c(t, n, a)
					 """,
					new
					{
						Tenants = entries.Select(static e => e.TenantId).ToArray(),
						Names = entries.Select(static e => e.Name).ToArray(),
						Activities = entries.Select(static e => e.ActivityName).ToArray(),
					},
					transaction,
					commandTimeout: DbTimeouts.LongRunningTimeoutSeconds,
					cancellationToken: cancellationToken)).ConfigureAwait(false);

			await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

			return tenants;
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
	public async Task<int> CreateActivityGroupAsync(string tenantId, string name,
		string activityName, CancellationToken cancellationToken)
	{
		// The shipped schema sizes the key to fit SQL Server's 900-byte limit on a clustered key, and
		// both providers declare the same widths. Refuse an over-length term here, with the limit named,
		// rather than let the database reject the row with an opaque key-size error on one provider
		// while the other accepts it.
		ThrowIfLongerThan(tenantId, Excalibur.Dispatch.TenantId.MaxLength, nameof(tenantId));
		ThrowIfLongerThan(name, MaxNameLength, nameof(name));
		ThrowIfLongerThan(activityName, MaxActivityNameLength, nameof(activityName));

		var sql = $"""
		                      INSERT INTO {_table} (
		                       tenant_id,
		                       name,
		                       activity_name
		                      ) VALUES (
		                       @TenantId,
		                       @ActivityGroupName,
		                       @ActivityName
		                      );
		                   """;

		return await _connection.ExecuteAsync(
			new CommandDefinition(sql,
				new { TenantId = tenantId, ActivityGroupName = name, ActivityName = activityName },
				commandTimeout: DbTimeouts.RegularTimeoutSeconds,
				cancellationToken: cancellationToken)).ConfigureAwait(false);
	}

	private static void ThrowIfLongerThan(string value, int maxLength, string paramName)
	{
		ArgumentNullException.ThrowIfNull(value, paramName);

		if (value.Length > maxLength)
		{
			throw new ArgumentException(
				$"The value is {value.Length} characters; the activity-group schema accepts at most {maxLength}.",
				paramName);
		}
	}
}
