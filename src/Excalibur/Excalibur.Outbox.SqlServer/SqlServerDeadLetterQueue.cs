// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.Dispatch.ErrorHandling;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Outbox.SqlServer;

/// <summary>
/// SQL Server implementation of <see cref="IDeadLetterQueue"/> for production scenarios.
/// </summary>
/// <remarks>
/// <para>
/// This implementation stores dead letter entries in SQL Server with full support for
/// filtering, replay, and purge operations. Uses Dapper for data access.
/// </para>
/// <para>
/// This class supports two constructor patterns:
/// <list type="bullet">
/// <item><description>Simple: Options-based for most users</description></item>
/// <item><description>Advanced: Connection factory for multi-database, pooling, or IDb integration</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>Tenancy.</strong> The dead-letter queue is an <em>operator</em> surface (it implements
/// <see cref="IDeadLetterQueueAdmin"/>): a platform operator inspects, replays, and purges failed messages
/// across the whole estate, so the inspection and purge operations are deliberately estate-wide and never
/// filtered by the ambient tenant. Each entry still carries its originating tenant as provenance:
/// <see cref="EnqueueAsync{T}"/> stamps the ambient tenant (or the reserved untenanted sentinel when no
/// tenant is in scope) into the <c>TenantId</c> column, and <see cref="ReplayAsync"/> re-enters that
/// <em>stored</em> tenant — never the caller's — so an operator replaying tenant A's dead letter cannot
/// inject it into a different tenant. The tenant term is retained in storage and used for scoping and for
/// restoring the correct tenant on replay; it is <em>not</em> projected onto <see cref="DeadLetterEntry"/>,
/// so a caller cannot read an entry's tenant from the returned value and a tenant-facing view cannot be
/// built from what this surface returns.
/// </para>
/// </remarks>
public sealed class SqlServerDeadLetterQueue : IDeadLetterQueue, IDeadLetterQueueAdmin
{
	private readonly Func<SqlConnection> _connectionFactory;
	private readonly SqlServerDeadLetterQueueOptions _options;
	private readonly ILogger<SqlServerDeadLetterQueue> _logger;
	private readonly Func<object, Task>? _replayHandler;
	private readonly ITenantContext? _tenantContext;
	private readonly JsonSerializerOptions _jsonOptions;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerDeadLetterQueue"/> class.
	/// </summary>
	/// <param name="options">The configuration options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="replayHandler">Optional handler for replaying messages.</param>
	/// <param name="tenantContext">
	/// The ambient tenant context, or <see langword="null"/> when multi-tenancy is not registered. Used to
	/// stamp the originating tenant onto each dead-lettered entry (provenance) and to re-enter that tenant
	/// on replay; inspection and purge remain estate-wide.
	/// </param>
	/// <remarks>
	/// This is the simple constructor for most users.
	/// Use <see cref="SqlServerDeadLetterQueue(Func{SqlConnection}, SqlServerDeadLetterQueueOptions, ILogger{SqlServerDeadLetterQueue}, Func{object, Task}?, ITenantContext?)"/>
	/// for advanced scenarios like multi-database setups or custom connection pooling.
	/// </remarks>
	public SqlServerDeadLetterQueue(
		IOptions<SqlServerDeadLetterQueueOptions> options,
		ILogger<SqlServerDeadLetterQueue> logger,
		Func<object, Task>? replayHandler = null,
		ITenantContext? tenantContext = null)
		: this(
			CreateConnectionFactory((options ?? throw new ArgumentNullException(nameof(options))).Value),
			options.Value,
			logger,
			replayHandler,
			tenantContext)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerDeadLetterQueue"/> class with a connection factory.
	/// </summary>
	/// <param name="connectionFactory">
	/// A factory function that creates <see cref="SqlConnection"/> instances.
	/// The caller is responsible for ensuring the factory returns properly configured connections.
	/// </param>
	/// <param name="options">The configuration options (used for table names, timeouts, etc.).</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="replayHandler">Optional handler for replaying messages.</param>
	/// <param name="tenantContext">
	/// The ambient tenant context, or <see langword="null"/> when multi-tenancy is not registered. Used to
	/// stamp the originating tenant onto each dead-lettered entry (provenance) and to re-enter that tenant
	/// on replay; inspection and purge remain estate-wide.
	/// </param>
	/// <remarks>
	/// <para>
	/// This is the advanced constructor for scenarios that need custom connection management:
	/// </para>
	/// <list type="bullet">
	/// <item><description>Multi-database setups with marker interfaces (e.g., IDomainDb, IDlqDb)</description></item>
	/// <item><description>Custom connection pooling</description></item>
	/// <item><description>Integration with <see cref="IDb"/> abstraction</description></item>
	/// </list>
	/// </remarks>
	public SqlServerDeadLetterQueue(
		Func<SqlConnection> connectionFactory,
		SqlServerDeadLetterQueueOptions options,
		ILogger<SqlServerDeadLetterQueue> logger,
		Func<object, Task>? replayHandler = null,
		ITenantContext? tenantContext = null)
	{
		ArgumentNullException.ThrowIfNull(connectionFactory);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_connectionFactory = connectionFactory;
		_options = options;
		_logger = logger;
		_replayHandler = replayHandler;
		_tenantContext = tenantContext;
		_jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
	}

