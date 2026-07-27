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
		var eventId = await NewEventAsync();

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
		var eventId = await NewEventAsync();

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
		var eventId = await NewEventAsync();

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
		var eventId = await NewEventAsync();

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
		var eventId = await NewEventAsync();

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
		var eventId = await NewEventAsync();
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

		var evt1 = await NewEventAsync();
		var evt2 = await NewEventAsync();
		var evt3 = await NewEventAsync();

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
		var evt1 = await NewEventAsync();
		var evt2 = await NewEventAsync();

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

		var evt1 = await NewEventAsync();
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
			var eid = await NewEventAsync();
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

		var bookmarked = await NewEventAsync();
		var notBookmarked = await NewEventAsync();

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

		var withNote = await NewEventAsync();
		var withoutNote = await NewEventAsync();

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
		var eventId = await NewEventAsync();

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
		var eventId = await NewEventAsync();

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

	#region Cross-Tenant Isolation

	/// <summary>
	/// SAFETY. One actor identity spanning two tenants — a shared operator or service account, the ordinary
	/// case in multi-tenant SaaS — must not let tenant B's bookmark overwrite tenant A's annotation of tenant
	/// A's event.
	/// </summary>
	/// <remarks>
	/// This is a cross-tenant WRITE, not a read leak: the MERGE matched on
	/// <c>(EventId, ActorId, AnnotationType)</c> with no tenant term, so B took the MATCHED branch against A's
	/// row and replaced its Content. The victim's data is destroyed, and nothing in A's tenant ever shows an
	/// error. Asserted against real SQL Server because the defect lives in the server's MERGE matching, which
	/// no mocked command can reproduce.
	/// </remarks>
	[Fact]
	public async Task Not_let_a_shared_actor_overwrite_another_tenants_bookmark()
	{
		await InitializeAnnotationTableAsync();

		const string sharedActor = "shared-operator@example.com";
		var tenantAEvent = await NewEventAsync("tenant-a");

		var storeA = CreateStoreForTenant("tenant-a", sharedActor);
		var storeB = CreateStoreForTenant("tenant-b", sharedActor);

		await storeA.BookmarkAsync(tenantAEvent, "tenant A's own label", TestCancellationToken);

		// Tenant B bookmarks an event id belonging to tenant A. B may legitimately KNOW the id — ids leak
		// through logs, URLs and support tickets — so guessing it must simply achieve nothing.
		await storeB.BookmarkAsync(tenantAEvent, "TENANT B OVERWRITE", TestCancellationToken);

		var content = await ReadBookmarkContentAsync(tenantAEvent, sharedActor);

		content.ShouldBe(
			"tenant A's own label",
			"tenant B's write must not reach tenant A's annotation — a MERGE that matches without a tenant term DESTROYS the victim's content, silently and with no error on either side.");
	}

	/// <summary>
	/// SAFETY, second direction: B's out-of-scope bookmark must not be INSERTED either. Refusing the overwrite
	/// while inserting a second row would leave B holding an annotation against A's event.
	/// </summary>
	[Fact]
	public async Task Not_insert_a_bookmark_against_another_tenants_event()
	{
		await InitializeAnnotationTableAsync();

		const string sharedActor = "shared-operator-2@example.com";
		var tenantAEvent = await NewEventAsync("tenant-a");

		await CreateStoreForTenant("tenant-b", sharedActor)
			.BookmarkAsync(tenantAEvent, "B should get nothing", TestCancellationToken);

		var rows = await QuerySingleIntAsync(
			"SELECT COUNT(*) FROM [audit].[AuditAnnotations] WHERE EventId = @EventId",
			new { EventId = tenantAEvent });

		rows.ShouldBe(
			0,
			"an out-of-scope bookmark must produce NO row: the tenant term restricts the MERGE's USING source, so the source is empty and WHEN NOT MATCHED is unreachable.");
	}

	/// <summary>
	/// LIVENESS. The arms above are satisfied by a store that writes nothing for anyone, which would be a
	/// perfectly isolated and completely useless audit log. A tenant must still be able to bookmark its OWN
	/// event, and re-bookmarking must still REPLACE rather than duplicate.
	/// </summary>
	[Fact]
	public async Task Still_bookmark_and_replace_within_the_callers_own_tenant()
	{
		await InitializeAnnotationTableAsync();

		const string actor = "tenant-b-operator@example.com";
		var tenantBEvent = await NewEventAsync("tenant-b");
		var storeB = CreateStoreForTenant("tenant-b", actor);

		await storeB.BookmarkAsync(tenantBEvent, "first", TestCancellationToken);
		(await ReadBookmarkContentAsync(tenantBEvent, actor)).ShouldBe(
			"first",
			"a tenant must be able to bookmark its own event — isolation that also blocks the legitimate path is not isolation, it is an outage.");

		await storeB.BookmarkAsync(tenantBEvent, "second", TestCancellationToken);

		(await ReadBookmarkContentAsync(tenantBEvent, actor)).ShouldBe(
			"second",
			"replace semantics must survive the tenant restriction: the MATCHED branch must still be reachable WITHIN the caller's own tenant.");

		(await QuerySingleIntAsync(
			"SELECT COUNT(*) FROM [audit].[AuditAnnotations] WHERE EventId = @EventId AND ActorId = @ActorId",
			new { EventId = tenantBEvent, ActorId = actor }))
			.ShouldBe(1, "re-bookmarking must replace the row, never accumulate a second one.");
	}

	/// <summary>
	/// LIVENESS for the untenanted partition: a single-tenant host (no ambient tenant) must keep working. The
	/// sentinel fold is what makes this reachable, and it is the configuration every other arm in this file
	/// runs under.
	/// </summary>
	[Fact]
	public async Task Still_bookmark_when_no_ambient_tenant_is_resolved()
	{
		await InitializeAnnotationTableAsync();

		const string actor = "single-tenant-operator@example.com";
		var untenantedEvent = await NewEventAsync(tenantId: null);

		await CreateStoreForTenant(tenantId: null, actor)
			.BookmarkAsync(untenantedEvent, "single-tenant label", TestCancellationToken);

		(await ReadBookmarkContentAsync(untenantedEvent, actor)).ShouldBe(
			"single-tenant label",
			"a host with no ambient tenant must still write: the NULL TenantId folds onto the untenanted sentinel rather than failing to match.");
	}

	#endregion

	private async Task<int> QuerySingleIntAsync(string sql, object parameters)
	{
#pragma warning disable CA2100 // Test code with controlled input
		await using var connection = new SqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync(TestCancellationToken);
		return await connection.ExecuteScalarAsync<int>(sql, parameters);
#pragma warning restore CA2100
	}

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

	/// <summary>
	/// Creates a unique event id and seeds the audit EVENT row it names, untenanted.
	/// </summary>
	/// <remarks>
	/// Every annotation write derives its tenant by joining the events table — the annotations table carries no
	/// tenant column — so an annotation whose event does not exist is not merely untenanted, it is unreachable:
	/// the <c>USING</c> source is empty and nothing is written. Before that join existed an event id was just a
	/// string and no row had to back it, which is why these arms passed while seeding nothing.
	/// <para>
	/// The seeded row leaves <c>TenantId</c> NULL. The store folds NULL onto the untenanted sentinel
	/// (<c>COALESCE(ae.TenantId, '__untenanted__')</c>), so a store built with no ambient tenant reaches it —
	/// which is the configuration every pre-existing arm here uses.
	/// </para>
	/// </remarks>
	private async Task<string> NewEventAsync(string? tenantId = null)
	{
		var eventId = UniqueEventId();

		// Columns match the shipped audit-events shape exactly — this is the SAME table the audit store owns
		// (schema `audit`, name `AuditEvents`), shared with SqlServerAuditStoreIntegrationShould in this
		// collection. Inventing a narrower local shape here would race that fixture's `IF NOT EXISTS`: whichever
		// class ran first would win, and the other's inserts would fail on missing columns.
		await ExecuteSqlAsync(
			"""
			INSERT INTO [audit].[AuditEvents]
			    (EventId, EventType, Action, Outcome, [Timestamp], ActorId, TenantId, EventHash)
			VALUES
			    (@EventId, 0, 'test-seed', 0, @Timestamp, @ActorId, @TenantId, @EventHash)
			""",
			new
			{
				EventId = eventId,
				Timestamp = DateTimeOffset.UtcNow,
				ActorId = DefaultActor,
				TenantId = tenantId,
				EventHash = $"hash-{eventId}",
			});

		return eventId;
	}

	/// <summary>
	/// Builds the store bound to a specific ambient tenant, or to none when <paramref name="tenantId"/> is
	/// <see langword="null"/>.
	/// </summary>
	private IAuditAnnotationStore CreateStoreForTenant(string? tenantId, string actorId)
	{
		var services = new ServiceCollection();

		services.AddSqlServerAuditAnnotationStore(opts =>
		{
			opts.ConnectionString = _fixture.ConnectionString;
			opts.SchemaName = "audit";
			opts.TableName = "AuditAnnotations";
			opts.CommandTimeoutSeconds = 30;
		});

		var actorProvider = A.Fake<IAuditActorProvider>();
		A.CallTo(() => actorProvider.GetCurrentActorIdAsync(A<CancellationToken>._))
			.Returns(Task.FromResult(actorId));
		services.AddSingleton(actorProvider);

		if (tenantId is not null)
		{
			var tenantContext = A.Fake<ITenantContext>();
			A.CallTo(() => tenantContext.TenantId).Returns(tenantId);
			A.CallTo(() => tenantContext.HasTenant).Returns(true);
			services.AddSingleton(tenantContext);
		}

		services.AddSingleton(TimeProvider.System);
		services.AddLogging();

		var sp = services.BuildServiceProvider();
		return sp.GetRequiredService<IAuditAnnotationStore>();
	}

	private async Task<string?> ReadBookmarkContentAsync(string eventId, string actorId)
	{
		await using var connection = new SqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync(TestCancellationToken);

		return await connection.QuerySingleOrDefaultAsync<string>(
			"SELECT Content FROM [audit].[AuditAnnotations] WHERE EventId = @EventId AND ActorId = @ActorId AND AnnotationType = @Type",
			new { EventId = eventId, ActorId = actorId, Type = (int)AuditAnnotationType.Bookmark });
	}

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

			-- The EVENTS table is where an annotation's tenant lives. The annotations table has no tenant
			-- column by design, so every tenant-scoped statement in the store joins here and folds a NULL
			-- TenantId onto the untenanted sentinel. Without this table the store's statements reference a
			-- missing object and every write fails; with it but WITHOUT TenantId, the join predicate cannot
			-- compile — which is why this column, not just this table, is load-bearing for the isolation arms.
			IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[audit].[AuditEvents]') AND type in (N'U'))
			BEGIN
			    CREATE TABLE [audit].[AuditEvents] (
			        [SequenceNumber] BIGINT IDENTITY(1,1) NOT NULL,
			        [EventId] NVARCHAR(64) NOT NULL,
			        [EventType] INT NOT NULL,
			        [Action] NVARCHAR(100) NOT NULL,
			        [Outcome] INT NOT NULL,
			        [Timestamp] DATETIMEOFFSET(7) NOT NULL,
			        [ActorId] NVARCHAR(256) NOT NULL,
			        [ActorType] NVARCHAR(50) NULL,
			        [ResourceId] NVARCHAR(256) NULL,
			        [ResourceType] NVARCHAR(100) NULL,
			        [ResourceClassification] INT NULL,
			        [TenantId] NVARCHAR(64) NULL,
			        [ApplicationName] NVARCHAR(256) NULL,
			        [CorrelationId] NVARCHAR(64) NULL,
			        [SessionId] NVARCHAR(64) NULL,
			        [IpAddress] NVARCHAR(45) NULL,
			        [UserAgent] NVARCHAR(500) NULL,
			        [Reason] NVARCHAR(1000) NULL,
			        [Metadata] NVARCHAR(MAX) NULL,
			        [PreviousEventHash] NVARCHAR(512) NULL,
			        [EventHash] NVARCHAR(512) NOT NULL,
			        CONSTRAINT [PK_AuditEvents] PRIMARY KEY CLUSTERED ([SequenceNumber] ASC),
			        CONSTRAINT [UQ_AuditEvents_EventId] UNIQUE NONCLUSTERED ([EventId])
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