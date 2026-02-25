using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Compliance.Tests.Monitoring;

public class KeyRotationAlertServiceShould
{
    private readonly IKeyRotationAlertHandler _handler;
    private readonly KeyRotationAlertService _sut;

    public KeyRotationAlertServiceShould()
    {
        _handler = A.Fake<IKeyRotationAlertHandler>();
        _sut = new KeyRotationAlertService(
            [_handler],
            null,
            NullLogger<KeyRotationAlertService>.Instance);
    }

    [Fact]
    public async Task Report_rotation_failure_and_track_count()
    {
        await _sut.ReportRotationFailureAsync("key-1", "AzureKV", "Timeout error", CancellationToken.None);

        _sut.GetFailureCount("key-1", "AzureKV").ShouldBe(1);
    }

    [Fact]
    public async Task Increment_consecutive_failure_count()
    {
        await _sut.ReportRotationFailureAsync("key-1", "AzureKV", "Error 1", CancellationToken.None);
        await _sut.ReportRotationFailureAsync("key-1", "AzureKV", "Error 2", CancellationToken.None);

        _sut.GetFailureCount("key-1", "AzureKV").ShouldBe(2);
    }

    [Fact]
    public async Task Notify_handler_when_threshold_reached()
    {
        // Default threshold is 1 (alert on first failure)
        await _sut.ReportRotationFailureAsync("key-1", "AzureKV", "Error", CancellationToken.None);

        A.CallTo(() => _handler.HandleRotationFailureAsync(
            A<KeyRotationFailureAlert>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Not_notify_handler_below_threshold()
    {
        var options = new KeyRotationAlertOptions { AlertAfterFailures = 3 };
        var sut = new KeyRotationAlertService(
            [_handler], null, NullLogger<KeyRotationAlertService>.Instance, options);

        await sut.ReportRotationFailureAsync("key-1", "AzureKV", "Error", CancellationToken.None);

        A.CallTo(() => _handler.HandleRotationFailureAsync(
            A<KeyRotationFailureAlert>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task Clear_failure_count_on_success()
    {
        await _sut.ReportRotationFailureAsync("key-1", "AzureKV", "Error", CancellationToken.None);
        _sut.GetFailureCount("key-1", "AzureKV").ShouldBe(1);

        await _sut.ReportRotationSuccessAsync("key-1", "AzureKV", "v1", "v2", CancellationToken.None);

        _sut.GetFailureCount("key-1", "AzureKV").ShouldBe(0);
    }

    [Fact]
    public async Task Notify_on_success_when_enabled()
    {
        var options = new KeyRotationAlertOptions { NotifyOnSuccess = true };
        var sut = new KeyRotationAlertService(
            [_handler], null, NullLogger<KeyRotationAlertService>.Instance, options);

        await sut.ReportRotationSuccessAsync("key-1", "AzureKV", "v1", "v2", CancellationToken.None);

        A.CallTo(() => _handler.HandleRotationSuccessAsync(
            A<KeyRotationSuccessNotification>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Not_notify_on_success_when_disabled()
    {
        await _sut.ReportRotationSuccessAsync("key-1", "AzureKV", "v1", "v2", CancellationToken.None);

        A.CallTo(() => _handler.HandleRotationSuccessAsync(
            A<KeyRotationSuccessNotification>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task Report_expiration_warning_within_threshold()
    {
        var expiresAt = DateTimeOffset.UtcNow.AddDays(7);

        await _sut.ReportExpirationWarningAsync("key-1", "AzureKV", expiresAt, CancellationToken.None);

        A.CallTo(() => _handler.HandleExpirationWarningAsync(
            A<KeyExpirationAlert>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Not_report_expiration_warning_beyond_threshold()
    {
        var expiresAt = DateTimeOffset.UtcNow.AddDays(30); // Beyond default 14-day threshold

        await _sut.ReportExpirationWarningAsync("key-1", "AzureKV", expiresAt, CancellationToken.None);

        A.CallTo(() => _handler.HandleExpirationWarningAsync(
            A<KeyExpirationAlert>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task Reset_failure_count()
    {
        await _sut.ReportRotationFailureAsync("key-1", "AzureKV", "Error", CancellationToken.None);

        _sut.ResetFailureCount("key-1", "AzureKV");

        _sut.GetFailureCount("key-1", "AzureKV").ShouldBe(0);
    }

    [Fact]
    public void Return_zero_for_unknown_key_failure_count()
    {
        _sut.GetFailureCount("unknown", "provider").ShouldBe(0);
    }

    [Fact]
    public async Task Throw_when_key_id_is_null_for_failure()
    {
        await Should.ThrowAsync<ArgumentException>(
            () => _sut.ReportRotationFailureAsync(null!, "provider", "error", CancellationToken.None));
    }

    [Fact]
    public async Task Throw_when_provider_is_null_for_failure()
    {
        await Should.ThrowAsync<ArgumentException>(
            () => _sut.ReportRotationFailureAsync("key", null!, "error", CancellationToken.None));
    }

    [Fact]
    public async Task Throw_when_key_id_is_null_for_success()
    {
        await Should.ThrowAsync<ArgumentException>(
            () => _sut.ReportRotationSuccessAsync(null!, "provider", null, null, CancellationToken.None));
    }

    [Fact]
    public async Task Throw_when_key_id_is_null_for_expiration()
    {
        await Should.ThrowAsync<ArgumentException>(
            () => _sut.ReportExpirationWarningAsync(null!, "provider", DateTimeOffset.UtcNow.AddDays(7), CancellationToken.None));
    }

    [Fact]
    public async Task Continue_when_handler_throws()
    {
        var failingHandler = A.Fake<IKeyRotationAlertHandler>();
        A.CallTo(() => failingHandler.HandleRotationFailureAsync(
            A<KeyRotationFailureAlert>._, A<CancellationToken>._))
            .Throws(new InvalidOperationException("Handler failed"));

        var sut = new KeyRotationAlertService(
            [failingHandler], null, NullLogger<KeyRotationAlertService>.Instance);

        // Should not throw
        await sut.ReportRotationFailureAsync("key-1", "AzureKV", "Error", CancellationToken.None);
    }
}
