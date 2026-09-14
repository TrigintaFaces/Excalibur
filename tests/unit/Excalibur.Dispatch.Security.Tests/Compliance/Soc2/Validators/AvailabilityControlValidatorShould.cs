using Excalibur.Compliance.Soc2.Validators;
// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Security.Tests.Compliance.Soc2.Validators;

/// <summary>
/// Unit tests for <see cref="AvailabilityControlValidator"/>.
/// Tests T401.12 scenarios: backup verification, health monitoring, and performance metrics validation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Security)]
public sealed class AvailabilityControlValidatorShould
{
	private readonly IComplianceMetrics? _fakeComplianceMetrics;
	private readonly IBackupConfigurationProvider? _fakeBackupConfigProvider;
	private AvailabilityControlValidator _sut;

	public AvailabilityControlValidatorShould()
	{
		_fakeComplianceMetrics = A.Fake<IComplianceMetrics>();
		_fakeBackupConfigProvider = A.Fake<IBackupConfigurationProvider>();
		_sut = new AvailabilityControlValidator(_fakeComplianceMetrics, _fakeBackupConfigProvider);
	}

	#region SupportedControls Tests

	[Fact]
	public void SupportedControls_ReturnCorrectControlIds()
	{
		// Act
		var controls = _sut.SupportedControls;

		// Assert
		controls.ShouldContain("AVL-001");
		controls.ShouldContain("AVL-002");
		controls.ShouldContain("AVL-003");
		controls.Count.ShouldBe(3);
	}

	#endregion

	#region SupportedCriteria Tests

	[Fact]
	public void SupportedCriteria_ReturnCorrectCriteria()
	{
		// Act
		var criteria = _sut.SupportedCriteria;

		// Assert
		criteria.ShouldContain(TrustServicesCriterion.A1_InfrastructureManagement);
		criteria.ShouldContain(TrustServicesCriterion.A2_CapacityManagement);
		criteria.ShouldContain(TrustServicesCriterion.A3_BackupRecovery);
	}

	#endregion

	#region ValidateAsync - AVL-001 (Health Monitoring) Tests

	[Fact]
	public async Task ValidateAsync_AVL001_ReturnSuccess_ForHealthMonitoring()
	{
		// Act
		var result = await _sut.ValidateAsync("AVL-001", CancellationToken.None);

		// Assert
		// Health monitoring is performed by the HOST, typically ASP.NET Core health checks, and is
		// not observable from this framework. Offering the capability is not operating the control.
		result.IsEffective.ShouldBeFalse();
		result.EffectivenessScore.ShouldBeInRange(1, 99);
		result.ConfigurationIssues.ShouldNotBeEmpty();
		result.ControlId.ShouldBe("AVL-001");
	}

	[Fact]
	public async Task ValidateAsync_AVL001_CollectHealthCheckEvidence()
	{
		// Act
		var result = await _sut.ValidateAsync("AVL-001", CancellationToken.None);

		// Assert
		result.Evidence.ShouldContain(e => e.Description.Contains("Health"));
	}

	#endregion

	#region ValidateAsync - AVL-002 (Performance Metrics) Tests

	[Fact]
	public async Task ValidateAsync_AVL002_ReportsConfiguredButUnverified_WhenComplianceMetricsPresent()
	{
		// Act
		var result = await _sut.ValidateAsync("AVL-002", CancellationToken.None);

		// Assert
		// Renamed from ReturnSuccess_When...Configured, which stated the substitution in its own name:
		// a component being CONFIGURED was read as the control SUCCEEDING. The mechanism is present and
		// the result still says so; nothing here observed it operating.
		result.IsConfigured.ShouldBeTrue();
		result.IsEffective.ShouldBeFalse();
		result.ControlId.ShouldBe("AVL-002");
	}

	[Fact]
	public async Task ValidateAsync_AVL002_ReportUnverified_WhenNoComplianceMetrics()
	{
		// Arrange
		_sut = new AvailabilityControlValidator(null, _fakeBackupConfigProvider);

		// Act
		var result = await _sut.ValidateAsync("AVL-002", CancellationToken.None);

		// Assert
		result.IsEffective.ShouldBeFalse();
		// Partial by design: a compensating external arrangement may exist, but the declared control is
		// absent and unverifiable here — neither a pass nor a total failure. The band is the property;
		// the exact figure is the framework's encoding and may be restated without changing the meaning.
		result.EffectivenessScore.ShouldBeInRange(1, 99);
		result.ConfigurationIssues.ShouldContain(i => i.Contains("IComplianceMetrics"));
	}

