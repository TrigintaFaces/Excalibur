// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Excalibur.AuditLogging;
using Excalibur.Compliance;

using Microsoft.Data.SqlClient;

namespace Excalibur.Dispatch.Integration.Tests.Compliance.SqlServer;

[IntegrationTest]
[Collection(ContainerCollections.SqlServer)]
[Trait("Component", TestComponents.AuditLogging)]
[Trait("Infrastructure", TestInfrastructure.SqlServer)]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait(TraitNames.Component, TestComponents.Compliance)]
public sealed class SqlServerAuditAnnotationStoreIntegrationShould : IntegrationTestBase
{
	private const string DefaultActor = "actor-test";
	private readonly SqlServerFixture _fixture;

	public SqlServerAuditAnnotationStoreIntegrationShould(SqlServerFixture fixture)
	{
		_fixture = fixture;
	}

	#region CRUD Lifecycle

	[Fact]
	public async Task Tag_and_query_round_trip()
	{
		await InitializeAnnotationTableAsync();
		var store = CreateStore();
		var eventId = UniqueEventId();

		await store.TagAsync(eventId, new[] { "important", "reviewed" }, TestCancellationToken);

		var annotations = await store.GetAnnotationsAsync(eventId, TestCancellationToken);

		annotations.EventId.ShouldBe(eventId);
		annotations.Tags.Count.ShouldBe(2);
		annotations.Tags.ShouldContain("important");
		annotations.Tags.ShouldContain("reviewed");
	}

	[Fact]
	public async Task Tag_then_remove_tag_via_query_shows_gone()
	{
		await InitializeAnnotationTableAsync();
		var store = CreateStore();
		var eventId = UniqueEventId();

		await store.TagAsync(eventId, new[] { "flagged" }, TestCancellationToken);

		var before = await store.GetAnnotationsAsync(eventId, TestCancellationToken);
		before.Tags.Count.ShouldBe(1);

		// Remove the tag directly via SQL (IAuditAnnotationStore has no RemoveTag method)
		await ExecuteSqlAsync($"DELETE FROM [audit].[AuditAnnotations] WHERE EventId = @EventId AND Content = @Tag",
			new { EventId = eventId, Tag = "flagged" });

		var after = await store.GetAnnotationsAsync(eventId, TestCancellationToken);
		after.Tags.Count.ShouldBe(0);
	}

	#endregion

	#region Bookmark Replace Semantics

	[Fact]
	public async Task Bookmark_replaces_existing_for_same_actor_and_event()
	{
		await InitializeAnnotationTableAsync();
		var store = CreateStore();
		var eventId = UniqueEventId();

		await store.BookmarkAsync(eventId, "first-label", TestCancellationToken);
		await store.BookmarkAsync(eventId, "second-label", TestCancellationToken);

		var annotations = await store.GetAnnotationsAsync(eventId, TestCancellationToken);

		// MERGE semantics: one bookmark per actor per event
		annotations.Bookmarks.Count.ShouldBe(1);
		annotations.Bookmarks[0].Content.ShouldBe("second-label");
		annotations.Bookmarks[0].ActorId.ShouldBe(DefaultActor);
	}

	[Fact]
	public async Task Bookmark_remove_deletes_for_current_actor()
	{
		await InitializeAnnotationTableAsync();
		var store = CreateStore();
		var eventId = UniqueEventId();

		await store.BookmarkAsync(eventId, "my-bookmark", TestCancellationToken);

		var before = await store.GetAnnotationsAsync(eventId, TestCancellationToken);
		before.Bookmarks.Count.ShouldBe(1);

		await store.RemoveBookmarkAsync(eventId, TestCancellationToken);

		var after = await store.GetAnnotationsAsync(eventId, TestCancellationToken);
		after.Bookmarks.Count.ShouldBe(0);
	}

	#endregion

	#region Tag Idempotency

	[Fact]
	public async Task Tag_same_value_twice_produces_single_entry()
	{
		await InitializeAnnotationTableAsync();
		var store = CreateStore();
		var eventId = UniqueEventId();

		await store.TagAsync(eventId, new[] { "duplicate" }, TestCancellationToken);
		await store.TagAsync(eventId, new[] { "duplicate" }, TestCancellationToken);

		var annotations = await store.GetAnnotationsAsync(eventId, TestCancellationToken);
		annotations.Tags.Count.ShouldBe(1);
		annotations.Tags[0].ShouldBe("duplicate");
	}

