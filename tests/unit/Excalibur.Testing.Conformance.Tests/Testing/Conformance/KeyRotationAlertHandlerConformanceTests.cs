// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;

using Excalibur.Testing.Conformance;

using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace Excalibur.Tests.Testing.Conformance;

/// <summary>
/// Conformance tests for <see cref="LoggingAlertHandler"/> validating IKeyRotationAlertHandler contract compliance.
/// </summary>
/// <remarks>
/// <para>
/// LoggingAlertHandler is the default implementation that logs alerts using structured logging.
/// It has the SIMPLEST constructor - only requires ILogger.
/// </para>
/// <para>
/// <strong>ALERT-HANDLER PATTERN:</strong> IKeyRotationAlertHandler is an event-driven notification
/// interface. Methods return Task.CompletedTask - they do NOT throw on valid inputs.
/// </para>
/// <para>
/// Key behaviors verified:
/// <list type="bullet">
/// <item><description>HandleRotationFailureAsync null throws ArgumentNullException</description></item>
/// <item><description>HandleRotationFailureAsync completes for all severity levels</description></item>
/// <item><description>HandleExpirationWarningAsync null throws ArgumentNullException</description></item>
/// <item><description>HandleExpirationWarningAsync completes for all severity levels</description></item>
/// <item><description>HandleRotationSuccessAsync null throws ArgumentNullException</description></item>
/// <item><description>HandleRotationSuccessAsync completes with various version combinations</description></item>
/// </list>
/// </para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
[Trait("Category", "Integration")]
[Trait("Component", "Compliance")]
[Trait("Pattern", "ALERT-HANDLER")]
public sealed class KeyRotationAlertHandlerConformanceTests : KeyRotationAlertHandlerConformanceTestKit
{
	/// <inheritdoc />
	protected override IKeyRotationAlertHandler CreateHandler() =>
		new LoggingAlertHandler(NullLogger<LoggingAlertHandler>.Instance);

	#region HandleRotationFailureAsync Tests

	[Fact]
	public Task HandleRotationFailureAsync_NullAlert_ShouldThrowArgumentNullException_Test() =>
		HandleRotationFailureAsync_NullAlert_ShouldThrowArgumentNullException();

	[Fact]
	public Task HandleRotationFailureAsync_LowSeverity_ShouldCompleteSuccessfully_Test() =>
		HandleRotationFailureAsync_LowSeverity_ShouldCompleteSuccessfully();

	[Fact]
	public Task HandleRotationFailureAsync_MediumSeverity_ShouldCompleteSuccessfully_Test() =>
		HandleRotationFailureAsync_MediumSeverity_ShouldCompleteSuccessfully();

	[Fact]
	public Task HandleRotationFailureAsync_CriticalSeverity_ShouldCompleteSuccessfully_Test() =>
		HandleRotationFailureAsync_CriticalSeverity_ShouldCompleteSuccessfully();

	#endregion HandleRotationFailureAsync Tests

	#region HandleExpirationWarningAsync Tests

	[Fact]
	public Task HandleExpirationWarningAsync_NullAlert_ShouldThrowArgumentNullException_Test() =>
		HandleExpirationWarningAsync_NullAlert_ShouldThrowArgumentNullException();

	[Fact]
	public Task HandleExpirationWarningAsync_LowSeverity_ShouldCompleteSuccessfully_Test() =>
		HandleExpirationWarningAsync_LowSeverity_ShouldCompleteSuccessfully();

	[Fact]
	public Task HandleExpirationWarningAsync_HighSeverity_ShouldCompleteSuccessfully_Test() =>
		HandleExpirationWarningAsync_HighSeverity_ShouldCompleteSuccessfully();

	[Fact]
	public Task HandleExpirationWarningAsync_CriticalSeverity_ShouldCompleteSuccessfully_Test() =>
		HandleExpirationWarningAsync_CriticalSeverity_ShouldCompleteSuccessfully();

	#endregion HandleExpirationWarningAsync Tests

	#region HandleRotationSuccessAsync Tests

	[Fact]
	public Task HandleRotationSuccessAsync_NullNotification_ShouldThrowArgumentNullException_Test() =>
		HandleRotationSuccessAsync_NullNotification_ShouldThrowArgumentNullException();

	[Fact]
	public Task HandleRotationSuccessAsync_ValidNotification_ShouldCompleteSuccessfully_Test() =>
		HandleRotationSuccessAsync_ValidNotification_ShouldCompleteSuccessfully();

	[Fact]
	public Task HandleRotationSuccessAsync_NullOldVersion_ShouldCompleteSuccessfully_Test() =>
		HandleRotationSuccessAsync_NullOldVersion_ShouldCompleteSuccessfully();

	[Fact]
	public Task HandleRotationSuccessAsync_NullBothVersions_ShouldCompleteSuccessfully_Test() =>
		HandleRotationSuccessAsync_NullBothVersions_ShouldCompleteSuccessfully();

	#endregion HandleRotationSuccessAsync Tests
}
