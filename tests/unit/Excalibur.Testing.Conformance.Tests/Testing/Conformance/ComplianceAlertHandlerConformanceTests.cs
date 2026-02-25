// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;

using Excalibur.Testing.Conformance;

using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace Excalibur.Tests.Testing.Conformance;

/// <summary>
/// Conformance tests for <see cref="LoggingComplianceAlertHandler"/> validating IComplianceAlertHandler contract compliance.
/// </summary>
/// <remarks>
/// <para>
/// LoggingComplianceAlertHandler is the default implementation that logs SOC 2 compliance alerts
/// using structured logging. It has a simple constructor - only requires ILogger.
/// </para>
/// <para>
/// <strong>ALERT-HANDLER PATTERN:</strong> IComplianceAlertHandler is an event-driven notification
/// interface. Methods return Task.CompletedTask - they do NOT throw on valid inputs.
/// </para>
/// <para>
/// Key behaviors verified:
/// <list type="bullet">
/// <item><description>HandleComplianceGapAsync null throws ArgumentNullException</description></item>
/// <item><description>HandleComplianceGapAsync completes for all severity levels</description></item>
/// <item><description>HandleValidationFailureAsync null throws ArgumentNullException</description></item>
/// <item><description>HandleValidationFailureAsync completes for all severity levels</description></item>
/// <item><description>HandleStatusChangeAsync null throws ArgumentNullException</description></item>
/// <item><description>HandleStatusChangeAsync completes for compliance lost/restored transitions</description></item>
/// </list>
/// </para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
[Trait("Category", "Integration")]
[Trait("Component", "Compliance")]
[Trait("Pattern", "ALERT-HANDLER")]
public sealed class ComplianceAlertHandlerConformanceTests : ComplianceAlertHandlerConformanceTestKit
{
	/// <inheritdoc />
	protected override IComplianceAlertHandler CreateHandler() =>
		new LoggingComplianceAlertHandler(NullLogger<LoggingComplianceAlertHandler>.Instance);

	#region HandleComplianceGapAsync Tests

	[Fact]
	public Task HandleComplianceGapAsync_NullAlert_ShouldThrowArgumentNullException_Test() =>
		HandleComplianceGapAsync_NullAlert_ShouldThrowArgumentNullException();

	[Fact]
	public Task HandleComplianceGapAsync_LowSeverity_ShouldCompleteSuccessfully_Test() =>
		HandleComplianceGapAsync_LowSeverity_ShouldCompleteSuccessfully();

	[Fact]
	public Task HandleComplianceGapAsync_CriticalSeverity_ShouldCompleteSuccessfully_Test() =>
		HandleComplianceGapAsync_CriticalSeverity_ShouldCompleteSuccessfully();

	#endregion HandleComplianceGapAsync Tests

	#region HandleValidationFailureAsync Tests

	[Fact]
	public Task HandleValidationFailureAsync_NullAlert_ShouldThrowArgumentNullException_Test() =>
		HandleValidationFailureAsync_NullAlert_ShouldThrowArgumentNullException();

	[Fact]
	public Task HandleValidationFailureAsync_MediumSeverity_ShouldCompleteSuccessfully_Test() =>
		HandleValidationFailureAsync_MediumSeverity_ShouldCompleteSuccessfully();

	[Fact]
	public Task HandleValidationFailureAsync_CriticalSeverity_ShouldCompleteSuccessfully_Test() =>
		HandleValidationFailureAsync_CriticalSeverity_ShouldCompleteSuccessfully();

	#endregion HandleValidationFailureAsync Tests

	#region HandleStatusChangeAsync Tests

	[Fact]
	public Task HandleStatusChangeAsync_NullNotification_ShouldThrowArgumentNullException_Test() =>
		HandleStatusChangeAsync_NullNotification_ShouldThrowArgumentNullException();

	[Fact]
	public Task HandleStatusChangeAsync_ComplianceRestored_ShouldCompleteSuccessfully_Test() =>
		HandleStatusChangeAsync_ComplianceRestored_ShouldCompleteSuccessfully();

	[Fact]
	public Task HandleStatusChangeAsync_ComplianceLost_ShouldCompleteSuccessfully_Test() =>
		HandleStatusChangeAsync_ComplianceLost_ShouldCompleteSuccessfully();

	#endregion HandleStatusChangeAsync Tests
}
