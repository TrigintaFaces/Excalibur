// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Security.Tests.Compliance.Soc2.Validators;

/// <summary>
/// Unit tests for <see cref="EncryptionControlValidator"/>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class EncryptionControlValidatorShould
{
	private readonly IEncryptionProvider _fakeEncryptionProvider;
	private readonly IKeyManagementProvider _fakeKeyProvider;
	private readonly EncryptionControlValidator _sut;

	public EncryptionControlValidatorShould()
	{
		_fakeEncryptionProvider = A.Fake<IEncryptionProvider>();
		_fakeKeyProvider = A.Fake<IKeyManagementProvider>();

		_sut = new EncryptionControlValidator(_fakeEncryptionProvider, _fakeKeyProvider);
	}

	#region SupportedControls Tests

	[Fact]
	public void SupportedControls_ReturnCorrectControlIds()
	{
		// Act
		var controls = _sut.SupportedControls;

		// Assert
		controls.ShouldContain("SEC-001");
		controls.ShouldContain("SEC-002");
		controls.ShouldContain("SEC-003");
		controls.Count.ShouldBe(3);
	}

	#endregion SupportedControls Tests

	#region SupportedCriteria Tests

	[Fact]
	public void SupportedCriteria_ReturnCorrectCriteria()
	{
		// Act
		var criteria = _sut.SupportedCriteria;

		// Assert
		criteria.ShouldContain(TrustServicesCriterion.CC6_LogicalAccess);
		criteria.ShouldContain(TrustServicesCriterion.CC9_RiskMitigation);
	}

	#endregion SupportedCriteria Tests

	#region ValidateAsync - SEC-001 Tests

	[Fact]
	public async Task ValidateAsync_SEC001_ReturnFailure_WhenNoEncryptionProvider()
	{
		// Arrange
		var sut = new EncryptionControlValidator(null, _fakeKeyProvider);

		// Act
		var result = await sut.ValidateAsync("SEC-001", CancellationToken.None);

		// Assert
		result.IsConfigured.ShouldBeFalse();
		result.IsEffective.ShouldBeFalse();
		result.ConfigurationIssues.ShouldContain("Encryption provider not configured");
	}

	[Fact]
	public async Task ValidateAsync_SEC001_ReturnSuccess_WhenEncryptionProviderConfigured()
	{
		// Arrange
		_ = A.CallTo(() => _fakeEncryptionProvider.ValidateFipsComplianceAsync(A<CancellationToken>._))
			.Returns(true);

		// Act
		var result = await _sut.ValidateAsync("SEC-001", CancellationToken.None);

		// Assert
		result.IsEffective.ShouldBeTrue();
		result.EffectivenessScore.ShouldBe(100);
	}

	[Fact]
	public async Task ValidateAsync_SEC001_CollectFipsComplianceEvidence()
	{
		// Arrange
		_ = A.CallTo(() => _fakeEncryptionProvider.ValidateFipsComplianceAsync(A<CancellationToken>._))
			.Returns(true);

		// Act
		var result = await _sut.ValidateAsync("SEC-001", CancellationToken.None);

		// Assert
		result.Evidence.ShouldContain(e => e.Description.Contains("FIPS"));
	}

	[Fact]
	public async Task ValidateAsync_SEC001_HandleFipsValidationException()
	{
		// Arrange
		_ = A.CallTo(() => _fakeEncryptionProvider.ValidateFipsComplianceAsync(A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("FIPS not available"));

		// Act
		var result = await _sut.ValidateAsync("SEC-001", CancellationToken.None);

		// Assert
		result.IsEffective.ShouldBeTrue();
		result.Evidence.ShouldContain(e => e.Description.Contains("FIPS validation check"));
	}

	#endregion ValidateAsync - SEC-001 Tests

	#region ValidateAsync - SEC-002 Tests

	[Fact]
	public async Task ValidateAsync_SEC002_ReturnSuccess()
	{
		// Act
		var result = await _sut.ValidateAsync("SEC-002", CancellationToken.None);

		// Assert
		result.IsEffective.ShouldBeTrue();
		result.ControlId.ShouldBe("SEC-002");
	}

	[Fact]
	public async Task ValidateAsync_SEC002_CollectTlsEvidence()
	{
		// Act
		var result = await _sut.ValidateAsync("SEC-002", CancellationToken.None);

		// Assert
		result.Evidence.ShouldContain(e => e.Description.Contains("TLS"));
	}

	#endregion ValidateAsync - SEC-002 Tests

	#region ValidateAsync - SEC-003 Tests

	[Fact]
	public async Task ValidateAsync_SEC003_ReturnFailure_WhenNoKeyProvider()
	{
		// Arrange
		var sut = new EncryptionControlValidator(_fakeEncryptionProvider, null);

		// Act
		var result = await sut.ValidateAsync("SEC-003", CancellationToken.None);

		// Assert
		result.IsEffective.ShouldBeFalse();
		result.ConfigurationIssues.ShouldContain("Key management provider not configured");
	}

	[Fact]
	public async Task ValidateAsync_SEC003_ReturnSuccess_WhenActiveKeyAvailable()
	{
		// Arrange
		_ = A.CallTo(() => _fakeKeyProvider.GetActiveKeyAsync(A<string>._, A<CancellationToken>._))
			.Returns(new KeyMetadata
			{
				KeyId = "key-1",
				Version = 1,
				Status = KeyStatus.Active,
				Algorithm = EncryptionAlgorithm.Aes256Gcm,
				CreatedAt = DateTimeOffset.UtcNow.AddDays(-30),
				ExpiresAt = DateTimeOffset.UtcNow.AddDays(335)
			});

		// Act
		var result = await _sut.ValidateAsync("SEC-003", CancellationToken.None);

		// Assert
		result.IsEffective.ShouldBeTrue();
		result.EffectivenessScore.ShouldBe(100);
	}

	[Fact]
	public async Task ValidateAsync_SEC003_ReturnFailure_WhenNoActiveKey()
	{
		// Arrange
		_ = A.CallTo(() => _fakeKeyProvider.GetActiveKeyAsync(A<string>._, A<CancellationToken>._))
			.Returns((KeyMetadata?)null);

		// Act
		var result = await _sut.ValidateAsync("SEC-003", CancellationToken.None);

		// Assert
		result.IsEffective.ShouldBeFalse();
		result.ConfigurationIssues.ShouldContain("No active encryption key available");
	}

	[Fact]
	public async Task ValidateAsync_SEC003_ReturnFailure_WhenKeyExpired()
	{
		// Arrange
		_ = A.CallTo(() => _fakeKeyProvider.GetActiveKeyAsync(A<string>._, A<CancellationToken>._))
			.Returns(new KeyMetadata
			{
				KeyId = "key-1",
				Version = 1,
				Status = KeyStatus.Active,
				Algorithm = EncryptionAlgorithm.Aes256Gcm,
				CreatedAt = DateTimeOffset.UtcNow.AddDays(-400),
				ExpiresAt = DateTimeOffset.UtcNow.AddDays(-10) // Expired
			});

		// Act
		var result = await _sut.ValidateAsync("SEC-003", CancellationToken.None);

		// Assert
		result.IsEffective.ShouldBeFalse();
		result.ConfigurationIssues.ShouldContain(i => i.Contains("expired"));
	}

	[Fact]
	public async Task ValidateAsync_SEC003_CollectKeyVersionEvidence()
	{
		// Arrange
		_ = A.CallTo(() => _fakeKeyProvider.GetActiveKeyAsync(A<string>._, A<CancellationToken>._))
			.Returns(new KeyMetadata
			{
				KeyId = "key-1",
				Version = 3,
				Status = KeyStatus.Active,
				Algorithm = EncryptionAlgorithm.Aes256Gcm,
				CreatedAt = DateTimeOffset.UtcNow.AddDays(-30),
				ExpiresAt = DateTimeOffset.UtcNow.AddDays(335)
			});

		// Act
		var result = await _sut.ValidateAsync("SEC-003", CancellationToken.None);

		// Assert
		result.Evidence.ShouldContain(e => e.Description.Contains("version"));
	}

	[Fact]
	public async Task ValidateAsync_SEC003_HandleKeyProviderException()
	{
		// Arrange
		_ = A.CallTo(() => _fakeKeyProvider.GetActiveKeyAsync(A<string>._, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("Connection failed"));

		// Act
		var result = await _sut.ValidateAsync("SEC-003", CancellationToken.None);

		// Assert
		result.IsEffective.ShouldBeFalse();
		result.ConfigurationIssues.ShouldContain(i => i.Contains("Failed to validate"));
	}

	#endregion ValidateAsync - SEC-003 Tests

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

	#endregion ValidateAsync - Unknown Control Tests

	#region GetControlDescription Tests

	[Fact]
	public void GetControlDescription_ReturnDescription_ForSEC001()
	{
		// Act
		var description = _sut.GetControlDescription("SEC-001");

		// Assert
		_ = description.ShouldNotBeNull();
		description.ControlId.ShouldBe("SEC-001");
		description.Name.ShouldBe("Encryption at Rest");
		description.Type.ShouldBe(ControlType.Preventive);
	}

	[Fact]
	public void GetControlDescription_ReturnDescription_ForSEC002()
	{
		// Act
		var description = _sut.GetControlDescription("SEC-002");

		// Assert
		_ = description.ShouldNotBeNull();
		description.ControlId.ShouldBe("SEC-002");
		description.Name.ShouldBe("Encryption in Transit");
	}

	[Fact]
	public void GetControlDescription_ReturnDescription_ForSEC003()
	{
		// Act
		var description = _sut.GetControlDescription("SEC-003");

		// Assert
		_ = description.ShouldNotBeNull();
		description.ControlId.ShouldBe("SEC-003");
		description.Name.ShouldBe("Key Management");
	}

	[Fact]
	public void GetControlDescription_ReturnNull_ForUnknownControl()
	{
		// Act
		var description = _sut.GetControlDescription("UNKNOWN-001");

		// Assert
		description.ShouldBeNull();
	}

	#endregion GetControlDescription Tests

	#region RunTestAsync Tests (inherited from BaseControlValidator)

	[Fact]
	public async Task RunTestAsync_ReturnNoExceptions_WhenControlEffective()
	{
		// Arrange
		_ = A.CallTo(() => _fakeEncryptionProvider.ValidateFipsComplianceAsync(A<CancellationToken>._))
			.Returns(true);

		var parameters = new ControlTestParameters { SampleSize = 25 };

		// Act
		var result = await _sut.RunTestAsync("SEC-001", parameters, CancellationToken.None);

		// Assert
		result.Outcome.ShouldBe(TestOutcome.NoExceptions);
		result.ExceptionsFound.ShouldBe(0);
	}

	[Fact]
	public async Task RunTestAsync_ReturnExceptions_WhenControlNotEffective()
	{
		// Arrange
		var sut = new EncryptionControlValidator(null, _fakeKeyProvider);
		var parameters = new ControlTestParameters { SampleSize = 25 };

		// Act
		var result = await sut.RunTestAsync("SEC-001", parameters, CancellationToken.None);

		// Assert
		result.Outcome.ShouldBe(TestOutcome.SignificantExceptions);
		result.ExceptionsFound.ShouldBe(1);
	}

	[Fact]
	public async Task RunTestAsync_UseParameterSampleSize()
	{
		// Arrange
		_ = A.CallTo(() => _fakeEncryptionProvider.ValidateFipsComplianceAsync(A<CancellationToken>._))
			.Returns(true);

		var parameters = new ControlTestParameters { SampleSize = 50 };

		// Act
		var result = await _sut.RunTestAsync("SEC-001", parameters, CancellationToken.None);

		// Assert
		result.ItemsTested.ShouldBe(50);
	}

	#endregion RunTestAsync Tests (inherited from BaseControlValidator)
}
