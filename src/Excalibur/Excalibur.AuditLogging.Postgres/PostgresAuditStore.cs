// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;
using System.Text.RegularExpressions;

using Dapper;

using Excalibur.Compliance;
using Excalibur.Dispatch;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;

namespace Excalibur.AuditLogging.Postgres;

/// <summary>
/// Postgres implementation of <see cref="IAuditStore"/> using Dapper and Npgsql.
/// </summary>
/// <remarks>
/// <para>
/// This implementation provides:
/// - Hash-chain integrity for tamper detection
/// - Tenant-scoped reads, resolved from the ambient tenant context rather than from the caller's query
/// - Indexes for the compliance query patterns this store issues, in the provisioning script shipped
///   under <c>scripts/</c> in this package
/// </para>
/// <para>
/// Not provided by this type, so that the absence is explicit rather than inferred: it implements no
/// retention or purge path — it exposes no <c>EnforceRetentionAsync</c> and no purge capability, so a host
/// that needs audit retention on PostgreSQL must delete by its own policy. It also offers no batch or bulk
/// insert; <c>StoreAsync</c> writes one event per call, which the hash chain requires in order to link each
/// event to the tag preceding it.
/// </para>
/// </remarks>
public sealed partial class PostgresAuditStore : IAuditStore, IDurableAuditStore, IDisposable
{
	private readonly PostgresAuditOptions _options;
	private readonly IAuditIntegrityStrategy _integrity;
	private readonly ITenantContext _tenantContext;
	/// <summary>
	/// Gets the tenant term this store runs under, resolved in one place so every statement it builds binds
	/// the same value. The context is a required dependency, so the term is decided identically on every
	/// path: the store cannot resolve one partition on write and a different one on read.
	/// </summary>
	private TenantScope CurrentTenantScope =>
		TenantScope.FromContext(_tenantContext);

	private readonly ILogger<PostgresAuditStore> _logger;
	private readonly SemaphoreSlim _hashChainLock = new(1, 1);
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresAuditStore"/> class.
	/// </summary>
	public PostgresAuditStore(
		IOptions<PostgresAuditOptions> options,
		IAuditIntegrityStrategy integrity,
		ITenantContext tenantContext,
		ILogger<PostgresAuditStore> logger)
	{
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_integrity = integrity ?? throw new ArgumentNullException(nameof(integrity));
		ArgumentNullException.ThrowIfNull(tenantContext);
		_tenantContext = tenantContext;
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		if (string.IsNullOrEmpty(_options.ConnectionString))
		{
			throw new ArgumentException(
				$"'{nameof(PostgresAuditOptions)}.{nameof(PostgresAuditOptions.ConnectionString)}' is required. " +
				$"Configure it via services.Configure<{nameof(PostgresAuditOptions)}>(config.GetSection(\"PostgresAudit\")) " +
				"or set the ConnectionString property directly.",
				nameof(options));
		}

		ValidateSqlIdentifier(_options.SchemaName, nameof(PostgresAuditOptions.SchemaName));
		ValidateSqlIdentifier(_options.TableName, nameof(PostgresAuditOptions.TableName));
	}

	/// <summary>
	/// The tenant term as this table means it, with every spelling of "no tenant" — a NULL column, and the
	/// reserved sentinel — reconciled onto one value.
	/// </summary>
	/// <remarks>
	/// Composed into every statement that filters or groups on tenant, so the predicate a record is written
	/// under and the partitioning verification reads it back under cannot drift onto different notions of
	/// which rows share a chain. Binds <c>@UntenantedSentinel</c>.
	/// </remarks>
	private const string CanonicalTenantSql = "COALESCE(tenant_id, @UntenantedSentinel)";

	/// <summary>
	/// The application term as this table means it, with a null and an empty application name folded onto
	/// one value. Binds <c>@NoApplicationSentinel</c>.
	/// </summary>
	private const string CanonicalApplicationSql = "COALESCE(NULLIF(application_name, ''), @NoApplicationSentinel)";

	/// <summary>
	/// Adds the tenant scope to a query being built. This is the <strong>only</strong> place the tenant
	/// predicate is constructed, so a second read path cannot acquire a subtly different one.
	/// </summary>
	/// <param name="whereClauses">The clause list being built.</param>
	/// <param name="parameters">The parameter set being built.</param>
	/// <remarks>
	/// <c>COALESCE</c> is load-bearing: a bare <c>tenant_id = @TenantId</c> never matches a NULL-tenant row,
	/// so an untenanted caller would read nothing at all rather than its own rows. Every spelling of "no
	/// tenant" must name the same partition, and this is where they are reconciled.
	/// </remarks>
	private void AddTenantScope(List<string> whereClauses, DynamicParameters parameters)
	{
		whereClauses.Add($"{CanonicalTenantSql} = @TenantId");
		parameters.Add("UntenantedSentinel", KeyedTenantPartition.Untenanted.TenantId);
		parameters.Add("TenantId", ResolveTenantTerm());
	}

