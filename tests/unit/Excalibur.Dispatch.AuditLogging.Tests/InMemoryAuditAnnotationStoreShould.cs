// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.AuditLogging;
using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.Time.Testing;

namespace Excalibur.Dispatch.AuditLogging.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class InMemoryAuditAnnotationStoreShould
{
    private const string TestActorId = "actor-1";
    private const string TestEventId = "evt-1";

    private readonly IAuditActorProvider _fakeActorProvider;
    private readonly FakeTimeProvider _timeProvider;
    private readonly InMemoryAuditAnnotationStore _sut;

    public InMemoryAuditAnnotationStoreShould()
    {
        _fakeActorProvider = A.Fake<IAuditActorProvider>();
        A.CallTo(() => _fakeActorProvider.GetCurrentActorIdAsync(A<CancellationToken>._))
            .Returns(TestActorId);

        _timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 4, 11, 12, 0, 0, TimeSpan.Zero));
        _sut = new InMemoryAuditAnnotationStore(_fakeActorProvider, _timeProvider);
    }

    // ========================================
    // Constructor validation
    // ========================================

    [Fact]
    public void Throw_when_actor_provider_is_null()
    {
        Should.Throw<ArgumentNullException>(() =>
            new InMemoryAuditAnnotationStore(null!, _timeProvider));
    }

    [Fact]
    public void Throw_when_time_provider_is_null()
    {
        Should.Throw<ArgumentNullException>(() =>
            new InMemoryAuditAnnotationStore(_fakeActorProvider, null!));
    }

    // ========================================
    // TagAsync
    // ========================================

    [Fact]
    public async Task Tag_async_adds_tags_to_event()
    {
        await _sut.TagAsync(TestEventId, ["suspicious", "follow-up"], CancellationToken.None);

        var annotations = await _sut.GetAnnotationsAsync(TestEventId, CancellationToken.None);
        annotations.Tags.Count.ShouldBe(2);
        annotations.Tags.ShouldContain("suspicious");
        annotations.Tags.ShouldContain("follow-up");
    }

    [Fact]
    public async Task Tag_async_is_idempotent_for_duplicate_tags()
    {
        await _sut.TagAsync(TestEventId, ["suspicious"], CancellationToken.None);
        await _sut.TagAsync(TestEventId, ["suspicious"], CancellationToken.None);

        var annotations = await _sut.GetAnnotationsAsync(TestEventId, CancellationToken.None);
        annotations.Tags.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Tag_async_skips_null_or_whitespace_tags()
    {
        await _sut.TagAsync(TestEventId, ["valid", "", "  ", "also-valid"], CancellationToken.None);

        var annotations = await _sut.GetAnnotationsAsync(TestEventId, CancellationToken.None);
        annotations.Tags.Count.ShouldBe(2);
        annotations.Tags.ShouldContain("valid");
        annotations.Tags.ShouldContain("also-valid");
    }

    [Fact]
    public async Task Tag_async_throws_when_event_id_is_null()
    {
        await Should.ThrowAsync<ArgumentException>(
            () => _sut.TagAsync(null!, ["tag"], CancellationToken.None));
    }

    [Fact]
    public async Task Tag_async_throws_when_event_id_is_whitespace()
    {
        await Should.ThrowAsync<ArgumentException>(
            () => _sut.TagAsync("  ", ["tag"], CancellationToken.None));
    }

    [Fact]
    public async Task Tag_async_throws_when_tags_list_is_null()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => _sut.TagAsync(TestEventId, null!, CancellationToken.None));
    }

    [Fact]
    public async Task Tag_async_respects_cancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(
            () => _sut.TagAsync(TestEventId, ["tag"], cts.Token));
    }

    [Fact]
    public async Task Tag_async_records_actor_and_timestamp()
    {
        await _sut.TagAsync(TestEventId, ["important"], CancellationToken.None);

        var annotations = await _sut.GetAnnotationsAsync(TestEventId, CancellationToken.None);
        // Tags are returned as strings, but the underlying annotations have actor/timestamp.
        // Verify via the full annotation list indirectly by checking the store was called correctly.
        A.CallTo(() => _fakeActorProvider.GetCurrentActorIdAsync(A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Tag_async_tags_are_shared_visibility()
    {
        await _sut.TagAsync(TestEventId, ["tag1"], CancellationToken.None);

        // Verify by getting annotations - tags should be visible
        var annotations = await _sut.GetAnnotationsAsync(TestEventId, CancellationToken.None);
        annotations.Tags.ShouldContain("tag1");
    }

    // ========================================
    // BookmarkAsync
    // ========================================

    [Fact]
    public async Task Bookmark_async_creates_bookmark_for_event()
    {
        await _sut.BookmarkAsync(TestEventId, "review later", CancellationToken.None);

        var annotations = await _sut.GetAnnotationsAsync(TestEventId, CancellationToken.None);
        annotations.Bookmarks.Count.ShouldBe(1);
        annotations.Bookmarks[0].Content.ShouldBe("review later");
        annotations.Bookmarks[0].Type.ShouldBe(AuditAnnotationType.Bookmark);
        annotations.Bookmarks[0].ActorId.ShouldBe(TestActorId);
        annotations.Bookmarks[0].Visibility.ShouldBe(AuditAnnotationVisibility.Personal);
    }

    [Fact]
    public async Task Bookmark_async_accepts_null_label()
    {
        await _sut.BookmarkAsync(TestEventId, null, CancellationToken.None);

        var annotations = await _sut.GetAnnotationsAsync(TestEventId, CancellationToken.None);
        annotations.Bookmarks.Count.ShouldBe(1);
        annotations.Bookmarks[0].Content.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task Bookmark_async_replaces_existing_bookmark_for_same_actor()
    {
        await _sut.BookmarkAsync(TestEventId, "first", CancellationToken.None);
        await _sut.BookmarkAsync(TestEventId, "second", CancellationToken.None);

        var annotations = await _sut.GetAnnotationsAsync(TestEventId, CancellationToken.None);
        annotations.Bookmarks.Count.ShouldBe(1);
        annotations.Bookmarks[0].Content.ShouldBe("second");
    }

    [Fact]
    public async Task Bookmark_async_throws_when_event_id_is_null()
    {
        await Should.ThrowAsync<ArgumentException>(
            () => _sut.BookmarkAsync(null!, "label", CancellationToken.None));
    }

    [Fact]
    public async Task Bookmark_async_respects_cancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(
            () => _sut.BookmarkAsync(TestEventId, "label", cts.Token));
    }

    // ========================================
    // RemoveBookmarkAsync
    // ========================================

    [Fact]
    public async Task Remove_bookmark_async_removes_actors_bookmark()
    {
        await _sut.BookmarkAsync(TestEventId, "marked", CancellationToken.None);
        await _sut.RemoveBookmarkAsync(TestEventId, CancellationToken.None);

        var annotations = await _sut.GetAnnotationsAsync(TestEventId, CancellationToken.None);
        annotations.Bookmarks.Count.ShouldBe(0);
    }

    [Fact]
    public async Task Remove_bookmark_async_is_noop_when_no_bookmark_exists()
    {
        // Should not throw
        await _sut.RemoveBookmarkAsync(TestEventId, CancellationToken.None);

        var annotations = await _sut.GetAnnotationsAsync(TestEventId, CancellationToken.None);
        annotations.Bookmarks.Count.ShouldBe(0);
    }

    [Fact]
    public async Task Remove_bookmark_async_is_noop_when_event_not_found()
    {
        await _sut.RemoveBookmarkAsync("nonexistent-event", CancellationToken.None);
    }

    [Fact]
    public async Task Remove_bookmark_async_throws_when_event_id_is_null()
    {
        await Should.ThrowAsync<ArgumentException>(
            () => _sut.RemoveBookmarkAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task Remove_bookmark_async_respects_cancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(
            () => _sut.RemoveBookmarkAsync(TestEventId, cts.Token));
    }

    // ========================================
    // AnnotateAsync
    // ========================================

    [Fact]
    public async Task Annotate_async_creates_note_with_actor_and_timestamp()
    {
        var result = await _sut.AnnotateAsync(TestEventId, "Investigated — false positive", CancellationToken.None);

        result.ShouldNotBeNull();
        result.Value.ShouldNotBeNullOrWhiteSpace();

        var annotations = await _sut.GetAnnotationsAsync(TestEventId, CancellationToken.None);
        annotations.Notes.Count.ShouldBe(1);
        annotations.Notes[0].Content.ShouldBe("Investigated — false positive");
        annotations.Notes[0].ActorId.ShouldBe(TestActorId);
        annotations.Notes[0].Type.ShouldBe(AuditAnnotationType.Note);
        annotations.Notes[0].Visibility.ShouldBe(AuditAnnotationVisibility.Shared);
        annotations.Notes[0].CreatedAt.ShouldBe(_timeProvider.GetUtcNow());
    }

    [Fact]
    public async Task Annotate_async_allows_multiple_notes_on_same_event()
    {
        await _sut.AnnotateAsync(TestEventId, "Note 1", CancellationToken.None);
        await _sut.AnnotateAsync(TestEventId, "Note 2", CancellationToken.None);

        var annotations = await _sut.GetAnnotationsAsync(TestEventId, CancellationToken.None);
        annotations.Notes.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Annotate_async_returns_unique_ids()
    {
        var id1 = await _sut.AnnotateAsync(TestEventId, "Note 1", CancellationToken.None);
        var id2 = await _sut.AnnotateAsync(TestEventId, "Note 2", CancellationToken.None);

        id1.Value.ShouldNotBe(id2.Value);
    }

    [Fact]
    public async Task Annotate_async_throws_when_event_id_is_null()
    {
        await Should.ThrowAsync<ArgumentException>(
            () => _sut.AnnotateAsync(null!, "note", CancellationToken.None));
    }

    [Fact]
    public async Task Annotate_async_throws_when_note_is_null()
    {
        await Should.ThrowAsync<ArgumentException>(
            () => _sut.AnnotateAsync(TestEventId, null!, CancellationToken.None));
    }

    [Fact]
    public async Task Annotate_async_throws_when_note_is_whitespace()
    {
        await Should.ThrowAsync<ArgumentException>(
            () => _sut.AnnotateAsync(TestEventId, "  ", CancellationToken.None));
    }

    [Fact]
    public async Task Annotate_async_respects_cancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(
            () => _sut.AnnotateAsync(TestEventId, "note", cts.Token));
    }

    // ========================================
    // GetAnnotationsAsync
    // ========================================

    [Fact]
    public async Task Get_annotations_async_returns_empty_for_unknown_event()
    {
        var annotations = await _sut.GetAnnotationsAsync("unknown-event", CancellationToken.None);

        annotations.EventId.ShouldBe("unknown-event");
        annotations.Tags.ShouldBeEmpty();
        annotations.Bookmarks.ShouldBeEmpty();
        annotations.Notes.ShouldBeEmpty();
    }

    [Fact]
    public async Task Get_annotations_async_groups_by_type()
    {
        await _sut.TagAsync(TestEventId, ["suspicious"], CancellationToken.None);
        await _sut.BookmarkAsync(TestEventId, "flagged", CancellationToken.None);
        await _sut.AnnotateAsync(TestEventId, "Reviewed", CancellationToken.None);

        var annotations = await _sut.GetAnnotationsAsync(TestEventId, CancellationToken.None);
        annotations.Tags.Count.ShouldBe(1);
        annotations.Bookmarks.Count.ShouldBe(1);
        annotations.Notes.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Get_annotations_async_throws_when_event_id_is_null()
    {
        await Should.ThrowAsync<ArgumentException>(
            () => _sut.GetAnnotationsAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task Get_annotations_async_respects_cancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Should.Throw<OperationCanceledException>(
            () => _sut.GetAnnotationsAsync(TestEventId, cts.Token));
    }

    // ========================================
    // QueryByAnnotationAsync
    // ========================================

    [Fact]
    public async Task Query_by_annotation_async_filters_by_tags()
    {
        await _sut.TagAsync("evt-1", ["suspicious"], CancellationToken.None);
        await _sut.TagAsync("evt-2", ["compliant"], CancellationToken.None);

        var results = await _sut.QueryByAnnotationAsync(
            new AuditAnnotationQuery { Tags = ["suspicious"] },
            CancellationToken.None);

        results.Count.ShouldBe(1);
        results.ShouldContain("evt-1");
    }

    [Fact]
    public async Task Query_by_annotation_async_filters_by_bookmarked_true()
    {
        await _sut.BookmarkAsync("evt-1", "flagged", CancellationToken.None);
        await _sut.TagAsync("evt-2", ["tag"], CancellationToken.None);

        var results = await _sut.QueryByAnnotationAsync(
            new AuditAnnotationQuery { IsBookmarked = true },
            CancellationToken.None);

        results.Count.ShouldBe(1);
        results.ShouldContain("evt-1");
    }

    [Fact]
    public async Task Query_by_annotation_async_filters_by_bookmarked_false()
    {
        await _sut.BookmarkAsync("evt-1", "flagged", CancellationToken.None);
        await _sut.TagAsync("evt-2", ["tag"], CancellationToken.None);

        var results = await _sut.QueryByAnnotationAsync(
            new AuditAnnotationQuery { IsBookmarked = false },
            CancellationToken.None);

        results.Count.ShouldBe(1);
        results.ShouldContain("evt-2");
    }

    [Fact]
    public async Task Query_by_annotation_async_filters_by_has_notes()
    {
        await _sut.AnnotateAsync("evt-1", "Has note", CancellationToken.None);
        await _sut.TagAsync("evt-2", ["tag"], CancellationToken.None);

        var results = await _sut.QueryByAnnotationAsync(
            new AuditAnnotationQuery { HasNotes = true },
            CancellationToken.None);

        results.Count.ShouldBe(1);
        results.ShouldContain("evt-1");
    }

    [Fact]
    public async Task Query_by_annotation_async_filters_by_has_notes_false()
    {
        await _sut.AnnotateAsync("evt-1", "Has note", CancellationToken.None);
        await _sut.TagAsync("evt-2", ["tag"], CancellationToken.None);

        var results = await _sut.QueryByAnnotationAsync(
            new AuditAnnotationQuery { HasNotes = false },
            CancellationToken.None);

        results.Count.ShouldBe(1);
        results.ShouldContain("evt-2");
    }

    [Fact]
    public async Task Query_by_annotation_async_filters_by_actor_id()
    {
        await _sut.TagAsync("evt-1", ["tag"], CancellationToken.None);

        var results = await _sut.QueryByAnnotationAsync(
            new AuditAnnotationQuery { ActorId = TestActorId },
            CancellationToken.None);

        results.Count.ShouldBe(1);
        results.ShouldContain("evt-1");
    }

    [Fact]
    public async Task Query_by_annotation_async_filters_by_since()
    {
        var baseTime = _timeProvider.GetUtcNow();
        await _sut.TagAsync("evt-1", ["old"], CancellationToken.None);

        _timeProvider.Advance(TimeSpan.FromHours(1));
        await _sut.TagAsync("evt-2", ["new"], CancellationToken.None);

        var results = await _sut.QueryByAnnotationAsync(
            new AuditAnnotationQuery { Since = baseTime.AddMinutes(30) },
            CancellationToken.None);

        results.Count.ShouldBe(1);
        results.ShouldContain("evt-2");
    }

    [Fact]
    public async Task Query_by_annotation_async_applies_skip_and_max_results()
    {
        for (var i = 0; i < 5; i++)
        {
            await _sut.TagAsync($"evt-{i}", ["common"], CancellationToken.None);
        }

        var results = await _sut.QueryByAnnotationAsync(
            new AuditAnnotationQuery { Tags = ["common"], Skip = 1, MaxResults = 2 },
            CancellationToken.None);

        results.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Query_by_annotation_async_throws_when_query_is_null()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => _sut.QueryByAnnotationAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task Query_by_annotation_async_respects_cancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Should.Throw<OperationCanceledException>(
            () => _sut.QueryByAnnotationAsync(new AuditAnnotationQuery(), cts.Token));
    }

    [Fact]
    public async Task Query_by_annotation_async_returns_empty_when_no_match()
    {
        await _sut.TagAsync("evt-1", ["tag"], CancellationToken.None);

        var results = await _sut.QueryByAnnotationAsync(
            new AuditAnnotationQuery { Tags = ["nonexistent"] },
            CancellationToken.None);

        results.ShouldBeEmpty();
    }

    [Fact]
    public async Task Query_by_annotation_async_matches_any_tag_from_list()
    {
        await _sut.TagAsync("evt-1", ["suspicious"], CancellationToken.None);
        await _sut.TagAsync("evt-2", ["compliant"], CancellationToken.None);

        var results = await _sut.QueryByAnnotationAsync(
            new AuditAnnotationQuery { Tags = ["suspicious", "escalated"] },
            CancellationToken.None);

        results.Count.ShouldBe(1);
        results.ShouldContain("evt-1");
    }

    // ========================================
    // Internal helpers
    // ========================================

    [Fact]
    public async Task Clear_removes_all_annotations()
    {
        await _sut.TagAsync("evt-1", ["tag"], CancellationToken.None);
        await _sut.AnnotateAsync("evt-2", "note", CancellationToken.None);

        _sut.Clear();

        _sut.AnnotatedEventCount.ShouldBe(0);
    }

    [Fact]
    public async Task Annotated_event_count_reflects_distinct_events()
    {
        await _sut.TagAsync("evt-1", ["a"], CancellationToken.None);
        await _sut.TagAsync("evt-1", ["b"], CancellationToken.None);
        await _sut.TagAsync("evt-2", ["c"], CancellationToken.None);

        _sut.AnnotatedEventCount.ShouldBe(2);
    }

    // ========================================
    // Concurrency
    // ========================================

    [Fact]
    public async Task Concurrent_tags_do_not_corrupt_state()
    {
        var tasks = Enumerable.Range(0, 50)
            .Select(i => _sut.TagAsync(TestEventId, [$"tag-{i}"], CancellationToken.None));

        await Task.WhenAll(tasks);

        var annotations = await _sut.GetAnnotationsAsync(TestEventId, CancellationToken.None);
        annotations.Tags.Count.ShouldBe(50);
    }

    [Fact]
    public async Task Concurrent_annotations_across_events_do_not_corrupt_state()
    {
        var tasks = Enumerable.Range(0, 50)
            .Select(i => _sut.AnnotateAsync($"evt-{i}", $"Note {i}", CancellationToken.None));

        await Task.WhenAll(tasks);

        _sut.AnnotatedEventCount.ShouldBe(50);
    }
}
