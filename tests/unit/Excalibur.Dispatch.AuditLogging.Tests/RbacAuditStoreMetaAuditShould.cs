using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.AuditLogging.Tests;

public class RbacAuditStoreMetaAuditShould
{
    private readonly IAuditStore _innerStore = A.Fake<IAuditStore>();
    private readonly IAuditRoleProvider _roleProvider = A.Fake<IAuditRoleProvider>();
    private readonly IAuditActorProvider _actorProvider = A.Fake<IAuditActorProvider>();
    private readonly IAuditLogger _metaAuditLogger = A.Fake<IAuditLogger>();

    private RbacAuditStore CreateSut(
        IAuditActorProvider? actorProvider = null,
        IAuditLogger? metaLogger = null) =>
        new(
            _innerStore,
            _roleProvider,
            NullLogger<RbacAuditStore>.Instance,
            actorProvider,
            metaLogger);

    [Fact]
    public async Task Log_meta_audit_on_get_by_id_with_actor_provider()
    {
        var sut = CreateSut(_actorProvider, _metaAuditLogger);

        A.CallTo(() => _roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
            .Returns(AuditLogRole.Administrator);
        A.CallTo(() => _actorProvider.GetCurrentActorIdAsync(A<CancellationToken>._))
            .Returns("admin@example.com");
        A.CallTo(() => _innerStore.GetByIdAsync("evt-1", A<CancellationToken>._))
            .Returns(new AuditEvent
            {
                EventId = "evt-1",
                EventType = AuditEventType.Security,
                Action = "Read",
                Outcome = AuditOutcome.Success,
                Timestamp = DateTimeOffset.UtcNow,
                ActorId = "user-1"
            });

        await sut.GetByIdAsync("evt-1", CancellationToken.None);

        A.CallTo(() => _metaAuditLogger.LogAsync(
                A<AuditEvent>.That.Matches(e => e.ActorId == "admin@example.com"),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Log_meta_audit_with_role_fallback_when_no_actor_provider()
    {
        var sut = CreateSut(actorProvider: null, metaLogger: _metaAuditLogger);

        A.CallTo(() => _roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
            .Returns(AuditLogRole.ComplianceOfficer);
        A.CallTo(() => _innerStore.GetByIdAsync("evt-1", A<CancellationToken>._))
            .Returns(new AuditEvent
            {
                EventId = "evt-1",
                EventType = AuditEventType.Security,
                Action = "Read",
                Outcome = AuditOutcome.Success,
                Timestamp = DateTimeOffset.UtcNow,
                ActorId = "user-1"
            });

        await sut.GetByIdAsync("evt-1", CancellationToken.None);

        A.CallTo(() => _metaAuditLogger.LogAsync(
                A<AuditEvent>.That.Matches(e => e.ActorId == "role:ComplianceOfficer"),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Not_fail_when_meta_audit_logger_throws()
    {
        var sut = CreateSut(actorProvider: null, metaLogger: _metaAuditLogger);

        A.CallTo(() => _roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
            .Returns(AuditLogRole.Administrator);
        A.CallTo(() => _innerStore.GetByIdAsync("evt-1", A<CancellationToken>._))
            .Returns(new AuditEvent
            {
                EventId = "evt-1",
                EventType = AuditEventType.Security,
                Action = "Read",
                Outcome = AuditOutcome.Success,
                Timestamp = DateTimeOffset.UtcNow,
                ActorId = "user-1"
            });
        A.CallTo(() => _metaAuditLogger.LogAsync(A<AuditEvent>._, A<CancellationToken>._))
            .Throws(new InvalidOperationException("Meta audit failed"));

        // Should not throw, meta-audit failures are swallowed
        var result = await sut.GetByIdAsync("evt-1", CancellationToken.None);

        result.ShouldNotBeNull();
    }

    [Fact]
    public async Task Not_call_meta_audit_when_no_logger_registered()
    {
        var sut = CreateSut(actorProvider: null, metaLogger: null);

        A.CallTo(() => _roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
            .Returns(AuditLogRole.Administrator);
        A.CallTo(() => _innerStore.GetByIdAsync("evt-1", A<CancellationToken>._))
            .Returns(new AuditEvent
            {
                EventId = "evt-1",
                EventType = AuditEventType.Security,
                Action = "Read",
                Outcome = AuditOutcome.Success,
                Timestamp = DateTimeOffset.UtcNow,
                ActorId = "user-1"
            });

        var result = await sut.GetByIdAsync("evt-1", CancellationToken.None);

        result.ShouldNotBeNull();
        // No meta-audit logger, so no calls expected
    }

    [Fact]
    public async Task Log_meta_audit_on_query()
    {
        var sut = CreateSut(actorProvider: null, metaLogger: _metaAuditLogger);

        A.CallTo(() => _roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
            .Returns(AuditLogRole.Administrator);
        A.CallTo(() => _innerStore.QueryAsync(A<AuditQuery>._, A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<AuditEvent>>([]));

        await sut.QueryAsync(new AuditQuery(), CancellationToken.None);

        A.CallTo(() => _metaAuditLogger.LogAsync(
                A<AuditEvent>.That.Matches(e => e.Action == "AuditLog.Query"),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Log_meta_audit_on_verify_integrity()
    {
        var sut = CreateSut(actorProvider: null, metaLogger: _metaAuditLogger);
        var start = DateTimeOffset.UtcNow.AddDays(-1);
        var end = DateTimeOffset.UtcNow;

        A.CallTo(() => _roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
            .Returns(AuditLogRole.Administrator);
        A.CallTo(() => _innerStore.VerifyChainIntegrityAsync(start, end, A<CancellationToken>._))
            .Returns(AuditIntegrityResult.Valid(0, start, end));

        await sut.VerifyChainIntegrityAsync(start, end, CancellationToken.None);

        A.CallTo(() => _metaAuditLogger.LogAsync(
                A<AuditEvent>.That.Matches(e => e.Action == "AuditLog.VerifyIntegrity"),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }
}