	/// <summary>
	/// Resolves the tenant term every read is confined to — a <em>scope</em> from ambient context, never a
	/// filter supplied by the caller.
	/// </summary>
	/// <returns>The ambient tenant identifier, or the reserved untenanted sentinel.</returns>
	/// <exception cref="TenantRequiredException">
	/// Multi-tenancy is registered but resolves no tenant — the read fails closed.
	/// </exception>
	private string ResolveTenantTerm()
	{
		var scope = CurrentTenantScope;

		return scope.TenantId;
	}

	/// <summary>
	/// Indicates whether a Postgres error is a unique-constraint violation (SQLSTATE 23505).
	/// </summary>
	/// <remarks>
	/// Used as an exception filter so only this one condition is translated. A broad catch would report
	/// an unrelated failure — a dropped connection, a timeout, a check constraint — as a duplicate, which
	/// is worse than not translating at all: the caller would be told the row exists when it does not.
	/// </remarks>
	private static bool IsUniqueViolation(PostgresException ex)
		=> string.Equals(ex.SqlState, PostgresErrorCodes.UniqueViolation, StringComparison.Ordinal);

	/// <inheritdoc />
	public async Task<AuditEventId> StoreAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(auditEvent);

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		string? previousHash = null;
		string eventHash;

		if (_options.EnableHashChain)
		{
			await _hashChainLock.WaitAsync(cancellationToken).ConfigureAwait(false);
			try
			{
				// The head of the chain partition this record joins, scoped by tenant and application.
				previousHash = await GetChainHeadTagAsync(auditEvent.ApplicationName, cancellationToken).ConfigureAwait(false);
				eventHash = await _integrity.ComputeTagAsync(
					AuditEventCanonicalizer.Canonicalize(auditEvent), previousHash, cancellationToken).ConfigureAwait(false);
			}
			finally
			{
				_ = _hashChainLock.Release();
			}
		}
		else
		{
			eventHash = await _integrity.ComputeTagAsync(
				AuditEventCanonicalizer.Canonicalize(auditEvent), null, cancellationToken).ConfigureAwait(false);
		}

		var parameters = new DynamicParameters();
		parameters.Add("EventId", auditEvent.EventId);
		parameters.Add("EventType", (int)auditEvent.EventType);
		parameters.Add("Action", auditEvent.Action);
		parameters.Add("Outcome", (int)auditEvent.Outcome);
		parameters.Add("Timestamp", auditEvent.Timestamp);
		parameters.Add("ActorId", auditEvent.ActorId);
		parameters.Add("ActorType", auditEvent.ActorType);
		parameters.Add("ResourceId", auditEvent.ResourceId);
		parameters.Add("ResourceType", auditEvent.ResourceType);
		parameters.Add("ResourceClassification",
			auditEvent.ResourceClassification.HasValue ? (int)auditEvent.ResourceClassification.Value : null);
		// tenant_id is NOT NULL with the untenanted sentinel as its default, so the raw nullable cannot be
		// bound here: an event captured with no ambient tenant carries a null TenantId by design, and binding
		// it throws on every untenanted audit write.
		//
		// FromStoredValue rather than a null-coalesce because it is total over every value that cannot name a
		// real tenant — null, empty and whitespace all resolve to the sentinel. A bare `?? sentinel` would
		// satisfy NOT NULL while letting whitespace through and persisting a row that no tenant-scoped
		// predicate can match, which is the fail-open this column was made NOT NULL to prevent.
		parameters.Add("TenantId", KeyedTenantPartition.FromStoredValue(auditEvent.TenantId).TenantId);
		parameters.Add("ApplicationName", auditEvent.ApplicationName);
		parameters.Add("CorrelationId", auditEvent.CorrelationId);
		parameters.Add("SessionId", auditEvent.SessionId);
		parameters.Add("IpAddress", auditEvent.IpAddress);
		parameters.Add("UserAgent", auditEvent.UserAgent);
		parameters.Add("Reason", auditEvent.Reason);
		parameters.Add("Metadata", auditEvent.Metadata is not null
			? JsonSerializer.Serialize(
				auditEvent.Metadata,
				PostgresAuditJsonContext.Default.IReadOnlyDictionaryStringString)
			: null);
		parameters.Add("PreviousEventHash", previousHash);
		parameters.Add("EventHash", eventHash);

