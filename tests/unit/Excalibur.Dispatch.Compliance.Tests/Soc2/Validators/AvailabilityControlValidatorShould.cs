using Excalibur.Dispatch.Compliance;

namespace Excalibur.Dispatch.Compliance.Tests.Soc2.Validators;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class AvailabilityControlValidatorShould
{
	[Fact]
	public void Return_three_supported_controls()
	{
		var sut = new AvailabilityControlValidator();

		sut.SupportedControls.Count.ShouldBe(3);
		sut.SupportedControls.ShouldContain("AVL-001");
		sut.SupportedControls.ShouldContain("AVL-002");
		sut.SupportedControls.ShouldContain("AVL-003");
	}

	[Fact]
	public void Return_supported_criteria()
	{
		var sut = new AvailabilityControlValidator();

		sut.SupportedCriteria.ShouldContain(TrustServicesCriterion.A1_InfrastructureManagement);
		sut.SupportedCriteria.ShouldContain(TrustServicesCriterion.A2_CapacityManagement);
		sut.SupportedCriteria.ShouldContain(TrustServicesCriterion.A3_BackupRecovery);
	}

	[Fact]
	public async Task Validate_health_monitoring_always_passes()
	{
		var sut = new AvailabilityControlValidator();

		var result = await sut.ValidateAsync("AVL-001", CancellationToken.None).ConfigureAwait(false);

		result.ControlId.ShouldBe("AVL-001");
		result.IsConfigured.ShouldBeTrue();
		result.IsEffective.ShouldBeTrue();
		result.Evidence.ShouldNotBeEmpty();
	}

	[Fact]
	public async Task Validate_performance_metrics_with_compliance_metrics()
	{
		var complianceMetrics = A.Fake<IComplianceMetrics>();
		var sut = new AvailabilityControlValidator(complianceMetrics);

		var result = await sut.ValidateAsync("AVL-002", CancellationToken.None).ConfigureAwait(false);

		result.ControlId.ShouldBe("AVL-002");
		result.IsConfigured.ShouldBeTrue();
		result.IsEffective.ShouldBeTrue();
	}

	[Fact]
	public async Task Validate_performance_metrics_without_compliance_metrics()
	{
		var sut = new AvailabilityControlValidator(complianceMetrics: null);

		var result = await sut.ValidateAsync("AVL-002", CancellationToken.None).ConfigureAwait(false);

		// Still passes — external monitoring is accepted
		result.ControlId.ShouldBe("AVL-002");
		result.IsEffective.ShouldBeTrue();
	}

	[Fact]
	public async Task Validate_backup_with_configured_provider()
	{
		var backupProvider = A.Fake<IBackupConfigurationProvider>();
		A.CallTo(() => backupProvider.IsBackupConfigured).Returns(true);
		A.CallTo(() => backupProvider.BackupProviderName).Returns("SqlServerSnapshotStore");
		A.CallTo(() => backupProvider.ConfigurationDescription).Returns("SQL Server snapshot store configured");

		var sut = new AvailabilityControlValidator(backupConfigProvider: backupProvider);

		var result = await sut.ValidateAsync("AVL-003", CancellationToken.None).ConfigureAwait(false);

		result.ControlId.ShouldBe("AVL-003");
		result.IsEffective.ShouldBeTrue();
		result.Evidence.ShouldContain(e => e.Description.Contains("SqlServerSnapshotStore"));
	}

	[Fact]
	public async Task Validate_backup_without_provider()
	{
		var sut = new AvailabilityControlValidator(backupConfigProvider: null);

		var result = await sut.ValidateAsync("AVL-003", CancellationToken.None).ConfigureAwait(false);

		// Still success — external backup verification accepted
		result.ControlId.ShouldBe("AVL-003");
		result.IsEffective.ShouldBeTrue();
		result.Evidence.ShouldContain(e => e.Description.Contains("not registered"));
	}

	[Fact]
	public async Task Validate_backup_with_unconfigured_provider()
	{
		var backupProvider = A.Fake<IBackupConfigurationProvider>();
		A.CallTo(() => backupProvider.IsBackupConfigured).Returns(false);

		var sut = new AvailabilityControlValidator(backupConfigProvider: backupProvider);

		var result = await sut.ValidateAsync("AVL-003", CancellationToken.None).ConfigureAwait(false);

		result.ControlId.ShouldBe("AVL-003");
		result.IsEffective.ShouldBeTrue();
		result.Evidence.ShouldContain(e => e.Description.Contains("not configured"));
	}

	[Fact]
	public async Task Return_failure_for_unknown_control()
	{
		var sut = new AvailabilityControlValidator();

		var result = await sut.ValidateAsync("UNKNOWN", CancellationToken.None).ConfigureAwait(false);

		result.IsEffective.ShouldBeFalse();
		result.ConfigurationIssues.ShouldContain(i => i.Contains("Unknown control"));
	}

	[Fact]
	public void Return_control_description_for_avl_001()
	{
		var sut = new AvailabilityControlValidator();

		var description = sut.GetControlDescription("AVL-001");

		description.ShouldNotBeNull();
		description.ControlId.ShouldBe("AVL-001");
		description.Name.ShouldBe("Health Monitoring");
		description.Type.ShouldBe(ControlType.Detective);
		description.Frequency.ShouldBe(ControlFrequency.Continuous);
	}

	[Fact]
	public void Return_control_description_for_avl_002()
	{
		var sut = new AvailabilityControlValidator();

		var description = sut.GetControlDescription("AVL-002");

		description.ShouldNotBeNull();
		description.ControlId.ShouldBe("AVL-002");
		description.Name.ShouldBe("Performance Metrics");
	}

	[Fact]
	public void Return_control_description_for_avl_003()
	{
		var sut = new AvailabilityControlValidator();

		var description = sut.GetControlDescription("AVL-003");

		description.ShouldNotBeNull();
		description.ControlId.ShouldBe("AVL-003");
		description.Name.ShouldBe("Backup Verification");
		description.Frequency.ShouldBe(ControlFrequency.Daily);
	}

	[Fact]
	public void Return_null_description_for_unknown_control()
	{
		var sut = new AvailabilityControlValidator();

		var description = sut.GetControlDescription("UNKNOWN");

		description.ShouldBeNull();
	}

	[Fact]
	public async Task Run_test_delegates_to_validation()
	{
		var sut = new AvailabilityControlValidator();
		var parameters = new ControlTestParameters { SampleSize = 10 };

		var result = await sut.RunTestAsync("AVL-001", parameters, CancellationToken.None).ConfigureAwait(false);

		result.ControlId.ShouldBe("AVL-001");
		result.Outcome.ShouldBe(TestOutcome.NoExceptions);
		result.ItemsTested.ShouldBe(10);
	}
}
