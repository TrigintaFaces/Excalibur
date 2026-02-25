// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;

using Excalibur.Testing.Conformance;

using Xunit;

namespace Excalibur.Tests.Testing.Conformance;

/// <summary>
/// Conformance tests for <see cref="ControlValidationService"/> validating IControlValidationService contract compliance.
/// </summary>
/// <remarks>
/// <para>
/// ControlValidationService is a SOC 2 control validation orchestrator that coordinates multiple
/// IControlValidator instances via collection injection.
/// </para>
/// <para>
/// <strong>SERVICE PATTERN:</strong> IControlValidationService is an orchestrator with 5 methods
/// (3 async + 2 sync). It uses collection injection (IEnumerable&lt;IControlValidator&gt;) to receive
/// validators and builds internal mappings for control-to-validator and criterion-to-controls.
/// </para>
/// <para>
/// Key behaviors verified:
/// <list type="bullet">
/// <item><description>ValidateControlAsync returns result for registered controls</description></item>
/// <item><description>ValidateControlAsync returns failure for unregistered controls</description></item>
/// <item><description>ValidateCriterionAsync validates all controls in a criterion</description></item>
/// <item><description>RunControlTestAsync returns result for registered controls</description></item>
/// <item><description>GetAvailableControls returns control IDs from validators</description></item>
/// <item><description>GetControlsForCriterion returns controls mapped to the criterion</description></item>
/// </list>
/// </para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
[Trait("Category", "Integration")]
[Trait("Component", "Compliance")]
[Trait("Pattern", "SERVICE")]
public sealed class ControlValidationServiceConformanceTests : ControlValidationServiceConformanceTestKit
{
	/// <inheritdoc />
	protected override IControlValidationService CreateService()
	{
		// Inject AuditLogControlValidator - parameterless due to optional dependencies
		var validators = new IControlValidator[] { new AuditLogControlValidator() };
		return new ControlValidationService(validators);
	}

	#region ValidateControlAsync Method Tests

	[Fact]
	public Task ValidateControlAsync_RegisteredControl_ShouldReturnResult_Test() =>
		ValidateControlAsync_RegisteredControl_ShouldReturnResult();

	[Fact]
	public Task ValidateControlAsync_RegisteredControl_ShouldReturnResultWithRequiredProperties_Test() =>
		ValidateControlAsync_RegisteredControl_ShouldReturnResultWithRequiredProperties();

	[Fact]
	public Task ValidateControlAsync_UnregisteredControl_ShouldReturnFailure_Test() =>
		ValidateControlAsync_UnregisteredControl_ShouldReturnFailure();

	#endregion ValidateControlAsync Method Tests

	#region ValidateCriterionAsync Method Tests

	[Fact]
	public Task ValidateCriterionAsync_RegisteredCriterion_ShouldReturnResults_Test() =>
		ValidateCriterionAsync_RegisteredCriterion_ShouldReturnResults();

	[Fact]
	public Task ValidateCriterionAsync_RegisteredCriterion_ShouldValidateAllControls_Test() =>
		ValidateCriterionAsync_RegisteredCriterion_ShouldValidateAllControls();

	[Fact]
	public Task ValidateCriterionAsync_UnregisteredCriterion_ShouldReturnEmpty_Test() =>
		ValidateCriterionAsync_UnregisteredCriterion_ShouldReturnEmpty();

	#endregion ValidateCriterionAsync Method Tests

	#region RunControlTestAsync Method Tests

	[Fact]
	public Task RunControlTestAsync_RegisteredControl_ShouldReturnResult_Test() =>
		RunControlTestAsync_RegisteredControl_ShouldReturnResult();

	[Fact]
	public Task RunControlTestAsync_RegisteredControl_ShouldReturnResultWithValidProperties_Test() =>
		RunControlTestAsync_RegisteredControl_ShouldReturnResultWithValidProperties();

	[Fact]
	public Task RunControlTestAsync_UnregisteredControl_ShouldReturnFailure_Test() =>
		RunControlTestAsync_UnregisteredControl_ShouldReturnFailure();

	#endregion RunControlTestAsync Method Tests

	#region GetAvailableControls Method Tests

	[Fact]
	public void GetAvailableControls_ShouldNotBeNull_Test() =>
		GetAvailableControls_ShouldNotBeNull();

	[Fact]
	public void GetAvailableControls_WithValidators_ShouldReturnControlIds_Test() =>
		GetAvailableControls_WithValidators_ShouldReturnControlIds();

	#endregion GetAvailableControls Method Tests

	#region GetControlsForCriterion Method Tests

	[Fact]
	public void GetControlsForCriterion_ShouldNotBeNull_Test() =>
		GetControlsForCriterion_ShouldNotBeNull();

	[Fact]
	public void GetControlsForCriterion_RegisteredCriterion_ShouldReturnControls_Test() =>
		GetControlsForCriterion_RegisteredCriterion_ShouldReturnControls();

	[Fact]
	public void GetControlsForCriterion_UnregisteredCriterion_ShouldReturnEmpty_Test() =>
		GetControlsForCriterion_UnregisteredCriterion_ShouldReturnEmpty();

	#endregion GetControlsForCriterion Method Tests
}
