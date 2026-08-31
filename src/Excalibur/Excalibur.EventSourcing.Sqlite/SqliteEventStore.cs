// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Dapper;

using Excalibur.Dispatch;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Sqlite;

/// <summary>
/// SQLite implementation of <see cref="IEventStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// Lightweight event store for local development, testing, and embedded scenarios.
/// Auto-creates tables on first use. Zero Docker dependency.
/// </para>
/// <para>
/// SQLite uses WAL mode for concurrent read access while writing.
/// Concurrency is enforced via UNIQUE(AggregateId, AggregateType, Version, TenantId).
/// </para>
/// <para>
/// Every stream is scoped to a tenant, matching every other <see cref="IEventStore"/> provider in this
/// framework. The tenant participates in stream IDENTITY, not merely in read filters: two tenants may
/// independently own a stream at the same natural aggregate id, and a duplicate append is rejected only
/// within the writer's own tenant.
/// </para>
/// </remarks>
public sealed class SqliteEventStore : IEventStore
{
	private readonly string _connectionString;
	private readonly string _table;
	private readonly ILogger<SqliteEventStore> _logger;
	private readonly JsonSerializerOptions _jsonOptions;

	/// <summary>
	/// Whether the host supplied an event type-info resolver, selecting the reflection-free serialization
	/// path. Decided once at construction because the resolver cannot change for a constructed store.
	/// </summary>
	private readonly bool _hasEventTypeInfoResolver;
	private readonly ITenantContext _tenantContext;

	// The DEPLOYMENT MODE (TenantContextOptions.RequireTenant, set by AddMultiTenancy()) -- NOT "is a
	// context present", which is now always true. Only a single-tenant deployment may have its legacy
	// untenanted rows converged onto the single-tenant identity; doing that in a multi-tenant deployment
	// would move that host's genuinely-untenanted system rows into the default tenant's partition. Mirrors
	// SqliteSnapshotStore's identical field, for the identical reason.
	private readonly bool _requireTenant;

	/// <summary>
	/// Gets the tenant term this store runs under, resolved in one place so every statement it builds binds
	/// the same value. The context is a required dependency, so the term is decided identically on every
	/// path: the store cannot resolve one partition on write and a different one on read.
	/// </summary>
	private TenantScope CurrentTenantScope =>
		TenantScope.FromContext(_tenantContext);

	/// <summary>
	/// Initializes a new instance of the <see cref="SqliteEventStore"/> class.
	/// </summary>
	/// <param name="connectionString">The SQLite connection string (e.g., "Data Source=events.db").</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="tenantContext">
	/// The ambient tenant context. Required: this store partitions streams by tenant, and it resolves that
	/// partition from here, so there is no state in which the partition is undecided. A single-tenant host
	/// receives the framework default context and operates as the one canonical tenant.
	/// </param>
	/// <param name="table">The event store table name. Default: "Events".</param>
	/// <param name="tenantContextOptions">
	/// The tenant-context options; its <see cref="TenantContextOptions.RequireTenant"/> (set by
	/// <c>AddMultiTenancy()</c>) selects the deployment mode for the startup schema handshake, which decides
	/// whether legacy untenanted rows may be converged onto the single-tenant identity.
	/// </param>
	/// <param name="eventTypeInfoResolver">
	/// An optional source-generated JSON type-info resolver covering the application's domain event types
	/// and the runtime types of the values it places in
	/// <see cref="Excalibur.Dispatch.IDomainEvent.Metadata"/>. Supplied, the store serializes without
	/// reflection, which is what a native-AOT host published with reflection-based serialization disabled
	/// requires. Omitted, the store serializes through the reflection-based serializer exactly as before, so
	/// an existing caller is unaffected. The stored wire format is byte-identical either way.
	/// </param>
	public SqliteEventStore(
		string connectionString,
		ILogger<SqliteEventStore> logger,
		ITenantContext tenantContext,
		IOptions<TenantContextOptions> tenantContextOptions,
		string table = "Events",
		System.Text.Json.Serialization.Metadata.IJsonTypeInfoResolver? eventTypeInfoResolver = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		ArgumentNullException.ThrowIfNull(logger);
		ArgumentNullException.ThrowIfNull(tenantContext);

		_connectionString = connectionString;
		_logger = logger;
		_table = table;
		_jsonOptions = Excalibur.Dispatch.EventSerializationDefaults.CreateCanonicalOptions();
		_hasEventTypeInfoResolver = EventSerializationDefaults.TryApplyTypeInfoResolver(_jsonOptions, eventTypeInfoResolver);
		_tenantContext = tenantContext;
		ArgumentNullException.ThrowIfNull(tenantContextOptions);
		_requireTenant = tenantContextOptions.Value.RequireTenant;
	}

