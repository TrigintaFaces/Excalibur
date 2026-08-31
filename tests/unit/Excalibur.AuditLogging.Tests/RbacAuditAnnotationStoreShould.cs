// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.AuditLogging;
using Excalibur.Compliance;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.AuditLogging.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class RbacAuditAnnotationStoreShould
{
    private const string TestEventId = "evt-1";
    private const string TestActorId = "actor-1";

    private readonly IAuditAnnotationStore _fakeInnerStore;
    private readonly IAuditRoleProvider _fakeRoleProvider;
    private readonly IAuditActorProvider _fakeActorProvider;
    private readonly IAuditLogger _fakeMetaAuditLogger;
    private readonly ILogger<RbacAuditAnnotationStore> _logger;
    private readonly RbacAuditAnnotationStore _sut;

    public RbacAuditAnnotationStoreShould()
    {
        _fakeInnerStore = A.Fake<IAuditAnnotationStore>();
        _fakeRoleProvider = A.Fake<IAuditRoleProvider>();
        _fakeActorProvider = A.Fake<IAuditActorProvider>();
        _fakeMetaAuditLogger = A.Fake<IAuditLogger>();

        A.CallTo(() => _fakeActorProvider.GetCurrentActorIdAsync(A<CancellationToken>._))
            .Returns(TestActorId);
        A.CallTo(() => _fakeMetaAuditLogger.LogAsync(A<AuditEvent>._, A<CancellationToken>._))
            .Returns(CreateAuditEventId("meta-1"));

        // FakeItEasy cannot proxy ILogger<InternalType>, use NullLogger
        _logger = NullLogger<RbacAuditAnnotationStore>.Instance;

        _sut = new RbacAuditAnnotationStore(
            _fakeInnerStore,
            TestScopeFactory.For(_fakeRoleProvider, _fakeActorProvider, _fakeMetaAuditLogger),
            _logger);
    }

    // ========================================
    // Constructor validation
    // ========================================

    [Fact]
    public void Throw_when_inner_store_is_null()
    {
        Should.Throw<ArgumentNullException>(() =>
            new RbacAuditAnnotationStore(
                null!,
                TestScopeFactory.For(_fakeRoleProvider, _fakeActorProvider, _fakeMetaAuditLogger),
                _logger));
    }

    [Fact]
    public void Throw_when_scope_factory_is_null()
    {
        Should.Throw<ArgumentNullException>(() =>
            new RbacAuditAnnotationStore(_fakeInnerStore, null!, _logger));
    }

    [Fact]
    public async Task Throw_when_no_role_provider_is_registered()
    {
        // The role provider is an access-control input and is resolved per operation, so its absence is a
        // resolution failure on the first checked call rather than a constructor argument check. It must
        // still fail closed: a host that registered none is refused, never defaulted to a role.
        var store = new RbacAuditAnnotationStore(
            _fakeInnerStore,
            TestScopeFactory.For(actorProvider: _fakeActorProvider, metaAuditLogger: _fakeMetaAuditLogger),
            _logger);

        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => store.TagAsync(TestEventId, ["tag"], CancellationToken.None));
    }

    [Fact]
    public void Throw_when_logger_is_null()
    {
        Should.Throw<ArgumentNullException>(() =>
            new RbacAuditAnnotationStore(
                _fakeInnerStore,
                TestScopeFactory.For(_fakeRoleProvider, _fakeActorProvider, _fakeMetaAuditLogger),
                null!));
    }

    [Fact]
    public void Accept_no_registered_actor_provider()
    {
        // The actor provider stays optional; it is now absent from the scope rather than null in the ctor.
        var store = new RbacAuditAnnotationStore(
            _fakeInnerStore,
            TestScopeFactory.For(_fakeRoleProvider, metaAuditLogger: _fakeMetaAuditLogger),
            _logger);
        store.ShouldNotBeNull();
    }

    [Fact]
    public void Accept_no_registered_meta_audit_logger()
    {
        // The meta-audit logger stays optional on the ANNOTATION store.
        var store = new RbacAuditAnnotationStore(
            _fakeInnerStore,
            TestScopeFactory.For(_fakeRoleProvider, _fakeActorProvider),
            _logger);
        store.ShouldNotBeNull();
    }

    // ========================================
    // RBAC: Access denied for None/Developer roles
    // ========================================

    [Theory]
    [InlineData(AuditLogRole.None)]
    [InlineData(AuditLogRole.Developer)]
    public async Task Tag_async_denies_access_for_insufficient_role(AuditLogRole role)
    {
        SetRole(role);

        await Should.ThrowAsync<UnauthorizedAccessException>(
            () => _sut.TagAsync(TestEventId, ["tag"], CancellationToken.None));

        A.CallTo(() => _fakeInnerStore.TagAsync(A<string>._, A<IReadOnlyList<string>>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Theory]
    [InlineData(AuditLogRole.None)]
    [InlineData(AuditLogRole.Developer)]
    public async Task Bookmark_async_denies_access_for_insufficient_role(AuditLogRole role)
    {
        SetRole(role);

        await Should.ThrowAsync<UnauthorizedAccessException>(
            () => _sut.BookmarkAsync(TestEventId, "label", CancellationToken.None));

        A.CallTo(() => _fakeInnerStore.BookmarkAsync(A<string>._, A<string?>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Theory]
    [InlineData(AuditLogRole.None)]
    [InlineData(AuditLogRole.Developer)]
    public async Task Remove_bookmark_async_denies_access_for_insufficient_role(AuditLogRole role)
    {
        SetRole(role);

        await Should.ThrowAsync<UnauthorizedAccessException>(
            () => _sut.RemoveBookmarkAsync(TestEventId, CancellationToken.None));

        A.CallTo(() => _fakeInnerStore.RemoveBookmarkAsync(A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Theory]
    [InlineData(AuditLogRole.None)]
    [InlineData(AuditLogRole.Developer)]
    public async Task Annotate_async_denies_access_for_insufficient_role(AuditLogRole role)
    {
        SetRole(role);

        await Should.ThrowAsync<UnauthorizedAccessException>(
            () => _sut.AnnotateAsync(TestEventId, "note", CancellationToken.None));

        A.CallTo(() => _fakeInnerStore.AnnotateAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Theory]
    [InlineData(AuditLogRole.None)]
    [InlineData(AuditLogRole.Developer)]
    public async Task Get_annotations_async_denies_access_for_insufficient_role(AuditLogRole role)
    {
        SetRole(role);

        await Should.ThrowAsync<UnauthorizedAccessException>(
            () => _sut.GetAnnotationsAsync(TestEventId, CancellationToken.None));

        A.CallTo(() => _fakeInnerStore.GetAnnotationsAsync(A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Theory]
    [InlineData(AuditLogRole.None)]
    [InlineData(AuditLogRole.Developer)]
    public async Task Query_by_annotation_async_denies_access_for_insufficient_role(AuditLogRole role)
    {
        SetRole(role);

        await Should.ThrowAsync<UnauthorizedAccessException>(
            () => _sut.QueryByAnnotationAsync(new AuditAnnotationQuery(), CancellationToken.None));

        A.CallTo(() => _fakeInnerStore.QueryByAnnotationAsync(A<AuditAnnotationQuery>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    // ========================================
    // RBAC: Access granted for SecurityAnalyst+
    // ========================================

    [Theory]
    [InlineData(AuditLogRole.SecurityAnalyst)]
    [InlineData(AuditLogRole.ComplianceOfficer)]
    [InlineData(AuditLogRole.Administrator)]
    public async Task Tag_async_allows_access_for_authorized_roles(AuditLogRole role)
    {
        SetRole(role);

        await _sut.TagAsync(TestEventId, ["tag"], CancellationToken.None);

        A.CallTo(() => _fakeInnerStore.TagAsync(TestEventId, A<IReadOnlyList<string>>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData(AuditLogRole.SecurityAnalyst)]
    [InlineData(AuditLogRole.ComplianceOfficer)]
    [InlineData(AuditLogRole.Administrator)]
    public async Task Bookmark_async_allows_access_for_authorized_roles(AuditLogRole role)
    {
        SetRole(role);

        await _sut.BookmarkAsync(TestEventId, "label", CancellationToken.None);

        A.CallTo(() => _fakeInnerStore.BookmarkAsync(TestEventId, "label", A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData(AuditLogRole.SecurityAnalyst)]
    [InlineData(AuditLogRole.ComplianceOfficer)]
    [InlineData(AuditLogRole.Administrator)]
    public async Task Annotate_async_allows_access_for_authorized_roles(AuditLogRole role)
    {
        SetRole(role);
        A.CallTo(() => _fakeInnerStore.AnnotateAsync(TestEventId, "note", A<CancellationToken>._))
            .Returns(new AuditAnnotationId("ann-1"));

        var result = await _sut.AnnotateAsync(TestEventId, "note", CancellationToken.None);

        result.Value.ShouldBe("ann-1");
    }

    // ========================================
    // SecurityAnalyst: Shared-only visibility
    // ========================================

    [Fact]
    public async Task Security_analyst_sees_only_shared_annotations()
    {
        SetRole(AuditLogRole.SecurityAnalyst);

        var personalBookmark = new AuditAnnotation
        {
            Id = "b-1",
            EventId = TestEventId,
            Type = AuditAnnotationType.Bookmark,
            Content = "personal",
            ActorId = "other-actor",
            CreatedAt = DateTimeOffset.UtcNow,
            Visibility = AuditAnnotationVisibility.Personal
        };

        var sharedBookmark = new AuditAnnotation
        {
            Id = "b-2",
            EventId = TestEventId,
            Type = AuditAnnotationType.Bookmark,
            Content = "shared",
            ActorId = "other-actor",
            CreatedAt = DateTimeOffset.UtcNow,
            Visibility = AuditAnnotationVisibility.Shared
        };

        var personalNote = new AuditAnnotation
        {
            Id = "n-1",
            EventId = TestEventId,
            Type = AuditAnnotationType.Note,
            Content = "private note",
            ActorId = "other-actor",
            CreatedAt = DateTimeOffset.UtcNow,
            Visibility = AuditAnnotationVisibility.Personal
        };

        var sharedNote = new AuditAnnotation
        {
            Id = "n-2",
            EventId = TestEventId,
            Type = AuditAnnotationType.Note,
            Content = "shared note",
            ActorId = "other-actor",
            CreatedAt = DateTimeOffset.UtcNow,
            Visibility = AuditAnnotationVisibility.Shared
        };

        A.CallTo(() => _fakeInnerStore.GetAnnotationsAsync(TestEventId, A<CancellationToken>._))
            .Returns(new AuditAnnotations(
                TestEventId,
                ["tag1"],
                [personalBookmark, sharedBookmark],
                [personalNote, sharedNote]));

        var result = await _sut.GetAnnotationsAsync(TestEventId, CancellationToken.None);

        // Tags are always visible (they have no personal visibility concept)
        result.Tags.Count.ShouldBe(1);
        // Only shared bookmarks visible
        result.Bookmarks.Count.ShouldBe(1);
        result.Bookmarks[0].Visibility.ShouldBe(AuditAnnotationVisibility.Shared);
        // Only shared notes visible
        result.Notes.Count.ShouldBe(1);
        result.Notes[0].Visibility.ShouldBe(AuditAnnotationVisibility.Shared);
    }

    [Fact]
    public async Task Compliance_officer_sees_all_annotations()
    {
        SetRole(AuditLogRole.ComplianceOfficer);

        var personalBookmark = new AuditAnnotation
        {
            Id = "b-1",
            EventId = TestEventId,
            Type = AuditAnnotationType.Bookmark,
            Content = "personal",
            ActorId = "other-actor",
            CreatedAt = DateTimeOffset.UtcNow,
            Visibility = AuditAnnotationVisibility.Personal
        };

        var sharedBookmark = new AuditAnnotation
        {
            Id = "b-2",
            EventId = TestEventId,
            Type = AuditAnnotationType.Bookmark,
            Content = "shared",
            ActorId = "other-actor",
            CreatedAt = DateTimeOffset.UtcNow,
            Visibility = AuditAnnotationVisibility.Shared
        };

        A.CallTo(() => _fakeInnerStore.GetAnnotationsAsync(TestEventId, A<CancellationToken>._))
            .Returns(new AuditAnnotations(
                TestEventId,
                ["tag1"],
                [personalBookmark, sharedBookmark],
                []));

        var result = await _sut.GetAnnotationsAsync(TestEventId, CancellationToken.None);

        // This arm asserted 2 — that a ComplianceOfficer saw ANOTHER actor's PERSONAL annotation. That
        // privilege was deliberately removed: reads are scoped by authorship for every role, and no role
        // bypasses it. Administering the annotation log is not a licence to read private notes, so the
        // elevated role now sees shared annotations and its own, exactly like anyone else.
        // SAFETY: another actor's personal bookmark stays hidden even from the highest role.
        result.Bookmarks.ShouldNotContain(
            b => b.Id == "b-1",
            "a Personal annotation authored by other-actor must not be visible to a different actor, "
            + "whatever their role — administration is not authorship.");

        // LIVENESS: the shared one is still returned, so a filter that hid everything would not pass.
        result.Bookmarks.Count.ShouldBe(
            1,
            "the shared bookmark must still come back; 0 would mean the filter is blind rather than "
            + "selective, and 2 would mean the role bypass is back.");
        result.Bookmarks[0].Visibility.ShouldBe(AuditAnnotationVisibility.Shared);
    }

    // ========================================
    // Meta-audit logging
    // ========================================

    [Fact]
    public async Task Tag_async_emits_meta_audit_event()
    {
        SetRole(AuditLogRole.ComplianceOfficer);

        await _sut.TagAsync(TestEventId, ["suspicious"], CancellationToken.None);

        A.CallTo(() => _fakeMetaAuditLogger.LogAsync(
                A<AuditEvent>.That.Matches(e =>
                    e.EventType == AuditEventType.Administrative &&
                    e.Action == "AuditAnnotation.Tag" &&
                    e.Outcome == AuditOutcome.Success),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Annotate_async_emits_meta_audit_event()
    {
        SetRole(AuditLogRole.SecurityAnalyst);
        A.CallTo(() => _fakeInnerStore.AnnotateAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .Returns(new AuditAnnotationId("ann-1"));

        await _sut.AnnotateAsync(TestEventId, "note", CancellationToken.None);

        A.CallTo(() => _fakeMetaAuditLogger.LogAsync(
                A<AuditEvent>.That.Matches(e =>
                    e.EventType == AuditEventType.Administrative &&
                    e.Action == "AuditAnnotation.Annotate" &&
                    e.Reason!.Contains("ann-1")),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Bookmark_async_emits_meta_audit_event()
    {
        SetRole(AuditLogRole.Administrator);

        await _sut.BookmarkAsync(TestEventId, "label", CancellationToken.None);

        A.CallTo(() => _fakeMetaAuditLogger.LogAsync(
                A<AuditEvent>.That.Matches(e =>
                    e.Action == "AuditAnnotation.Bookmark"),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Remove_bookmark_async_emits_meta_audit_event()
    {
        SetRole(AuditLogRole.Administrator);

        await _sut.RemoveBookmarkAsync(TestEventId, CancellationToken.None);

        A.CallTo(() => _fakeMetaAuditLogger.LogAsync(
                A<AuditEvent>.That.Matches(e =>
                    e.Action == "AuditAnnotation.RemoveBookmark"),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Meta_audit_skipped_when_logger_is_null()
    {
        SetRole(AuditLogRole.ComplianceOfficer);

        var sutNoMeta = new RbacAuditAnnotationStore(
            _fakeInnerStore,
            TestScopeFactory.For(_fakeRoleProvider, _fakeActorProvider),
            _logger);

        // Should not throw even without meta-audit logger
        await sutNoMeta.TagAsync(TestEventId, ["tag"], CancellationToken.None);

        A.CallTo(() => _fakeInnerStore.TagAsync(TestEventId, A<IReadOnlyList<string>>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Meta_audit_failure_does_not_throw()
    {
        SetRole(AuditLogRole.ComplianceOfficer);
        A.CallTo(() => _fakeMetaAuditLogger.LogAsync(A<AuditEvent>._, A<CancellationToken>._))
            .Throws(new InvalidOperationException("Meta-audit store unavailable"));

        // Should not throw — meta-audit failure is swallowed
        await _sut.TagAsync(TestEventId, ["tag"], CancellationToken.None);

        // Inner store call should still have happened
        A.CallTo(() => _fakeInnerStore.TagAsync(TestEventId, A<IReadOnlyList<string>>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Meta_audit_includes_actor_from_provider()
    {
        SetRole(AuditLogRole.ComplianceOfficer);

        await _sut.TagAsync(TestEventId, ["tag"], CancellationToken.None);

        A.CallTo(() => _fakeMetaAuditLogger.LogAsync(
                A<AuditEvent>.That.Matches(e => e.ActorId == TestActorId),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Meta_audit_uses_role_as_actor_when_no_actor_provider()
    {
        SetRole(AuditLogRole.ComplianceOfficer);

        var sutNoActor = new RbacAuditAnnotationStore(
            _fakeInnerStore,
            TestScopeFactory.For(_fakeRoleProvider, metaAuditLogger: _fakeMetaAuditLogger),
            _logger);

        await sutNoActor.TagAsync(TestEventId, ["tag"], CancellationToken.None);

        A.CallTo(() => _fakeMetaAuditLogger.LogAsync(
                A<AuditEvent>.That.Matches(e => e.ActorId == "role:ComplianceOfficer"),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    // ========================================
    // Delegation to inner store
    // ========================================

    [Fact]
    public async Task Query_by_annotation_async_returns_only_candidates_the_caller_may_actually_read()
    {
        // This arm previously asserted pure delegation — whatever the inner store returned came back
        // untouched. The decorator deliberately stopped doing that: a query result discloses through
        // MEMBERSHIP, so returning an event id because it carries an annotation the caller may not read
        // tells them that annotation exists, one method away from GetAnnotationsAsync denying the same
        // content. Every candidate is now re-checked through the predicate the direct read uses, and this
        // arm binds that filtering instead of the delegation it replaced.
        SetRole(AuditLogRole.ComplianceOfficer);
        var query = new AuditAnnotationQuery { Tags = ["suspicious"] };

        A.CallTo(() => _fakeInnerStore.QueryByAnnotationAsync(query, A<CancellationToken>._))
            .Returns<IReadOnlyList<string>>(["evt-readable", "evt-hidden"]);

        // evt-readable carries a tag, and tags have no personal-visibility concept, so they are readable.
        A.CallTo(() => _fakeInnerStore.GetAnnotationsAsync("evt-readable", A<CancellationToken>._))
            .Returns(new AuditAnnotations("evt-readable", ["suspicious"], [], []));

        // evt-hidden matched the query but exposes nothing this caller may read.
        A.CallTo(() => _fakeInnerStore.GetAnnotationsAsync("evt-hidden", A<CancellationToken>._))
            .Returns(new AuditAnnotations("evt-hidden", [], [], []));

        var result = await _sut.QueryByAnnotationAsync(query, CancellationToken.None);

        // SAFETY: the unreadable candidate must not appear — its presence alone confirms a matching
        // annotation exists on it.
        result.ShouldNotContain(
            "evt-hidden",
            "returning an id whose annotations are all unreadable leaks the existence of those annotations, "
            + "which is exactly what the direct read refuses to disclose.");

        // LIVENESS: paired deliberately — a decorator returning an empty list to everybody would satisfy
        // the safety arm above while destroying the query.
        result.ShouldContain(
            "evt-readable",
            "a candidate the caller CAN read must still come back, or the query is inert.");
    }

    // ========================================
    // Helpers
    // ========================================

    private void SetRole(AuditLogRole role)
    {
        A.CallTo(() => _fakeRoleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
            .Returns(role);
    }

    private static AuditEventId CreateAuditEventId(string eventId) => new()
    {
        EventId = eventId,
        EventHash = $"hash-{eventId}",
        SequenceNumber = 1,
        RecordedAt = DateTimeOffset.UtcNow
    };
}
