// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;

using Excalibur.Testing.Conformance;

using Xunit;

namespace Excalibur.Tests.Testing.Conformance;

/// <summary>
/// Conformance tests for <see cref="AuditLogControlValidator"/> validating IControlValidator contract compliance.
/// </summary>
/// <remarks>
/// <para>
/// AuditLogControlValidator is a SOC 2 control validator for audit logging controls (SEC-004, SEC-005).
/// It has OPTIONAL dependencies - both IAuditLogger and IAuditStore can be null.
/// </para>
/// <para>
/// <strong>VALIDATOR PATTERN:</strong> IControlValidator is a mixed sync/async interface with
/// base class inheritance. BaseControlValidator provides default RunTestAsync implementation.
/// </para>
/// <para>
/// Key behaviors verified:
/// <list type="bullet">
/// <item><description>SupportedControls returns non-null, non-empty list</description></item>
/// <item><description>SupportedCriteria returns non-null, non-empty list</description></item>
/// <item><description>ValidateAsync returns result for supported controls</description></item>
/// <item><description>ValidateAsync returns failure for unsupported controls</description></item>
/// <item><description>RunTestAsync returns result with valid properties</description></item>
/// <item><description>GetControlDescription returns non-null for supported, null for unsupported</description></item>
/// </list>
/// </para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
[Trait("Category", "Integration")]
[Trait("Component", "Compliance")]
[Trait("Pattern", "VALIDATOR")]
public sealed class ControlValidatorConformanceTests : ControlValidatorConformanceTestKit
{
	/// <inheritdoc />
	protected override IControlValidator CreateValidator() =>
		new AuditLogControlValidator();

	#region SupportedControls Property Tests

	[Fact]
	public void SupportedControls_ShouldNotBeNull_Test() =>
		SupportedControls_ShouldNotBeNull();

	[Fact]
	public void SupportedControls_ShouldNotBeEmpty_Test() =>
		SupportedControls_ShouldNotBeEmpty();

	#endregion SupportedControls Property Tests

	#region SupportedCriteria Property Tests

	[Fact]
	public void SupportedCriteria_ShouldNotBeNull_Test() =>
		SupportedCriteria_ShouldNotBeNull();

	[Fact]
	public void SupportedCriteria_ShouldNotBeEmpty_Test() =>
		SupportedCriteria_ShouldNotBeEmpty();

	#endregion SupportedCriteria Property Tests

	#region ValidateAsync Method Tests

	[Fact]
	public Task ValidateAsync_SupportedControl_ShouldReturnResult_Test() =>
		ValidateAsync_SupportedControl_ShouldReturnResult();

	[Fact]
	public Task ValidateAsync_UnsupportedControl_ShouldReturnFailure_Test() =>
		ValidateAsync_UnsupportedControl_ShouldReturnFailure();

	[Fact]
	public Task ValidateAsync_SupportedControl_ShouldReturnResultWithRequiredProperties_Test() =>
		ValidateAsync_SupportedControl_ShouldReturnResultWithRequiredProperties();

	#endregion ValidateAsync Method Tests

	#region RunTestAsync Method Tests

	[Fact]
	public Task RunTestAsync_SupportedControl_ShouldReturnResult_Test() =>
		RunTestAsync_SupportedControl_ShouldReturnResult();

	[Fact]
	public Task RunTestAsync_SupportedControl_ShouldReturnResultWithValidProperties_Test() =>
		RunTestAsync_SupportedControl_ShouldReturnResultWithValidProperties();

	[Fact]
	public Task RunTestAsync_UnsupportedControl_ShouldReturnExceptions_Test() =>
		RunTestAsync_UnsupportedControl_ShouldReturnExceptions();

	#endregion RunTestAsync Method Tests

	#region GetControlDescription Method Tests

	[Fact]
	public void GetControlDescription_SupportedControl_ShouldReturnDescription_Test() =>
		GetControlDescription_SupportedControl_ShouldReturnDescription();

	[Fact]
	public void GetControlDescription_UnsupportedControl_ShouldReturnNull_Test() =>
		GetControlDescription_UnsupportedControl_ShouldReturnNull();

	#endregion GetControlDescription Method Tests
}
