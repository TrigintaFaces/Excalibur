// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;
using System.Text.RegularExpressions;

using Dapper;

using Excalibur.Compliance;

using Microsoft.Data.SqlClient;
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
	private readonly SqlServerAuditAnnotationStoreOptions _options;
	private readonly IAuditActorProvider _actorProvider;
	private readonly TimeProvider _timeProvider;
	private readonly ILogger<SqlServerAuditAnnotationStore> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerAuditAnnotationStore"/> class.
	/// </summary>
	public SqlServerAuditAnnotationStore(
		IOptions<SqlServerAuditAnnotationStoreOptions> options,
		IAuditActorProvider actorProvider,
		TimeProvider timeProvider,
		ILogger<SqlServerAuditAnnotationStore> logger)
	{
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_actorProvider = actorProvider ?? throw new ArgumentNullException(nameof(actorProvider));
		_timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		ValidateSqlIdentifier(_options.SchemaName, nameof(SqlServerAuditAnnotationStoreOptions.SchemaName));
		ValidateSqlIdentifier(_options.TableName, nameof(SqlServerAuditAnnotationStoreOptions.TableName));
	}

	/// <inheritdoc />
	public async Task TagAsync(string eventId, IReadOnlyList<string> tags, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
		ArgumentNullException.ThrowIfNull(tags);

		if (tags.Count == 0)
		{
			return;
		}

		var actorId = await _actorProvider.GetCurrentActorIdAsync(cancellationToken).ConfigureAwait(false);
		var now = _timeProvider.GetUtcNow();

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		// Idempotent: INSERT WHERE NOT EXISTS with HOLDLOCK to prevent
		// concurrent TOCTOU duplicates (two transactions both seeing "no row"
		// and both inserting). UPDLOCK + HOLDLOCK serializes the check-and-insert
		// within a single implicit transaction scope.
		var sql = $@"
			INSERT INTO {_options.FullyQualifiedTableName}
				(Id, EventId, AnnotationType, Content, ActorId, CreatedAt, Visibility)
			SELECT @Id, @EventId, @AnnotationType, @Content, @ActorId, @CreatedAt, @Visibility
			WHERE NOT EXISTS (
				SELECT 1 FROM {_options.FullyQualifiedTableName} WITH (UPDLOCK, HOLDLOCK)
				WHERE EventId = @EventId AND AnnotationType = @AnnotationType AND Content = @Content
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

		var actorId = await _actorProvider.GetCurrentActorIdAsync(cancellationToken).ConfigureAwait(false);
		var now = _timeProvider.GetUtcNow();

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		// Replace semantics: MERGE to upsert one bookmark per actor per event
		var sql = $@"
			MERGE {_options.FullyQualifiedTableName} AS target
			USING (SELECT @EventId AS EventId, @ActorId AS ActorId) AS source
			ON target.EventId = source.EventId
				AND target.ActorId = source.ActorId
				AND target.AnnotationType = @AnnotationType
			WHEN MATCHED THEN
				UPDATE SET Content = @Content, CreatedAt = @CreatedAt
			WHEN NOT MATCHED THEN
				INSERT (Id, EventId, AnnotationType, Content, ActorId, CreatedAt, Visibility)
				VALUES (@Id, @EventId, @AnnotationType, @Content, @ActorId, @CreatedAt, @Visibility);";

		var parameters = new DynamicParameters();
		parameters.Add("@Id", Guid.NewGuid().ToString("N"));
		parameters.Add("@EventId", eventId);
		parameters.Add("@AnnotationType", (int)AuditAnnotationType.Bookmark);
		parameters.Add("@Content", label ?? string.Empty);
		parameters.Add("@ActorId", actorId);
		parameters.Add("@CreatedAt", now);
		parameters.Add("@Visibility", (int)AuditAnnotationVisibility.Personal);

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

		var actorId = await _actorProvider.GetCurrentActorIdAsync(cancellationToken).ConfigureAwait(false);

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var sql = $@"
			DELETE FROM {_options.FullyQualifiedTableName}
			WHERE EventId = @EventId
				AND ActorId = @ActorId
				AND AnnotationType = @AnnotationType";

		var parameters = new DynamicParameters();
		parameters.Add("@EventId", eventId);
		parameters.Add("@ActorId", actorId);
		parameters.Add("@AnnotationType", (int)AuditAnnotationType.Bookmark);

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

		var actorId = await _actorProvider.GetCurrentActorIdAsync(cancellationToken).ConfigureAwait(false);
		var now = _timeProvider.GetUtcNow();
		var id = Guid.NewGuid().ToString("N");

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var sql = $@"
			INSERT INTO {_options.FullyQualifiedTableName}
				(Id, EventId, AnnotationType, Content, ActorId, CreatedAt, Visibility)
			VALUES (@Id, @EventId, @AnnotationType, @Content, @ActorId, @CreatedAt, @Visibility)";

		var parameters = new DynamicParameters();
		parameters.Add("@Id", id);
		parameters.Add("@EventId", eventId);
		parameters.Add("@AnnotationType", (int)AuditAnnotationType.Note);
		parameters.Add("@Content", note);
		parameters.Add("@ActorId", actorId);
		parameters.Add("@CreatedAt", now);
		parameters.Add("@Visibility", (int)AuditAnnotationVisibility.Shared);

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
			SELECT Id, EventId, AnnotationType, Content, ActorId, CreatedAt, Visibility
			FROM {_options.FullyQualifiedTableName}
			WHERE EventId = @EventId
			ORDER BY CreatedAt ASC";

		var rows = await connection.QueryAsync<AnnotationRow>(
				new CommandDefinition(sql, new { EventId = eventId },
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

		sb.Append($"SELECT DISTINCT a.EventId FROM {_options.FullyQualifiedTableName} a");

		var whereClauses = new List<string>();

		if (query.Tags is { Count: > 0 })
		{
			whereClauses.Add("(a.AnnotationType = @TagType AND a.Content IN @Tags)");
			parameters.Add("@TagType", (int)AuditAnnotationType.Tag);
			parameters.Add("@Tags", query.Tags);
		}

		if (query.IsBookmarked == true)
		{
			whereClauses.Add($"a.EventId IN (SELECT EventId FROM {_options.FullyQualifiedTableName} WHERE AnnotationType = @BookmarkType)");
			parameters.Add("@BookmarkType", (int)AuditAnnotationType.Bookmark);
		}
		else if (query.IsBookmarked == false)
		{
			whereClauses.Add($"a.EventId NOT IN (SELECT EventId FROM {_options.FullyQualifiedTableName} WHERE AnnotationType = @BookmarkTypeExcl)");
			parameters.Add("@BookmarkTypeExcl", (int)AuditAnnotationType.Bookmark);
		}

		if (query.HasNotes == true)
		{
			whereClauses.Add($"a.EventId IN (SELECT EventId FROM {_options.FullyQualifiedTableName} WHERE AnnotationType = @NoteType)");
			parameters.Add("@NoteType", (int)AuditAnnotationType.Note);
		}
		else if (query.HasNotes == false)
		{
			whereClauses.Add($"a.EventId NOT IN (SELECT EventId FROM {_options.FullyQualifiedTableName} WHERE AnnotationType = @NoteTypeExcl)");
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
