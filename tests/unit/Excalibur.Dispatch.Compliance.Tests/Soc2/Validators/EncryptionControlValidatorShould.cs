using Excalibur.Dispatch.Compliance;

namespace Excalibur.Dispatch.Compliance.Tests.Soc2.Validators;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class EncryptionControlValidatorShould
{
	private readonly IEncryptionProvider _encryptionProvider = A.Fake<IEncryptionProvider>();
	private readonly IKeyManagementProvider _keyManagementProvider = A.Fake<IKeyManagementProvider>();

	[Fact]
	public void Return_three_supported_controls()
	{
		var sut = new EncryptionControlValidator(_encryptionProvider, _keyManagementProvider);

		sut.SupportedControls.Count.ShouldBe(3);
		sut.SupportedControls.ShouldContain("SEC-001");
		sut.SupportedControls.ShouldContain("SEC-002");
		sut.SupportedControls.ShouldContain("SEC-003");
	}

	[Fact]
	public void Return_supported_criteria()
	{
		var sut = new EncryptionControlValidator(_encryptionProvider, _keyManagementProvider);

		sut.SupportedCriteria.ShouldContain(TrustServicesCriterion.CC6_LogicalAccess);
		sut.SupportedCriteria.ShouldContain(TrustServicesCriterion.CC9_RiskMitigation);
	}

	[Fact]
	public async Task Validate_encryption_at_rest_with_provider()
	{
		A.CallTo(() => _encryptionProvider.ValidateFipsComplianceAsync(A<CancellationToken>._))
			.Returns(true);

		var sut = new EncryptionControlValidator(_encryptionProvider, _keyManagementProvider);

		var result = await sut.ValidateAsync("SEC-001", CancellationToken.None).ConfigureAwait(false);

		result.ControlId.ShouldBe("SEC-001");
		result.IsConfigured.ShouldBeTrue();
		result.IsEffective.ShouldBeTrue();
		result.EffectivenessScore.ShouldBe(100);
	}

	[Fact]
	public async Task Fail_encryption_at_rest_without_provider()
	{
		var sut = new EncryptionControlValidator(encryptionProvider: null, keyManagementProvider: null);

		var result = await sut.ValidateAsync("SEC-001", CancellationToken.None).ConfigureAwait(false);

		result.ControlId.ShouldBe("SEC-001");
		result.IsEffective.ShouldBeFalse();
		result.ConfigurationIssues.ShouldContain(i => i.Contains("not configured"));
	}

	[Fact]
	public async Task Validate_encryption_in_transit()
	{
		var sut = new EncryptionControlValidator(_encryptionProvider, _keyManagementProvider);

		var result = await sut.ValidateAsync("SEC-002", CancellationToken.None).ConfigureAwait(false);

		result.ControlId.ShouldBe("SEC-002");
		result.IsConfigured.ShouldBeTrue();
		result.IsEffective.ShouldBeTrue();
	}

	[Fact]
	public async Task Validate_key_management_with_active_key()
	{
		A.CallTo(() => _keyManagementProvider.GetActiveKeyAsync(A<string?>._, A<CancellationToken>._))
			.Returns(new KeyMetadata
			{
				KeyId = "key-1",
				Version = 1,
				Status = KeyStatus.Active,
				Algorithm = EncryptionAlgorithm.Aes256Gcm,
				CreatedAt = DateTimeOffset.UtcNow.AddDays(-30),
				ExpiresAt = DateTimeOffset.UtcNow.AddDays(335)
			});

		var sut = new EncryptionControlValidator(_encryptionProvider, _keyManagementProvider);

		var result = await sut.ValidateAsync("SEC-003", CancellationToken.None).ConfigureAwait(false);

		result.ControlId.ShouldBe("SEC-003");
		result.IsConfigured.ShouldBeTrue();
		result.IsEffective.ShouldBeTrue();
	}

	[Fact]
	public async Task Fail_key_management_without_provider()
	{
		var sut = new EncryptionControlValidator(_encryptionProvider, keyManagementProvider: null);

		var result = await sut.ValidateAsync("SEC-003", CancellationToken.None).ConfigureAwait(false);

		result.ControlId.ShouldBe("SEC-003");
		result.IsEffective.ShouldBeFalse();
		result.ConfigurationIssues.ShouldContain(i => i.Contains("not configured"));
	}

