using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Compliance.Tests.Monitoring;

public class LoggingAlertHandlerShould
{
    private readonly LoggingAlertHandler _sut;

    public LoggingAlertHandlerShould()
    {
        _sut = new LoggingAlertHandler(NullLogger<LoggingAlertHandler>.Instance);
    }

    [Fact]
    public async Task Handle_rotation_failure_with_critical_severity()
    {
        var alert = new KeyRotationFailureAlert("key-1", "AzureKV", "Error", DateTimeOffset.UtcNow, 5);
        alert.Severity.ShouldBe(AlertSeverity.Critical);

        // Should not throw
        await _sut.HandleRotationFailureAsync(alert, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_rotation_failure_with_high_severity()
    {
        var alert = new KeyRotationFailureAlert("key-1", "AzureKV", "Error", DateTimeOffset.UtcNow, 3);
        alert.Severity.ShouldBe(AlertSeverity.High);

        await _sut.HandleRotationFailureAsync(alert, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_rotation_failure_with_medium_severity()
    {
        var alert = new KeyRotationFailureAlert("key-1", "AzureKV", "Error", DateTimeOffset.UtcNow, 1);
        alert.Severity.ShouldBe(AlertSeverity.Medium);

        await _sut.HandleRotationFailureAsync(alert, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_expiration_warning_with_critical_severity()
    {
        var alert = new KeyExpirationAlert("key-1", "AzureKV", DateTimeOffset.UtcNow.AddHours(12), 0);
        alert.Severity.ShouldBe(AlertSeverity.Critical);

        await _sut.HandleExpirationWarningAsync(alert, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_expiration_warning_with_high_severity()
    {
        var alert = new KeyExpirationAlert("key-1", "AzureKV", DateTimeOffset.UtcNow.AddDays(5), 5);
        alert.Severity.ShouldBe(AlertSeverity.High);

        await _sut.HandleExpirationWarningAsync(alert, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_expiration_warning_with_medium_severity()
    {
        var alert = new KeyExpirationAlert("key-1", "AzureKV", DateTimeOffset.UtcNow.AddDays(10), 10);
        alert.Severity.ShouldBe(AlertSeverity.Medium);

        await _sut.HandleExpirationWarningAsync(alert, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_expiration_warning_with_low_severity()
    {
        var alert = new KeyExpirationAlert("key-1", "AzureKV", DateTimeOffset.UtcNow.AddDays(30), 30);
        alert.Severity.ShouldBe(AlertSeverity.Low);

        await _sut.HandleExpirationWarningAsync(alert, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_rotation_success()
    {
        var notification = new KeyRotationSuccessNotification("key-1", "AzureKV", "v1", "v2", DateTimeOffset.UtcNow);

        await _sut.HandleRotationSuccessAsync(notification, CancellationToken.None);
    }

    [Fact]
    public async Task Throw_when_failure_alert_is_null()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => _sut.HandleRotationFailureAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task Throw_when_expiration_alert_is_null()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => _sut.HandleExpirationWarningAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task Throw_when_success_notification_is_null()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => _sut.HandleRotationSuccessAsync(null!, CancellationToken.None));
    }

    [Fact]
    public void Throw_when_logger_is_null()
    {
        Should.Throw<ArgumentNullException>(() => new LoggingAlertHandler(null!));
    }
}
