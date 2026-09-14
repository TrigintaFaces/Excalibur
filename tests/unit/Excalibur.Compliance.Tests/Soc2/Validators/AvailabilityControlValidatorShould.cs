using Excalibur.Compliance;
using Excalibur.Compliance.Soc2.Validators;

namespace Excalibur.Compliance.Tests.Soc2.Validators;

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
		// This arm was named "always passes" and required exactly that: IsEffective true at full
		// score from a method that observes nothing. The capability really is shipped, which is
		// what the Configuration evidence says; whether this deployment operates it is not
		// observable from here, so the control is unverified rather than effective.
		result.IsEffective.ShouldBeFalse();
		result.EffectivenessScore.ShouldBeInRange(1, 99);
		result.ConfigurationIssues.ShouldNotBeEmpty();
		result.Evidence.ShouldNotBeEmpty();
	}

	[Fact]
	public async Task Validate_performance_metrics_with_compliance_metrics()
	{
		var complianceMetrics = A.Fake<IComplianceMetrics>();
		var sut = new AvailabilityControlValidator(complianceMetrics);

		var result = await sut.ValidateAsync("AVL-002", CancellationToken.None).ConfigureAwait(false);

		result.ControlId.ShouldBe("AVL-002");
		// The mechanism is present and the result still says so -- IsConfigured stays true. What it no
		// longer says is that the CONTROL operated, because nothing here observed it operating.
		result.IsConfigured.ShouldBeTrue();
		result.IsEffective.ShouldBeFalse();
	}

	[Fact]
	public async Task Validate_performance_metrics_without_compliance_metrics()
	{
		var sut = new AvailabilityControlValidator(complianceMetrics: null);

		var result = await sut.ValidateAsync("AVL-002", CancellationToken.None).ConfigureAwait(false);

		result.ControlId.ShouldBe("AVL-002");
		result.IsEffective.ShouldBeFalse();
		result.IsConfigured.ShouldBeFalse();
		// Partial by design: a compensating external arrangement may exist, but the declared control is
		// absent and unverifiable here — neither a pass nor a total failure. The band is the property;
		// the exact figure is the framework's encoding and may be restated without changing the meaning.
		result.EffectivenessScore.ShouldBeInRange(1, 99);
		result.ConfigurationIssues.ShouldContain(i => i.Contains("IComplianceMetrics"));
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
		// The mechanism is present and the result still says so -- IsConfigured stays true. What it no
		// longer says is that the CONTROL operated, because nothing here observed it operating.
		result.IsConfigured.ShouldBeTrue();
		result.IsEffective.ShouldBeFalse();
		result.Evidence.ShouldContain(e => e.Description.Contains("SqlServerSnapshotStore"));
	}

	[Fact]
	public async Task Validate_backup_without_provider()
	{
		var sut = new AvailabilityControlValidator(backupConfigProvider: null);

		var result = await sut.ValidateAsync("AVL-003", CancellationToken.None).ConfigureAwait(false);

		result.ControlId.ShouldBe("AVL-003");
		result.IsEffective.ShouldBeFalse();
		// Partial by design: a compensating external arrangement may exist, but the declared control is
		// absent and unverifiable here — neither a pass nor a total failure. The band is the property;
		// the exact figure is the framework's encoding and may be restated without changing the meaning.
		result.EffectivenessScore.ShouldBeInRange(1, 99);
		result.ConfigurationIssues.ShouldContain(i => i.Contains("not registered"));
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
		result.IsEffective.ShouldBeFalse();
		// Partial by design: a compensating external arrangement may exist, but the declared control is
		// absent and unverifiable here — neither a pass nor a total failure. The band is the property;
		// the exact figure is the framework's encoding and may be restated without changing the meaning.
		result.EffectivenessScore.ShouldBeInRange(1, 99);
		result.ConfigurationIssues.ShouldContain(i => i.Contains("reports itself not configured"));
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
		// The verdict, not NotTested: a validator ran. What did not happen is SAMPLING, which the
		// item count and the null finding count below carry.
		result.Outcome.ShouldBe(TestOutcome.NotTested);
		// Zero, not the requested 10: RunTestAsync forwards a verdict and samples nothing. The
		// requested size stays available on Parameters, where it is a request rather than a measurement.
		result.ItemsTested.ShouldBe(0);
	}
}
