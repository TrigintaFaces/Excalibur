using Excalibur.Dispatch.Compliance.Restriction;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Compliance.Tests.Restriction;

public class ProcessingRestrictionServiceShould
{
    private readonly ProcessingRestrictionService _sut;
    private readonly ProcessingRestrictionOptions _options = new();

    public ProcessingRestrictionServiceShould()
    {
        _sut = new ProcessingRestrictionService(
            Microsoft.Extensions.Options.Options.Create(_options),
            NullLogger<ProcessingRestrictionService>.Instance);
    }

    [Fact]
    public async Task Restrict_processing_for_subject()
    {
        await _sut.RestrictAsync("user-1", RestrictionReason.AccuracyContested, CancellationToken.None);

        var isRestricted = await _sut.IsRestrictedAsync("user-1", CancellationToken.None);
        isRestricted.ShouldBeTrue();
    }

    [Fact]
    public async Task Return_false_for_unrestricted_subject()
    {
        var isRestricted = await _sut.IsRestrictedAsync("user-1", CancellationToken.None);

        isRestricted.ShouldBeFalse();
    }

    [Fact]
    public async Task Unrestrict_subject()
    {
        await _sut.RestrictAsync("user-1", RestrictionReason.UnlawfulProcessing, CancellationToken.None);
        await _sut.UnrestrictAsync("user-1", CancellationToken.None);

        var isRestricted = await _sut.IsRestrictedAsync("user-1", CancellationToken.None);
        isRestricted.ShouldBeFalse();
    }

    [Fact]
    public async Task Unrestrict_silently_for_nonexistent_subject()
    {
        // Should not throw
        await _sut.UnrestrictAsync("nonexistent", CancellationToken.None);
    }

    [Fact]
    public async Task Return_false_for_expired_restriction()
    {
        var options = new ProcessingRestrictionOptions
        {
            DefaultRestrictionDuration = TimeSpan.FromMilliseconds(1)
        };
        var sut = new ProcessingRestrictionService(
            Microsoft.Extensions.Options.Options.Create(options),
            NullLogger<ProcessingRestrictionService>.Instance);

        await sut.RestrictAsync("user-1", RestrictionReason.AccuracyContested, CancellationToken.None);
        await global::Tests.Shared.Infrastructure.TestTiming.PauseAsync(50); // Wait for expiration

        var isRestricted = await sut.IsRestrictedAsync("user-1", CancellationToken.None);
        isRestricted.ShouldBeFalse();
    }

    [Fact]
    public async Task Overwrite_existing_restriction()
    {
        await _sut.RestrictAsync("user-1", RestrictionReason.AccuracyContested, CancellationToken.None);
        await _sut.RestrictAsync("user-1", RestrictionReason.LegalClaim, CancellationToken.None);

        // Should still be restricted after override
        var isRestricted = await _sut.IsRestrictedAsync("user-1", CancellationToken.None);
        isRestricted.ShouldBeTrue();
    }

    [Fact]
    public async Task Track_count_of_restrictions()
    {
        _sut.Count.ShouldBe(0);

        await _sut.RestrictAsync("user-1", RestrictionReason.AccuracyContested, CancellationToken.None);

        _sut.Count.ShouldBe(1);
    }

    [Fact]
    public void Clear_all_restrictions()
    {
        _sut.Clear();
        _sut.Count.ShouldBe(0);
    }

    [Fact]
    public async Task Throw_when_subject_id_is_null_for_restrict()
    {
        await Should.ThrowAsync<ArgumentException>(
            () => _sut.RestrictAsync(null!, RestrictionReason.AccuracyContested, CancellationToken.None));
    }

    [Fact]
    public async Task Throw_when_subject_id_is_null_for_unrestrict()
    {
        await Should.ThrowAsync<ArgumentException>(
            () => _sut.UnrestrictAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task Throw_when_subject_id_is_null_for_is_restricted()
    {
        await Should.ThrowAsync<ArgumentException>(
            () => _sut.IsRestrictedAsync(null!, CancellationToken.None));
    }

    [Fact]
    public void Throw_when_options_are_null()
    {
        Should.Throw<ArgumentNullException>(
            () => new ProcessingRestrictionService(null!, NullLogger<ProcessingRestrictionService>.Instance));
    }

    [Fact]
    public void Throw_when_logger_is_null()
    {
        Should.Throw<ArgumentNullException>(
            () => new ProcessingRestrictionService(Microsoft.Extensions.Options.Options.Create(new ProcessingRestrictionOptions()), null!));
    }
}