	#endregion

	#region Note Annotation

	[Fact]
	public async Task Annotate_with_note_stores_actor_and_timestamp()
	{
		await InitializeAnnotationTableAsync();
		var store = CreateStore();
		var eventId = UniqueEventId();
		var beforeWrite = DateTimeOffset.UtcNow.AddSeconds(-1);

		var noteId = await store.AnnotateAsync(eventId, "This is a compliance note.", TestCancellationToken);

		noteId.ShouldNotBeNull();
		noteId.Value.ShouldNotBeNullOrWhiteSpace();

		var annotations = await store.GetAnnotationsAsync(eventId, TestCancellationToken);
		annotations.Notes.Count.ShouldBe(1);

		var note = annotations.Notes[0];
		note.Content.ShouldBe("This is a compliance note.");
		note.ActorId.ShouldBe(DefaultActor);
		note.CreatedAt.ShouldBeGreaterThan(beforeWrite);
	}

	#endregion

	#region Query Filters

	[Fact]
	public async Task Query_by_tag_value_returns_matching_events()
	{
		await InitializeAnnotationTableAsync();
		var store = CreateStore();

		var evt1 = UniqueEventId();
		var evt2 = UniqueEventId();
		var evt3 = UniqueEventId();

		await store.TagAsync(evt1, new[] { "critical" }, TestCancellationToken);
		await store.TagAsync(evt2, new[] { "critical", "reviewed" }, TestCancellationToken);
		await store.TagAsync(evt3, new[] { "low-priority" }, TestCancellationToken);

		var results = await store.QueryByAnnotationAsync(
			new AuditAnnotationQuery { Tags = new[] { "critical" } },
			TestCancellationToken);

		results.Count.ShouldBe(2);
		results.ShouldContain(evt1);
		results.ShouldContain(evt2);
		results.ShouldNotContain(evt3);
	}

	[Fact]
	public async Task Query_by_actor_returns_matching_events()
	{
		await InitializeAnnotationTableAsync();
		var store = CreateStore();

		// Use a unique tag to isolate this test's data from other tests
		// that also use DefaultActor in the shared SQL Server container.
		var isolationTag = $"actor-query-{Guid.NewGuid():N}";
		var evt1 = UniqueEventId();
		var evt2 = UniqueEventId();

		await store.TagAsync(evt1, new[] { isolationTag }, TestCancellationToken);
		await store.TagAsync(evt2, new[] { isolationTag }, TestCancellationToken);

		// Query by both actor AND the unique tag to get deterministic results
		var results = await store.QueryByAnnotationAsync(
			new AuditAnnotationQuery { ActorId = DefaultActor, Tags = new[] { isolationTag } },
			TestCancellationToken);

		results.Count.ShouldBe(2);
		results.ShouldContain(evt1);
		results.ShouldContain(evt2);
	}

	[Fact]
	public async Task Query_by_since_returns_recent_events_only()
	{
		await InitializeAnnotationTableAsync();
		var store = CreateStore();

		var evt1 = UniqueEventId();
		await store.TagAsync(evt1, new[] { "old" }, TestCancellationToken);

		// Query with Since = now should exclude the event we just created
		// (it was created before "now" from the query perspective)
		var future = DateTimeOffset.UtcNow.AddMinutes(5);
		var results = await store.QueryByAnnotationAsync(
			new AuditAnnotationQuery { Since = future },
			TestCancellationToken);

		results.Count.ShouldBe(0);
	}

	[Fact]
	public async Task Query_with_skip_and_max_results_paginates()
	{
		await InitializeAnnotationTableAsync();
		var store = CreateStore();

		// Create 5 events with tags
		var eventIds = new List<string>();
		for (var i = 0; i < 5; i++)
		{
			var eid = UniqueEventId();
			eventIds.Add(eid);
			await store.TagAsync(eid, new[] { "paginate" }, TestCancellationToken);
		}

		var page1 = await store.QueryByAnnotationAsync(
			new AuditAnnotationQuery { Tags = new[] { "paginate" }, Skip = 0, MaxResults = 2 },
			TestCancellationToken);

		var page2 = await store.QueryByAnnotationAsync(
			new AuditAnnotationQuery { Tags = new[] { "paginate" }, Skip = 2, MaxResults = 2 },
			TestCancellationToken);

		page1.Count.ShouldBe(2);
		page2.Count.ShouldBe(2);

		// Pages should not overlap
		page1.Intersect(page2).Count().ShouldBe(0);
	}