	[Fact]
	public async Task Fail_key_management_when_no_active_key()
	{
		A.CallTo(() => _keyManagementProvider.GetActiveKeyAsync(A<string?>._, A<CancellationToken>._))
			.Returns((KeyMetadata?)null);

		var sut = new EncryptionControlValidator(_encryptionProvider, _keyManagementProvider);

		var result = await sut.ValidateAsync("SEC-003", CancellationToken.None).ConfigureAwait(false);

		result.IsEffective.ShouldBeFalse();
		result.ConfigurationIssues.ShouldContain(i => i.Contains("No active encryption key"));
	}

	[Fact]
	public async Task Fail_key_management_when_key_expired()
	{
		A.CallTo(() => _keyManagementProvider.GetActiveKeyAsync(A<string?>._, A<CancellationToken>._))
			.Returns(new KeyMetadata
			{
				KeyId = "key-1",
				Version = 1,
				Status = KeyStatus.Active,
				Algorithm = EncryptionAlgorithm.Aes256Gcm,
				CreatedAt = DateTimeOffset.UtcNow.AddDays(-365),
				ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1) // expired yesterday
			});

		var sut = new EncryptionControlValidator(_encryptionProvider, _keyManagementProvider);

		var result = await sut.ValidateAsync("SEC-003", CancellationToken.None).ConfigureAwait(false);

		result.IsEffective.ShouldBeFalse();
		result.ConfigurationIssues.ShouldContain(i => i.Contains("expired"));
	}

	[Fact]
	public async Task Return_failure_for_unknown_control()
	{
		var sut = new EncryptionControlValidator(_encryptionProvider, _keyManagementProvider);

		var result = await sut.ValidateAsync("UNKNOWN", CancellationToken.None).ConfigureAwait(false);

		result.IsEffective.ShouldBeFalse();
		result.ConfigurationIssues.ShouldContain(i => i.Contains("Unknown control"));
	}

	[Fact]
	public void Return_control_description_for_sec_001()
	{
		var sut = new EncryptionControlValidator(_encryptionProvider, _keyManagementProvider);

		var description = sut.GetControlDescription("SEC-001");

		description.ShouldNotBeNull();
		description.ControlId.ShouldBe("SEC-001");
		description.Name.ShouldBe("Encryption at Rest");
		description.Type.ShouldBe(ControlType.Preventive);
		description.Frequency.ShouldBe(ControlFrequency.Continuous);
	}

	[Fact]
	public void Return_control_description_for_sec_002()
	{
		var sut = new EncryptionControlValidator(_encryptionProvider, _keyManagementProvider);

		var description = sut.GetControlDescription("SEC-002");

		description.ShouldNotBeNull();
		description.ControlId.ShouldBe("SEC-002");
		description.Name.ShouldBe("Encryption in Transit");
	}

	[Fact]
	public void Return_control_description_for_sec_003()
	{
		var sut = new EncryptionControlValidator(_encryptionProvider, _keyManagementProvider);

		var description = sut.GetControlDescription("SEC-003");

		description.ShouldNotBeNull();
		description.ControlId.ShouldBe("SEC-003");
		description.Name.ShouldBe("Key Management");
	}

	[Fact]
	public void Return_null_description_for_unknown_control()
	{
		var sut = new EncryptionControlValidator(_encryptionProvider, _keyManagementProvider);

		var description = sut.GetControlDescription("UNKNOWN");

		description.ShouldBeNull();
	}

	[Fact]
	public async Task Run_test_delegates_to_validation()
	{
		A.CallTo(() => _encryptionProvider.ValidateFipsComplianceAsync(A<CancellationToken>._))
			.Returns(true);

		var sut = new EncryptionControlValidator(_encryptionProvider, _keyManagementProvider);
		var parameters = new ControlTestParameters { SampleSize = 10 };

		var result = await sut.RunTestAsync("SEC-001", parameters, CancellationToken.None).ConfigureAwait(false);

		result.ControlId.ShouldBe("SEC-001");
		result.Outcome.ShouldBe(TestOutcome.NoExceptions);
		result.ItemsTested.ShouldBe(10);
		result.ExceptionsFound.ShouldBe(0);
	}

	[Fact]
	public async Task Handle_fips_validation_exception_gracefully()
	{
		A.CallTo(() => _encryptionProvider.ValidateFipsComplianceAsync(A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("FIPS check unavailable"));

		var sut = new EncryptionControlValidator(_encryptionProvider, _keyManagementProvider);

		var result = await sut.ValidateAsync("SEC-001", CancellationToken.None).ConfigureAwait(false);

		// Should still pass — FIPS failure is logged as evidence but doesn't block
		result.ControlId.ShouldBe("SEC-001");
		result.IsConfigured.ShouldBeTrue();
		result.IsEffective.ShouldBeTrue();
		result.Evidence.ShouldNotBeEmpty();
	}
}
