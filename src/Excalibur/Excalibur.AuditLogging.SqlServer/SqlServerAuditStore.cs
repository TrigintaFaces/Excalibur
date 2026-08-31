// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;
using System.Text.RegularExpressions;

using Dapper;

using Excalibur.Compliance;
using Excalibur.Dispatch;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.AuditLogging.SqlServer;

/// <summary>
/// SQL Server implementation of <see cref="IAuditStore"/> using Dapper.
/// </summary>
/// <remarks>
/// <para>
/// This implementation provides:
/// - Hash-chain integrity for tamper detection
/// - Retention policy enforcement
/// - Batch insert support for high-throughput scenarios
/// - Optimized indexes for compliance queries
/// </para>
/// </remarks>
internal sealed partial class SqlServerAuditStore : IAuditStore, IDurableAuditStore, IAuditPurgeCapability, IDisposable
{
	private readonly SqlServerAuditOptions _options;
	private readonly IAuditIntegrityStrategy _integrity;

	// Retention must delete an event's annotations in the same transaction as the event itself (see
	// PurgeExpiredAsync). The annotation table's identity is owned by SqlServerAuditAnnotationStoreOptions
	// and is CONSUMED here rather than re-declared: two independently-configured names for one table would let
	// a consumer who renames it fix one site and silently leave the other pointing at nothing.
	private readonly string _annotationsTableName;

	private readonly ITenantContext _tenantContext;
	/// <summary>
	/// Gets the tenant term this store runs under, resolved in one place so every statement it builds binds
	/// the same value. The context is a required dependency, so the term is decided identically on every
	/// path: the store cannot resolve one partition on write and a different one on read.
	/// </summary>
	private TenantScope CurrentTenantScope =>
		TenantScope.FromContext(_tenantContext);