	[Fact]
	public async Task Query_by_bookmarked_true_returns_bookmarked_events()
	{
		await InitializeAnnotationTableAsync();
		var store = CreateStore();

		var bookmarked = UniqueEventId();
		var notBookmarked = UniqueEventId();

		await store.TagAsync(bookmarked, new[] { "x" }, TestCancellationToken);
		await store.TagAsync(notBookmarked, new[] { "x" }, TestCancellationToken);
		await store.BookmarkAsync(bookmarked, "mark", TestCancellationToken);

		var results = await store.QueryByAnnotationAsync(
			new AuditAnnotationQuery { IsBookmarked = true },
			TestCancellationToken);

		results.ShouldContain(bookmarked);
		results.ShouldNotContain(notBookmarked);
	}

	[Fact]
	public async Task Query_by_has_notes_true_returns_annotated_events()
	{
		await InitializeAnnotationTableAsync();
		var store = CreateStore();

		var withNote = UniqueEventId();
		var withoutNote = UniqueEventId();

		await store.TagAsync(withNote, new[] { "x" }, TestCancellationToken);
		await store.TagAsync(withoutNote, new[] { "x" }, TestCancellationToken);
		await store.AnnotateAsync(withNote, "A note", TestCancellationToken);

		var results = await store.QueryByAnnotationAsync(
			new AuditAnnotationQuery { HasNotes = true },
			TestCancellationToken);

		results.ShouldContain(withNote);
		results.ShouldNotContain(withoutNote);
	}

	#endregion

	#region Concurrent Writes

	[Fact]
	public async Task Concurrent_annotations_on_same_event_all_stored()
	{
		await InitializeAnnotationTableAsync();
		var store = CreateStore();
		var eventId = UniqueEventId();

		// 10+ parallel annotations
		var tasks = Enumerable.Range(0, 15)
			.Select(i => store.AnnotateAsync(eventId, $"note-{i}", TestCancellationToken));

		var noteIds = await Task.WhenAll(tasks);

		noteIds.Length.ShouldBe(15);
		noteIds.Select(n => n.Value).Distinct().Count().ShouldBe(15);

		var annotations = await store.GetAnnotationsAsync(eventId, TestCancellationToken);
		annotations.Notes.Count.ShouldBe(15);
	}

	[Fact]
	public async Task Concurrent_tags_on_same_event_are_idempotent()
	{
		await InitializeAnnotationTableAsync();
		var store = CreateStore();
		var eventId = UniqueEventId();

		// 10 parallel identical tag operations
		var tasks = Enumerable.Range(0, 10)
			.Select(_ => store.TagAsync(eventId, new[] { "concurrent-tag" }, TestCancellationToken));

		await Task.WhenAll(tasks);

		var annotations = await store.GetAnnotationsAsync(eventId, TestCancellationToken);
		annotations.Tags.Count.ShouldBe(1);
		annotations.Tags[0].ShouldBe("concurrent-tag");
	}

	#endregion

	#region Empty Results

	[Fact]
	public async Task Get_annotations_for_nonexistent_event_returns_empty()
	{
		await InitializeAnnotationTableAsync();
		var store = CreateStore();

		var annotations = await store.GetAnnotationsAsync("nonexistent-event-id", TestCancellationToken);

		annotations.EventId.ShouldBe("nonexistent-event-id");
		annotations.Tags.Count.ShouldBe(0);
		annotations.Bookmarks.Count.ShouldBe(0);
		annotations.Notes.Count.ShouldBe(0);
	}

	[Fact]
	public async Task Query_nonexistent_tags_returns_empty()
	{
		await InitializeAnnotationTableAsync();
		var store = CreateStore();

		var results = await store.QueryByAnnotationAsync(
			new AuditAnnotationQuery { Tags = new[] { "does-not-exist" } },
			TestCancellationToken);

		results.Count.ShouldBe(0);
	}

