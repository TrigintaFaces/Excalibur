// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Governance.Provisioning;

using Microsoft.Extensions.Logging;

namespace Excalibur.A3.Governance;

/// <summary>
/// Default implementation of <see cref="IGrantRiskAssessor"/> that returns a risk score of 0
/// and logs a warning indicating that no risk assessment logic is configured.
/// </summary>
internal sealed partial class DefaultGrantRiskAssessor(
	ILogger<DefaultGrantRiskAssessor> logger) : IGrantRiskAssessor
{
	/// <inheritdoc />
	public Task<int> AssessRiskAsync(
		string userId,
		string grantScope,
		string grantType,
		CancellationToken cancellationToken)
	{
		LogNoRiskAssessment(logger, userId, grantScope);
		return Task.FromResult(0);
	}

	[LoggerMessage(EventId = 3550, Level = LogLevel.Warning,
		Message = "No IGrantRiskAssessor configured. Returning risk score 0 for user '{UserId}', scope '{GrantScope}'.")]
	private static partial void LogNoRiskAssessment(ILogger logger, string userId, string grantScope);
}
