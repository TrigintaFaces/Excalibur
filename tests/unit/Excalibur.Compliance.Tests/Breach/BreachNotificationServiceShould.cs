using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Excalibur.Compliance.Breach;

using Excalibur.Compliance;namespace Excalibur.Compliance.Tests.Breach;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class BreachNotificationServiceShould
{
    private readonly BreachNotificationService _sut;
    private readonly BreachNotificationOptions _options = new();

    public BreachNotificationServiceShould()
    {
        _sut = new BreachNotificationService(
            Microsoft.Extensions.Options.Options.Create(_options),
            NullLogger<BreachNotificationService>.Instance);
    }

    private static BreachReport CreateReport(string? breachId = null) => new()
    {
        BreachId = breachId ?? "breach-001",
        Description = "Test breach",
        DetectedAt = DateTimeOffset.UtcNow,
        AffectedSubjectCount = 100
    };

    [Fact]
    public async Task Report_breach_with_reported_status()
    {
        var result = await _sut.ReportBreachAsync(CreateReport(), CancellationToken.None);

        result.ShouldNotBeNull();
        result.BreachId.ShouldBe("breach-001");
        result.Status.ShouldBe(BreachNotificationStatus.Reported);
        result.ReportedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Set_notification_deadline_based_on_options()
    {
        var report = CreateReport();
        var result = await _sut.ReportBreachAsync(report, CancellationToken.None);

        result.NotificationDeadline.ShouldNotBeNull();
    }

    [Fact]
    public async Task Refuse_auto_notify_but_still_record_the_breach()
    {
        var options = new BreachNotificationOptions { AutoNotify = true };
        var sut = new BreachNotificationService(
            Microsoft.Extensions.Options.Options.Create(options),
            NullLogger<BreachNotificationService>.Instance);

        // FLIPPED: previously asserted that AutoNotify stamps SubjectsNotified. That is the same
        // fabricated attestation as the manual path, reached automatically and therefore more quietly.
        _ = await Should.ThrowAsync<NotSupportedException>(
            () => sut.ReportBreachAsync(CreateReport(), CancellationToken.None));

        // The breach itself must still be recorded — losing the report and its Art. 33 deadline would be
        // a worse outcome than the defect being fixed.
        var recorded = await sut.GetBreachStatusAsync("breach-001", CancellationToken.None);

        recorded.ShouldNotBeNull();
        recorded.NotificationDeadline.ShouldNotBeNull();
        recorded.Status.ShouldNotBe(BreachNotificationStatus.SubjectsNotified);
        recorded.SubjectsNotifiedAt.ShouldBeNull();
    }

    [Fact]
    public async Task Get_breach_status()
    {
        await _sut.ReportBreachAsync(CreateReport("breach-002"), CancellationToken.None);

        var status = await _sut.GetBreachStatusAsync("breach-002", CancellationToken.None);

        status.ShouldNotBeNull();
        status.BreachId.ShouldBe("breach-002");
    }

    [Fact]
    public async Task Return_null_for_unknown_breach()
    {
        var status = await _sut.GetBreachStatusAsync("unknown", CancellationToken.None);

        status.ShouldBeNull();
    }

    [Fact]
    public async Task Refuse_to_attest_notification_it_cannot_perform()
    {
        // FLIPPED: this test previously asserted that a transport-less service returns SubjectsNotified
        // with a timestamp — it certified the fabricated attestation rather than catching it. Under GDPR
        // Art. 34 that status and timestamp are the evidence a controller produces to a supervisory
        // authority, so writing them without notifying anybody manufactures a false regulatory record.
        // The contract is now: refuse, and leave no attestation behind.
        await _sut.ReportBreachAsync(CreateReport("breach-003"), CancellationToken.None);

        _ = await Should.ThrowAsync<NotSupportedException>(
            () => _sut.NotifyAffectedSubjectsAsync("breach-003", CancellationToken.None));

        var after = await _sut.GetBreachStatusAsync("breach-003", CancellationToken.None);

        after.ShouldNotBeNull();
        after.Status.ShouldNotBe(BreachNotificationStatus.SubjectsNotified);
        after.SubjectsNotifiedAt.ShouldBeNull();
    }

    [Fact]
    public async Task Throw_when_notifying_unknown_breach()
    {
        await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.NotifyAffectedSubjectsAsync("unknown", CancellationToken.None));
    }

    [Fact]
    public async Task Refuse_every_notification_attempt_not_just_the_first()
    {
        // FLIPPED: the double-notification guard could only be reached by first performing a notification
        // that never happened. With the attestation refused, the first call is the one that throws — the
        // guard itself is unreachable for this implementation and asserting it would require re-creating
        // the false record to set it up.
        await _sut.ReportBreachAsync(CreateReport("breach-004"), CancellationToken.None);

        _ = await Should.ThrowAsync<NotSupportedException>(
            () => _sut.NotifyAffectedSubjectsAsync("breach-004", CancellationToken.None));

        _ = await Should.ThrowAsync<NotSupportedException>(
            () => _sut.NotifyAffectedSubjectsAsync("breach-004", CancellationToken.None));
    }

    [Fact]
    public async Task Throw_when_reporting_null_breach()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => _sut.ReportBreachAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task Throw_when_getting_status_with_null_id()
    {
        await Should.ThrowAsync<ArgumentException>(
            () => _sut.GetBreachStatusAsync(null!, CancellationToken.None));
    }

    [Fact]
    public void Throw_when_options_are_null()
    {
        Should.Throw<ArgumentNullException>(
            () => new BreachNotificationService(null!, NullLogger<BreachNotificationService>.Instance));
    }

    [Fact]
    public void Throw_when_logger_is_null()
    {
        Should.Throw<ArgumentNullException>(
            () => new BreachNotificationService(Microsoft.Extensions.Options.Options.Create(new BreachNotificationOptions()), null!));
    }
}