	#endregion

	#region ValidateAsync - AVL-003 (Backup Verification) Tests

	[Fact]
	public async Task ValidateAsync_AVL003_ReportsConfiguredButUnverified_WhenBackupPresent()
	{
		// Arrange
		_ = A.CallTo(() => _fakeBackupConfigProvider.IsBackupConfigured).Returns(true);
		_ = A.CallTo(() => _fakeBackupConfigProvider.BackupProviderName).Returns("SqlServerSnapshotStore");
		_ = A.CallTo(() => _fakeBackupConfigProvider.ConfigurationDescription).Returns("SQL Server event store snapshots");

		// Act
		var result = await _sut.ValidateAsync("AVL-003", CancellationToken.None);

		// Assert
		// Renamed from ReturnSuccess_When...Configured, which stated the substitution in its own name:
		// a component being CONFIGURED was read as the control SUCCEEDING. The mechanism is present and
		// the result still says so; nothing here observed it operating.
		result.IsConfigured.ShouldBeTrue();
		result.IsEffective.ShouldBeFalse();
		result.ControlId.ShouldBe("AVL-003");
		result.Evidence.ShouldContain(e => e.Description.Contains("SqlServerSnapshotStore"));
	}

	[Fact]
	public async Task ValidateAsync_AVL003_ReportUnverified_WhenBackupNotConfigured_WithRecommendation()
	{
		// Arrange
		_ = A.CallTo(() => _fakeBackupConfigProvider.IsBackupConfigured).Returns(false);

		// Act
		var result = await _sut.ValidateAsync("AVL-003", CancellationToken.None);

		// Assert
		result.IsEffective.ShouldBeFalse();
		// Partial by design: a compensating external arrangement may exist, but the declared control is
		// absent and unverifiable here — neither a pass nor a total failure. The band is the property;
		// the exact figure is the framework's encoding and may be restated without changing the meaning.
		result.EffectivenessScore.ShouldBeInRange(1, 99);
		result.ConfigurationIssues.ShouldContain(i => i.Contains("reports itself not configured"));
		result.Evidence.ShouldContain(e => e.Description.Contains("not configured"));
		result.Evidence.ShouldContain(e => e.Description.Contains("Recommendation"));
	}

	[Fact]
	public async Task ValidateAsync_AVL003_ReportUnverified_WhenBackupProviderNull_WithRecommendation()
	{
		// Arrange
		_sut = new AvailabilityControlValidator(_fakeComplianceMetrics, null);

		// Act
		var result = await _sut.ValidateAsync("AVL-003", CancellationToken.None);

		// Assert
		result.IsEffective.ShouldBeFalse();
		// Partial by design: a compensating external arrangement may exist, but the declared control is
		// absent and unverifiable here — neither a pass nor a total failure. The band is the property;
		// the exact figure is the framework's encoding and may be restated without changing the meaning.
		result.EffectivenessScore.ShouldBeInRange(1, 99);
		result.ConfigurationIssues.ShouldContain(i => i.Contains("not registered"));
		result.Evidence.ShouldContain(e => e.Description.Contains("not registered"));
		result.Evidence.ShouldContain(e => e.Description.Contains("Recommendation"));
	}

	[Fact]
	public async Task ValidateAsync_AVL003_EvidenceContainsConfigurationStatus()
	{
		// Arrange
		_ = A.CallTo(() => _fakeBackupConfigProvider.IsBackupConfigured).Returns(true);
		_ = A.CallTo(() => _fakeBackupConfigProvider.BackupProviderName).Returns("AzureBlobSnapshotStore");
		_ = A.CallTo(() => _fakeBackupConfigProvider.ConfigurationDescription).Returns("Azure Blob Storage backups");

		// Act
		var result = await _sut.ValidateAsync("AVL-003", CancellationToken.None);

		// Assert
		result.Evidence.ShouldContain(e => e.Type == EvidenceType.Configuration);
		result.Evidence.ShouldContain(e => e.Description.Contains("AzureBlobSnapshotStore"));
	}

