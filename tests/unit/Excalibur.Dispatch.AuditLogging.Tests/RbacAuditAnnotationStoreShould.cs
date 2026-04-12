// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.AuditLogging;
using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.AuditLogging.Tests;

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
            _fakeRoleProvider,
            _fakeActorProvider,
            _fakeMetaAuditLogger,
            _logger);
    }

    // ========================================
    // Constructor validation
    // ========================================

    [Fact]
    public void Throw_when_inner_store_is_null()
    {
        Should.Throw<ArgumentNullException>(() =>
            new RbacAuditAnnotationStore(null!, _fakeRoleProvider, _fakeActorProvider, _fakeMetaAuditLogger, _logger));
    }

    [Fact]
    public void Throw_when_role_provider_is_null()
    {
        Should.Throw<ArgumentNullException>(() =>
            new RbacAuditAnnotationStore(_fakeInnerStore, null!, _fakeActorProvider, _fakeMetaAuditLogger, _logger));
    }

    [Fact]
    public void Throw_when_logger_is_null()
    {
        Should.Throw<ArgumentNullException>(() =>
            new RbacAuditAnnotationStore(_fakeInnerStore, _fakeRoleProvider, _fakeActorProvider, _fakeMetaAuditLogger, null!));
    }

    [Fact]
    public void Accept_null_actor_provider()
    {
        // actorProvider is optional
        var store = new RbacAuditAnnotationStore(
            _fakeInnerStore, _fakeRoleProvider, null, _fakeMetaAuditLogger, _logger);
        store.ShouldNotBeNull();
    }

    [Fact]
    public void Accept_null_meta_audit_logger()
    {
        // metaAuditLogger is optional
        var store = new RbacAuditAnnotationStore(
            _fakeInnerStore, _fakeRoleProvider, _fakeActorProvider, null, _logger);
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

        // ComplianceOfficer sees ALL annotations including personal
        result.Bookmarks.Count.ShouldBe(2);
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
            _fakeInnerStore, _fakeRoleProvider, _fakeActorProvider, null, _logger);

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
            _fakeInnerStore, _fakeRoleProvider, null, _fakeMetaAuditLogger, _logger);

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
    public async Task Query_by_annotation_async_delegates_to_inner_store()
    {
        SetRole(AuditLogRole.ComplianceOfficer);
        var query = new AuditAnnotationQuery { Tags = ["suspicious"] };
        IReadOnlyList<string> expected = ["evt-1", "evt-2"];

        A.CallTo(() => _fakeInnerStore.QueryByAnnotationAsync(query, A<CancellationToken>._))
            .Returns(expected);

        var result = await _sut.QueryByAnnotationAsync(query, CancellationToken.None);

        result.ShouldBe(expected);
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
