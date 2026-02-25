// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Security.Tests.Compliance.Soc2;

/// <summary>
/// Unit tests for <see cref="ControlValidationService"/>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class ControlValidationServiceShould
{
	private readonly IControlValidator _fakeValidator;
	private readonly ControlValidationService _sut;

	public ControlValidationServiceShould()
	{
		_fakeValidator = A.Fake<IControlValidator>();
		_ = A.CallTo(() => _fakeValidator.SupportedControls).Returns(["SEC-001", "SEC-002"]);
		_ = A.CallTo(() => _fakeValidator.SupportedCriteria).Returns(
		[
			TrustServicesCriterion.CC6_LogicalAccess,
			TrustServicesCriterion.CC7_SystemOperations
		]);

		_sut = new ControlValidationService([_fakeValidator]);
	}

	#region Constructor Tests

	[Fact]
	public void Constructor_BuildControlMappings_FromValidators()
	{
		// Act
		var availableControls = _sut.GetAvailableControls();

		// Assert
		availableControls.ShouldContain("SEC-001");
		availableControls.ShouldContain("SEC-002");
	}

	[Fact]
	public void Constructor_HandleEmptyValidators()
	{
		// Act
		var sut = new ControlValidationService([]);
		var availableControls = sut.GetAvailableControls();

		// Assert
		availableControls.ShouldBeEmpty();
	}

	[Fact]
	public void Constructor_MergeMappings_FromMultipleValidators()
	{
		// Arrange
		var validator1 = A.Fake<IControlValidator>();
		_ = A.CallTo(() => validator1.SupportedControls).Returns(["SEC-001"]);
		_ = A.CallTo(() => validator1.SupportedCriteria).Returns([TrustServicesCriterion.CC6_LogicalAccess]);

		var validator2 = A.Fake<IControlValidator>();
		_ = A.CallTo(() => validator2.SupportedControls).Returns(["AVL-001"]);
		_ = A.CallTo(() => validator2.SupportedCriteria).Returns([TrustServicesCriterion.A1_InfrastructureManagement]);

		// Act
		var sut = new ControlValidationService([validator1, validator2]);
		var availableControls = sut.GetAvailableControls();

		// Assert
		availableControls.ShouldContain("SEC-001");
		availableControls.ShouldContain("AVL-001");
	}

	#endregion Constructor Tests

	#region ValidateControlAsync Tests

	[Fact]
	public async Task ValidateControlAsync_ReturnNotConfigured_WhenNoValidatorRegistered()
	{
		// Act
		var result = await _sut.ValidateControlAsync("UNKNOWN-001", CancellationToken.None);

		// Assert
		result.IsConfigured.ShouldBeFalse();
		result.IsEffective.ShouldBeFalse();
		result.EffectivenessScore.ShouldBe(0);
		result.ConfigurationIssues.ShouldContain(i => i.Contains("No validator registered"));
	}

	[Fact]
	public async Task ValidateControlAsync_DelegateToValidator_WhenRegistered()
	{
		// Arrange
		var expectedResult = new ControlValidationResult
		{
			ControlId = "SEC-001",
			IsConfigured = true,
			IsEffective = true,
			EffectivenessScore = 100
		};
		_ = A.CallTo(() => _fakeValidator.ValidateAsync("SEC-001", A<CancellationToken>._))
			.Returns(expectedResult);

		// Act
		var result = await _sut.ValidateControlAsync("SEC-001", CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
		_ = A.CallTo(() => _fakeValidator.ValidateAsync("SEC-001", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ValidateControlAsync_BeCaseInsensitive()
	{
		// Arrange
		var expectedResult = new ControlValidationResult
		{
			ControlId = "SEC-001",
			IsConfigured = true,
			IsEffective = true,
			EffectivenessScore = 100
		};
		_ = A.CallTo(() => _fakeValidator.ValidateAsync("SEC-001", A<CancellationToken>._))
			.Returns(expectedResult);

		// Act
		var result = await _sut.ValidateControlAsync("sec-001", CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		_ = A.CallTo(() => _fakeValidator.ValidateAsync("sec-001", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ValidateControlAsync_PassCancellationToken()
	{
		// Arrange
		var cts = new CancellationTokenSource();
		_ = A.CallTo(() => _fakeValidator.ValidateAsync("SEC-001", cts.Token))
			.Returns(new ControlValidationResult { ControlId = "SEC-001", IsConfigured = true, IsEffective = true, EffectivenessScore = 100 });

		// Act
		_ = await _sut.ValidateControlAsync("SEC-001", cts.Token);

		// Assert
		_ = A.CallTo(() => _fakeValidator.ValidateAsync("SEC-001", cts.Token))
			.MustHaveHappenedOnceExactly();
	}

	#endregion ValidateControlAsync Tests

	#region ValidateCriterionAsync Tests

	[Fact]
	public async Task ValidateCriterionAsync_ValidateAllControlsForCriterion()
	{
		// Arrange
		_ = A.CallTo(() => _fakeValidator.ValidateAsync(A<string>._, A<CancellationToken>._))
			.Returns(new ControlValidationResult { ControlId = "SEC-001", IsConfigured = true, IsEffective = true, EffectivenessScore = 100 });

		// Act
		var results = await _sut.ValidateCriterionAsync(TrustServicesCriterion.CC6_LogicalAccess, CancellationToken.None);

		// Assert
		results.Count.ShouldBe(2); // SEC-001 and SEC-002
	}

	[Fact]
	public async Task ValidateCriterionAsync_ReturnEmptyList_WhenNoCriterionControls()
	{
		// Arrange
		var sut = new ControlValidationService([]);

		// Act
		var results = await sut.ValidateCriterionAsync(TrustServicesCriterion.CC6_LogicalAccess, CancellationToken.None);

		// Assert
		results.ShouldBeEmpty();
	}

	[Fact]
	public async Task ValidateCriterionAsync_ReturnAllValidationResults()
	{
		// Arrange
		_ = A.CallTo(() => _fakeValidator.ValidateAsync("SEC-001", A<CancellationToken>._))
			.Returns(new ControlValidationResult { ControlId = "SEC-001", IsConfigured = true, IsEffective = true, EffectivenessScore = 100 });
		_ = A.CallTo(() => _fakeValidator.ValidateAsync("SEC-002", A<CancellationToken>._))
			.Returns(new ControlValidationResult { ControlId = "SEC-002", IsConfigured = true, IsEffective = false, EffectivenessScore = 50 });

		// Act
		var results = await _sut.ValidateCriterionAsync(TrustServicesCriterion.CC6_LogicalAccess, CancellationToken.None);

		// Assert
		results.ShouldContain(r => r.ControlId == "SEC-001" && r.IsEffective);
		results.ShouldContain(r => r.ControlId == "SEC-002" && !r.IsEffective);
	}

	#endregion ValidateCriterionAsync Tests

	#region RunControlTestAsync Tests

	[Fact]
	public async Task RunControlTestAsync_ReturnControlFailure_WhenNoValidatorRegistered()
	{
		// Arrange
		var parameters = new ControlTestParameters { SampleSize = 25 };

		// Act
		var result = await _sut.RunControlTestAsync("UNKNOWN-001", parameters, CancellationToken.None);

		// Assert
		result.Outcome.ShouldBe(TestOutcome.ControlFailure);
		result.ItemsTested.ShouldBe(0);
		result.Exceptions.ShouldContain(e => e.Description.Contains("No validator registered"));
	}

	[Fact]
	public async Task RunControlTestAsync_DelegateToValidator_WhenRegistered()
	{
		// Arrange
		var parameters = new ControlTestParameters
		{
			SampleSize = 50,
			PeriodStart = DateTimeOffset.UtcNow.AddDays(-30),
			PeriodEnd = DateTimeOffset.UtcNow
		};
		var expectedResult = new ControlTestResult
		{
			ControlId = "SEC-001",
			Parameters = parameters,
			ItemsTested = 50,
			ExceptionsFound = 0,
			Outcome = TestOutcome.NoExceptions
		};
		_ = A.CallTo(() => _fakeValidator.RunTestAsync("SEC-001", parameters, A<CancellationToken>._))
			.Returns(expectedResult);

		// Act
		var result = await _sut.RunControlTestAsync("SEC-001", parameters, CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
	}

	[Fact]
	public async Task RunControlTestAsync_PassParametersToValidator()
	{
		// Arrange
		var parameters = new ControlTestParameters
		{
			SampleSize = 100,
			IncludeDetailedEvidence = true
		};
		_ = A.CallTo(() => _fakeValidator.RunTestAsync("SEC-001", parameters, A<CancellationToken>._))
			.Returns(new ControlTestResult { ControlId = "SEC-001", Parameters = parameters, ItemsTested = 25, ExceptionsFound = 0, Outcome = TestOutcome.NoExceptions });

		// Act
		_ = await _sut.RunControlTestAsync("SEC-001", parameters, CancellationToken.None);

		// Assert
		_ = A.CallTo(() => _fakeValidator.RunTestAsync("SEC-001", parameters, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task RunControlTestAsync_SetCriticalSeverity_ForUnregisteredControl()
	{
		// Arrange
		var parameters = new ControlTestParameters { SampleSize = 25 };

		// Act
		var result = await _sut.RunControlTestAsync("UNKNOWN-001", parameters, CancellationToken.None);

		// Assert
		result.Exceptions[0].Severity.ShouldBe(GapSeverity.Critical);
	}

	#endregion RunControlTestAsync Tests

	#region GetAvailableControls Tests

	[Fact]
	public void GetAvailableControls_ReturnAllRegisteredControls()
	{
		// Act
		var controls = _sut.GetAvailableControls();

		// Assert
		controls.Count.ShouldBe(2);
		controls.ShouldContain("SEC-001");
		controls.ShouldContain("SEC-002");
	}

	[Fact]
	public void GetAvailableControls_ReturnEmptyList_WhenNoValidators()
	{
		// Arrange
		var sut = new ControlValidationService([]);

		// Act
		var controls = sut.GetAvailableControls();

		// Assert
		controls.ShouldBeEmpty();
	}

	#endregion GetAvailableControls Tests

	#region GetControlsForCriterion Tests

	[Fact]
	public void GetControlsForCriterion_ReturnMappedControls()
	{
		// Act
		var controls = _sut.GetControlsForCriterion(TrustServicesCriterion.CC6_LogicalAccess);

		// Assert
		controls.ShouldContain("SEC-001");
		controls.ShouldContain("SEC-002");
	}

	[Fact]
	public void GetControlsForCriterion_ReturnEmptyList_WhenNoCriterionMapped()
	{
		// Act
		var controls = _sut.GetControlsForCriterion(TrustServicesCriterion.A1_InfrastructureManagement);

		// Assert
		controls.ShouldBeEmpty();
	}

	[Fact]
	public void GetControlsForCriterion_ReturnDistinctControls_FromMultipleValidators()
	{
		// Arrange
		var validator1 = A.Fake<IControlValidator>();
		_ = A.CallTo(() => validator1.SupportedControls).Returns(["SEC-001", "SEC-002"]);
		_ = A.CallTo(() => validator1.SupportedCriteria).Returns([TrustServicesCriterion.CC6_LogicalAccess]);

		var validator2 = A.Fake<IControlValidator>();
		_ = A.CallTo(() => validator2.SupportedControls).Returns(["SEC-003"]);
		_ = A.CallTo(() => validator2.SupportedCriteria).Returns([TrustServicesCriterion.CC6_LogicalAccess]);

		var sut = new ControlValidationService([validator1, validator2]);

		// Act
		var controls = sut.GetControlsForCriterion(TrustServicesCriterion.CC6_LogicalAccess);

		// Assert
		controls.Count.ShouldBe(3);
		controls.ShouldContain("SEC-001");
		controls.ShouldContain("SEC-002");
		controls.ShouldContain("SEC-003");
	}

	#endregion GetControlsForCriterion Tests
}