		var sql = $@"
			INSERT INTO {_options.FullyQualifiedTableName}
			(event_id, event_type, action, outcome, timestamp, actor_id, actor_type,
			 resource_id, resource_type, resource_classification, tenant_id, application_name,
			 correlation_id, session_id, ip_address, user_agent, reason, metadata,
			 previous_event_hash, event_hash)
			VALUES
			(@EventId, @EventType, @Action, @Outcome, @Timestamp, @ActorId, @ActorType,
			 @ResourceId, @ResourceType, @ResourceClassification, @TenantId, @ApplicationName,
			 @CorrelationId, @SessionId, @IpAddress, @UserAgent, @Reason, @Metadata::jsonb,
			 @PreviousEventHash, @EventHash)
			RETURNING sequence_number";

		long sequenceNumber;
		try
		{
			sequenceNumber = await connection.ExecuteScalarAsync<long>(
					new CommandDefinition(sql, parameters, commandTimeout: _options.CommandTimeoutSeconds,
						cancellationToken: cancellationToken))
				.ConfigureAwait(false);
		}
		catch (PostgresException ex) when (IsUniqueViolation(ex))
		{
			// This method inserts; it does not upsert. A caller that re-stores an existing event id — the
			// shape a retried publisher produces — is making a mistake the shipped conformance contract
			// already names (StoreAsync_DuplicateId_ShouldThrowInvalidOperationException). The raw provider
			// type is the wrong way to tell them: it forces every consumer to reference Npgsql and know its
			// error codes just to catch a condition the abstraction already defines, and it is a driver type
			// the framework otherwise keeps behind IDataRequest. The filter is narrow on purpose — only a
			// unique violation is translated, so a connection failure, a timeout, or a constraint we did not
			// anticipate still surfaces unchanged rather than being reported as a duplicate.
			throw new InvalidOperationException(
				$"An audit event with id '{auditEvent.EventId}' already exists.", ex);
		}

		LogStoredAuditEvent(auditEvent.EventId, sequenceNumber);

