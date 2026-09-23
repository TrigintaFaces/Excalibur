// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Buffers;
using System.Data;
using System.Data.Common;
using System.Text;
using System.Text.Json;
using System.Transactions;

using Dapper;

using Excalibur.Dispatch;

using Excalibur.A3.Authorization;

using Microsoft.Extensions.Options;

namespace Excalibur.Data.SqlServer.Authorization;

/// <summary>
/// SQL Server implementation of <see cref="IActivityGroupStore"/> using inline Dapper queries.
/// </summary>
/// <remarks>
/// <see cref="ReplaceAllActivityGroupsAsync"/> reads the catalogue with <c>OPENJSON</c>, so the database's
/// compatibility level must be 130 or higher.
/// </remarks>
public sealed class SqlServerActivityGroupStore : IActivityGroupStore
{
	/// <summary>The longest activity-group name the shipped schema accepts.</summary>
	internal const int MaxNameLength = ActivityGroupCatalogue.MaxNameLength;

	/// <summary>The longest activity name the shipped schema accepts.</summary>
	internal const int MaxActivityNameLength = ActivityGroupCatalogue.MaxActivityNameLength;

	// How long a replace waits for another replace to finish before it refuses. Shorter than the command
	// timeout, so a lock that is never granted is reported as such rather than as a generic timeout.
	private const int ReplaceLockTimeoutMilliseconds = DbTimeouts.RegularTimeoutSeconds * 1000;

	private readonly IDbConnection _connection;
	private readonly string _table;
	private readonly string _lockResource;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerActivityGroupStore"/> class.
	/// </summary>
	/// <param name="domainDb">The domain database connection provider.</param>
	/// <param name="options">Where the authorization tables live.</param>
	public SqlServerActivityGroupStore(IDomainDb domainDb, IOptions<SqlServerAuthorizationOptions> options)
	{
		ArgumentNullException.ThrowIfNull(domainDb);
		ArgumentNullException.ThrowIfNull(options);

		var schema = options.Value.SchemaName;
		Excalibur.Data.Validation.SqlIdentifierValidator.ThrowIfInvalid(schema, nameof(options.Value.SchemaName));

		_connection = domainDb.Connection;
		_table = $"[{schema}].[ActivityGroup]";

		// Derived from the qualified table, so replaces of the same catalogue are ordered and replaces of
		// different catalogues -- another schema, another application -- never wait on each other.
		_lockResource = $"Excalibur.A3.ActivityGroupCatalogue:{schema}.ActivityGroup";
	}

	/// <inheritdoc />
	public async Task<bool> ActivityGroupExistsAsync(string tenantId, string activityGroupName,
		CancellationToken cancellationToken)
	{
		// T-SQL has no scalar SELECT EXISTS(...) form -- that is PostgreSQL. The equivalent is
		// SELECT CASE WHEN EXISTS (...) THEN 1 ELSE 0 END, which is what every other existence check
		// in the SQL Server providers uses. Reading the 1/0 as an int and comparing, rather than
		// binding straight to bool, matches those siblings and leaves no dependency on a provider
		// coercion for an authorization decision.
		var sql = $"""
		                   SELECT CASE WHEN EXISTS
		                   (
		                     SELECT 1
		                     FROM {_table}
		                     WHERE TenantId=@tenantId AND Name=@activityGroupName
		                   ) THEN 1 ELSE 0 END;
		                   """;

		var exists = await _connection.ExecuteScalarAsync<int>(
			new CommandDefinition(sql,
				new { tenantId, activityGroupName },
				commandTimeout: DbTimeouts.RegularTimeoutSeconds,
				cancellationToken: cancellationToken)).ConfigureAwait(false);

		return exists != 0;
	}

	/// <inheritdoc />
	public async Task<IReadOnlyDictionary<string, IReadOnlyCollection<string>>> FindActivityGroupsAsync(
		string tenantId,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

		var sql = $"""
		                        SELECT Name, TenantId, ActivityName
		                        FROM {_table}
		                        WHERE TenantId=@tenantId
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
		var sql = $"DELETE FROM {_table} WHERE TenantId=@tenantId";

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

		// One batch, one lock, one set-based insert.
		//   sp_getapplock does not raise when it is not granted -- it returns a negative code -- so the code is
		//   checked, and a replace that did not get the lock changes nothing.
		//   TABLOCKX holds the table exclusively until commit, so a reader under READ COMMITTED without row
		//   versioning cannot see the table between the delete and the insert.
		//   DISTINCT leaves duplicate detection to the database's own key comparison.
		// XACT_ABORT is deliberately not set: it is a session setting and would outlive this call on the
		// caller's shared connection. Any error reaches the client as an exception before the commit below, and
		// the uncommitted transaction is rolled back when it is disposed.
		var sql = $"""
		           DECLARE @lock INT;
		           EXEC @lock = sp_getapplock @Resource = @LockResource, @LockMode = 'Exclusive',
		                @LockOwner = 'Transaction', @LockTimeout = @LockTimeoutMilliseconds;
		           IF @lock < 0
		               THROW 51000, 'The activity-group catalogue lock was not granted; nothing was changed.', 1;

		           DECLARE @previous TABLE (TenantId NVARCHAR(64) COLLATE Latin1_General_BIN2 NOT NULL);

		           DELETE FROM {_table} WITH (TABLOCKX)
		           OUTPUT deleted.TenantId INTO @previous;

		           INSERT INTO {_table} (TenantId, Name, ActivityName)
		           SELECT DISTINCT Tenant, Name, ActivityName
		           FROM OPENJSON(@Catalogue)
		           WITH (Tenant NVARCHAR(64) '$.t', Name NVARCHAR(128) '$.n', ActivityName NVARCHAR(256) '$.a');

		           SELECT DISTINCT TenantId FROM @previous;
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
						LockResource = _lockResource,
						LockTimeoutMilliseconds = ReplaceLockTimeoutMilliseconds,
						Catalogue = ToJson(catalogue),
					},
					transaction,
					commandTimeout: DbTimeouts.LongRunningTimeoutSeconds,
					cancellationToken: cancellationToken)).ConfigureAwait(false);

			var tenants = previous.ToArray();

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
		                       TenantId,
		                       Name,
		                       ActivityName
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

	private static string ToJson(ActivityGroupCatalogue catalogue)
	{
		var buffer = new ArrayBufferWriter<byte>();

		using (var writer = new Utf8JsonWriter(buffer))
		{
			writer.WriteStartArray();

			foreach (var entry in catalogue.Entries)
			{
				writer.WriteStartObject();
				writer.WriteString("t", entry.TenantId);
				writer.WriteString("n", entry.Name);
				writer.WriteString("a", entry.ActivityName);
				writer.WriteEndObject();
			}

			writer.WriteEndArray();
		}

		return Encoding.UTF8.GetString(buffer.WrittenSpan);
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