	private readonly ILogger<SqlServerAuditStore> _logger;
	private readonly SemaphoreSlim _hashChainLock = new(1, 1);
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerAuditStore"/> class.
	/// </summary>
	public SqlServerAuditStore(
		IOptions<SqlServerAuditOptions> options,
		IOptions<SqlServerAuditAnnotationStoreOptions> annotationOptions,
		IAuditIntegrityStrategy integrity,
		ITenantContext tenantContext,
		ILogger<SqlServerAuditStore> logger)
	{
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		ArgumentNullException.ThrowIfNull(annotationOptions);
		_integrity = integrity ?? throw new ArgumentNullException(nameof(integrity));
		ArgumentNullException.ThrowIfNull(tenantContext);
		_tenantContext = tenantContext;
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		if (string.IsNullOrEmpty(_options.ConnectionString))
		{
			throw new ArgumentException(Resources.SqlServerAuditStore_ConnectionStringRequired, nameof(options));
		}

		ValidateSqlIdentifier(_options.SchemaName, nameof(SqlServerAuditOptions.SchemaName));
		ValidateSqlIdentifier(_options.TableName, nameof(SqlServerAuditOptions.TableName));

		// Validated on the same path as this store's own identifiers: the annotation table name is
		// interpolated into the retention statement, so it is subject to the identical injection guard.
		var annotations = annotationOptions.Value;
		ValidateSqlIdentifier(annotations.SchemaName, nameof(SqlServerAuditAnnotationStoreOptions.SchemaName));
		ValidateSqlIdentifier(annotations.TableName, nameof(SqlServerAuditAnnotationStoreOptions.TableName));
		_annotationsTableName = annotations.FullyQualifiedTableName;
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
	private const string CanonicalTenantSql = "COALESCE(TenantId, @UntenantedSentinel)";

	/// <summary>
	/// The application term as this table means it, with a null and an empty application name folded onto
	/// one value. Binds <c>@NoApplicationSentinel</c>.
	/// </summary>
	private const string CanonicalApplicationSql = "COALESCE(NULLIF([ApplicationName], ''), @NoApplicationSentinel)";

	/// <summary>
	/// Adds the tenant scope to a query being built. This is the <strong>only</strong> place the tenant
	/// predicate is constructed: the NULL-safe form and the ambient resolution are stated once, so a second
	/// read path cannot acquire a subtly different one.
	/// </summary>
	/// <param name="whereClauses">The clause list being built.</param>
	/// <param name="parameters">The parameter set being built.</param>
	/// <remarks>
	/// <c>COALESCE</c> is load-bearing, not defensive: a bare <c>TenantId = @TenantId</c> never matches a row
	/// whose tenant column is NULL, so an untenanted caller would read nothing at all rather than reading its
	/// own rows. Every spelling of "no tenant" — a NULL column, and the reserved sentinel the scope binds —
	/// must name the same partition, and this is where those two are reconciled.
	/// </remarks>
	private void AddTenantScope(List<string> whereClauses, DynamicParameters parameters)
	{
		whereClauses.Add($"{CanonicalTenantSql} = @TenantId");
		parameters.Add("@UntenantedSentinel", KeyedTenantPartition.Untenanted.TenantId);
		parameters.Add("@TenantId", ResolveTenantTerm());
	}

	/// <summary>
	/// Resolves the tenant term every read is confined to. Tenancy here is a <em>scope</em> taken from
	/// ambient context, never a filter supplied by the caller: a caller cannot widen the read by omitting a
	/// tenant, nor redirect it by naming another one.
	/// </summary>
	/// <returns>The ambient tenant identifier, or the reserved untenanted sentinel.</returns>
	/// <exception cref="TenantRequiredException">
	/// Multi-tenancy is registered but resolves no tenant — the read fails closed rather than widening to
	/// every tenant's audit events.
	/// </exception>
	private string ResolveTenantTerm()
	{
		var scope = CurrentTenantScope;

		return scope.TenantId;
	}

	/// <inheritdoc />
	public async Task<AuditEventId> StoreAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(auditEvent);

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		string? previousHash = null;
		string eventHash;

		// When hash chain is enabled, the lock must span from hash computation through INSERT
		// to prevent concurrent threads from computing hashes against the same previous hash,
		// which would break the tamper-detection chain.
		if (_options.EnableHashChain)
		{
			await _hashChainLock.WaitAsync(cancellationToken).ConfigureAwait(false);
			try
			{
				// The head of the chain partition this record joins, scoped by tenant and application.
				previousHash = await GetChainHeadTagAsync(auditEvent.ApplicationName, cancellationToken)
					.ConfigureAwait(false);

				// Compute hash for this event
				eventHash = await ComputeEventTagAsync(auditEvent, previousHash, cancellationToken).ConfigureAwait(false);

				// INSERT must happen inside the lock to preserve hash chain integrity
				var sequenceNumber = await InsertAuditEventAsync(
					connection, auditEvent, previousHash, eventHash, cancellationToken).ConfigureAwait(false);

				LogStoredAuditEvent(auditEvent.EventId, sequenceNumber);

				return new AuditEventId
				{
					EventId = auditEvent.EventId,
					SequenceNumber = sequenceNumber,
					EventHash = eventHash,
					RecordedAt = auditEvent.Timestamp
				};
			}
			finally
			{
				_ = _hashChainLock.Release();
			}
		}

		eventHash = await ComputeEventTagAsync(auditEvent, null, cancellationToken).ConfigureAwait(false);

		{
			var sequenceNumber = await InsertAuditEventAsync(
				connection, auditEvent, previousHash, eventHash, cancellationToken).ConfigureAwait(false);

			LogStoredAuditEvent(auditEvent.EventId, sequenceNumber);

			return new AuditEventId
			{
				EventId = auditEvent.EventId,
				SequenceNumber = sequenceNumber,
				EventHash = eventHash,
				RecordedAt = auditEvent.Timestamp
			};
		}
	}

