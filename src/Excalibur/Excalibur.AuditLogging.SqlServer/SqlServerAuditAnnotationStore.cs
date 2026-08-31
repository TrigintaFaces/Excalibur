// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;
using System.Text.RegularExpressions;

using Dapper;

using Excalibur.Compliance;
using Excalibur.Dispatch;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.AuditLogging.SqlServer;

/// <summary>
/// SQL Server implementation of <see cref="IAuditAnnotationStore"/> using Dapper.
/// </summary>
/// <remarks>
/// <para>
/// Annotations are stored in a single table with a discriminator column for type
/// (Tag, Bookmark, Note). Tags are idempotent — duplicate inserts are ignored.
/// Bookmarks use replace semantics per actor per event.
/// </para>
/// </remarks>
internal sealed partial class SqlServerAuditAnnotationStore : IAuditAnnotationStore
{
	/// <summary>
	/// Restricts a set of annotations to those whose <em>event</em> belongs to the ambient tenant.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The annotations table carries no tenant column, by design: an annotation is existentially
	/// dependent on its event, so the event's tenant is the annotation's tenant and duplicating it would
	/// create two facts that can disagree. Every predicate therefore derives the tenant by joining the
	/// event, which makes a mis-tenanted annotation unrepresentable rather than merely unlikely.
	/// </para>
	/// <para>
	/// The comparison folds a null event tenant onto the reserved untenanted sentinel. Written the
	/// obvious way as <c>ae.TenantId = @TenantId</c> this predicate would fail open: the events table
	/// permits a null tenant, and <c>NULL = @TenantId</c> is never true, so every legacy untenanted row
	/// would silently drop out of its own scope's results. Folding through the sentinel makes the
	/// untenanted partition bind a real term like any other tenant.
	/// </para>
	/// <para>
	/// The sentinel is <em>bound as a parameter</em> rather than interpolated into the statement text.
	/// Interpolation was safe by accident — the value is a framework constant, never consumer input — but
	/// it put a framework value into the SQL string, which is a shape that only stays safe while nobody
	/// changes where the value comes from. Binding removes the question instead of answering it, and it
	/// keeps this predicate byte-identical to the one the audit store emits.
	/// </para>
	/// </remarks>
	private const string TenantJoinPredicate =
		"COALESCE(ae.TenantId, @UntenantedSentinel) = @TenantId";

	private readonly SqlServerAuditAnnotationStoreOptions _options;
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly TimeProvider _timeProvider;
	private readonly ITenantContext _tenantContext;
	/// <summary>
	/// Gets the tenant term this store runs under, resolved in one place so every statement it builds binds
	/// the same value. The context is a required dependency, so the term is decided identically on every
	/// path: the store cannot resolve one partition on write and a different one on read.
	/// </summary>
	private KeyedTenantPartition CurrentTenantPartition =>
		KeyedTenantPartition.FromContext(_tenantContext);

	private readonly ILogger<SqlServerAuditAnnotationStore> _logger;

