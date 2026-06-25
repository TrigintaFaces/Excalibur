// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Governance;
using Excalibur.A3.Governance.Provisioning;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.A3.Governance.Tests.Provisioning;

/// <summary>
/// Regression lock for <c>bd-lftwn1</c> (Sprint 847, Lane F2 — risk assessor fail-closed): when no real
/// <see cref="IGrantRiskAssessor"/> is configured, the default MUST fail <b>safe</b> — returning the
/// maximum risk score so any <c>RiskScoreAbove</c> approval step always fires — rather than returning a
/// benign score that silently bypasses the human-approval gate.
/// </summary>
/// <remarks>
/// Authored independently of the implementer (author ≠ impl). <b>Non-vacuity (RED on the true pre-fix
/// parent):</b> bound to the stable <see cref="IGrantRiskAssessor.AssessRiskAsync"/> signature. Pre-fix
/// the default returned <c>0</c> (so a <c>RiskScoreAbove=N</c> step never triggered — the fail-open
/// defect); post-fix it returns the documented maximum (100). RED pre-fix, GREEN post-fix.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class DefaultGrantRiskAssessorShould
{
	[Fact]
	public async Task ReturnMaximumRiskScore_FailSafe_WhenAssessmentIsUnavailable()
	{
		// Arrange — the default (fallback) assessor that stands in for an unconfigured real assessor.
		var assessor = new DefaultGrantRiskAssessor(NullLogger<DefaultGrantRiskAssessor>.Instance);

		// Act
		var score = await assessor.AssessRiskAsync(
			userId: "user-lftwn1",
			grantScope: "high-privilege-scope",
			grantType: "activity",
			CancellationToken.None);

		// Assert — fail-safe maximum (100), not a benign 0. RED on pre-fix HEAD (returned 0 → any
		// RiskScoreAbove=N gate silently bypassed). The max guarantees every risk-gated step triggers.
		score.ShouldBe(100);
		score.ShouldBeGreaterThan(
			50,
			customMessage: "A RiskScoreAbove=N approval step (e.g. N=50) must fire when risk is indeterminate.");
	}
}