	/// <inheritdoc />
	public async Task<AuditEvent?> GetByIdAsync(string eventId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(eventId);

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		// A lookup by primary key is still a tenant-scoped read. The identifier alone does not authorise the
		// row: without this predicate a caller holding an event id obtained from anywhere — a log line, an
		// export, a correlation trail — reads another tenant's audit record verbatim. Composed from the same
		// AddTenantScope every other read path uses, so the tenant term has exactly one definition per store.
		var whereClauses = new List<string> { "EventId = @EventId" };
		var parameters = new DynamicParameters();
		parameters.Add("@EventId", eventId);
		AddTenantScope(whereClauses, parameters);

		var sql = $@"
			SELECT EventId, EventType, [Action], Outcome, [Timestamp], ActorId, ActorType,
				   ResourceId, ResourceType, ResourceClassification, TenantId, [ApplicationName], CorrelationId,
				   SessionId, IpAddress, UserAgent, Reason, Metadata, PreviousEventHash, EventHash
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

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var (whereClauses, parameters) = BuildQueryClauses(query);
		var orderBy = query.OrderByDescending
			? "ORDER BY [Timestamp] DESC, SequenceNumber DESC"
			: "ORDER BY [Timestamp] ASC, SequenceNumber ASC";

		var sql = $@"
			SELECT EventId, EventType, [Action], Outcome, [Timestamp], ActorId, ActorType,
				   ResourceId, ResourceType, ResourceClassification, TenantId, [ApplicationName], CorrelationId,
				   SessionId, IpAddress, UserAgent, Reason, Metadata, PreviousEventHash, EventHash
			FROM {_options.FullyQualifiedTableName}
			{(whereClauses.Count > 0 ? "WHERE " + string.Join(" AND ", whereClauses) : "")}
			{orderBy}
			OFFSET @Skip ROWS FETCH NEXT @MaxResults ROWS ONLY";

		parameters.Add("@Skip", query.Skip);
		parameters.Add("@MaxResults", query.MaxResults);

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

		await using var connection = new SqlConnection(_options.ConnectionString);
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
		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		// Selected by sequence bounds rather than on the timestamp directly. Records are chained in write
		// order, so a record written between two in-range records but stamped outside them is still a link in
		// the chain; selecting on the timestamp alone would leave a hole indistinguishable from a deletion.
		var sql = $@"
			SELECT {IntegrityColumnsSql}
			FROM {_options.FullyQualifiedTableName}
			WHERE SequenceNumber >= ({RangeLowerBoundSql})
			  AND SequenceNumber <= ({RangeUpperBoundSql})
			ORDER BY SequenceNumber ASC";

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
		$@"SELECT MIN(SequenceNumber) FROM {_options.FullyQualifiedTableName}
		   WHERE [Timestamp] >= @StartDate AND [Timestamp] <= @EndDate";

	/// <summary>
	/// The highest sequence number in the verified window. Shared between the record selection and the
	/// successor lookup so the two cannot drift onto different right edges.
	/// </summary>
	private string RangeUpperBoundSql =>
		$@"SELECT MAX(SequenceNumber) FROM {_options.FullyQualifiedTableName}
		   WHERE [Timestamp] >= @StartDate AND [Timestamp] <= @EndDate";

	/// <summary>
	/// The integrity-covered columns, in one place. The in-range records and the successor that pins the
	/// range's right edge must be canonicalized from identical projections; stating the list twice is how the
	/// two would come to differ, and a successor canonicalized from a narrower row fails to verify against an
	/// intact chain.
	/// </summary>
	private const string IntegrityColumnsSql =
		@"EventId, EventType, [Action], Outcome, [Timestamp], ActorId, ActorType,
		  ResourceId, ResourceType, ResourceClassification, TenantId, [ApplicationName], CorrelationId,
		  SessionId, IpAddress, UserAgent, Reason, Metadata, PreviousEventHash, EventHash";

	/// <summary>
	/// Loads, for every chain partition, the tag of the record immediately preceding the verified window.
	/// </summary>
	/// <remarks>
	/// Without these a range slice is indistinguishable from a chain that begins at genesis, so records
	/// deleted from the front of the range would go unreported. A partition with no earlier record is absent
	/// from the result, which is how the caller learns its first in-range record genuinely is the genesis one.
	/// </remarks>
	private async Task<Dictionary<AuditChainKey, string?>> LoadChainAnchorsAsync(
		SqlConnection connection,
		DateTimeOffset startDate,
		DateTimeOffset endDate,
		CancellationToken cancellationToken)
	{
		var sql = $@"
			SELECT TenantId, [ApplicationName], EventHash
			FROM (
				SELECT TenantId, [ApplicationName], EventHash,
					   ROW_NUMBER() OVER (
						   PARTITION BY {CanonicalTenantSql}, {CanonicalApplicationSql}
						   ORDER BY SequenceNumber DESC) AS RowNum
				FROM {_options.FullyQualifiedTableName}
				WHERE SequenceNumber < ({RangeLowerBoundSql})
			) AS Anchors
			WHERE Anchors.RowNum = 1";

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
		SqlConnection connection,
		DateTimeOffset startDate,
		DateTimeOffset endDate,
		CancellationToken cancellationToken)
	{
		var sql = $@"
			SELECT {IntegrityColumnsSql}
			FROM (
				SELECT {IntegrityColumnsSql},
					   ROW_NUMBER() OVER (
						   PARTITION BY {CanonicalTenantSql}, {CanonicalApplicationSql}
						   ORDER BY SequenceNumber ASC) AS RowNum
				FROM {_options.FullyQualifiedTableName}
				WHERE SequenceNumber > ({RangeUpperBoundSql})
			) AS Successors
			WHERE Successors.RowNum = 1";

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
	/// verification, and it is why <see cref="SqlServerAuditOptions.EnableHashChain"/> exists as an explicit
	/// opt-out rather than being silently assumed.
	/// </para>
	/// </remarks>
	private async Task<List<AuditChainPartition>> BuildVerificationPartitionsAsync(
		SqlConnection connection,
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
	public Task<AuditEvent?> GetLastEventAsync(string? tenantId, CancellationToken cancellationToken)
	{
		return GetLastEventInternalAsync(tenantId, applicationName: null, cancellationToken);
	}

	/// <summary>
	/// Stores multiple audit events in a batch.
	/// </summary>
	/// <param name="auditEvents">The audit events to store.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The IDs of the stored events.</returns>
	public async Task<IReadOnlyList<AuditEventId>> StoreBatchAsync(
		IEnumerable<AuditEvent> auditEvents,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(auditEvents);

		var results = new List<AuditEventId>();
		foreach (var auditEvent in auditEvents)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var id = await StoreAsync(auditEvent, cancellationToken).ConfigureAwait(false);
			results.Add(id);
		}

		return results;
	}

	/// <inheritdoc />
	public async Task<int> PurgeExpiredAsync(DateTimeOffset cutoff, CancellationToken cancellationToken) =>
		// Estate-wide: no tenant fragment. This is the ONLY way to express an unscoped purge on this store,
		// and it is reachable only by naming this method — never by omitting or widening an argument to the
		// tenant-scoped one. The empty fragment is supplied here, at the site whose name declares the intent.
		await PurgeCoreAsync(cutoff, tenantPredicate: string.Empty, tenant: null, cancellationToken)
			.ConfigureAwait(false);

	/// <inheritdoc />
	public async Task<int> PurgeTenantAsync(
		DateTimeOffset cutoff,
		KeyedTenantPartition tenant,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(tenant);

		return await PurgeCoreAsync(
				cutoff,
				// NULL-safe by necessity: a bare [TenantId] = @TenantId never matches a row whose tenant
				// column is NULL, so every row written before this table had a tenant column would be
				// unpurgeable — retained past policy, invisibly. Folding NULL onto the reserved sentinel
				// is how the keyed partition type defines a stored value that cannot name a real tenant.
				tenantPredicate: "\n\t\t\t  AND COALESCE([TenantId], @UntenantedSentinel) = @TenantId",
				tenant,
				cancellationToken)
			.ConfigureAwait(false);
	}

	/// <summary>
	/// Shared body of both purge members. The tenant fragment is supplied by the caller rather than derived
	/// from a nullable parameter, so "estate-wide" is a decision made at a named entry point and never the
	/// accidental result of a tenant argument that arrived null.
	/// </summary>
	private async Task<int> PurgeCoreAsync(
		DateTimeOffset cutoff,
		string tenantPredicate,
		KeyedTenantPartition? tenant,
		CancellationToken cancellationToken)
	{
		// The scope of this delete is carried by TWO arguments that must agree, and disagreement in one
		// direction DESTROYS DATA: a tenant-scoped call whose fragment is empty silently deletes every
		// tenant's rows. That mistake compiles, and it passes both the estate-wide arm ("everything older
		// than the cutoff is gone") and the naive tenant arm ("the named tenant's rows are gone") — it is
		// only visible to an arm asserting that OTHER tenants SURVIVE. Rather than rely on that arm always
		// existing, the pairing is asserted here, so a mismatch fails loudly instead of over-deleting.
		if ((tenant is null) != (tenantPredicate.Length == 0))
		{
			throw new InvalidOperationException(
				"Purge scope is inconsistent: a tenant partition must be accompanied by a tenant predicate, "
				+ "and an estate-wide purge must have neither. This indicates a defect in the calling member, "
				+ "not in caller input — refusing rather than deleting a wider set than intended.");
		}

		var cutoffDate = cutoff;
		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		// An annotation is EXISTENTIALLY DEPENDENT on the event it annotates: its tenant is not stored on
		// the annotation row, it is derived by joining EventId -> AuditEvents.TenantId. Deleting an event
		// and leaving its annotations behind therefore does not merely strand rows — it produces rows whose
		// tenant is NO LONGER DERIVABLE, and every join shape then fails in one of two ways: an INNER JOIN
		// makes them vanish from every tenant (silent, permanent loss), and a LEFT JOIN folded to the
		// untenanted sentinel makes them readable by an untenanted scope (a cross-tenant exposure). Neither
		// is a case worth handling, because the orphan state itself is the invariant violation. So the
		// annotations are deleted WITH their events, in ONE transaction and against the SAME batch, which
		// makes the orphan state unreachable rather than survivable.
		//
		// THE CASCADE IS ENFORCED HERE, IN THIS STATEMENT — NOT BY THE SCHEMA. There is deliberately no
		// FOREIGN KEY and no ON DELETE CASCADE on AuditAnnotations: this package ships no annotation DDL,
		// so a schema-level cascade would hold only for consumers who happened to provision it and would
		// silently not hold for everyone else. Do not delete this join on the assumption that the database
		// is doing it.
		//
		// OUTPUT captures the rows the DELETE ACTUALLY removed, and the annotation delete is keyed to that
		// capture rather than re-evaluating the cutoff predicate. Re-evaluating would race: an event
		// inserted between the two statements could be matched by the second predicate but not the first.
		var sql = $@"
			SET XACT_ABORT ON;
			BEGIN TRANSACTION;

			DECLARE @ExpiredEvents TABLE ([EventId] NVARCHAR(64) NOT NULL PRIMARY KEY);

			-- The tenant fragment is supplied by the calling member, not decided here. An empty fragment is
			-- an estate-wide retention sweep (PurgeExpiredAsync); a non-empty one scopes to a single
			-- partition (PurgeTenantAsync). Neither is reachable by omitting an argument to the other.
			DELETE TOP (@BatchSize) FROM {_options.FullyQualifiedTableName}
			OUTPUT deleted.[EventId] INTO @ExpiredEvents
			WHERE [Timestamp] < @CutoffDate{tenantPredicate};

			-- Annotations are an optional companion feature; a host that never registered the annotation
			-- store has no such table, and retention must not fail for it. Presence is checked rather than
			-- assumed, so this is a no-op there instead of an error.
			IF OBJECT_ID(N'{_annotationsTableName}', N'U') IS NOT NULL
			BEGIN
				DELETE a
				FROM {_annotationsTableName} a
				INNER JOIN @ExpiredEvents e ON a.[EventId] = e.[EventId];
			END

			COMMIT TRANSACTION;

			SELECT COUNT(*) FROM @ExpiredEvents;";

		var totalDeleted = 0;
		int deleted;

		do
		{
			// ExecuteScalar, not Execute: the batch now performs two deletes inside one transaction, so
			// Dapper's rows-affected would report the SUM of events AND annotations. The loop's termination
			// condition compares against CleanupBatchSize, so an inflated count would end the sweep early
			// and leave expired events behind — the statement therefore returns the EVENT count explicitly.
			deleted = await connection.ExecuteScalarAsync<int>(
					new CommandDefinition(
						sql,
						new
						{
							BatchSize = _options.Retention.CleanupBatchSize,
							CutoffDate = cutoffDate,
							// Bound unconditionally. Dapper ignores parameters the statement does not
							// reference, so the estate-wide path simply never uses these — the alternative
							// (branching the parameter set) would give the two paths two shapes to diverge in.
							TenantId = tenant?.TenantId,
							UntenantedSentinel = KeyedTenantPartition.Untenanted.TenantId,
						},
						commandTimeout: _options.CommandTimeoutSeconds,
						cancellationToken: cancellationToken))
				.ConfigureAwait(false);

			totalDeleted += deleted;

			if (deleted > 0)
			{
				LogDeletedExpiredEvents(deleted, cutoffDate);
			}
		} while (deleted == _options.Retention.CleanupBatchSize);

		return totalDeleted;
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

	private async Task<string> ComputeEventTagAsync(AuditEvent auditEvent, string? previousHash, CancellationToken cancellationToken) =>
		await _integrity.ComputeTagAsync(
			AuditEventCanonicalizer.Canonicalize(auditEvent), previousHash, cancellationToken).ConfigureAwait(false);

	private (List<string> WhereClauses, DynamicParameters Parameters) BuildQueryClauses(AuditQuery query)
	{
		var whereClauses = new List<string>();
		var parameters = new DynamicParameters();

		if (query.StartDate.HasValue)
		{
			whereClauses.Add("[Timestamp] >= @StartDate");
			parameters.Add("@StartDate", query.StartDate.Value);
		}

		if (query.EndDate.HasValue)
		{
			whereClauses.Add("[Timestamp] <= @EndDate");
			parameters.Add("@EndDate", query.EndDate.Value);
		}

		if (query.EventTypes is { Count: > 0 })
		{
			whereClauses.Add("EventType IN @EventTypes");
			parameters.Add("@EventTypes", query.EventTypes.Select(e => (int)e).ToArray());
		}

		if (query.Outcomes is { Count: > 0 })
		{
			whereClauses.Add("Outcome IN @Outcomes");
			parameters.Add("@Outcomes", query.Outcomes.Select(o => (int)o).ToArray());
		}

		if (!string.IsNullOrEmpty(query.ActorId))
		{
			whereClauses.Add("ActorId = @ActorId");
			parameters.Add("@ActorId", query.ActorId);
		}

		if (!string.IsNullOrEmpty(query.ResourceId))
		{
			whereClauses.Add("ResourceId = @ResourceId");
			parameters.Add("@ResourceId", query.ResourceId);
		}

		if (!string.IsNullOrEmpty(query.ResourceType))
		{
			whereClauses.Add("ResourceType = @ResourceType");
			parameters.Add("@ResourceType", query.ResourceType);
		}

		if (query.MinimumClassification.HasValue)
		{
			whereClauses.Add("ResourceClassification >= @MinClassification");
			parameters.Add("@MinClassification", (int)query.MinimumClassification.Value);
		}

		// SECURITY: the tenant term is a SCOPE, not a filter, and is therefore added UNCONDITIONALLY from
		// ambient context. It previously sat in this list between MinimumClassification and ApplicationName
		// — one optional predicate among many — so omitting query.TenantId returned every tenant's audit
		// events, and naming another tenant returned theirs. query.TenantId is deliberately not consulted.
		AddTenantScope(whereClauses, parameters);

		if (!string.IsNullOrEmpty(query.ApplicationName))
		{
			whereClauses.Add("[ApplicationName] = @ApplicationName");
			parameters.Add("@ApplicationName", query.ApplicationName);
		}

		if (!string.IsNullOrEmpty(query.CorrelationId))
		{
			whereClauses.Add("CorrelationId = @CorrelationId");
			parameters.Add("@CorrelationId", query.CorrelationId);
		}

		if (!string.IsNullOrEmpty(query.Action))
		{
			whereClauses.Add("[Action] = @Action");
			parameters.Add("@Action", query.Action);
		}

		if (!string.IsNullOrEmpty(query.IpAddress))
		{
			whereClauses.Add("IpAddress = @IpAddress");
			parameters.Add("@IpAddress", query.IpAddress);
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
					SqlServerAuditJsonContext.Default.DictionaryStringString),
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

	private async Task<long> InsertAuditEventAsync(
		SqlConnection connection,
		AuditEvent auditEvent,
		string? previousHash,
		string eventHash,
		CancellationToken cancellationToken)
	{
		var parameters = new DynamicParameters();
		parameters.Add("@EventId", auditEvent.EventId);
		parameters.Add("@EventType", (int)auditEvent.EventType);
		parameters.Add("@Action", auditEvent.Action);
		parameters.Add("@Outcome", (int)auditEvent.Outcome);
		parameters.Add("@Timestamp", auditEvent.Timestamp);
		parameters.Add("@ActorId", auditEvent.ActorId);
		parameters.Add("@ActorType", auditEvent.ActorType);
		parameters.Add("@ResourceId", auditEvent.ResourceId);
		parameters.Add("@ResourceType", auditEvent.ResourceType);
		parameters.Add("@ResourceClassification",
			auditEvent.ResourceClassification.HasValue ? (int)auditEvent.ResourceClassification.Value : null);
		// The column is NOT NULL and untenanted rows carry the reserved sentinel, so the raw nullable cannot
		// be bound directly: an event captured with no ambient tenant has a null TenantId by design (the
		// context middleware documents that), and binding it would throw on every untenanted audit write.
		//
		// FromStoredValue rather than `?? sentinel` because it is total over every value that cannot name a
		// real tenant: null, empty AND whitespace all map to the sentinel. A bare null-coalesce would let
		// whitespace through and persist a row that no tenant-scoped predicate can ever match — unreachable
		// audit data, which is the failure this column's NOT NULL exists to prevent.
		parameters.Add("@TenantId", KeyedTenantPartition.FromStoredValue(auditEvent.TenantId).TenantId);
		parameters.Add("@ApplicationName", auditEvent.ApplicationName);
		parameters.Add("@CorrelationId", auditEvent.CorrelationId);
		parameters.Add("@SessionId", auditEvent.SessionId);
		parameters.Add("@IpAddress", auditEvent.IpAddress);
		parameters.Add("@UserAgent", auditEvent.UserAgent);
		parameters.Add("@Reason", auditEvent.Reason);
		parameters.Add("@Metadata", auditEvent.Metadata is not null
			? JsonSerializer.Serialize(
				auditEvent.Metadata,
				SqlServerAuditJsonContext.Default.IReadOnlyDictionaryStringString)
			: null);
		parameters.Add("@PreviousEventHash", previousHash);
		parameters.Add("@EventHash", eventHash);

		var sql = $@"
			INSERT INTO {_options.FullyQualifiedTableName}
			(EventId, EventType, [Action], Outcome, [Timestamp], ActorId, ActorType,
			 ResourceId, ResourceType, ResourceClassification, TenantId, [ApplicationName], CorrelationId,
			 SessionId, IpAddress, UserAgent, Reason, Metadata, PreviousEventHash, EventHash)
			OUTPUT INSERTED.SequenceNumber
			VALUES
			(@EventId, @EventType, @Action, @Outcome, @Timestamp, @ActorId, @ActorType,
			 @ResourceId, @ResourceType, @ResourceClassification, @TenantId, @ApplicationName, @CorrelationId,
			 @SessionId, @IpAddress, @UserAgent, @Reason, @Metadata, @PreviousEventHash, @EventHash)";

		try
		{
			return await connection.ExecuteScalarAsync<long>(
					new CommandDefinition(sql, parameters, commandTimeout: _options.CommandTimeoutSeconds,
						cancellationToken: cancellationToken))
				.ConfigureAwait(false);
		}
		catch (SqlException ex) when (IsDuplicateKeyViolation(ex))
		{
			// This method inserts; it does not upsert. A caller that re-stores an existing event id — the
			// shape a retried publisher produces — is making a mistake the shipped conformance contract
			// already names (StoreAsync_DuplicateId_ShouldThrowInvalidOperationException). The raw provider
			// type is the wrong way to tell them: it forces every consumer to reference the SQL Server client
			// and know its error numbers just to catch a condition the abstraction already defines, and it is
			// a driver type the framework otherwise keeps behind IDataRequest. The filter is narrow on
			// purpose — only a duplicate-key violation is translated, so a connection failure, a timeout, or a
			// constraint we did not anticipate still surfaces unchanged rather than being reported as a
			// duplicate.
			throw new InvalidOperationException(
				$"An audit event with id '{auditEvent.EventId}' already exists.", ex);
		}
	}

	/// <summary>
	/// Indicates whether a SQL Server error is a primary-key or unique-index violation (2627 / 2601).
	/// </summary>
	/// <remarks>
	/// Used as an exception filter so only this one condition is translated. A broad catch would report
	/// an unrelated failure — a dropped connection, a timeout, a check constraint — as a duplicate, which
	/// is worse than not translating at all: the caller would be told the row exists when it does not.
	/// </remarks>
	private static bool IsDuplicateKeyViolation(SqlException ex)
		=> ex.Number is 2627 or 2601;

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
		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var whereClauses = new List<string>();
		var parameters = new DynamicParameters();

		AddTenantScope(whereClauses, parameters);

		whereClauses.Add($"{CanonicalApplicationSql} = COALESCE(NULLIF(@ApplicationName, ''), @NoApplicationSentinel)");
		parameters.Add("@ApplicationName", applicationName);
		parameters.Add("@NoApplicationSentinel", string.Empty);

		var sql = $@"
			SELECT TOP 1 EventHash
			FROM {_options.FullyQualifiedTableName}
			WHERE {string.Join(" AND ", whereClauses)}
			ORDER BY SequenceNumber DESC";

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
		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var whereClauses = new List<string>();
		var parameters = new DynamicParameters();

		// SECURITY: scope, not filter — bound unconditionally from ambient context. The caller-supplied
		// tenantId argument is deliberately not consulted; passing null previously widened this read to
		// every tenant, and passing another tenant's id redirected it to theirs.
		AddTenantScope(whereClauses, parameters);

		if (!string.IsNullOrEmpty(applicationName))
		{
			whereClauses.Add("[ApplicationName] = @ApplicationName");
			parameters.Add("@ApplicationName", applicationName);
		}

		var whereClause = whereClauses.Count > 0
			? "WHERE " + string.Join(" AND ", whereClauses)
			: "";

		var sql = $@"
			SELECT TOP 1 EventId, EventType, [Action], Outcome, [Timestamp], ActorId, ActorType,
				   ResourceId, ResourceType, ResourceClassification, TenantId, [ApplicationName], CorrelationId,
				   SessionId, IpAddress, UserAgent, Reason, Metadata, PreviousEventHash, EventHash
			FROM {_options.FullyQualifiedTableName}
			{whereClause}
			ORDER BY SequenceNumber DESC";

		var row = await connection.QuerySingleOrDefaultAsync<AuditEventRow>(
				new CommandDefinition(sql, parameters, commandTimeout: _options.CommandTimeoutSeconds,
					cancellationToken: cancellationToken))
			.ConfigureAwait(false);

		return row is null ? null : MapToAuditEvent(row);
	}

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

	[LoggerMessage(LogLevel.Information, "Deleted {Count} audit events older than {CutoffDate}")]
	private partial void LogDeletedExpiredEvents(int count, DateTimeOffset cutoffDate);

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
