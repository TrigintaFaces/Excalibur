using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Compliance.Tests.Consent;

public class ConsentServiceShould
{
    private readonly ConsentService _sut;
    private readonly ConsentOptions _options = new();

    public ConsentServiceShould()
    {
        _sut = new ConsentService(
            Microsoft.Extensions.Options.Options.Create(_options),
            NullLogger<ConsentService>.Instance);
    }

    [Fact]
    public async Task Record_consent_and_retrieve_it()
    {
        var record = new ConsentRecord
        {
            SubjectId = "user-1",
            Purpose = "marketing"
        };

        await _sut.RecordConsentAsync(record, CancellationToken.None);
        var result = await _sut.GetConsentAsync("user-1", "marketing", CancellationToken.None);

        result.ShouldNotBeNull();
        result.SubjectId.ShouldBe("user-1");
        result.Purpose.ShouldBe("marketing");
    }

    [Fact]
    public async Task Return_null_for_nonexistent_consent()
    {
        var result = await _sut.GetConsentAsync("nonexistent", "purpose", CancellationToken.None);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task Withdraw_consent_successfully()
    {
        var record = new ConsentRecord
        {
            SubjectId = "user-1",
            Purpose = "analytics"
        };
        await _sut.RecordConsentAsync(record, CancellationToken.None);

        var withdrawn = await _sut.WithdrawConsentAsync("user-1", "analytics", CancellationToken.None);

        withdrawn.ShouldBeTrue();
    }

    [Fact]
    public async Task Return_false_when_withdrawing_nonexistent_consent()
    {
        var withdrawn = await _sut.WithdrawConsentAsync("nonexistent", "purpose", CancellationToken.None);

        withdrawn.ShouldBeFalse();
    }

    [Fact]
    public async Task Return_null_after_consent_withdrawal()
    {
        var record = new ConsentRecord
        {
            SubjectId = "user-1",
            Purpose = "marketing"
        };
        await _sut.RecordConsentAsync(record, CancellationToken.None);
        await _sut.WithdrawConsentAsync("user-1", "marketing", CancellationToken.None);

        var result = await _sut.GetConsentAsync("user-1", "marketing", CancellationToken.None);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task Return_false_when_withdrawing_already_withdrawn_consent()
    {
        var record = new ConsentRecord
        {
            SubjectId = "user-1",
            Purpose = "marketing"
        };
        await _sut.RecordConsentAsync(record, CancellationToken.None);
        await _sut.WithdrawConsentAsync("user-1", "marketing", CancellationToken.None);

        var result = await _sut.WithdrawConsentAsync("user-1", "marketing", CancellationToken.None);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task Apply_default_expiration_when_configured()
    {
        var options = new ConsentOptions { DefaultExpirationDays = 30 };
        var sut = new ConsentService(
            Microsoft.Extensions.Options.Options.Create(options),
            NullLogger<ConsentService>.Instance);

        var record = new ConsentRecord
        {
            SubjectId = "user-1",
            Purpose = "marketing",
            GrantedAt = DateTimeOffset.UtcNow
        };

        await sut.RecordConsentAsync(record, CancellationToken.None);
        var result = await sut.GetConsentAsync("user-1", "marketing", CancellationToken.None);

        result.ShouldNotBeNull();
    }

    [Fact]
    public async Task Return_null_for_expired_consent()
    {
        var record = new ConsentRecord
        {
            SubjectId = "user-1",
            Purpose = "marketing",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1)
        };
        await _sut.RecordConsentAsync(record, CancellationToken.None);

        var result = await _sut.GetConsentAsync("user-1", "marketing", CancellationToken.None);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task Throw_when_recording_null_consent()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => _sut.RecordConsentAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task Throw_when_getting_consent_with_null_subject()
    {
        await Should.ThrowAsync<ArgumentException>(
            () => _sut.GetConsentAsync(null!, "purpose", CancellationToken.None));
    }

    [Fact]
    public async Task Throw_when_getting_consent_with_null_purpose()
    {
        await Should.ThrowAsync<ArgumentException>(
            () => _sut.GetConsentAsync("subject", null!, CancellationToken.None));
    }

    [Fact]
    public void Throw_when_options_are_null()
    {
        Should.Throw<ArgumentNullException>(
            () => new ConsentService(null!, NullLogger<ConsentService>.Instance));
    }

    [Fact]
    public void Throw_when_logger_is_null()
    {
        Should.Throw<ArgumentNullException>(
            () => new ConsentService(Microsoft.Extensions.Options.Options.Create(new ConsentOptions()), null!));
    }
}