		return new AuditEventId
		{
			EventId = auditEvent.EventId,
			SequenceNumber = sequenceNumber,
			EventHash = eventHash,
			RecordedAt = auditEvent.Timestamp
		};
	}

	/// <inheritdoc />
	public async Task<AuditEvent?> GetByIdAsync(string eventId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(eventId);

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		// A lookup by primary key is still a tenant-scoped read. The identifier alone does not authorise the
		// row: without this predicate a caller holding an event id obtained from anywhere — a log line, an
		// export, a correlation trail — reads another tenant's audit record verbatim. Composed from the same
		// AddTenantScope every other read path uses, so the tenant term has exactly one definition per store.
		var whereClauses = new List<string> { "event_id = @EventId" };
		var parameters = new DynamicParameters();
		parameters.Add("@EventId", eventId);
		AddTenantScope(whereClauses, parameters);

		var sql = $@"
			SELECT event_id AS EventId, event_type AS EventType, action AS Action, outcome AS Outcome,
				   timestamp AS Timestamp, actor_id AS ActorId, actor_type AS ActorType,
				   resource_id AS ResourceId, resource_type AS ResourceType,
				   resource_classification AS ResourceClassification, tenant_id AS TenantId,
				   application_name AS ApplicationName, correlation_id AS CorrelationId,
				   session_id AS SessionId, ip_address AS IpAddress, user_agent AS UserAgent,
				   reason AS Reason, metadata AS Metadata,
				   previous_event_hash AS PreviousEventHash, event_hash AS EventHash
			FROM {_options.FullyQualifiedTableName}
			WHERE {string.Join(" AND ", whereClauses)}";

		var row = await connection.QuerySingleOrDefaultAsync<AuditEventRow>(
				new CommandDefinition(sql, parameters, commandTimeout: _options.CommandTimeoutSeconds,
					cancellationToken: cancellationToken))
			.ConfigureAwait(false);

		return row is null ? null : MapToAuditEvent(row);
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<AuditEvent>> QueryAsync(AuditQuery query, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(query);

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var (whereClauses, parameters) = BuildQueryClauses(query);
		var orderBy = query.OrderByDescending
			? "ORDER BY timestamp DESC, sequence_number DESC"
			: "ORDER BY timestamp ASC, sequence_number ASC";

		var sql = $@"
			SELECT event_id AS EventId, event_type AS EventType, action AS Action, outcome AS Outcome,
				   timestamp AS Timestamp, actor_id AS ActorId, actor_type AS ActorType,
				   resource_id AS ResourceId, resource_type AS ResourceType,
				   resource_classification AS ResourceClassification, tenant_id AS TenantId,
				   application_name AS ApplicationName, correlation_id AS CorrelationId,
				   session_id AS SessionId, ip_address AS IpAddress, user_agent AS UserAgent,
				   reason AS Reason, metadata AS Metadata,
				   previous_event_hash AS PreviousEventHash, event_hash AS EventHash
			FROM {_options.FullyQualifiedTableName}
			{(whereClauses.Count > 0 ? "WHERE " + string.Join(" AND ", whereClauses) : "")}
			{orderBy}
			LIMIT @MaxResults OFFSET @Skip";

		parameters.Add("Skip", query.Skip);
		parameters.Add("MaxResults", query.MaxResults);

		var rows = await connection.QueryAsync<AuditEventRow>(
				new CommandDefinition(sql, parameters, commandTimeout: _options.CommandTimeoutSeconds,
					cancellationToken: cancellationToken))
			.ConfigureAwait(false);

		return rows.Select(MapToAuditEvent).ToList();
	}

	/// <inheritdoc />
	public async Task<long> CountAsync(AuditQuery query, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(query);

		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var (whereClauses, parameters) = BuildQueryClauses(query);

		var sql = $@"
			SELECT COUNT(*)
			FROM {_options.FullyQualifiedTableName}
			{(whereClauses.Count > 0 ? "WHERE " + string.Join(" AND ", whereClauses) : "")}";

		return await connection.ExecuteScalarAsync<long>(
				new CommandDefinition(sql, parameters, commandTimeout: _options.CommandTimeoutSeconds,
					cancellationToken: cancellationToken))
			.ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<AuditIntegrityResult> VerifyChainIntegrityAsync(
		DateTimeOffset startDate,
		DateTimeOffset endDate,
		CancellationToken cancellationToken)
	{
		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		// Selected by sequence bounds rather than on the timestamp directly. Records are chained in write
		// order, so a record written between two in-range records but stamped outside them is still a link in
		// the chain; selecting on the timestamp alone would leave a hole indistinguishable from a deletion.
		var sql = $@"
			SELECT {IntegrityColumnsSql}
			FROM {_options.FullyQualifiedTableName}
			WHERE sequence_number >= ({RangeLowerBoundSql})
			  AND sequence_number <= ({RangeUpperBoundSql})
			ORDER BY sequence_number ASC";

		var rows = await connection.QueryAsync<AuditEventRow>(
				new CommandDefinition(sql, new { StartDate = startDate, EndDate = endDate }, commandTimeout: _options.CommandTimeoutSeconds,
					cancellationToken: cancellationToken))
			.ConfigureAwait(false);

		var events = rows.Select(MapToAuditEvent).ToList();
		if (events.Count == 0)
		{
			return AuditIntegrityResult.NoEventsInScope(startDate, endDate);
		}

		var partitions = await BuildVerificationPartitionsAsync(connection, events, startDate, endDate, cancellationToken)
			.ConfigureAwait(false);

		var result = await AuditChainVerifier
			.VerifyAsync(_integrity, partitions, startDate, endDate, _options.EnableHashChain, cancellationToken)
			.ConfigureAwait(false);

		if (result.Outcome == AuditIntegrityOutcome.ViolationsDetected)
		{
			LogIntegrityViolationDetected(result.FirstViolationEventId!, result.ViolationDescription!);
		}
		else
		{
			LogIntegrityVerificationPassed(events.Count, startDate, endDate);
		}

		return result;
	}

	/// <summary>
	/// The lowest sequence number in the verified window. Shared between the record selection and the anchor
	/// lookup so the two cannot drift onto different left edges.
	/// </summary>
	private string RangeLowerBoundSql =>
		$@"SELECT MIN(sequence_number) FROM {_options.FullyQualifiedTableName}
		   WHERE timestamp >= @StartDate AND timestamp <= @EndDate";

	/// <summary>
	/// The highest sequence number in the verified window. Shared between the record selection and the
	/// successor lookup so the two cannot drift onto different right edges.
	/// </summary>
	private string RangeUpperBoundSql =>
		$@"SELECT MAX(sequence_number) FROM {_options.FullyQualifiedTableName}
		   WHERE timestamp >= @StartDate AND timestamp <= @EndDate";

	/// <summary>
	/// The integrity-covered columns, in one place. The in-range records and the successor that pins the
	/// range's right edge must be canonicalized from identical projections; stating the list twice is how the
	/// two would come to differ, and a successor canonicalized from a narrower row fails to verify against an
	/// intact chain.
	/// </summary>
	private const string IntegrityColumnsSql =
		@"event_id AS EventId, event_type AS EventType, action AS Action, outcome AS Outcome,
		  timestamp AS Timestamp, actor_id AS ActorId, actor_type AS ActorType,
		  resource_id AS ResourceId, resource_type AS ResourceType,
		  resource_classification AS ResourceClassification, tenant_id AS TenantId,
		  application_name AS ApplicationName, correlation_id AS CorrelationId,
		  session_id AS SessionId, ip_address AS IpAddress, user_agent AS UserAgent,
		  reason AS Reason, metadata AS Metadata,
		  previous_event_hash AS PreviousEventHash, event_hash AS EventHash";

	/// <summary>
	/// Loads, for every chain partition, the tag of the record immediately preceding the verified window.
	/// </summary>
	/// <remarks>
	/// Without these a range slice is indistinguishable from a chain that begins at genesis, so records
	/// deleted from the front of the range would go unreported. A partition with no earlier record is absent
	/// from the result, which is how the caller learns its first in-range record genuinely is the genesis one.
	/// </remarks>
	private async Task<Dictionary<AuditChainKey, string?>> LoadChainAnchorsAsync(
		NpgsqlConnection connection,
		DateTimeOffset startDate,
		DateTimeOffset endDate,
		CancellationToken cancellationToken)
	{
		var sql = $@"
			SELECT DISTINCT ON ({CanonicalTenantSql}, {CanonicalApplicationSql})
				   tenant_id AS TenantId, application_name AS ApplicationName, event_hash AS EventHash
			FROM {_options.FullyQualifiedTableName}
			WHERE sequence_number < ({RangeLowerBoundSql})
			ORDER BY {CanonicalTenantSql}, {CanonicalApplicationSql}, sequence_number DESC";

		var anchorRows = await connection.QueryAsync<ChainAnchorRow>(
				new CommandDefinition(
					sql,
					new
					{
						StartDate = startDate,
						EndDate = endDate,
						UntenantedSentinel = KeyedTenantPartition.Untenanted.TenantId,
						NoApplicationSentinel = string.Empty
					},
					commandTimeout: _options.CommandTimeoutSeconds,
					cancellationToken: cancellationToken))
			.ConfigureAwait(false);

		var anchors = new Dictionary<AuditChainKey, string?>();
		foreach (var anchor in anchorRows)
		{
			anchors[AuditChainKey.For(anchor.TenantId, anchor.ApplicationName)] = anchor.EventHash;
		}

		return anchors;
	}

	/// <summary>
	/// Loads, for every chain partition, the record immediately following the verified window.
	/// </summary>
	/// <remarks>
	/// This is the right edge, and it is the mirror of the anchor. Delete records from the end of a range and
	/// the survivors still chain perfectly to one another and to the anchor: the loop holds, and nothing in
	/// the examined records mentions the removed suffix, so there is nothing left inside the range to detect.
	/// The record that follows the range is the only one still carrying the tag of what was there, and its
	/// keyed MAC cannot be recomputed without the signing key. A partition whose range runs to the end of its
	/// chain has no successor and is absent from the result — that case is the trail's irreducible blind spot
	/// and needs an attestation kept where the audit writer cannot reach it.
	/// </remarks>
	private async Task<Dictionary<AuditChainKey, AuditEvent>> LoadChainSuccessorsAsync(
		NpgsqlConnection connection,
		DateTimeOffset startDate,
		DateTimeOffset endDate,
		CancellationToken cancellationToken)
	{
		var sql = $@"
			SELECT DISTINCT ON ({CanonicalTenantSql}, {CanonicalApplicationSql})
				   {IntegrityColumnsSql}
			FROM {_options.FullyQualifiedTableName}
			WHERE sequence_number > ({RangeUpperBoundSql})
			ORDER BY {CanonicalTenantSql}, {CanonicalApplicationSql}, sequence_number ASC";

		var successorRows = await connection.QueryAsync<AuditEventRow>(
				new CommandDefinition(
					sql,
					new
					{
						StartDate = startDate,
						EndDate = endDate,
						UntenantedSentinel = KeyedTenantPartition.Untenanted.TenantId,
						NoApplicationSentinel = string.Empty
					},
					commandTimeout: _options.CommandTimeoutSeconds,
					cancellationToken: cancellationToken))
			.ConfigureAwait(false);

		var successors = new Dictionary<AuditChainKey, AuditEvent>();
		foreach (var row in successorRows)
		{
			var successor = MapToAuditEvent(row);
			successors[AuditChainKey.For(successor.TenantId, successor.ApplicationName)] = successor;
		}

		return successors;
	}

	/// <summary>
	/// Builds the partitions <see cref="AuditChainVerifier"/> walks, honoring what the write path actually
	/// did.
	/// </summary>
	/// <remarks>
	/// <para>
	/// With hash chaining enabled, <see cref="StoreAsync"/> carries each record's tag forward as the next
	/// record's prior tag, so linkage (D2) is meaningful and is verified across the whole partition.
	/// </para>
	/// <para>
	/// With hash chaining <b>disabled</b>, <see cref="StoreAsync"/> signs every record independently with a
	/// null prior tag — there is no chain, by the store's own configuration. Asserting linkage anyway (the
	/// enabled path's grouped, tag-carried-forward partitions) would carry each record's tag forward as the
	/// next one's <em>expected</em> prior tag regardless, and report an untouched trail as tampered, because
	/// nothing was ever chained to break. Each record is instead verified as its own single-record
	/// partition, asserting only what the write path established: that record's own content integrity (D1)
	/// against the null prior tag it was actually signed with. Deletion, insertion, and reordering are
	/// undetectable while chaining is disabled — that is the configuration's tradeoff, not a defect in this
	/// verification, and it is why <see cref="PostgresAuditOptions.EnableHashChain"/> exists as an explicit
	/// opt-out rather than being silently assumed.
	/// </para>
	/// </remarks>
	private async Task<List<AuditChainPartition>> BuildVerificationPartitionsAsync(
		NpgsqlConnection connection,
		List<AuditEvent> events,
		DateTimeOffset startDate,
		DateTimeOffset endDate,
		CancellationToken cancellationToken)
	{
		if (!_options.EnableHashChain)
		{
			return events
				.ConvertAll(e => AuditChainPartition.FromList(anchorPriorTag: null, events: [e], successor: null));
		}

		var anchors = await LoadChainAnchorsAsync(connection, startDate, endDate, cancellationToken).ConfigureAwait(false);
		var successors = await LoadChainSuccessorsAsync(connection, startDate, endDate, cancellationToken).ConfigureAwait(false);
		return BuildChainPartitions(events, anchors, successors);
	}

	/// <summary>
	/// Groups the window's records into the chain partitions they were written under, preserving write order
	/// within each.
	/// </summary>
	/// <remarks>
	/// The grouping key must match the one the write path chains over — tenant and application. Verifying
	/// without it compares each record against whichever record happens to sit next to it in the global
	/// sequence, which on an estate holding more than one tenant or application is a record from a different
	/// chain, and reports an intact trail as tampered.
	/// </remarks>
	private static List<AuditChainPartition> BuildChainPartitions(
		List<AuditEvent> orderedEvents,
		Dictionary<AuditChainKey, string?> anchors,
		Dictionary<AuditChainKey, AuditEvent> successors)
	{
		var grouped = new Dictionary<AuditChainKey, List<AuditEvent>>();
		var order = new List<AuditChainKey>();

		foreach (var auditEvent in orderedEvents)
		{
			var key = AuditChainKey.For(auditEvent.TenantId, auditEvent.ApplicationName);
			if (!grouped.TryGetValue(key, out var bucket))
			{
				bucket = [];
				grouped[key] = bucket;
				order.Add(key);
			}

			bucket.Add(auditEvent);
		}

		var partitions = new List<AuditChainPartition>(order.Count);
		foreach (var key in order)
		{
			_ = anchors.TryGetValue(key, out var anchorPriorTag);
			_ = successors.TryGetValue(key, out var successor);
			partitions.Add(AuditChainPartition.FromList(anchorPriorTag, grouped[key], successor));
		}

		return partitions;
	}

	/// <summary>A chain partition's key together with the tag of its last record before the verified window.</summary>
	private sealed class ChainAnchorRow
	{
		public string? TenantId { get; set; }

		public string? ApplicationName { get; set; }

		public string? EventHash { get; set; }
	}

	/// <inheritdoc />
	public async Task<AuditEvent?> GetLastEventAsync(string? tenantId, CancellationToken cancellationToken)
	{
		return await GetLastEventInternalAsync(tenantId, applicationName: null, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public void Dispose()
	{
		if (!_disposed)
		{
			_hashChainLock.Dispose();
			_disposed = true;
		}
	}

	/// <summary>
	/// Reads the tag at the head of the chain partition a record is about to be appended to.
	/// </summary>
	/// <param name="applicationName">The application the record belongs to, or <see langword="null"/>/empty for none.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The head record's tag, or <see langword="null"/> when the partition is empty.</returns>
	/// <remarks>
	/// Distinct from the last-event query, which deliberately spans applications. The application term is
	/// applied unconditionally here, with every spelling of "no application" folded onto one value, so that
	/// each record joins exactly one chain. Omitting the term for a record carrying no application name
	/// would chain it to whichever record was written last across all applications, interleaving two chains
	/// that verification must later separate — and no grouping on read can recover a partition the write
	/// never kept apart.
	/// </remarks>
	private async Task<string?> GetChainHeadTagAsync(string? applicationName, CancellationToken cancellationToken)
	{
		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var whereClauses = new List<string>();
		var parameters = new DynamicParameters();

		AddTenantScope(whereClauses, parameters);

		whereClauses.Add($"{CanonicalApplicationSql} = COALESCE(NULLIF(@ApplicationName, ''), @NoApplicationSentinel)");
		parameters.Add("ApplicationName", applicationName);
		parameters.Add("NoApplicationSentinel", string.Empty);

		var sql = $@"
			SELECT event_hash
			FROM {_options.FullyQualifiedTableName}
			WHERE {string.Join(" AND ", whereClauses)}
			ORDER BY sequence_number DESC
			LIMIT 1";

		return await connection.ExecuteScalarAsync<string?>(
				new CommandDefinition(sql, parameters, commandTimeout: _options.CommandTimeoutSeconds,
					cancellationToken: cancellationToken))
			.ConfigureAwait(false);
	}

	private async Task<AuditEvent?> GetLastEventInternalAsync(
		string? tenantId,
		string? applicationName,
		CancellationToken cancellationToken)
	{
		await using var connection = new NpgsqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var whereClauses = new List<string>();
		var parameters = new DynamicParameters();

		// SECURITY: scope, not filter — bound unconditionally from ambient context. The caller-supplied
		// tenantId is deliberately not consulted: passing null widened this read to every tenant.
		AddTenantScope(whereClauses, parameters);

		if (!string.IsNullOrEmpty(applicationName))
		{
			whereClauses.Add("application_name = @ApplicationName");
			parameters.Add("ApplicationName", applicationName);
		}

		var whereClause = whereClauses.Count > 0
			? "WHERE " + string.Join(" AND ", whereClauses)
			: "";

		var sql = $@"
			SELECT event_id AS EventId, event_type AS EventType, action AS Action, outcome AS Outcome,
				   timestamp AS Timestamp, actor_id AS ActorId, actor_type AS ActorType,
				   resource_id AS ResourceId, resource_type AS ResourceType,
				   resource_classification AS ResourceClassification, tenant_id AS TenantId,
				   application_name AS ApplicationName, correlation_id AS CorrelationId,
				   session_id AS SessionId, ip_address AS IpAddress, user_agent AS UserAgent,
				   reason AS Reason, metadata AS Metadata,
				   previous_event_hash AS PreviousEventHash, event_hash AS EventHash
			FROM {_options.FullyQualifiedTableName}
			{whereClause}
			ORDER BY sequence_number DESC
			LIMIT 1";

		var row = await connection.QuerySingleOrDefaultAsync<AuditEventRow>(
				new CommandDefinition(sql, parameters, commandTimeout: _options.CommandTimeoutSeconds,
					cancellationToken: cancellationToken))
			.ConfigureAwait(false);

		return row is null ? null : MapToAuditEvent(row);
	}

	private (List<string> WhereClauses, DynamicParameters Parameters) BuildQueryClauses(AuditQuery query)
	{
		var whereClauses = new List<string>();
		var parameters = new DynamicParameters();

		if (query.StartDate.HasValue)
		{
			whereClauses.Add("timestamp >= @StartDate");
			parameters.Add("StartDate", query.StartDate.Value);
		}

		if (query.EndDate.HasValue)
		{
			whereClauses.Add("timestamp <= @EndDate");
			parameters.Add("EndDate", query.EndDate.Value);
		}

		if (query.EventTypes is { Count: > 0 })
		{
			whereClauses.Add("event_type = ANY(@EventTypes)");
			parameters.Add("EventTypes", query.EventTypes.Select(e => (int)e).ToArray());
		}

		if (query.Outcomes is { Count: > 0 })
		{
			whereClauses.Add("outcome = ANY(@Outcomes)");
			parameters.Add("Outcomes", query.Outcomes.Select(o => (int)o).ToArray());
		}

		if (!string.IsNullOrEmpty(query.ActorId))
		{
			whereClauses.Add("actor_id = @ActorId");
			parameters.Add("ActorId", query.ActorId);
		}

		if (!string.IsNullOrEmpty(query.ResourceId))
		{
			whereClauses.Add("resource_id = @ResourceId");
			parameters.Add("ResourceId", query.ResourceId);
		}

		if (!string.IsNullOrEmpty(query.ResourceType))
		{
			whereClauses.Add("resource_type = @ResourceType");
			parameters.Add("ResourceType", query.ResourceType);
		}

		if (query.MinimumClassification.HasValue)
		{
			whereClauses.Add("resource_classification >= @MinClassification");
			parameters.Add("MinClassification", (int)query.MinimumClassification.Value);
		}

		// SECURITY: the tenant term is a SCOPE taken from ambient context, added unconditionally.
		// query.TenantId is deliberately not consulted — omitting it returned every tenant's audit
		// events, and naming another tenant returned theirs.
		AddTenantScope(whereClauses, parameters);

		if (!string.IsNullOrEmpty(query.ApplicationName))
		{
			whereClauses.Add("application_name = @ApplicationName");
			parameters.Add("ApplicationName", query.ApplicationName);
		}

		if (!string.IsNullOrEmpty(query.CorrelationId))
		{
			whereClauses.Add("correlation_id = @CorrelationId");
			parameters.Add("CorrelationId", query.CorrelationId);
		}

		if (!string.IsNullOrEmpty(query.Action))
		{
			whereClauses.Add("action = @Action");
			parameters.Add("Action", query.Action);
		}

		if (!string.IsNullOrEmpty(query.IpAddress))
		{
			whereClauses.Add("ip_address = @IpAddress");
			parameters.Add("IpAddress", query.IpAddress);
		}

		return (whereClauses, parameters);
	}


	private static AuditEvent MapToAuditEvent(AuditEventRow row)
	{
		return new AuditEvent
		{
			EventId = row.EventId,
			EventType = (AuditEventType)row.EventType,
			Action = row.Action,
			Outcome = (AuditOutcome)row.Outcome,
			Timestamp = row.Timestamp,
			ActorId = row.ActorId,
			ActorType = row.ActorType,
			ResourceId = row.ResourceId,
			ResourceType = row.ResourceType,
			ResourceClassification = row.ResourceClassification.HasValue
				? (DataClassification)row.ResourceClassification.Value
				: null,
			TenantId = AuditChainKey.SignedTenantId(row.TenantId),
			ApplicationName = row.ApplicationName,
			CorrelationId = row.CorrelationId,
			SessionId = row.SessionId,
			IpAddress = row.IpAddress,
			UserAgent = row.UserAgent,
			Reason = row.Reason,
			Metadata = string.IsNullOrEmpty(row.Metadata)
				? null
				: JsonSerializer.Deserialize(
					row.Metadata,
					PostgresAuditJsonContext.Default.DictionaryStringString),
			PreviousEventHash = row.PreviousEventHash,
			EventHash = row.EventHash
		};
	}

	private static void ValidateSqlIdentifier(string identifier, string parameterName)
	{
		if (!SqlIdentifierRegex().IsMatch(identifier))
		{
			throw new ArgumentException(
				$"SQL identifier '{parameterName}' contains invalid characters. Only alphanumeric characters and underscores are allowed.",
				parameterName);
		}
	}

	[GeneratedRegex(@"^[a-zA-Z0-9_]+$")]
	private static partial Regex SqlIdentifierRegex();

	[LoggerMessage(LogLevel.Debug, "Stored audit event {EventId} with sequence {SequenceNumber}")]
	private partial void LogStoredAuditEvent(string eventId, long sequenceNumber);

	[LoggerMessage(LogLevel.Warning, "Audit integrity violation detected at event {EventId}: {Description}")]
	private partial void LogIntegrityViolationDetected(string eventId, string description);

	[LoggerMessage(LogLevel.Information,
		"Integrity verification passed for {EventCount} events from {StartDate} to {EndDate}")]
	private partial void LogIntegrityVerificationPassed(
		int eventCount,
		DateTimeOffset startDate,
		DateTimeOffset endDate);

	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Performance",
		"CA1812:Avoid uninstantiated internal classes",
		Justification = "Dapper materializes rows via reflection.")]
	private sealed class AuditEventRow
	{
		public string EventId { get; init; } = string.Empty;
		public int EventType { get; init; }
		public string Action { get; init; } = string.Empty;
		public int Outcome { get; init; }
		public DateTimeOffset Timestamp { get; init; }
		public string ActorId { get; init; } = string.Empty;
		public string? ActorType { get; init; }
		public string? ResourceId { get; init; }
		public string? ResourceType { get; init; }
		public int? ResourceClassification { get; init; }
		public string? TenantId { get; init; }
		public string? ApplicationName { get; init; }
		public string? CorrelationId { get; init; }
		public string? SessionId { get; init; }
		public string? IpAddress { get; init; }
		public string? UserAgent { get; init; }
		public string? Reason { get; init; }
		public string? Metadata { get; init; }
		public string? PreviousEventHash { get; init; }
		public string? EventHash { get; init; }
	}
}