	/// <inheritdoc/>
	public async ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);
		return await LoadAsync(aggregateId, aggregateType, -1, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
		string aggregateId,
		string aggregateType,
		long fromVersion,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);
		await using var connection = CreateConnection();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
		await SqliteTableInitializer.EnsureEventsTableAsync(connection, _table, _requireTenant, cancellationToken)
			.ConfigureAwait(false);

		var scope = CurrentTenantScope;
		// UNCONDITIONAL, matching SqliteSnapshotStore's identical reasoning: the write stores every row
		// under a tenant term (the resolved tenant, or the reserved untenanted sentinel), so an UNSCOPED
		// read must filter too -- a conditional predicate would match ANY tenant's stream for this
		// aggregate. Read and write must agree on the key; here that means both are unconditional.
		var sql = $"""
			SELECT EventId, AggregateId, AggregateType, EventType,
			       EventData, Metadata, Version, Timestamp
			FROM [{_table}]
			WHERE AggregateId = @AggregateId AND AggregateType = @AggregateType AND TenantId = @TenantId AND Version > @FromVersion
			ORDER BY Version ASC
			""";

		var rows = await connection.QueryAsync<StoredEventRow>(
			new CommandDefinition(
				sql,
				new
				{
					AggregateId = aggregateId,
					AggregateType = aggregateType,
					TenantId = KeyedTenantPartition.FromScope(scope).TenantId,
					FromVersion = fromVersion,
				},
				cancellationToken: cancellationToken)).ConfigureAwait(false);

		return rows.Select(r => new StoredEvent(
			r.EventId,
			r.AggregateId,
			r.AggregateType,
			r.EventType,
			r.EventData,
			r.Metadata,
			r.Version,
			DateTimeOffset.Parse(r.Timestamp))).ToList();
	}

	/// <inheritdoc/>
	public async ValueTask<AppendResult> AppendAsync(
		string aggregateId,
		string aggregateType,
		IEnumerable<IDomainEvent> events,
		long expectedVersion,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);
		ArgumentNullException.ThrowIfNull(events);
		var eventList = events as IReadOnlyCollection<IDomainEvent> ?? events.ToList();

		if (eventList.Count == 0)
		{
			// No events appended, so there is no first-event position. Report null — the canonical
			// "no position" sentinel shared by InMemory/SqlServer/Postgres — never a real position like 0.
			return AppendResult.CreateSuccess(expectedVersion, firstEventPosition: null);
		}

		var tenantId = KeyedTenantPartition.FromScope(CurrentTenantScope).TenantId;

		await using var connection = CreateConnection();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
		await SqliteTableInitializer.EnsureEventsTableAsync(connection, _table, _requireTenant, cancellationToken)
			.ConfigureAwait(false);

		// Enable WAL mode for better concurrent access
		await connection.ExecuteAsync("PRAGMA journal_mode=WAL;").ConfigureAwait(false);

		await using var transaction = await connection.BeginTransactionAsync(cancellationToken)
			.ConfigureAwait(false);

		try
		{
			// Check current version, scoped to this writer's tenant. UNCONDITIONAL for the same reason as
			// the read path: the version probe and the UNIQUE key it protects must agree on what identifies
			// a stream, or the probe and the constraint disagree exactly the way that produced the
			// non-converging conflict this fix closes (see the DDL's own remarks).
			var currentVersion = await connection.ExecuteScalarAsync<long?>(
				new CommandDefinition(
					$"SELECT MAX(Version) FROM [{_table}] WHERE AggregateId = @AggregateId AND AggregateType = @AggregateType AND TenantId = @TenantId",
					new { AggregateId = aggregateId, AggregateType = aggregateType, TenantId = tenantId },
					transaction,
					cancellationToken: cancellationToken)).ConfigureAwait(false) ?? -1;

			if (currentVersion != expectedVersion)
			{
				return AppendResult.CreateConcurrencyConflict(expectedVersion, currentVersion);
			}

			var version = currentVersion;
			long firstPosition = 0;

			foreach (var @event in eventList)
			{
				version++;
#pragma warning disable IL2026, IL3050 // Serialization inherently uses reflection
				var eventData = SerializeEventPayload(@event, aggregateId, aggregateType);
				var metadata = @event.Metadata != null
					? SerializeMetadata(@event.Metadata)
					: null;
#pragma warning restore IL2026, IL3050

				var sql = $"""
					INSERT INTO [{_table}] (EventId, AggregateId, AggregateType, EventType, EventData, Metadata, Version, Timestamp, TenantId)
					VALUES (@EventId, @AggregateId, @AggregateType, @EventType, @EventData, @Metadata, @Version, @Timestamp, @TenantId);
					SELECT last_insert_rowid();
					""";

				var position = await connection.ExecuteScalarAsync<long>(
					new CommandDefinition(
						sql,
						new
						{
							@event.EventId,
							AggregateId = aggregateId,
							AggregateType = aggregateType,
							EventType = EventTypeNameHelper.GetEventTypeName(@event.GetType()),
							EventData = eventData,
							Metadata = metadata,
							Version = version,
							Timestamp = @event.OccurredAt.ToString("O"),
							TenantId = tenantId,
						},
						transaction,
						cancellationToken: cancellationToken)).ConfigureAwait(false);

				if (firstPosition == 0)
				{
					firstPosition = position;
				}
			}

			await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

			_logger.LogDebug(
				"Appended {Count} events to {AggregateType}/{AggregateId} at version {Version}",
				eventList.Count, aggregateType, aggregateId, version);

			return AppendResult.CreateSuccess(version, firstPosition);
		}
		catch (SqliteException ex) when (ex.SqliteErrorCode == SqliteConstraintErrorCode)
		{
			// Nothing was written -- the transaction never committed, so a connection that is not this one
			// sees none of it. The only question left is whether this append lost its version precondition
			// to another writer, which is a concurrency conflict, or breached a constraint of its own,
			// which is not.
			var currentVersion = await ReadActualVersionOnFreshConnectionAsync(aggregateId, aggregateType, cancellationToken)
				.ConfigureAwait(false);

			if (IsLostRace(ex, currentVersion, expectedVersion))
			{
				return AppendResult.CreateConcurrencyConflict(expectedVersion, currentVersion ?? expectedVersion);
			}

			throw;
		}
	}

	/// <summary>
	/// The primary SQLite result code for a constraint violation, <c>SQLITE_CONSTRAINT</c>.
	/// </summary>
	/// <remarks>
	/// A PRIMARY code, shared by every kind of constraint. Which constraint was breached is carried in the
	/// EXTENDED code, and the two must not be confused: see <see cref="IsStreamUniqueViolation"/>.
	/// </remarks>
	private const int SqliteConstraintErrorCode = 19;

	/// <summary>
	/// The extended SQLite result code for a uniqueness collision, <c>SQLITE_CONSTRAINT_UNIQUE</c>.
	/// </summary>
	private const int SqliteConstraintUniqueErrorCode = 2067;

	/// <summary>
	/// The extended SQLite result code for a primary-key collision, <c>SQLITE_CONSTRAINT_PRIMARYKEY</c>.
	/// </summary>
	private const int SqliteConstraintPrimaryKeyErrorCode = 1555;

	/// <summary>
	/// Determines whether the exception is the stream uniqueness violation used for optimistic concurrency,
	/// rather than any other constraint on the events table.
	/// </summary>
	/// <param name="ex"> The exception to classify. </param>
	/// <returns> <see langword="true"/> when the error is a uniqueness collision. </returns>
	/// <remarks>
	/// <para>
	/// The distinction is between SQLite's primary and extended result codes, and it is load-bearing.
	/// <c>SQLITE_CONSTRAINT</c> is the primary code for EVERY constraint the table declares -- and this one
	/// declares eight NOT NULL columns as well as the UNIQUE stream key. Reading only the primary code
	/// therefore classifies a NOT NULL breach, a CHECK breach or a foreign-key breach as a lost race, and a
	/// caller whose retry policy keys on the conflict flag then reloads and re-attempts a write that cannot
	/// ever succeed, for as many attempts as its policy allows. The extended code names which constraint
	/// was breached, and only a uniqueness collision proves that another writer claimed this version.
	/// </para>
	/// <para>
	/// Both uniqueness codes are accepted because the same logical collision is reported under either
	/// depending on how the uniqueness was declared. The inner-exception chain is walked so that a driver
	/// error arriving wrapped is classified by what it is rather than by what wrapped it.
	/// </para>
	/// </remarks>
	private static bool IsStreamUniqueViolation(Exception? ex)
	{
		for (var current = ex; current is not null; current = current.InnerException)
		{
			if (current is SqliteException sqliteException
				&& sqliteException.SqliteExtendedErrorCode
					is SqliteConstraintUniqueErrorCode or SqliteConstraintPrimaryKeyErrorCode)
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Determines whether a failed append lost an optimistic-concurrency race, rather than failing on its
	/// own account.
	/// </summary>
	/// <param name="ex"> The exception that ended the append. </param>
	/// <param name="currentVersion"> The stream's committed version re-read on a fresh connection, or <see langword="null"/> if it could not be read. </param>
	/// <param name="expectedVersion"> The version the append required the stream to be at. </param>
	/// <returns> <see langword="true"/> when the append is a concurrency conflict; otherwise <see langword="false"/>. </returns>
	/// <remarks>
	/// <para>
	/// A uniqueness collision on the stream key is a proof of conflict from the error alone, and it stands
	/// even when the follow-up read cannot be performed -- so it is the first branch. Without it, a lost
	/// race whose re-read also failed would be demoted to an ordinary failure.
	/// </para>
	/// <para>
	/// Behind it stands the structural test, which asks the question the error code only approximates: the
	/// append committed nothing, so if the stream is no longer at the version it required, the precondition
	/// was lost to another writer whatever constraint surfaced. It cannot over-report -- a stream still
	/// sitting at the expected version proves nothing else claimed it, so the breach is the append's own
	/// and is raised as one rather than dressed as a conflict the caller would pointlessly retry.
	/// </para>
	/// <para>
	/// A re-read that failed reports no value rather than an estimate, because this decision is what the
	/// value feeds: an estimate would read as "the stream moved" and manufacture a conflict out of a
	/// measurement that never happened, and the expected version would read as "it did not" and deny one
	/// on the same non-evidence.
	/// </para>
	/// </remarks>
	private static bool IsLostRace(Exception ex, long? currentVersion, long expectedVersion) =>
		IsStreamUniqueViolation(ex) || (currentVersion is { } version && version != expectedVersion);

	/// <summary>
	/// Reads the actual committed <c>MAX(Version)</c> for an aggregate <b>within this store's current
	/// tenant scope</b> on a FRESH connection, used to populate the concurrency-conflict result after a
	/// UNIQUE-constraint violation during append.
	/// </summary>
	/// <remarks>
	/// A fresh connection is required — NOT the appending connection: that connection holds a pending, failed
	/// transaction, so reusing it without that transaction throws <see cref="InvalidOperationException"/>
	/// ("transaction required"), and reading WITH it would return this writer's own uncommitted state. A separate
	/// connection reads the version the winning writer committed. Scoped to <see cref="CurrentTenantScope"/> so a
	/// conflict re-read never crosses into another tenant's stream — the constraint that produced the conflict is
	/// itself tenant-scoped, and the re-read must agree. Any non-cancellation re-read failure returns
	/// <see langword="null"/> (the caller then supplies a fallback) so a concurrency conflict is never turned into an
	/// escaped exception — the exact version is not load-bearing (classification drives the repository retry);
	/// cancellation propagates.
	/// Extracted as <see langword="internal"/> for direct, deterministic unit testing of the conflict re-read.
	/// </remarks>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>
	/// The committed <c>MAX(Version)</c> for the aggregate within this store's tenant scope (<c>-1</c> when it has
	/// no events), or <see langword="null"/> if the fresh-connection re-read itself failed (a non-cancellation
	/// error) — callers supply a fallback in that case.
	/// </returns>
	internal async ValueTask<long?> ReadActualVersionOnFreshConnectionAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		try
		{
			await using var conflictConnection = CreateConnection();
			await conflictConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

			return await conflictConnection.ExecuteScalarAsync<long?>(
				new CommandDefinition(
					$"SELECT MAX(Version) FROM [{_table}] WHERE AggregateId = @AggregateId AND AggregateType = @AggregateType AND TenantId = @TenantId",
					new
					{
						AggregateId = aggregateId,
						AggregateType = aggregateType,
						TenantId = KeyedTenantPartition.FromScope(CurrentTenantScope).TenantId,
					},
					cancellationToken: cancellationToken)).ConfigureAwait(false) ?? -1;
		}
		catch (Exception reReadEx) when (reReadEx is not OperationCanceledException)
		{
			_logger.LogDebug(reReadEx,
				"Could not re-read current version after constraint violation for {AggregateType}/{AggregateId}",
				aggregateType, aggregateId);
			return null;
		}
	}

	private SqliteConnection CreateConnection() => new(_connectionString);

	private sealed record StoredEventRow(
		string EventId,
		string AggregateId,
		string AggregateType,
		string EventType,
		byte[] EventData,
		byte[]? Metadata,
		long Version,
		string Timestamp);

	/// <summary>
	/// Serializes a domain event, resolving its type metadata from the host's source-generated resolver when
	/// one was supplied and falling back to reflection when none was.
	/// </summary>
	/// <param name="event">The domain event to serialize.</param>
	/// <param name="aggregateId">The stream the append targets, reported if the type is undeclared.</param>
	/// <param name="aggregateType">The aggregate type the append targets, reported if undeclared.</param>
	/// <returns>The UTF-8 encoded event payload.</returns>
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(Object, Type, JsonSerializerOptions)")]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(Object, Type, JsonSerializerOptions)")]
	private byte[] SerializeEventPayload(IDomainEvent @event, string? aggregateId, string? aggregateType) =>
		_hasEventTypeInfoResolver
			? ResolvedEventPayload.Serialize(@event, _jsonOptions, aggregateId, aggregateType)
			: JsonSerializer.SerializeToUtf8Bytes(@event, @event.GetType(), _jsonOptions);

	/// <summary>
	/// Serializes event metadata, dispatching each value through the host's source-generated resolver when
	/// one was supplied and falling back to reflection when none was.
	/// </summary>
	/// <param name="metadata">The event metadata to serialize.</param>
	/// <returns>The UTF-8 encoded metadata object.</returns>
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.SerializeToUtf8Bytes<TValue>(TValue, JsonSerializerOptions)")]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.SerializeToUtf8Bytes<TValue>(TValue, JsonSerializerOptions)")]
	private byte[] SerializeMetadata(IDictionary<string, object> metadata) =>
		_hasEventTypeInfoResolver
			? EventSerializationDefaults.SerializeMetadataWithResolver(metadata, _jsonOptions)
			: JsonSerializer.SerializeToUtf8Bytes(metadata, _jsonOptions);
}
