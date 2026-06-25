// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Governance.Provisioning;

using Microsoft.Extensions.Logging;

namespace Excalibur.A3.Governance;

/// <summary>
/// Default fail-safe implementation of <see cref="IGrantRiskAssessor"/> used when no real assessor
/// is configured. It returns the maximum risk score (<see cref="MaxRiskScore"/> = 100) so that any
/// risk-gated approval step (<see cref="ApprovalCondition.RiskScoreAbove"/>) always fires, and logs a
/// warning. This fails toward requiring human approval rather than silently bypassing the gate.
/// </summary>
internal sealed partial class DefaultGrantRiskAssessor(
	ILogger<DefaultGrantRiskAssessor> logger) : IGrantRiskAssessor
{
	/// <summary>The maximum (most-risky) score, per <see cref="IGrantRiskAssessor"/>'s documented 0-100 range.</summary>
	private const int MaxRiskScore = 100;

	/// <inheritdoc />
	public Task<int> AssessRiskAsync(
		string userId,
		string grantScope,
		string grantType,
		CancellationToken cancellationToken)
	{
		// Fail-safe: no real assessor is configured, so return the maximum score. This guarantees any
		// RiskScoreAbove approval step triggers (fail toward human review) instead of being silently
		// bypassed by a benign score (the lftwn1 fail-open defect).
		LogNoRiskAssessment(logger, userId, grantScope);
		return Task.FromResult(MaxRiskScore);
	}

	[LoggerMessage(EventId = 3550, Level = LogLevel.Warning,
		Message = "No IGrantRiskAssessor configured. Returning maximum risk score 100 (fail-safe) for user '{UserId}', scope '{GrantScope}'.")]
	private static partial void LogNoRiskAssessment(ILogger logger, string userId, string grantScope);
}