	/// <summary>
	/// Resolves the identity of the caller performing the current operation, from a scope opened for that
	/// operation.
	/// </summary>
	/// <remarks>
	/// The provider is never held in a field. This store is a singleton, so a provider captured at
	/// construction answers for one caller for the life of the process -- and the value it returns is
	/// written to the row as the annotation's author and used as the authorship term on reads, so every
	/// annotation the process wrote would be attributed to whoever happened to be first, and every read
	/// would be filtered by that same identity. A provider that reads ambient state (claims, an
	/// <c>IHttpContextAccessor</c>, an async-local) resolves correctly from the scope opened here, because
	/// that state flows with the call rather than with the container scope.
	/// </remarks>
	private async Task<string> GetCurrentActorIdAsync(CancellationToken cancellationToken)
	{
		await using var scope = _scopeFactory.CreateAsyncScope();

		return await scope.ServiceProvider
			.GetRequiredService<IAuditActorProvider>()
			.GetCurrentActorIdAsync(cancellationToken)
			.ConfigureAwait(false);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerAuditAnnotationStore"/> class.
	/// </summary>
	public SqlServerAuditAnnotationStore(
		IOptions<SqlServerAuditAnnotationStoreOptions> options,
		IServiceScopeFactory scopeFactory,
		TimeProvider timeProvider,
		ITenantContext tenantContext,
		ILogger<SqlServerAuditAnnotationStore> logger)
	{
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
		_timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

		ArgumentNullException.ThrowIfNull(tenantContext);
		_tenantContext = tenantContext;
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		ValidateSqlIdentifier(_options.SchemaName, nameof(SqlServerAuditAnnotationStoreOptions.SchemaName));
		ValidateSqlIdentifier(_options.TableName, nameof(SqlServerAuditAnnotationStoreOptions.TableName));
		ValidateSqlIdentifier(_options.EventsTableName, nameof(SqlServerAuditAnnotationStoreOptions.EventsTableName));
	}

	/// <summary>
	/// Gets the tenant term to bind: the ambient tenant, or the reserved untenanted sentinel.
	/// </summary>
	/// <remarks>
	/// Routed through <see cref="KeyedTenantPartition"/>, which has no empty inhabitant, so the value is
	/// always concrete and a tenant-blind statement cannot be produced by omission.
	/// </remarks>
	private string CurrentTenantTerm =>
		CurrentTenantPartition.TenantId;

	/// <inheritdoc />
	public async Task TagAsync(string eventId, IReadOnlyList<string> tags, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
		ArgumentNullException.ThrowIfNull(tags);

		if (tags.Count == 0)
		{
			return;
		}

		var actorId = await GetCurrentActorIdAsync(cancellationToken).ConfigureAwait(false);
		var now = _timeProvider.GetUtcNow();
		var tenantId = CurrentTenantTerm;

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		// Idempotent: INSERT WHERE NOT EXISTS with HOLDLOCK to prevent
		// concurrent TOCTOU duplicates (two transactions both seeing "no row"
		// and both inserting). UPDLOCK + HOLDLOCK serializes the check-and-insert
		// within a single implicit transaction scope.
		// The duplicate probe is scoped to the ambient tenant by joining the annotated event. Without the
		// join it matched across tenants, so one tenant's tag silently suppressed another's identical tag
		// — and, because the caller could observe the suppression, it also answered "has any other tenant
		// written this exact content?", an inference channel over another tenant's audit text.
		//
		// The insert is guarded by the same tenant term, so a tag can only ever be attached to an event
		// the caller's own tenant owns: EXISTS proves the target event is in scope, NOT EXISTS proves the
		// tag is not already there for that tenant.
		var sql = $@"
			INSERT INTO {_options.FullyQualifiedTableName}
				(Id, EventId, AnnotationType, Content, ActorId, CreatedAt, Visibility)
			SELECT @Id, @EventId, @AnnotationType, @Content, @ActorId, @CreatedAt, @Visibility
			WHERE EXISTS (
				SELECT 1 FROM {_options.FullyQualifiedEventsTableName} ae
				WHERE ae.EventId = @EventId AND {TenantJoinPredicate}
			)
			AND NOT EXISTS (
				SELECT 1 FROM {_options.FullyQualifiedTableName} a WITH (UPDLOCK, HOLDLOCK)
				INNER JOIN {_options.FullyQualifiedEventsTableName} ae ON ae.EventId = a.EventId
				WHERE a.EventId = @EventId AND a.AnnotationType = @AnnotationType AND a.Content = @Content
					AND {TenantJoinPredicate}
			)";

		foreach (var tag in tags)
		{
			if (string.IsNullOrWhiteSpace(tag))
			{
				continue;
			}

			var parameters = new DynamicParameters();
			parameters.Add("@Id", Guid.NewGuid().ToString("N"));
			parameters.Add("@EventId", eventId);
			parameters.Add("@AnnotationType", (int)AuditAnnotationType.Tag);
			parameters.Add("@Content", tag);
			parameters.Add("@ActorId", actorId);
			parameters.Add("@CreatedAt", now);
			parameters.Add("@Visibility", (int)AuditAnnotationVisibility.Shared);
			parameters.Add("@TenantId", tenantId);
			parameters.Add("@UntenantedSentinel", KeyedTenantPartition.Untenanted.TenantId);

			await connection.ExecuteAsync(
					new CommandDefinition(sql, parameters,
						commandTimeout: _options.CommandTimeoutSeconds,
						cancellationToken: cancellationToken))
				.ConfigureAwait(false);
		}

		LogTagsAdded(eventId, tags.Count);
	}

	/// <inheritdoc />
	public async Task BookmarkAsync(string eventId, string? label, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(eventId);

		var actorId = await GetCurrentActorIdAsync(cancellationToken).ConfigureAwait(false);
		var now = _timeProvider.GetUtcNow();

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		// Replace semantics: MERGE to upsert one bookmark per actor per event, WITHIN a tenant.
		//
		// The tenant term is restricted in the USING source rather than added to the ON clause, so an
		// out-of-scope event yields an empty source: nothing matches and nothing is inserted. Putting it
		// only in the ON clause would leave WHEN NOT MATCHED reachable, which would insert a bookmark
		// against another tenant's event instead of refusing.
		//
		// Why this arm existed: the match keyed on (EventId, ActorId, AnnotationType) with no tenant, so
		// wherever one actor identity spans tenants — a shared operator or service account, the ordinary
		// case in multi-tenant SaaS — tenant B's bookmark took the MATCHED branch against tenant A's row
		// and overwrote its Content. A cross-tenant write, not merely a cross-tenant read.
		var sql = $@"
			MERGE {_options.FullyQualifiedTableName} WITH (UPDLOCK, HOLDLOCK) AS target
			USING (
				SELECT ae.EventId AS EventId, @ActorId AS ActorId
				FROM {_options.FullyQualifiedEventsTableName} ae
				WHERE ae.EventId = @EventId AND {TenantJoinPredicate}
			) AS source
			ON target.EventId = source.EventId
				AND target.ActorId = source.ActorId
				AND target.AnnotationType = @AnnotationType
				AND EXISTS (
					SELECT 1 FROM {_options.FullyQualifiedEventsTableName} ae
					WHERE ae.EventId = target.EventId AND {TenantJoinPredicate}
				)
			WHEN MATCHED THEN
				UPDATE SET Content = @Content, CreatedAt = @CreatedAt
			WHEN NOT MATCHED THEN
				INSERT (Id, EventId, AnnotationType, Content, ActorId, CreatedAt, Visibility)
				VALUES (@Id, source.EventId, @AnnotationType, @Content, @ActorId, @CreatedAt, @Visibility);";

		var parameters = new DynamicParameters();
		parameters.Add("@Id", Guid.NewGuid().ToString("N"));
		parameters.Add("@EventId", eventId);
		parameters.Add("@AnnotationType", (int)AuditAnnotationType.Bookmark);
		parameters.Add("@Content", label ?? string.Empty);
		parameters.Add("@ActorId", actorId);
		parameters.Add("@CreatedAt", now);
		parameters.Add("@Visibility", (int)AuditAnnotationVisibility.Personal);
		parameters.Add("@TenantId", CurrentTenantTerm);
		parameters.Add("@UntenantedSentinel", KeyedTenantPartition.Untenanted.TenantId);

		await connection.ExecuteAsync(
				new CommandDefinition(sql, parameters,
					commandTimeout: _options.CommandTimeoutSeconds,
					cancellationToken: cancellationToken))
			.ConfigureAwait(false);

		LogBookmarkAdded(eventId, actorId);
	}

	/// <inheritdoc />
	public async Task RemoveBookmarkAsync(string eventId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(eventId);

		var actorId = await GetCurrentActorIdAsync(cancellationToken).ConfigureAwait(false);

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		// Tenant-scoped delete: a destructive statement must never resolve to a predicate that spans
		// tenants. As with the MERGE, a shared actor identity would otherwise let one tenant remove
		// another tenant's bookmark of the same event.
		var sql = $@"
			DELETE a FROM {_options.FullyQualifiedTableName} a
			INNER JOIN {_options.FullyQualifiedEventsTableName} ae ON ae.EventId = a.EventId
			WHERE a.EventId = @EventId
				AND a.ActorId = @ActorId
				AND a.AnnotationType = @AnnotationType
				AND {TenantJoinPredicate}";

		var parameters = new DynamicParameters();
		parameters.Add("@EventId", eventId);
		parameters.Add("@ActorId", actorId);
		parameters.Add("@AnnotationType", (int)AuditAnnotationType.Bookmark);
		parameters.Add("@TenantId", CurrentTenantTerm);
		parameters.Add("@UntenantedSentinel", KeyedTenantPartition.Untenanted.TenantId);

		await connection.ExecuteAsync(
				new CommandDefinition(sql, parameters,
					commandTimeout: _options.CommandTimeoutSeconds,
					cancellationToken: cancellationToken))
			.ConfigureAwait(false);

		LogBookmarkRemoved(eventId, actorId);
	}

	/// <inheritdoc />
	public async Task<AuditAnnotationId> AnnotateAsync(string eventId, string note, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
		ArgumentException.ThrowIfNullOrWhiteSpace(note);

		var actorId = await GetCurrentActorIdAsync(cancellationToken).ConfigureAwait(false);
		var now = _timeProvider.GetUtcNow();
		var id = Guid.NewGuid().ToString("N");

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		// A note may only be attached to an event the caller's own tenant owns. This site carries no
		// dedupe predicate — notes are not deduplicated — so the tenant term guards the write itself:
		// the INSERT is conditional on the target event being in scope, and inserts nothing otherwise.
		var sql = $@"
			INSERT INTO {_options.FullyQualifiedTableName}
				(Id, EventId, AnnotationType, Content, ActorId, CreatedAt, Visibility)
			SELECT @Id, @EventId, @AnnotationType, @Content, @ActorId, @CreatedAt, @Visibility
			WHERE EXISTS (
				SELECT 1 FROM {_options.FullyQualifiedEventsTableName} ae
				WHERE ae.EventId = @EventId AND {TenantJoinPredicate}
			)";

		var parameters = new DynamicParameters();
		parameters.Add("@Id", id);
		parameters.Add("@EventId", eventId);
		parameters.Add("@AnnotationType", (int)AuditAnnotationType.Note);
		parameters.Add("@Content", note);
		parameters.Add("@ActorId", actorId);
		parameters.Add("@CreatedAt", now);
		parameters.Add("@Visibility", (int)AuditAnnotationVisibility.Shared);
		parameters.Add("@TenantId", CurrentTenantTerm);
		parameters.Add("@UntenantedSentinel", KeyedTenantPartition.Untenanted.TenantId);

		await connection.ExecuteAsync(
				new CommandDefinition(sql, parameters,
					commandTimeout: _options.CommandTimeoutSeconds,
					cancellationToken: cancellationToken))
			.ConfigureAwait(false);

		LogNoteAdded(eventId, id);

		return new AuditAnnotationId(id);
	}

	/// <inheritdoc />
	public async Task<AuditAnnotations> GetAnnotationsAsync(string eventId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(eventId);

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var sql = $@"
			SELECT a.Id, a.EventId, a.AnnotationType, a.Content, a.ActorId, a.CreatedAt, a.Visibility
			FROM {_options.FullyQualifiedTableName} a
			INNER JOIN {_options.FullyQualifiedEventsTableName} ae ON ae.EventId = a.EventId
			WHERE a.EventId = @EventId
				AND {TenantJoinPredicate}
			ORDER BY a.CreatedAt ASC";

		var rows = await connection.QueryAsync<AnnotationRow>(
				new CommandDefinition(sql, new { EventId = eventId, TenantId = CurrentTenantTerm, UntenantedSentinel = KeyedTenantPartition.Untenanted.TenantId },
					commandTimeout: _options.CommandTimeoutSeconds,
					cancellationToken: cancellationToken))
			.ConfigureAwait(false);

		var tags = new List<string>();
		var bookmarks = new List<AuditAnnotation>();
		var notes = new List<AuditAnnotation>();

		foreach (var row in rows)
		{
			var type = (AuditAnnotationType)row.AnnotationType;
			switch (type)
			{
				case AuditAnnotationType.Tag:
					tags.Add(row.Content);
					break;

				case AuditAnnotationType.Bookmark:
					bookmarks.Add(MapToAnnotation(row));
					break;

				case AuditAnnotationType.Note:
					notes.Add(MapToAnnotation(row));
					break;
			}
		}

		return new AuditAnnotations(eventId, tags, bookmarks, notes);
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<string>> QueryByAnnotationAsync(
		AuditAnnotationQuery query,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(query);

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var sb = new StringBuilder();
		var parameters = new DynamicParameters();

		// Every arm of this query is tenant-scoped, including the negative ones. The tenant term is seeded
		// as a mandatory clause rather than added per-arm, so a query with no user-supplied filters is
		// still scoped: an unfiltered call must return the caller's own events, never the estate's.
		sb.Append($"SELECT DISTINCT a.EventId FROM {_options.FullyQualifiedTableName} a")
			.Append($" INNER JOIN {_options.FullyQualifiedEventsTableName} ae ON ae.EventId = a.EventId");

		var whereClauses = new List<string> { TenantJoinPredicate };
		parameters.Add("@TenantId", CurrentTenantTerm);
		parameters.Add("@UntenantedSentinel", KeyedTenantPartition.Untenanted.TenantId);

		// The bookmark/note existence arms are scoped for a second, distinct reason: unscoped, a NOT IN
		// over the whole table let the caller's own result set change according to whether a DIFFERENT
		// tenant had bookmarked or annotated the event — an existence oracle over another tenant's audit
		// activity, reached without ever reading a row of theirs.
		// They also carry an AUTHORSHIP term, and that one cannot be replaced by filtering the rows this
		// query returns. These arms disclose through the SHAPE of the result set rather than its contents:
		// IsBookmarked == false emits NOT IN, so an event is removed from the caller's results because an
		// annotation they may not read exists. No post-hoc row filter can restore a row that was excluded,
		// so the term belongs inside the subquery. An annotation counts toward existence only when the
		// caller could read it — shared, or authored by them.
		var currentActorId = await GetCurrentActorIdAsync(cancellationToken).ConfigureAwait(false);
		parameters.Add("@SharedVisibility", (int)AuditAnnotationVisibility.Shared);
		parameters.Add("@CurrentActorId", currentActorId);

		string ExistenceSubquery(string typeParameter) =>
			$"SELECT sa.EventId FROM {_options.FullyQualifiedTableName} sa"
			+ $" INNER JOIN {_options.FullyQualifiedEventsTableName} ae ON ae.EventId = sa.EventId"
			+ $" WHERE sa.AnnotationType = {typeParameter} AND {TenantJoinPredicate}"
			+ " AND (sa.Visibility = @SharedVisibility OR sa.ActorId = @CurrentActorId)";

		if (query.Tags is { Count: > 0 })
		{
			whereClauses.Add("(a.AnnotationType = @TagType AND a.Content IN @Tags)");
			parameters.Add("@TagType", (int)AuditAnnotationType.Tag);
			parameters.Add("@Tags", query.Tags);
		}

		if (query.IsBookmarked == true)
		{
			whereClauses.Add($"a.EventId IN ({ExistenceSubquery("@BookmarkType")})");
			parameters.Add("@BookmarkType", (int)AuditAnnotationType.Bookmark);
		}
		else if (query.IsBookmarked == false)
		{
			whereClauses.Add($"a.EventId NOT IN ({ExistenceSubquery("@BookmarkTypeExcl")})");
			parameters.Add("@BookmarkTypeExcl", (int)AuditAnnotationType.Bookmark);
		}

		if (query.HasNotes == true)
		{
			whereClauses.Add($"a.EventId IN ({ExistenceSubquery("@NoteType")})");
			parameters.Add("@NoteType", (int)AuditAnnotationType.Note);
		}
		else if (query.HasNotes == false)
		{
			whereClauses.Add($"a.EventId NOT IN ({ExistenceSubquery("@NoteTypeExcl")})");
			parameters.Add("@NoteTypeExcl", (int)AuditAnnotationType.Note);
		}

		if (!string.IsNullOrEmpty(query.ActorId))
		{
			whereClauses.Add("a.ActorId = @ActorId");
			parameters.Add("@ActorId", query.ActorId);
		}

		if (query.Since.HasValue)
		{
			whereClauses.Add("a.CreatedAt >= @Since");
			parameters.Add("@Since", query.Since.Value);
		}

		if (whereClauses.Count > 0)
		{
			sb.Append(" WHERE ");
			sb.Append(string.Join(" AND ", whereClauses));
		}

		sb.Append(" ORDER BY a.EventId");
		sb.Append(" OFFSET @Skip ROWS FETCH NEXT @MaxResults ROWS ONLY");

		parameters.Add("@Skip", query.Skip);
		parameters.Add("@MaxResults", query.MaxResults);

		var eventIds = await connection.QueryAsync<string>(
				new CommandDefinition(sb.ToString(), parameters,
					commandTimeout: _options.CommandTimeoutSeconds,
					cancellationToken: cancellationToken))
			.ConfigureAwait(false);

		return eventIds.ToList();
	}

	private static AuditAnnotation MapToAnnotation(AnnotationRow row) => new()
	{
		Id = row.Id,
		EventId = row.EventId,
		Type = (AuditAnnotationType)row.AnnotationType,
		Content = row.Content,
		ActorId = row.ActorId,
		CreatedAt = row.CreatedAt,
		Visibility = (AuditAnnotationVisibility)row.Visibility
	};

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

	[LoggerMessage(93800, LogLevel.Debug, "Added {Count} tags to audit event {EventId}")]
	private partial void LogTagsAdded(string eventId, int count);

	[LoggerMessage(93801, LogLevel.Debug, "Bookmark added for audit event {EventId} by actor {ActorId}")]
	private partial void LogBookmarkAdded(string eventId, string actorId);

	[LoggerMessage(93802, LogLevel.Debug, "Bookmark removed for audit event {EventId} by actor {ActorId}")]
	private partial void LogBookmarkRemoved(string eventId, string actorId);

	[LoggerMessage(93803, LogLevel.Debug, "Note {NoteId} added to audit event {EventId}")]
	private partial void LogNoteAdded(string eventId, string noteId);

	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Performance",
		"CA1812:Avoid uninstantiated internal classes",
		Justification = "Dapper materializes rows via reflection.")]
	private sealed class AnnotationRow
	{
		public string Id { get; init; } = string.Empty;
		public string EventId { get; init; } = string.Empty;
		public int AnnotationType { get; init; }
		public string Content { get; init; } = string.Empty;
		public string ActorId { get; init; } = string.Empty;
		public DateTimeOffset CreatedAt { get; init; }
		public int Visibility { get; init; }
	}
}
