// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;

using Excalibur.Testing.Conformance;

using Xunit;

namespace Excalibur.Tests.Testing.Conformance;

/// <summary>
/// Conformance tests for <see cref="EncryptionTelemetry"/> validating IEncryptionTelemetry contract compliance.
/// </summary>
/// <remarks>
/// <para>
/// EncryptionTelemetry is the OpenTelemetry-based implementation that records encryption-specific
/// metrics. It has a PARAMETERLESS constructor (zero dependencies).
/// </para>
/// <para>
/// <strong>TELEMETRY PATTERN:</strong> IEncryptionTelemetry is an observability interface.
/// Methods are void (fire-and-forget) but use STRICT null validation - all string parameters
/// throw ArgumentNullException on null. This differs from ComplianceMetrics which is LENIENT.
/// </para>
/// <para>
/// Key behaviors verified:
/// <list type="bullet">
/// <item><description>Meter property returns non-null with name "Excalibur.Dispatch.Encryption"</description></item>
/// <item><description>RecordOperation completes with valid params, throws on null</description></item>
/// <item><description>RecordOperationDuration completes with valid params, throws on null</description></item>
/// <item><description>UpdateProviderHealth completes with valid params, throws on null</description></item>
/// <item><description>RecordFieldsMigrated completes with count=0 or count>0</description></item>
/// <item><description>RecordKeyRotation completes with valid params, throws on null</description></item>
/// <item><description>RecordBytesProcessed completes with bytes=0 or bytes>0</description></item>
/// <item><description>RecordCacheAccess completes with hit=true or hit=false</description></item>
/// <item><description>UpdateActiveKeyCount completes successfully</description></item>
/// </list>
/// </para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
[Trait("Category", "Integration")]
[Trait("Component", "Compliance")]
[Trait("Pattern", "TELEMETRY")]
public sealed class EncryptionTelemetryConformanceTests : EncryptionTelemetryConformanceTestKit
{
	/// <inheritdoc />
	protected override IEncryptionTelemetry CreateTelemetry() =>
		new EncryptionTelemetry();

	#region Meter Property Tests

	[Fact]
	public void Meter_ShouldBeNonNullAndNamed_Test() =>
		Meter_ShouldBeNonNullAndNamed();

	#endregion Meter Property Tests

	#region RecordOperation Tests

	[Fact]
	public void RecordOperation_ShouldCompleteSuccessfully_Test() =>
		RecordOperation_ShouldCompleteSuccessfully();

	[Fact]
	public void RecordOperation_NullOperation_ShouldThrowArgumentNullException_Test() =>
		RecordOperation_NullOperation_ShouldThrowArgumentNullException();

	#endregion RecordOperation Tests

	#region RecordOperationDuration Tests

	[Fact]
	public void RecordOperationDuration_ShouldCompleteSuccessfully_Test() =>
		RecordOperationDuration_ShouldCompleteSuccessfully();

	[Fact]
	public void RecordOperationDuration_NullOperation_ShouldThrowArgumentNullException_Test() =>
		RecordOperationDuration_NullOperation_ShouldThrowArgumentNullException();

	#endregion RecordOperationDuration Tests

	#region UpdateProviderHealth Tests

	[Fact]
	public void UpdateProviderHealth_ShouldCompleteSuccessfully_Test() =>
		UpdateProviderHealth_ShouldCompleteSuccessfully();

	[Fact]
	public void UpdateProviderHealth_NullProvider_ShouldThrowArgumentNullException_Test() =>
		UpdateProviderHealth_NullProvider_ShouldThrowArgumentNullException();

	#endregion UpdateProviderHealth Tests

	#region RecordFieldsMigrated Tests

	[Fact]
	public void RecordFieldsMigrated_WithCount_ShouldCompleteSuccessfully_Test() =>
		RecordFieldsMigrated_WithCount_ShouldCompleteSuccessfully();

	[Fact]
	public void RecordFieldsMigrated_ZeroCount_ShouldCompleteSuccessfully_Test() =>
		RecordFieldsMigrated_ZeroCount_ShouldCompleteSuccessfully();

	#endregion RecordFieldsMigrated Tests

	#region RecordKeyRotation Tests

	[Fact]
	public void RecordKeyRotation_ShouldCompleteSuccessfully_Test() =>
		RecordKeyRotation_ShouldCompleteSuccessfully();

	[Fact]
	public void RecordKeyRotation_NullProvider_ShouldThrowArgumentNullException_Test() =>
		RecordKeyRotation_NullProvider_ShouldThrowArgumentNullException();

	#endregion RecordKeyRotation Tests

	#region RecordBytesProcessed Tests

	[Fact]
	public void RecordBytesProcessed_WithBytes_ShouldCompleteSuccessfully_Test() =>
		RecordBytesProcessed_WithBytes_ShouldCompleteSuccessfully();

	[Fact]
	public void RecordBytesProcessed_ZeroBytes_ShouldCompleteSuccessfully_Test() =>
		RecordBytesProcessed_ZeroBytes_ShouldCompleteSuccessfully();

	#endregion RecordBytesProcessed Tests

	#region RecordCacheAccess Tests

	[Fact]
	public void RecordCacheAccess_Hit_ShouldCompleteSuccessfully_Test() =>
		RecordCacheAccess_Hit_ShouldCompleteSuccessfully();

	[Fact]
	public void RecordCacheAccess_Miss_ShouldCompleteSuccessfully_Test() =>
		RecordCacheAccess_Miss_ShouldCompleteSuccessfully();

	#endregion RecordCacheAccess Tests

	#region UpdateActiveKeyCount Tests

	[Fact]
	public void UpdateActiveKeyCount_ShouldCompleteSuccessfully_Test() =>
		UpdateActiveKeyCount_ShouldCompleteSuccessfully();

	#endregion UpdateActiveKeyCount Tests
}