	[Fact]
	public async Task ValidateAsync_AVL003_NoPlaceholderTextInEvidence()
	{
		// Arrange
		_ = A.CallTo(() => _fakeBackupConfigProvider.IsBackupConfigured).Returns(true);
		_ = A.CallTo(() => _fakeBackupConfigProvider.BackupProviderName).Returns("TestProvider");
		_ = A.CallTo(() => _fakeBackupConfigProvider.ConfigurationDescription).Returns("Test backup configuration");

		// Act
		var result = await _sut.ValidateAsync("AVL-003", CancellationToken.None);

		// Assert
		foreach (var evidence in result.Evidence)
		{
			evidence.Description.ShouldNotContain("placeholder", Case.Insensitive);
			evidence.Description.ShouldNotContain("manual attestation", Case.Insensitive);
		}
	}

	#endregion

	#region ValidateAsync - Unknown Control Tests

	[Fact]
	public async Task ValidateAsync_ReturnFailure_ForUnknownControl()
	{
		// Act
		var result = await _sut.ValidateAsync("UNKNOWN-001", CancellationToken.None);

		// Assert
		result.IsEffective.ShouldBeFalse();
		result.ConfigurationIssues.ShouldContain(i => i.Contains("Unknown control"));
	}

	#endregion

	#region GetControlDescription Tests

	[Fact]
	public void GetControlDescription_ReturnDescription_ForAVL001()
	{
		// Act
		var description = _sut.GetControlDescription("AVL-001");

		// Assert
		_ = description.ShouldNotBeNull();
		description.ControlId.ShouldBe("AVL-001");
		description.Name.ShouldBe("Health Monitoring");
		description.Type.ShouldBe(ControlType.Detective);
		description.Frequency.ShouldBe(ControlFrequency.Continuous);
	}

	[Fact]
	public void GetControlDescription_ReturnDescription_ForAVL002()
	{
		// Act
		var description = _sut.GetControlDescription("AVL-002");

		// Assert
		_ = description.ShouldNotBeNull();
		description.ControlId.ShouldBe("AVL-002");
		description.Name.ShouldBe("Performance Metrics");
	}

	[Fact]
	public void GetControlDescription_ReturnDescription_ForAVL003()
	{
		// Act
		var description = _sut.GetControlDescription("AVL-003");

		// Assert
		_ = description.ShouldNotBeNull();
		description.ControlId.ShouldBe("AVL-003");
		description.Name.ShouldBe("Backup Verification");
		description.Frequency.ShouldBe(ControlFrequency.Daily);
	}

	[Fact]
	public void GetControlDescription_ReturnNull_ForUnknownControl()
	{
		// Act
		var description = _sut.GetControlDescription("UNKNOWN-001");

		// Assert
		description.ShouldBeNull();
	}

	#endregion

	#region RunTestAsync Tests (inherited from BaseControlValidator)

	[Fact]
	public async Task RunTestAsync_AVL001_ReturnNoExceptions_WhenControlEffective()
	{
		// Arrange
		var parameters = new ControlTestParameters { SampleSize = 25 };

		// Act
		var result = await _sut.RunTestAsync("AVL-001", parameters, CancellationToken.None);

		// Assert
		result.Outcome.ShouldBe(TestOutcome.NotTested);
		result.ExceptionsFound.ShouldBeNull();
	}

	[Fact]
	public async Task RunTestAsync_AVL003_ReturnNoExceptions_WhenBackupConfigured()
	{
		// Arrange
		_ = A.CallTo(() => _fakeBackupConfigProvider.IsBackupConfigured).Returns(true);
		_ = A.CallTo(() => _fakeBackupConfigProvider.BackupProviderName).Returns("TestProvider");
		_ = A.CallTo(() => _fakeBackupConfigProvider.ConfigurationDescription).Returns("Test config");
		var parameters = new ControlTestParameters { SampleSize = 25 };

		// Act
		var result = await _sut.RunTestAsync("AVL-003", parameters, CancellationToken.None);

		// Assert
		result.Outcome.ShouldBe(TestOutcome.NotTested);
	}

	#endregion
}
