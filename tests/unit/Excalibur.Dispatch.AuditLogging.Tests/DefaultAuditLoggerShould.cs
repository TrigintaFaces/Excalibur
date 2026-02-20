using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.AuditLogging.Tests;

public class DefaultAuditLoggerShould
{
    private readonly IAuditStore _fakeStore = A.Fake<IAuditStore>();
    private readonly ILogger<DefaultAuditLogger> _logger = NullLogger<DefaultAuditLogger>.Instance;
    private readonly DefaultAuditLogger _sut;

    public DefaultAuditLoggerShould()
    {
        _sut = new DefaultAuditLogger(_fakeStore, _logger);
    }

    private static AuditEvent CreateValidEvent(string eventId = "evt-1") =>
        new()
        {
            EventId = eventId,
            EventType = AuditEventType.DataAccess,
            Action = "Read",
            Outcome = AuditOutcome.Success,
            Timestamp = DateTimeOffset.UtcNow,
            ActorId = "user-1"
        };

    [Fact]
    public async Task Log_async_delegates_to_store()
    {
        var auditEvent = CreateValidEvent();
        var expectedResult = new AuditEventId
        {
            EventId = "evt-1",
            EventHash = "hash",
            SequenceNumber = 1,
            RecordedAt = DateTimeOffset.UtcNow
        };

        A.CallTo(() => _fakeStore.StoreAsync(auditEvent, A<CancellationToken>._))
            .Returns(expectedResult);

        var result = await _sut.LogAsync(auditEvent, CancellationToken.None);

        result.EventId.ShouldBe("evt-1");
        A.CallTo(() => _fakeStore.StoreAsync(auditEvent, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Throw_argument_null_exception_for_null_event()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => _sut.LogAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task Throw_argument_exception_for_empty_event_id()
    {
        var auditEvent = new AuditEvent
        {
            EventId = "",
            EventType = AuditEventType.DataAccess,
            Action = "Read",
            Outcome = AuditOutcome.Success,
            Timestamp = DateTimeOffset.UtcNow,
            ActorId = "user-1"
        };

        await Should.ThrowAsync<ArgumentException>(
            () => _sut.LogAsync(auditEvent, CancellationToken.None));
    }

    [Fact]
    public async Task Throw_argument_exception_for_empty_action()
    {
        var auditEvent = new AuditEvent
        {
            EventId = "evt-1",
            EventType = AuditEventType.DataAccess,
            Action = "",
            Outcome = AuditOutcome.Success,
            Timestamp = DateTimeOffset.UtcNow,
            ActorId = "user-1"
        };

        await Should.ThrowAsync<ArgumentException>(
            () => _sut.LogAsync(auditEvent, CancellationToken.None));
    }

    [Fact]
    public async Task Throw_argument_exception_for_empty_actor_id()
    {
        var auditEvent = new AuditEvent
        {
            EventId = "evt-1",
            EventType = AuditEventType.DataAccess,
            Action = "Read",
            Outcome = AuditOutcome.Success,
            Timestamp = DateTimeOffset.UtcNow,
            ActorId = ""
        };

        await Should.ThrowAsync<ArgumentException>(
            () => _sut.LogAsync(auditEvent, CancellationToken.None));
    }

    [Fact]
    public async Task Throw_argument_exception_for_default_timestamp()
    {
        var auditEvent = new AuditEvent
        {
            EventId = "evt-1",
            EventType = AuditEventType.DataAccess,
            Action = "Read",
            Outcome = AuditOutcome.Success,
            Timestamp = default,
            ActorId = "user-1"
        };

        await Should.ThrowAsync<ArgumentException>(
            () => _sut.LogAsync(auditEvent, CancellationToken.None));
    }

    [Fact]
    public async Task Return_failure_indicator_when_store_throws()
    {
        var auditEvent = CreateValidEvent();

        A.CallTo(() => _fakeStore.StoreAsync(auditEvent, A<CancellationToken>._))
            .Throws(new InvalidOperationException("Store failed"));

        var result = await _sut.LogAsync(auditEvent, CancellationToken.None);

        result.SequenceNumber.ShouldBe(-1);
        result.EventHash.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task Rethrow_operation_canceled_exception()
    {
        var auditEvent = CreateValidEvent();

        A.CallTo(() => _fakeStore.StoreAsync(auditEvent, A<CancellationToken>._))
            .Throws(new OperationCanceledException());

        await Should.ThrowAsync<OperationCanceledException>(
            () => _sut.LogAsync(auditEvent, CancellationToken.None));
    }

    [Fact]
    public async Task Verify_integrity_delegates_to_store()
    {
        var startDate = DateTimeOffset.UtcNow.AddDays(-1);
        var endDate = DateTimeOffset.UtcNow;
        var expectedResult = AuditIntegrityResult.Valid(100, startDate, endDate);

        A.CallTo(() => _fakeStore.VerifyChainIntegrityAsync(startDate, endDate, A<CancellationToken>._))
            .Returns(expectedResult);

        var result = await _sut.VerifyIntegrityAsync(startDate, endDate, CancellationToken.None);

        result.IsValid.ShouldBeTrue();
        result.EventsVerified.ShouldBe(100);
    }

    [Fact]
    public async Task Throw_when_start_date_after_end_date()
    {
        var startDate = DateTimeOffset.UtcNow;
        var endDate = startDate.AddDays(-1);

        await Should.ThrowAsync<ArgumentException>(
            () => _sut.VerifyIntegrityAsync(startDate, endDate, CancellationToken.None));
    }

    [Fact]
    public async Task Rethrow_when_verify_integrity_store_throws()
    {
        var startDate = DateTimeOffset.UtcNow.AddDays(-1);
        var endDate = DateTimeOffset.UtcNow;

        A.CallTo(() => _fakeStore.VerifyChainIntegrityAsync(startDate, endDate, A<CancellationToken>._))
            .Throws(new InvalidOperationException("Store error"));

        await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.VerifyIntegrityAsync(startDate, endDate, CancellationToken.None));
    }

    [Fact]
    public async Task Return_invalid_integrity_result_from_store()
    {
        var startDate = DateTimeOffset.UtcNow.AddDays(-1);
        var endDate = DateTimeOffset.UtcNow;
        var invalidResult = AuditIntegrityResult.Invalid(
            50, startDate, endDate, "evt-bad", "Hash mismatch", 1);

        A.CallTo(() => _fakeStore.VerifyChainIntegrityAsync(startDate, endDate, A<CancellationToken>._))
            .Returns(invalidResult);

        var result = await _sut.VerifyIntegrityAsync(startDate, endDate, CancellationToken.None);

        result.IsValid.ShouldBeFalse();
        result.FirstViolationEventId.ShouldBe("evt-bad");
    }

    [Fact]
    public void Throw_argument_null_exception_for_null_store()
    {
        Should.Throw<ArgumentNullException>(() =>
            new DefaultAuditLogger(null!, _logger));
    }

    [Fact]
    public void Throw_argument_null_exception_for_null_logger()
    {
        Should.Throw<ArgumentNullException>(() =>
            new DefaultAuditLogger(_fakeStore, null!));
    }
}