	/// <inheritdoc />
	public async Task<Guid> EnqueueAsync<T>(
		T message,
		DeadLetterReason reason,
		CancellationToken cancellationToken,
		Exception? exception = null,
		IDictionary<string, string>? metadata = null)
	{
		ArgumentNullException.ThrowIfNull(message);

		var id = Guid.NewGuid();

		// Stamp the originating tenant as provenance so a replay re-enters the SAME tenant. Routing through
		// the keyed partition always yields a concrete, non-null term: the ambient tenant when one is in
		// scope, or the reserved untenanted sentinel when none is — never NULL, so the keyed TenantId column
		// (NOT NULL) always has a value and the untenanted partition never collides with a real tenant.
		var tenantId = KeyedTenantPartition.FromContext(_tenantContext).TenantId;

#pragma warning disable IL2026, IL3050 // Serialization inherently uses reflection
		var payload = JsonSerializer.SerializeToUtf8Bytes(message, _jsonOptions);
		var metadataJson = metadata is { Count: > 0 }
			? JsonSerializer.Serialize(metadata, _jsonOptions)
			: null;
#pragma warning restore IL2026, IL3050

		var sql = $"""
		           INSERT INTO {_options.QualifiedTableName}
		           	(Id, TenantId, MessageType, Payload, Reason, ExceptionMessage, ExceptionStackTrace,
		           	 EnqueuedAt, OriginalAttempts, Metadata, CorrelationId, CausationId,
		           	 SourceQueue, IsReplayed, ReplayedAt)
		           VALUES
		           	(@Id, @TenantId, @MessageType, @Payload, @Reason, @ExceptionMessage, @ExceptionStackTrace,
		           	 @EnqueuedAt, @OriginalAttempts, @Metadata, @CorrelationId, @CausationId,
		           	 @SourceQueue, @IsReplayed, @ReplayedAt)
		           """;

		await using var connection = _connectionFactory();

		var command = new CommandDefinition(
			sql,
			new
			{
				Id = id,
				TenantId = tenantId,
				MessageType = typeof(T).FullName ?? typeof(T).Name,
				Payload = payload,
				Reason = (int)reason,
				ExceptionMessage = exception?.Message,
				ExceptionStackTrace = exception?.StackTrace,
				EnqueuedAt = DateTimeOffset.UtcNow,
				OriginalAttempts = 1,
				Metadata = metadataJson,
				CorrelationId = metadata is not null && metadata.TryGetValue("CorrelationId", out var corrId) ? corrId : null,
				CausationId = metadata is not null && metadata.TryGetValue("CausationId", out var causId) ? causId : null,
				SourceQueue = metadata is not null && metadata.TryGetValue("SourceQueue", out var srcQueue) ? srcQueue : null,
				IsReplayed = false,
				ReplayedAt = (DateTimeOffset?)null
			},
			commandTimeout: _options.CommandTimeoutSeconds,
			cancellationToken: cancellationToken);

		_ = await connection.ExecuteAsync(command).ConfigureAwait(false);

		_logger.LogInformation(
			"Dead lettered message {MessageType} with ID {EntryId} for reason {Reason}",
			typeof(T).FullName, id, reason);

		return id;
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<DeadLetterEntry>> GetEntriesAsync(
		CancellationToken cancellationToken,
		DeadLetterQueryFilter? filter = null,
		int limit = 100)
		=> GetEntriesCoreAsync(filter, limit, AmbientScope(), cancellationToken);

	private async Task<IReadOnlyList<DeadLetterEntry>> GetEntriesCoreAsync(
		DeadLetterQueryFilter? filter,
		int limit,
		KeyedTenantPartition? scope,
		CancellationToken cancellationToken)
	{
		var (whereClause, parameters) = BuildWhereClause(filter, scope);
		var offset = filter?.Skip ?? 0;

		var sql = $"""
		           SELECT Id, MessageType, Payload, Reason, ExceptionMessage, ExceptionStackTrace,
		           	   EnqueuedAt, OriginalAttempts, Metadata, CorrelationId, CausationId,
		           	   SourceQueue, IsReplayed, ReplayedAt
		           FROM {_options.QualifiedTableName}
		           {whereClause}
		           ORDER BY EnqueuedAt DESC
		           OFFSET @Offset ROWS FETCH NEXT @Limit ROWS ONLY
		           """;

		parameters.Add("@Offset", offset);
		parameters.Add("@Limit", limit);

		await using var connection = _connectionFactory();

		var command = new CommandDefinition(
			sql,
			parameters,
			commandTimeout: _options.CommandTimeoutSeconds,
			cancellationToken: cancellationToken);

		var rows = await connection.QueryAsync<DeadLetterRow>(command).ConfigureAwait(false);

		return rows.Select(MapRowToEntry).ToList();
	}

	/// <inheritdoc />
	public async Task<DeadLetterEntry?> GetEntryAsync(Guid entryId, CancellationToken cancellationToken)
	{
		var row = await GetRowAsync(entryId, AmbientScope(), cancellationToken).ConfigureAwait(false);

		return row is null ? null : MapRowToEntry(row);
	}

	/// <inheritdoc />
	public Task<bool> ReplayAsync(Guid entryId, CancellationToken cancellationToken)
		=> ReplayCoreAsync(entryId, AmbientScope(), cancellationToken);

	/// <summary>
	/// Replays a single entry, resolving it within <paramref name="scope"/> when one is supplied.
	/// </summary>
	/// <param name="entryId">The entry to replay.</param>
	/// <param name="scope">
	/// The tenant partition the entry must belong to, or <see langword="null"/> for the estate-wide
	/// operator path. No default: an estate-wide replay is stated, never inherited by omission.
	/// </param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <remarks>
	/// The scope restricts which entry may be ADDRESSED. It does not change which tenant the replay RUNS
	/// under — that is always the tenant stored on the row, resolved below.
	/// </remarks>
	private async Task<bool> ReplayCoreAsync(
		Guid entryId,
		KeyedTenantPartition? scope,
		CancellationToken cancellationToken)
	{
		var row = await GetRowAsync(entryId, scope, cancellationToken).ConfigureAwait(false);
		if (row is null)
		{
			return false;
		}

		var entry = MapRowToEntry(row);

		// The tenant of the row we actually read — not a second, independently-resolved lookup. Every write
		// below is keyed to (Id, TenantId) using this value, so replay affects exactly the row that was read.
		var storedTenant = row.TenantId;

		if (_replayHandler is not null)
		{
			try
			{
#pragma warning disable IL2026, IL3050 // Serialization inherently uses reflection
				var message = JsonSerializer.Deserialize<object>(entry.Payload, _jsonOptions);
#pragma warning restore IL2026, IL3050
				if (message is not null)
				{
					// SECURITY-CRITICAL: replay runs under the tenant STORED on the entry, never the ambient
					// caller's. An operator (no tenant, or a different tenant) replaying tenant A's dead letter
					// must re-enter A's context, or replay becomes a cross-tenant injection vector.
					//
					// A scope is entered UNCONDITIONALLY, including for an untenanted entry. Entering no scope
					// would not replay "with no tenant" — it would INHERIT the caller's, so an operator working
					// inside tenant B's scope would replay an untenanted entry as B's data: a wrong-tenant
					// write by a privileged caller. Passing null clears the ambient tenant for the duration of
					// the handler and restores the caller's on dispose, which is what "no tenant" has to mean.
					var reenterStoredTenant = !string.IsNullOrEmpty(storedTenant)
						&& !string.Equals(storedTenant, KeyedTenantPartition.Untenanted.TenantId, StringComparison.Ordinal);
					using var tenantScope = TenantContextHolder.BeginScope(reenterStoredTenant ? storedTenant : null);

					await _replayHandler(message).ConfigureAwait(false);
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to replay dead letter entry {EntryId}", entryId);
				throw;
			}
		}

		// Mark as replayed
		var sql = $"""
		           UPDATE {_options.QualifiedTableName}
		           SET IsReplayed = 1, ReplayedAt = @ReplayedAt
		           WHERE Id = @Id AND TenantId = @TenantId
		           """;

		await using var connection = _connectionFactory();

		var command = new CommandDefinition(
			sql,
			new { Id = entryId, TenantId = storedTenant, ReplayedAt = DateTimeOffset.UtcNow },
			commandTimeout: _options.CommandTimeoutSeconds,
			cancellationToken: cancellationToken);

		_ = await connection.ExecuteAsync(command).ConfigureAwait(false);

		_logger.LogInformation("Replayed dead letter entry {EntryId}", entryId);
		return true;
	}

	/// <inheritdoc />
	public async Task<ReplayBatchResult> ReplayBatchAsync(
		DeadLetterQueryFilter filter,
		int limit,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(filter);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

		// ESTATE-WIDE, DELIBERATELY. This is the operator surface, and it must NOT compose over the
		// tenant-scoped IDeadLetterQueue members: enumerating or replaying through those would silently
		// restrict the operator to whichever tenant happened to be ambient, so a batch would report a
		// truthful-looking count while skipping every other tenant's entries. Both cores are therefore
		// called with an explicit null scope.
		// Ask for ONE MORE than the caller allowed. Truncation is then OBSERVED — an extra row came back,
		// so entries beyond the limit demonstrably exist — rather than inferred from "we got exactly as many
		// as we asked for", which cannot distinguish a full batch from a drained queue.
		var probed = await GetEntriesCoreAsync(filter, limit + 1, scope: null, cancellationToken)
			.ConfigureAwait(false);

		var truncated = probed.Count > limit;
		var selected = truncated ? probed.Take(limit).ToList() : probed;

		var replayedCount = 0;

		foreach (var entry in selected)
		{
			if (await ReplayCoreAsync(entry.Id, scope: null, cancellationToken).ConfigureAwait(false))
			{
				replayedCount++;
			}
		}

		return new ReplayBatchResult(selected.Count, replayedCount, truncated);
	}

	/// <inheritdoc />
	public async Task<bool> PurgeAsync(Guid entryId, CancellationToken cancellationToken)
	{
		// Purge is keyed by the full primary key for the same reason replay is: (Id, TenantId) permits the
		// same entry Id in two tenants, so a DELETE on Id alone would destroy another tenant's undelivered
		// entry. Resolve the row first and delete exactly it; if no row resolves there is nothing to purge.
		// ESTATE-WIDE, DELIBERATELY: PurgeAsync is an IDeadLetterQueueAdmin member and the operator must be
		// able to address any tenant's entry. Scoping this would narrow the documented admin capability.
		var row = await GetRowAsync(entryId, scope: null, cancellationToken).ConfigureAwait(false);
		if (row is null)
		{
			return false;
		}

		var sql = $"""
		           DELETE FROM {_options.QualifiedTableName}
		           WHERE Id = @Id AND TenantId = @TenantId
		           """;

		await using var connection = _connectionFactory();

		var command = new CommandDefinition(
			sql,
			new { Id = entryId, TenantId = row.TenantId },
			commandTimeout: _options.CommandTimeoutSeconds,
			cancellationToken: cancellationToken);

		var deleted = await connection.ExecuteAsync(command).ConfigureAwait(false);

		if (deleted > 0)
		{
			_logger.LogInformation("Purged dead letter entry {EntryId}", entryId);
			return true;
		}

		return false;
	}

	/// <inheritdoc />
	public async Task<int> PurgeOlderThanAsync(TimeSpan olderThan, CancellationToken cancellationToken)
	{
		var cutoff = DateTimeOffset.UtcNow - olderThan;

		var sql = $"""
		           DELETE FROM {_options.QualifiedTableName}
		           WHERE EnqueuedAt < @Cutoff
		           """;

		await using var connection = _connectionFactory();

		var command = new CommandDefinition(
			sql,
			new { Cutoff = cutoff },
			commandTimeout: _options.CommandTimeoutSeconds,
			cancellationToken: cancellationToken);

		var purgedCount = await connection.ExecuteAsync(command).ConfigureAwait(false);

		if (purgedCount > 0)
		{
			_logger.LogInformation("Purged {Count} dead letter entries older than {Age}", purgedCount, olderThan);
		}

		return purgedCount;
	}

	/// <inheritdoc />
	public async Task<long> GetCountAsync(CancellationToken cancellationToken, DeadLetterQueryFilter? filter = null)
	{
		// A count is a disclosure: an estate-wide count tells one tenant how many failures every other
		// tenant had, so this is scoped exactly as the entry reads are.
		var (whereClause, parameters) = BuildWhereClause(filter, AmbientScope());

		var sql = $"""
		           SELECT COUNT(*)
		           FROM {_options.QualifiedTableName}
		           {whereClause}
		           """;

		await using var connection = _connectionFactory();

		var command = new CommandDefinition(
			sql,
			parameters,
			commandTimeout: _options.CommandTimeoutSeconds,
			cancellationToken: cancellationToken);

		return await connection.ExecuteScalarAsync<long>(command).ConfigureAwait(false);
	}

	private static Func<SqlConnection> CreateConnectionFactory(SqlServerDeadLetterQueueOptions? options)
	{
		ArgumentNullException.ThrowIfNull(options);

		// Apply ApplicationName for connection pool isolation
		var connectionString = options.ConnectionString;
		if (!string.IsNullOrWhiteSpace(options.ApplicationName))
		{
			var builder = new SqlConnectionStringBuilder(connectionString)
			{
				ApplicationName = options.ApplicationName,
			};
			connectionString = builder.ConnectionString;
		}

		return () => new SqlConnection(connectionString);
	}

	#region Private Methods

	/// <summary>
	/// Builds the WHERE clause for a query, restricted to <paramref name="scope"/> when one is supplied.
	/// </summary>
	/// <param name="filter">The optional caller-supplied filter.</param>
	/// <param name="scope">
	/// The tenant partition the query is restricted to, or <see langword="null"/> for a deliberately
	/// estate-wide query. This parameter has no default: an estate-wide read is a decision a call site must
	/// state, so a new caller cannot acquire estate-wide reach by forgetting to mention tenancy.
	/// </param>
	/// <remarks>
	/// The tenant predicate is composed OUTSIDE the filter-branch list and AND-ed onto whatever those
	/// branches produce. A branch added later therefore cannot widen the result set past the tenant
	/// boundary — the boundary is not one of the conditions it joins, it is applied to their conjunction.
	/// This is what makes "a new filter branch without a tenant term" inexpressible rather than merely
	/// discouraged.
	/// </remarks>
	private static (string whereClause, DynamicParameters parameters) BuildWhereClause(
		DeadLetterQueryFilter? filter,
		KeyedTenantPartition? scope)
	{
		var parameters = new DynamicParameters();
		var conditions = new List<string>();

		if (scope is not null)
		{
			conditions.Add("TenantId = @ScopeTenantId");
			parameters.Add("@ScopeTenantId", scope.TenantId);
		}

		if (filter is null)
		{
			return (RenderWhere(conditions), parameters);
		}

		if (!string.IsNullOrWhiteSpace(filter.MessageType))
		{
			conditions.Add("MessageType LIKE @MessageType");
			parameters.Add("@MessageType", $"%{filter.MessageType}%");
		}

		if (filter.Reason.HasValue)
		{
			conditions.Add("Reason = @Reason");
			parameters.Add("@Reason", (int)filter.Reason.Value);
		}

		if (filter.FromDate.HasValue)
		{
			conditions.Add("EnqueuedAt >= @FromDate");
			parameters.Add("@FromDate", filter.FromDate.Value);
		}

		if (filter.ToDate.HasValue)
		{
			conditions.Add("EnqueuedAt <= @ToDate");
			parameters.Add("@ToDate", filter.ToDate.Value);
		}

		if (filter.IsReplayed.HasValue)
		{
			conditions.Add("IsReplayed = @IsReplayed");
			parameters.Add("@IsReplayed", filter.IsReplayed.Value);
		}

		if (!string.IsNullOrWhiteSpace(filter.SourceQueue))
		{
			conditions.Add("SourceQueue = @SourceQueue");
			parameters.Add("@SourceQueue", filter.SourceQueue);
		}

		if (!string.IsNullOrWhiteSpace(filter.CorrelationId))
		{
			conditions.Add("CorrelationId = @CorrelationId");
			parameters.Add("@CorrelationId", filter.CorrelationId);
		}

		if (filter.MinAttempts.HasValue)
		{
			conditions.Add("OriginalAttempts >= @MinAttempts");
			parameters.Add("@MinAttempts", filter.MinAttempts.Value);
		}

		return (RenderWhere(conditions), parameters);
	}

	private static string RenderWhere(List<string> conditions)
		=> conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : string.Empty;

	/// <summary>
	/// The tenant partition of the ambient caller, used to restrict every <see cref="IDeadLetterQueue"/>
	/// operation. Resolved per call, never captured, so the singleton registration observes the tenant in
	/// scope at the moment of the call rather than the one present when the instance was constructed.
	/// </summary>
	private KeyedTenantPartition AmbientScope() => KeyedTenantPartition.FromContext(_tenantContext);

	/// <summary>
	/// Reads the row for <paramref name="entryId"/>, including its owning tenant. Selecting the tenant here
	/// — rather than in a second lookup — is what makes a subsequent write addressable: the primary key is
	/// (Id, TenantId), so a write keyed on Id alone matches every tenant's row that shares the Id. Reading
	/// once also removes a read-vs-read race: two independent Id-only queries could resolve different rows,
	/// so the tenant a caller re-entered was not necessarily the tenant of the row it had just read.
	/// The read is estate-wide only when the caller passes a null scope — the operator paths. A
	/// tenant-scoped caller supplies its partition and an entry outside it resolves as not found.
	/// </summary>
	private async Task<DeadLetterRow?> GetRowAsync(
		Guid entryId,
		KeyedTenantPartition? scope,
		CancellationToken cancellationToken)
	{
		// A scoped resolve adds the tenant term to the PREDICATE rather than filtering after the read: an
		// entry belonging to another tenant must be indistinguishable from one that does not exist. Reading
		// it and then discarding it would answer "exists but forbidden", which is a cross-tenant existence
		// oracle — the id itself is the disclosure.
		var tenantPredicate = scope is null ? string.Empty : " AND TenantId = @ScopeTenantId";

		var sql = $"""
		           SELECT Id, MessageType, Payload, Reason, ExceptionMessage, ExceptionStackTrace,
		           	   EnqueuedAt, OriginalAttempts, Metadata, CorrelationId, CausationId,
		           	   SourceQueue, IsReplayed, ReplayedAt, TenantId
		           FROM {_options.QualifiedTableName}
		           WHERE Id = @Id{tenantPredicate}
		           """;

		await using var connection = _connectionFactory();

		var command = new CommandDefinition(
			sql,
			new { Id = entryId, ScopeTenantId = scope?.TenantId },
			commandTimeout: _options.CommandTimeoutSeconds,
			cancellationToken: cancellationToken);

		try
		{
			return await connection.QuerySingleOrDefaultAsync<DeadLetterRow>(command).ConfigureAwait(false);
		}
		catch (InvalidOperationException ex)
		{
			// More than one row carries this entry id. Entry ids are generated per entry and are globally
			// unique, so two tenants holding one id is an unsupported state rather than a race — refusing is
			// correct, but refusing mutely is not: the bare exception names neither the id nor the reason,
			// leaving an operator to guess at a state we already know how to describe.
			throw new InvalidOperationException(
				$"Dead letter entry id '{entryId}' resolves to more than one row. Entry ids are globally " +
				"unique, so the same id held by two tenants is not a supported state; the entry cannot be " +
				"addressed unambiguously and the operation has been refused rather than applied to an " +
				"arbitrary row.",
				ex);
		}
	}

	private DeadLetterEntry MapRowToEntry(DeadLetterRow row)
	{
		IDictionary<string, string>? metadata = null;
		if (!string.IsNullOrEmpty(row.Metadata))
		{
#pragma warning disable IL2026, IL3050 // Serialization inherently uses reflection
			metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(row.Metadata, _jsonOptions);
#pragma warning restore IL2026, IL3050
		}

		return new DeadLetterEntry
		{
			Id = row.Id,
			MessageType = row.MessageType,
			Payload = row.Payload,
			Reason = (DeadLetterReason)row.Reason,
			ExceptionMessage = row.ExceptionMessage,
			ExceptionStackTrace = row.ExceptionStackTrace,
			EnqueuedAt = row.EnqueuedAt,
			OriginalAttempts = row.OriginalAttempts,
			Metadata = metadata,
			CorrelationId = row.CorrelationId,
			CausationId = row.CausationId,
			SourceQueue = row.SourceQueue,
			IsReplayed = row.IsReplayed,
			ReplayedAt = row.ReplayedAt
		};
	}

	#endregion Private Methods

	#region Row Type

	private sealed class DeadLetterRow
	{
		public Guid Id { get; set; }
		public string MessageType { get; set; } = string.Empty;
		public byte[] Payload { get; set; } = [];
		public int Reason { get; set; }
		public string? ExceptionMessage { get; set; }
		public string? ExceptionStackTrace { get; set; }
		public DateTimeOffset EnqueuedAt { get; set; }
		public int OriginalAttempts { get; set; }
		public string? Metadata { get; set; }
		public string? CorrelationId { get; set; }
		public string? CausationId { get; set; }
		public string? SourceQueue { get; set; }
		public bool IsReplayed { get; set; }
		public DateTimeOffset? ReplayedAt { get; set; }

		/// <summary>
		/// The tenant that owns this row. Never null in the shipped schema (the column is NOT NULL and an
		/// unscoped write stamps the reserved untenanted sentinel), and it is the second component of the
		/// primary key — so it is required to address this row uniquely for any write.
		/// </summary>
		public string TenantId { get; set; } = string.Empty;
	}

	#endregion Row Type
}
