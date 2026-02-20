using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.AuditLogging.Tests;

public class RbacAuditStoreShould
{
    private readonly IAuditStore _innerStore = A.Fake<IAuditStore>();
    private readonly IAuditRoleProvider _roleProvider = A.Fake<IAuditRoleProvider>();
    private readonly RbacAuditStore _sut;

    public RbacAuditStoreShould()
    {
        _sut = new RbacAuditStore(
            _innerStore,
            _roleProvider,
            NullLogger<RbacAuditStore>.Instance);
    }

    private static AuditEvent CreateEvent(
        string eventId = "evt-1",
        AuditEventType eventType = AuditEventType.DataAccess) =>
        new()
        {
            EventId = eventId,
            EventType = eventType,
            Action = "Read",
            Outcome = AuditOutcome.Success,
            Timestamp = DateTimeOffset.UtcNow,
            ActorId = "user-1"
        };

    [Fact]
    public async Task Store_always_delegates_to_inner_store()
    {
        var auditEvent = CreateEvent();
        var expectedResult = new AuditEventId
        {
            EventId = "evt-1",
            EventHash = "hash",
            SequenceNumber = 1,
            RecordedAt = DateTimeOffset.UtcNow
        };

        A.CallTo(() => _innerStore.StoreAsync(auditEvent, A<CancellationToken>._))
            .Returns(expectedResult);

        var result = await _sut.StoreAsync(auditEvent, CancellationToken.None);

        result.EventId.ShouldBe("evt-1");
        A.CallTo(() => _roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Theory]
    [InlineData(AuditLogRole.None)]
    [InlineData(AuditLogRole.Developer)]
    public async Task Deny_get_by_id_for_insufficient_roles(AuditLogRole role)
    {
        A.CallTo(() => _roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
            .Returns(role);

        await Should.ThrowAsync<UnauthorizedAccessException>(
            () => _sut.GetByIdAsync("evt-1", CancellationToken.None));
    }

    [Theory]
    [InlineData(AuditLogRole.SecurityAnalyst)]
    [InlineData(AuditLogRole.ComplianceOfficer)]
    [InlineData(AuditLogRole.Administrator)]
    public async Task Allow_get_by_id_for_sufficient_roles(AuditLogRole role)
    {
        A.CallTo(() => _roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
            .Returns(role);
        A.CallTo(() => _innerStore.GetByIdAsync("evt-1", A<CancellationToken>._))
            .Returns(CreateEvent("evt-1", AuditEventType.Security));

        var result = await _sut.GetByIdAsync("evt-1", CancellationToken.None);

        result.ShouldNotBeNull();
    }

    [Fact]
    public async Task Return_null_when_security_analyst_accesses_non_security_event()
    {
        A.CallTo(() => _roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
            .Returns(AuditLogRole.SecurityAnalyst);
        A.CallTo(() => _innerStore.GetByIdAsync("evt-1", A<CancellationToken>._))
            .Returns(CreateEvent("evt-1", AuditEventType.DataAccess));

        var result = await _sut.GetByIdAsync("evt-1", CancellationToken.None);

        result.ShouldBeNull();
    }

    [Theory]
    [InlineData(AuditEventType.Authentication)]
    [InlineData(AuditEventType.Authorization)]
    [InlineData(AuditEventType.Security)]
    public async Task Allow_security_analyst_to_access_security_event_types(AuditEventType eventType)
    {
        A.CallTo(() => _roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
            .Returns(AuditLogRole.SecurityAnalyst);
        A.CallTo(() => _innerStore.GetByIdAsync("evt-1", A<CancellationToken>._))
            .Returns(CreateEvent("evt-1", eventType));

        var result = await _sut.GetByIdAsync("evt-1", CancellationToken.None);

        result.ShouldNotBeNull();
    }

    [Theory]
    [InlineData(AuditLogRole.None)]
    [InlineData(AuditLogRole.Developer)]
    public async Task Deny_query_for_insufficient_roles(AuditLogRole role)
    {
        A.CallTo(() => _roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
            .Returns(role);

        await Should.ThrowAsync<UnauthorizedAccessException>(
            () => _sut.QueryAsync(new AuditQuery(), CancellationToken.None));
    }

    [Fact]
    public async Task Filter_query_event_types_for_security_analyst()
    {
        A.CallTo(() => _roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
            .Returns(AuditLogRole.SecurityAnalyst);

        AuditQuery? capturedQuery = null;
        A.CallTo(() => _innerStore.QueryAsync(A<AuditQuery>._, A<CancellationToken>._))
            .Invokes((AuditQuery q, CancellationToken _) => capturedQuery = q)
            .Returns(Task.FromResult<IReadOnlyList<AuditEvent>>([]));

        await _sut.QueryAsync(new AuditQuery(), CancellationToken.None);

        capturedQuery.ShouldNotBeNull();
        capturedQuery.EventTypes.ShouldNotBeNull();
        capturedQuery.EventTypes.ShouldContain(AuditEventType.Authentication);
        capturedQuery.EventTypes.ShouldContain(AuditEventType.Authorization);
        capturedQuery.EventTypes.ShouldContain(AuditEventType.Security);
    }

    [Fact]
    public async Task Not_filter_query_event_types_for_compliance_officer()
    {
        A.CallTo(() => _roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
            .Returns(AuditLogRole.ComplianceOfficer);

        var originalQuery = new AuditQuery();
        A.CallTo(() => _innerStore.QueryAsync(originalQuery, A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<AuditEvent>>([]));

        await _sut.QueryAsync(originalQuery, CancellationToken.None);

        A.CallTo(() => _innerStore.QueryAsync(originalQuery, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData(AuditLogRole.None)]
    [InlineData(AuditLogRole.Developer)]
    public async Task Deny_count_for_insufficient_roles(AuditLogRole role)
    {
        A.CallTo(() => _roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
            .Returns(role);

        await Should.ThrowAsync<UnauthorizedAccessException>(
            () => _sut.CountAsync(new AuditQuery(), CancellationToken.None));
    }

    [Theory]
    [InlineData(AuditLogRole.None)]
    [InlineData(AuditLogRole.Developer)]
    [InlineData(AuditLogRole.SecurityAnalyst)]
    public async Task Deny_verify_chain_integrity_for_non_privileged_roles(AuditLogRole role)
    {
        A.CallTo(() => _roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
            .Returns(role);

        await Should.ThrowAsync<UnauthorizedAccessException>(
            () => _sut.VerifyChainIntegrityAsync(
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow,
                CancellationToken.None));
    }

    [Theory]
    [InlineData(AuditLogRole.ComplianceOfficer)]
    [InlineData(AuditLogRole.Administrator)]
    public async Task Allow_verify_chain_integrity_for_privileged_roles(AuditLogRole role)
    {
        A.CallTo(() => _roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
            .Returns(role);

        var start = DateTimeOffset.UtcNow.AddDays(-1);
        var end = DateTimeOffset.UtcNow;
        var expected = AuditIntegrityResult.Valid(10, start, end);
        A.CallTo(() => _innerStore.VerifyChainIntegrityAsync(start, end, A<CancellationToken>._))
            .Returns(expected);

        var result = await _sut.VerifyChainIntegrityAsync(start, end, CancellationToken.None);

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(AuditLogRole.None)]
    [InlineData(AuditLogRole.Developer)]
    public async Task Deny_get_last_event_for_insufficient_roles(AuditLogRole role)
    {
        A.CallTo(() => _roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
            .Returns(role);

        await Should.ThrowAsync<UnauthorizedAccessException>(
            () => _sut.GetLastEventAsync(null, CancellationToken.None));
    }

    [Fact]
    public void Throw_argument_null_for_null_inner_store()
    {
        Should.Throw<ArgumentNullException>(() =>
            new RbacAuditStore(null!, _roleProvider, NullLogger<RbacAuditStore>.Instance));
    }

    [Fact]
    public void Throw_argument_null_for_null_role_provider()
    {
        Should.Throw<ArgumentNullException>(() =>
            new RbacAuditStore(_innerStore, null!, NullLogger<RbacAuditStore>.Instance));
    }

    [Fact]
    public void Throw_argument_null_for_null_logger()
    {
        Should.Throw<ArgumentNullException>(() =>
            new RbacAuditStore(_innerStore, _roleProvider, null!));
    }

    [Fact]
    public async Task Throw_argument_null_for_null_query_in_query()
    {
        A.CallTo(() => _roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
            .Returns(AuditLogRole.Administrator);

        await Should.ThrowAsync<ArgumentNullException>(
            () => _sut.QueryAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task Throw_argument_null_for_null_query_in_count()
    {
        A.CallTo(() => _roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
            .Returns(AuditLogRole.Administrator);

        await Should.ThrowAsync<ArgumentNullException>(
            () => _sut.CountAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task Intersect_event_type_filter_for_security_analyst()
    {
        A.CallTo(() => _roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
            .Returns(AuditLogRole.SecurityAnalyst);

        AuditQuery? capturedQuery = null;
        A.CallTo(() => _innerStore.QueryAsync(A<AuditQuery>._, A<CancellationToken>._))
            .Invokes((AuditQuery q, CancellationToken _) => capturedQuery = q)
            .Returns(Task.FromResult<IReadOnlyList<AuditEvent>>([]));

        // Query requests Authentication + DataAccess; analyst can only see Authentication
        var query = new AuditQuery
        {
            EventTypes = [AuditEventType.Authentication, AuditEventType.DataAccess]
        };
        await _sut.QueryAsync(query, CancellationToken.None);

        capturedQuery.ShouldNotBeNull();
        capturedQuery.EventTypes.ShouldNotBeNull();
        capturedQuery.EventTypes.ShouldContain(AuditEventType.Authentication);
        capturedQuery.EventTypes.ShouldNotContain(AuditEventType.DataAccess);
    }
}
