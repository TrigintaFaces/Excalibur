using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Compliance.Tests.Breach;

public class BreachNotificationServiceShould
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
    public async Task Auto_notify_when_option_is_enabled()
    {
        var options = new BreachNotificationOptions { AutoNotify = true };
        var sut = new BreachNotificationService(
            Microsoft.Extensions.Options.Options.Create(options),
            NullLogger<BreachNotificationService>.Instance);

        var result = await sut.ReportBreachAsync(CreateReport(), CancellationToken.None);

        result.Status.ShouldBe(BreachNotificationStatus.SubjectsNotified);
        result.SubjectsNotifiedAt.ShouldNotBeNull();
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
    public async Task Notify_affected_subjects()
    {
        await _sut.ReportBreachAsync(CreateReport("breach-003"), CancellationToken.None);

        var result = await _sut.NotifyAffectedSubjectsAsync("breach-003", CancellationToken.None);

        result.Status.ShouldBe(BreachNotificationStatus.SubjectsNotified);
        result.SubjectsNotifiedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Throw_when_notifying_unknown_breach()
    {
        await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.NotifyAffectedSubjectsAsync("unknown", CancellationToken.None));
    }

    [Fact]
    public async Task Throw_when_notifying_already_notified_breach()
    {
        await _sut.ReportBreachAsync(CreateReport("breach-004"), CancellationToken.None);
        await _sut.NotifyAffectedSubjectsAsync("breach-004", CancellationToken.None);

        await Should.ThrowAsync<InvalidOperationException>(
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
