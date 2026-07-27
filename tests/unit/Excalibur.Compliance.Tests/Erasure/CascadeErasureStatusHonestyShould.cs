// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance;
using Excalibur.Compliance.Erasure;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Compliance.Tests.Erasure;

/// <summary>
///     Regression lock (1fu58u, author≠impl) for the cascade-erasure status-honesty fix: the most-optimistic-
///     reporter bug counted EVERY subject as erased and reported <c>Success=true</c> regardless of each
///     subject's per-erasure outcome. The fix inspects each <see cref="ErasureResult.Status"/> and routes
///     blocked/failed subjects to their own buckets, never the erased/scheduled counts, and never masks an
///     incomplete cascade behind <c>Success=true</c>.
/// </summary>
/// <remarks>
///     <b>RED on the pre-fix impl</b> (which returned <c>Success=true</c>, <c>SubjectsErased=count</c> while
///     ignoring blocked/failed). Drives a faked <see cref="IErasureService"/> returning a Blocked outcome for
///     one related subject and a Failed outcome for another.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class CascadeErasureStatusHonestyShould
{
	private readonly IErasureService _erasureService = A.Fake<IErasureService>();
	private readonly ICascadeRelationshipResolver _resolver = A.Fake<ICascadeRelationshipResolver>();
	private readonly CascadeErasureService _sut;

	public CascadeErasureStatusHonestyShould()
		=> _sut = new CascadeErasureService(_erasureService, _resolver, NullLogger<CascadeErasureService>.Instance);

	[Fact]
	public async Task Not_count_blocked_or_failed_subjects_as_erased_and_report_failure()
	{
		// Arrange — primary + two related; "blocked-1" hits a legal hold, "failed-1" fails outright.
		const string primary = "user-1";
		const string blocked = "blocked-1";
		const string failed = "failed-1";

		A.CallTo(() => _resolver.GetRelatedSubjectsAsync(primary, A<CancellationToken>._))
			.Returns(new List<string> { blocked, failed });
		A.CallTo(() => _resolver.GetRelatedSubjectsAsync(A<string>.That.Not.IsEqualTo(primary), A<CancellationToken>._))
			.Returns(new List<string>());

		// Per-subject outcomes keyed off the request's DataSubjectId.
		A.CallTo(() => _erasureService.RequestErasureAsync(
				A<ErasureRequest>.That.Matches(r => r.DataSubjectId == primary), A<CancellationToken>._))
			.Returns(ErasureResult.Scheduled(Guid.NewGuid(), DateTimeOffset.UtcNow.AddHours(72), null));

		A.CallTo(() => _erasureService.RequestErasureAsync(
				A<ErasureRequest>.That.Matches(r => r.DataSubjectId == blocked), A<CancellationToken>._))
			.Returns(ErasureResult.Blocked(Guid.NewGuid(), new LegalHoldInfo
			{
				HoldId = Guid.NewGuid(),
				Basis = LegalHoldBasis.LegalObligation,
				CaseReference = "CASE-1fu58u",
				CreatedAt = DateTimeOffset.UtcNow,
			}));

		A.CallTo(() => _erasureService.RequestErasureAsync(
				A<ErasureRequest>.That.Matches(r => r.DataSubjectId == failed), A<CancellationToken>._))
			.Returns(ErasureResult.Failed(Guid.NewGuid(), "downstream store unavailable"));

		var options = new CascadeErasureOptions { IncludeRelatedRecords = true, RelationshipDepth = 2 };

		// Act
		var result = await _sut.EraseWithCascadeAsync(primary, options, CancellationToken.None);

		// Assert — honest aggregation: incomplete cascade is NOT masked as success.
		result.Success.ShouldBeFalse("a cascade with any blocked or failed subject must not report Success=true");

		result.BlockedSubjects.ShouldContain(blocked);
		result.FailedSubjects.ShouldContain(failed);

		// The blocked/failed subjects must NOT appear in the erased-related list.
		result.RelatedSubjectsErased.ShouldNotContain(blocked, "a legal-hold-blocked subject was not erased");
		result.RelatedSubjectsErased.ShouldNotContain(failed, "a failed subject was not erased");

		// Only the primary (Scheduled) counts as scheduled; nothing falsely counted as erased.
		result.SubjectsErased.ShouldBe(0);
		result.SubjectsScheduled.ShouldBe(1);
	}
}
