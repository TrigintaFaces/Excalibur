using Excalibur.Dispatch.Compliance.Restriction;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Compliance.Tests.Restriction;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ProcessingRestrictionServiceDepthShould
{
	private readonly ProcessingRestrictionOptions _options = new();
	private readonly NullLogger<ProcessingRestrictionService> _logger = NullLogger<ProcessingRestrictionService>.Instance;

	[Fact]
	public async Task Restrict_subject_with_reason()
	{
		var sut = CreateService();

		await sut.RestrictAsync("user-1", RestrictionReason.AccuracyContested, CancellationToken.None).ConfigureAwait(false);

		var isRestricted = await sut.IsRestrictedAsync("user-1", CancellationToken.None).ConfigureAwait(false);
		isRestricted.ShouldBeTrue();
	}

	[Fact]
	public async Task Unrestrict_subject_removes_restriction()
	{
		var sut = CreateService();
		await sut.RestrictAsync("user-1", RestrictionReason.UnlawfulProcessing, CancellationToken.None).ConfigureAwait(false);

		await sut.UnrestrictAsync("user-1", CancellationToken.None).ConfigureAwait(false);

		var isRestricted = await sut.IsRestrictedAsync("user-1", CancellationToken.None).ConfigureAwait(false);
		isRestricted.ShouldBeFalse();
	}

	[Fact]
	public async Task Unrestrict_nonexistent_subject_does_not_throw()
	{
		var sut = CreateService();

		await sut.UnrestrictAsync("nonexistent", CancellationToken.None).ConfigureAwait(false);

		// Should not throw
		sut.Count.ShouldBe(0);
	}

	[Fact]
	public async Task Is_restricted_returns_false_for_unknown_subject()
	{
		var sut = CreateService();

		var result = await sut.IsRestrictedAsync("unknown", CancellationToken.None).ConfigureAwait(false);

		result.ShouldBeFalse();
	}

	[Fact]
	public async Task Expired_restriction_returns_false_and_removes_entry()
	{
		_options.DefaultRestrictionDuration = TimeSpan.FromMilliseconds(1);
		var sut = CreateService();

		await sut.RestrictAsync("user-1", RestrictionReason.AccuracyContested, CancellationToken.None).ConfigureAwait(false);

		// Wait for restriction to expire
		await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(50).ConfigureAwait(false);

		var isRestricted = await sut.IsRestrictedAsync("user-1", CancellationToken.None).ConfigureAwait(false);
		isRestricted.ShouldBeFalse();

		// Entry should have been removed
		sut.Count.ShouldBe(0);
	}

	[Fact]
	public async Task Overwrite_existing_restriction()
	{
		var sut = CreateService();

		await sut.RestrictAsync("user-1", RestrictionReason.AccuracyContested, CancellationToken.None).ConfigureAwait(false);
		await sut.RestrictAsync("user-1", RestrictionReason.LegalClaim, CancellationToken.None).ConfigureAwait(false);

		sut.Count.ShouldBe(1);
		var isRestricted = await sut.IsRestrictedAsync("user-1", CancellationToken.None).ConfigureAwait(false);
		isRestricted.ShouldBeTrue();
	}

	[Fact]
	public async Task Track_count_of_active_restrictions()
	{
		var sut = CreateService();

		sut.Count.ShouldBe(0);

		await sut.RestrictAsync("user-1", RestrictionReason.AccuracyContested, CancellationToken.None).ConfigureAwait(false);
		sut.Count.ShouldBe(1);

		await sut.RestrictAsync("user-2", RestrictionReason.ErasureObjected, CancellationToken.None).ConfigureAwait(false);
		sut.Count.ShouldBe(2);

		await sut.UnrestrictAsync("user-1", CancellationToken.None).ConfigureAwait(false);
		sut.Count.ShouldBe(1);
	}

	[Fact]
	public async Task Clear_removes_all_restrictions()
	{
		var sut = CreateService();

		await sut.RestrictAsync("user-1", RestrictionReason.AccuracyContested, CancellationToken.None).ConfigureAwait(false);
		await sut.RestrictAsync("user-2", RestrictionReason.LegalClaim, CancellationToken.None).ConfigureAwait(false);

		sut.Clear();

		sut.Count.ShouldBe(0);
	}

	[Fact]
	public async Task Throw_for_null_or_empty_subject_id_in_restrict()
	{
		var sut = CreateService();

		await Should.ThrowAsync<ArgumentException>(
			() => sut.RestrictAsync(null!, RestrictionReason.AccuracyContested, CancellationToken.None)).ConfigureAwait(false);
		await Should.ThrowAsync<ArgumentException>(
			() => sut.RestrictAsync("", RestrictionReason.AccuracyContested, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task Throw_for_null_or_empty_subject_id_in_unrestrict()
	{
		var sut = CreateService();

		await Should.ThrowAsync<ArgumentException>(
			() => sut.UnrestrictAsync(null!, CancellationToken.None)).ConfigureAwait(false);
		await Should.ThrowAsync<ArgumentException>(
			() => sut.UnrestrictAsync("", CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task Throw_for_null_or_empty_subject_id_in_is_restricted()
	{
		var sut = CreateService();

		await Should.ThrowAsync<ArgumentException>(
			() => sut.IsRestrictedAsync(null!, CancellationToken.None)).ConfigureAwait(false);
		await Should.ThrowAsync<ArgumentException>(
			() => sut.IsRestrictedAsync("", CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public void Throw_for_null_options_in_constructor()
	{
		Should.Throw<ArgumentNullException>(
			() => new ProcessingRestrictionService(null!, _logger));
	}

	[Fact]
	public void Throw_for_null_logger_in_constructor()
	{
		Should.Throw<ArgumentNullException>(
			() => new ProcessingRestrictionService(
				Microsoft.Extensions.Options.Options.Create(_options), null!));
	}

	[Fact]
	public async Task Support_all_restriction_reasons()
	{
		var sut = CreateService();

		foreach (var reason in Enum.GetValues<RestrictionReason>())
		{
			var subjectId = $"user-{(int)reason}";
			await sut.RestrictAsync(subjectId, reason, CancellationToken.None).ConfigureAwait(false);
			var isRestricted = await sut.IsRestrictedAsync(subjectId, CancellationToken.None).ConfigureAwait(false);
			isRestricted.ShouldBeTrue();
		}

		sut.Count.ShouldBe(Enum.GetValues<RestrictionReason>().Length);
	}

	private ProcessingRestrictionService CreateService() =>
		new(Microsoft.Extensions.Options.Options.Create(_options), _logger);
}
