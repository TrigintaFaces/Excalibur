// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;

using Excalibur.Testing.Conformance;

using Xunit;

namespace Excalibur.Tests.Testing.Conformance;

/// <summary>
/// Conformance tests for <see cref="ComplianceMetrics"/> validating IComplianceMetrics contract compliance.
/// </summary>
/// <remarks>
/// <para>
/// ComplianceMetrics is the OpenTelemetry-based implementation that records metrics using
/// System.Diagnostics.Metrics API. It has the SIMPLEST constructor - PARAMETERLESS (zero dependencies).
/// </para>
/// <para>
/// <strong>METRICS PATTERN:</strong> IComplianceMetrics is an observability interface.
/// Methods are void (fire-and-forget) - they do NOT throw on valid inputs.
/// </para>
/// <para>
/// Key behaviors verified:
/// <list type="bullet">
/// <item><description>Meter property returns non-null with name "Excalibur.Dispatch.Compliance"</description></item>
/// <item><description>RecordKeyRotation completes successfully</description></item>
/// <item><description>RecordKeyRotationFailure completes successfully</description></item>
/// <item><description>UpdateKeysNearingExpiration completes successfully</description></item>
/// <item><description>RecordEncryptionLatency completes with success true/false</description></item>
/// <item><description>RecordEncryptionOperation completes with/without bytes</description></item>
/// <item><description>RecordAuditEventLogged completes with/without tenantId</description></item>
/// <item><description>UpdateAuditBacklogSize completes successfully</description></item>
/// <item><description>RecordAuditIntegrityCheck completes with/without violations</description></item>
/// <item><description>RecordKeyUsage completes successfully</description></item>
/// </list>
/// </para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
[Trait("Category", "Integration")]
[Trait("Component", "Compliance")]
[Trait("Pattern", "METRICS")]
public sealed class ComplianceMetricsConformanceTests : ComplianceMetricsConformanceTestKit
{
	/// <inheritdoc />
	protected override IComplianceMetrics CreateMetrics() =>
		new ComplianceMetrics();

	#region Meter Property Tests

	[Fact]
	public void Meter_ShouldBeNonNullAndNamed_Test() =>
		Meter_ShouldBeNonNullAndNamed();

	#endregion Meter Property Tests

	#region RecordKeyRotation Tests

	[Fact]
	public void RecordKeyRotation_ShouldCompleteSuccessfully_Test() =>
		RecordKeyRotation_ShouldCompleteSuccessfully();

	#endregion RecordKeyRotation Tests

	#region RecordKeyRotationFailure Tests

	[Fact]
	public void RecordKeyRotationFailure_ShouldCompleteSuccessfully_Test() =>
		RecordKeyRotationFailure_ShouldCompleteSuccessfully();

	#endregion RecordKeyRotationFailure Tests

	#region UpdateKeysNearingExpiration Tests

	[Fact]
	public void UpdateKeysNearingExpiration_ShouldCompleteSuccessfully_Test() =>
		UpdateKeysNearingExpiration_ShouldCompleteSuccessfully();

	#endregion UpdateKeysNearingExpiration Tests

	#region RecordEncryptionLatency Tests

	[Fact]
	public void RecordEncryptionLatency_SuccessTrue_ShouldCompleteSuccessfully_Test() =>
		RecordEncryptionLatency_SuccessTrue_ShouldCompleteSuccessfully();

	[Fact]
	public void RecordEncryptionLatency_SuccessFalse_ShouldCompleteSuccessfully_Test() =>
		RecordEncryptionLatency_SuccessFalse_ShouldCompleteSuccessfully();

	#endregion RecordEncryptionLatency Tests

	#region RecordEncryptionOperation Tests

	[Fact]
	public void RecordEncryptionOperation_WithBytes_ShouldCompleteSuccessfully_Test() =>
		RecordEncryptionOperation_WithBytes_ShouldCompleteSuccessfully();

	[Fact]
	public void RecordEncryptionOperation_ZeroBytes_ShouldCompleteSuccessfully_Test() =>
		RecordEncryptionOperation_ZeroBytes_ShouldCompleteSuccessfully();

	#endregion RecordEncryptionOperation Tests

	#region RecordAuditEventLogged Tests

	[Fact]
	public void RecordAuditEventLogged_WithTenant_ShouldCompleteSuccessfully_Test() =>
		RecordAuditEventLogged_WithTenant_ShouldCompleteSuccessfully();

	[Fact]
	public void RecordAuditEventLogged_WithoutTenant_ShouldCompleteSuccessfully_Test() =>
		RecordAuditEventLogged_WithoutTenant_ShouldCompleteSuccessfully();

	#endregion RecordAuditEventLogged Tests

	#region UpdateAuditBacklogSize Tests

	[Fact]
	public void UpdateAuditBacklogSize_ShouldCompleteSuccessfully_Test() =>
		UpdateAuditBacklogSize_ShouldCompleteSuccessfully();

	#endregion UpdateAuditBacklogSize Tests

	#region RecordAuditIntegrityCheck Tests

	[Fact]
	public void RecordAuditIntegrityCheck_WithViolations_ShouldCompleteSuccessfully_Test() =>
		RecordAuditIntegrityCheck_WithViolations_ShouldCompleteSuccessfully();

	[Fact]
	public void RecordAuditIntegrityCheck_NoViolations_ShouldCompleteSuccessfully_Test() =>
		RecordAuditIntegrityCheck_NoViolations_ShouldCompleteSuccessfully();

	#endregion RecordAuditIntegrityCheck Tests

	#region RecordKeyUsage Tests

	[Fact]
	public void RecordKeyUsage_ShouldCompleteSuccessfully_Test() =>
		RecordKeyUsage_ShouldCompleteSuccessfully();

	#endregion RecordKeyUsage Tests
}