	#endregion

	#region Cancellation

	[Fact]
	public async Task Tag_with_cancelled_token_throws_operation_cancelled()
	{
		await InitializeAnnotationTableAsync();
		var store = CreateStore();

		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();

		await Should.ThrowAsync<OperationCanceledException>(
			() => store.TagAsync("evt-cancel", new[] { "tag" }, cts.Token));
	}

	[Fact]
	public async Task Bookmark_with_cancelled_token_throws_operation_cancelled()
	{
		await InitializeAnnotationTableAsync();
		var store = CreateStore();

		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();

		await Should.ThrowAsync<OperationCanceledException>(
			() => store.BookmarkAsync("evt-cancel", "label", cts.Token));
	}

	[Fact]
	public async Task Annotate_with_cancelled_token_throws_operation_cancelled()
	{
		await InitializeAnnotationTableAsync();
		var store = CreateStore();

		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();

		await Should.ThrowAsync<OperationCanceledException>(
			() => store.AnnotateAsync("evt-cancel", "note", cts.Token));
	}

	[Fact]
	public async Task Query_with_cancelled_token_throws_operation_cancelled()
	{
		await InitializeAnnotationTableAsync();
		var store = CreateStore();

		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();

		await Should.ThrowAsync<OperationCanceledException>(
			() => store.QueryByAnnotationAsync(new AuditAnnotationQuery(), cts.Token));
	}

	#endregion

	#region Helpers

	private IAuditAnnotationStore CreateStore()
	{
		var services = new ServiceCollection();

		services.AddSqlServerAuditAnnotationStore(opts =>
		{
			opts.ConnectionString = _fixture.ConnectionString;
			opts.SchemaName = "audit";
			opts.TableName = "AuditAnnotations";
			opts.CommandTimeoutSeconds = 30;
		});

		// Register the fake actor provider
		var actorProvider = A.Fake<IAuditActorProvider>();
		A.CallTo(() => actorProvider.GetCurrentActorIdAsync(A<CancellationToken>._))
			.Returns(Task.FromResult(DefaultActor));
		services.AddSingleton(actorProvider);

		services.AddSingleton(TimeProvider.System);
		services.AddLogging();

		var sp = services.BuildServiceProvider();
		return sp.GetRequiredService<IAuditAnnotationStore>();
	}

	private static string UniqueEventId() => $"evt-{Guid.NewGuid():N}";

	private async Task InitializeAnnotationTableAsync()
	{
		const string createSchemaAndTableSql = """
			IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'audit')
			BEGIN
			    EXEC('CREATE SCHEMA [audit]');
			END;

			IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[audit].[AuditAnnotations]') AND type in (N'U'))
			BEGIN
			    CREATE TABLE [audit].[AuditAnnotations] (
			        [Id] NVARCHAR(64) NOT NULL,
			        [EventId] NVARCHAR(64) NOT NULL,
			        [AnnotationType] INT NOT NULL,
			        [Content] NVARCHAR(MAX) NOT NULL,
			        [ActorId] NVARCHAR(256) NOT NULL,
			        [CreatedAt] DATETIMEOFFSET(7) NOT NULL,
			        [Visibility] INT NOT NULL,
			        CONSTRAINT [PK_AuditAnnotations] PRIMARY KEY CLUSTERED ([Id]),
			        INDEX [IX_AuditAnnotations_EventId] NONCLUSTERED ([EventId]),
			        INDEX [IX_AuditAnnotations_EventId_Type] NONCLUSTERED ([EventId], [AnnotationType])
			    );
			END;

			-- NOTE: No blanket DELETE. Each test uses UniqueEventId() for data isolation,
			-- avoiding race conditions when tests share the same SQL Server container.
			""";

		await using var connection = new SqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync(TestCancellationToken);
		_ = await connection.ExecuteAsync(createSchemaAndTableSql);
	}

	private async Task ExecuteSqlAsync(string sql, object? parameters = null)
	{
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities - test code with controlled input
		await using var connection = new SqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync(TestCancellationToken);
		_ = await connection.ExecuteAsync(sql, parameters);
#pragma warning restore CA2100
	}

	#endregion
}