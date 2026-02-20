// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Security.Tests.Compliance.Soc2.Validators;

/// <summary>
/// Unit tests for <see cref="AuditLogControlValidator"/>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class AuditLogControlValidatorShould
{
	private readonly IAuditLogger _fakeAuditLogger;
	private readonly IAuditStore _fakeAuditStore;
	private readonly AuditLogControlValidator _sut;

	public AuditLogControlValidatorShould()
	{
		_fakeAuditLogger = A.Fake<IAuditLogger>();
		_fakeAuditStore = A.Fake<IAuditStore>();

		_sut = new AuditLogControlValidator(_fakeAuditLogger, _fakeAuditStore);
	}

	#region SupportedControls Tests

	[Fact]
	public void SupportedControls_ReturnCorrectControlIds()
	{
		// Act
		var controls = _sut.SupportedControls;

		// Assert
		controls.ShouldContain("SEC-004");
		controls.ShouldContain("SEC-005");
		controls.Count.ShouldBe(2);
	}

	#endregion SupportedControls Tests

	#region SupportedCriteria Tests

	[Fact]
	public void SupportedCriteria_ReturnCorrectCriteria()
	{
		// Act
		var criteria = _sut.SupportedCriteria;

		// Assert
		criteria.ShouldContain(TrustServicesCriterion.CC1_ControlEnvironment);
		criteria.ShouldContain(TrustServicesCriterion.CC4_Monitoring);
	}

	#endregion SupportedCriteria Tests

	#region ValidateAsync - SEC-004 Tests

	[Fact]
	public async Task ValidateAsync_SEC004_ReturnFailure_WhenNoAuditLogger()
	{
		// Arrange
		var sut = new AuditLogControlValidator(null, _fakeAuditStore);

		// Act
		var result = await sut.ValidateAsync("SEC-004", CancellationToken.None);

		// Assert
		result.IsEffective.ShouldBeFalse();
		result.ConfigurationIssues.ShouldContain("Audit logger not configured");
	}

	[Fact]
	public async Task ValidateAsync_SEC004_ReturnSuccess_WhenIntegrityValid()
	{
		// Arrange
		_ = A.CallTo(() => _fakeAuditLogger.VerifyIntegrityAsync(
				A<DateTimeOffset>._,
				A<DateTimeOffset>._,
				A<CancellationToken>._))
			.Returns(AuditIntegrityResult.Valid(100, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow));

		// Act
		var result = await _sut.ValidateAsync("SEC-004", CancellationToken.None);

		// Assert
		result.IsEffective.ShouldBeTrue();
		result.EffectivenessScore.ShouldBe(100);
	}

	[Fact]
	public async Task ValidateAsync_SEC004_ReturnFailure_WhenIntegrityInvalid()
	{
		// Arrange
		_ = A.CallTo(() => _fakeAuditLogger.VerifyIntegrityAsync(
				A<DateTimeOffset>._,
				A<DateTimeOffset>._,
				A<CancellationToken>._))
			.Returns(AuditIntegrityResult.Invalid(100, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow, "event-123", "Hash chain broken"));

		// Act
		var result = await _sut.ValidateAsync("SEC-004", CancellationToken.None);

		// Assert
		result.IsEffective.ShouldBeFalse();
		result.ConfigurationIssues.ShouldContain(i => i.Contains("integrity check failed"));
	}

	[Fact]
	public async Task ValidateAsync_SEC004_HandleIntegrityCheckException()
	{
		// Arrange
		_ = A.CallTo(() => _fakeAuditLogger.VerifyIntegrityAsync(
				A<DateTimeOffset>._,
				A<DateTimeOffset>._,
				A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("Connection failed"));

		// Act
		var result = await _sut.ValidateAsync("SEC-004", CancellationToken.None);

		// Assert
		// Exception shouldn't cause failure - just log evidence
		result.IsEffective.ShouldBeTrue();
		result.Evidence.ShouldContain(e => e.Description.Contains("Connection failed"));
	}

	[Fact]
	public async Task ValidateAsync_SEC004_CollectIntegrityEvidence()
	{
		// Arrange
		_ = A.CallTo(() => _fakeAuditLogger.VerifyIntegrityAsync(
				A<DateTimeOffset>._,
				A<DateTimeOffset>._,
				A<CancellationToken>._))
			.Returns(AuditIntegrityResult.Valid(100, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow));

		// Act
		var result = await _sut.ValidateAsync("SEC-004", CancellationToken.None);

		// Assert
		result.Evidence.ShouldContain(e => e.Description.Contains("integrity verification"));
	}

	[Fact]
	public async Task ValidateAsync_SEC004_VerifyLastTwentyFourHours()
	{
		// Arrange
		DateTimeOffset capturedStart = default;
		DateTimeOffset capturedEnd = default;
		var beforeTest = DateTimeOffset.UtcNow;

		_ = A.CallTo(() => _fakeAuditLogger.VerifyIntegrityAsync(
				A<DateTimeOffset>._,
				A<DateTimeOffset>._,
				A<CancellationToken>._))
			.Invokes((DateTimeOffset start, DateTimeOffset end, CancellationToken _) =>
			{
				capturedStart = start;
				capturedEnd = end;
			})
			.Returns(AuditIntegrityResult.Valid(100, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow));

		// Act
		_ = await _sut.ValidateAsync("SEC-004", CancellationToken.None);

		// Assert
		capturedEnd.ShouldBeGreaterThanOrEqualTo(beforeTest);
		capturedStart.ShouldBe(capturedEnd.AddDays(-1));
	}

	#endregion ValidateAsync - SEC-004 Tests

	#region ValidateAsync - SEC-005 Tests

	[Fact]
	public async Task ValidateAsync_SEC005_ReturnSuccess_WhenAuditLoggerConfigured()
	{
		// Act
		var result = await _sut.ValidateAsync("SEC-005", CancellationToken.None);

		// Assert
		result.IsEffective.ShouldBeTrue();
	}

	[Fact]
	public async Task ValidateAsync_SEC005_ReturnSuccess_WhenOnlyAuditStoreConfigured()
	{
		// Arrange
		var sut = new AuditLogControlValidator(null, _fakeAuditStore);

		// Act
		var result = await sut.ValidateAsync("SEC-005", CancellationToken.None);

		// Assert
		result.IsEffective.ShouldBeTrue();
		result.Evidence.ShouldContain(e => e.Description.Contains("Audit store configured"));
	}

	[Fact]
	public async Task ValidateAsync_SEC005_ReturnSuccess_WhenOnlyAuditLoggerConfigured()
	{
		// Arrange
		var sut = new AuditLogControlValidator(_fakeAuditLogger, null);

		// Act
		var result = await sut.ValidateAsync("SEC-005", CancellationToken.None);

		// Assert
		result.IsEffective.ShouldBeTrue();
		result.Evidence.ShouldContain(e => e.Description.Contains("logger-based monitoring"));
	}

	[Fact]
	public async Task ValidateAsync_SEC005_ReturnFailure_WhenNoAuditInfrastructure()
	{
		// Arrange
		var sut = new AuditLogControlValidator(null, null);

		// Act
		var result = await sut.ValidateAsync("SEC-005", CancellationToken.None);

		// Assert
		result.IsEffective.ShouldBeFalse();
		result.ConfigurationIssues.ShouldContain("No audit infrastructure configured for security monitoring");
	}

	#endregion ValidateAsync - SEC-005 Tests

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
	public void GetControlDescription_ReturnDescription_ForSEC004()
	{
		// Act
		var description = _sut.GetControlDescription("SEC-004");

		// Assert
		_ = description.ShouldNotBeNull();
		description.ControlId.ShouldBe("SEC-004");
		description.Name.ShouldBe("Audit Logging");
		description.Type.ShouldBe(ControlType.Detective);
	}

	[Fact]
	public void GetControlDescription_ReturnDescription_ForSEC005()
	{
		// Act
		var description = _sut.GetControlDescription("SEC-005");

		// Assert
		_ = description.ShouldNotBeNull();
		description.ControlId.ShouldBe("SEC-005");
		description.Name.ShouldBe("Security Monitoring");
		description.Type.ShouldBe(ControlType.Detective);
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
}
