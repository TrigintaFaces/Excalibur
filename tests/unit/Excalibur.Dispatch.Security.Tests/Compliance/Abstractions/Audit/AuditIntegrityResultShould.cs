// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Security.Tests.Compliance.Abstractions.Audit;

/// <summary>
/// Unit tests for <see cref="AuditIntegrityResult"/> record.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
[Trait("Feature", "Audit")]
public sealed class AuditIntegrityResultShould : UnitTestBase
{
	[Fact]
	public void CreateValidResultWithFactoryMethod()
	{
		// Arrange
		var startDate = DateTimeOffset.UtcNow.AddDays(-30);
		var endDate = DateTimeOffset.UtcNow;
		var before = DateTimeOffset.UtcNow;

		// Act
		var result = AuditIntegrityResult.Valid(10000, startDate, endDate);

		var after = DateTimeOffset.UtcNow;

		// Assert
		result.IsValid.ShouldBeTrue();
		result.EventsVerified.ShouldBe(10000);
		result.StartDate.ShouldBe(startDate);
		result.EndDate.ShouldBe(endDate);
		result.VerifiedAt.ShouldBeGreaterThanOrEqualTo(before);
		result.VerifiedAt.ShouldBeLessThanOrEqualTo(after);
		result.FirstViolationEventId.ShouldBeNull();
		result.ViolationDescription.ShouldBeNull();
		result.ViolationCount.ShouldBe(0);
	}

	[Fact]
	public void CreateInvalidResultWithFactoryMethod()
	{
		// Arrange
		var startDate = DateTimeOffset.UtcNow.AddDays(-30);
		var endDate = DateTimeOffset.UtcNow;

		// Act
		var result = AuditIntegrityResult.Invalid(
			eventsVerified: 5000,
			startDate: startDate,
			endDate: endDate,
			firstViolationEventId: "event-corrupt-001",
			violationDescription: "Hash mismatch detected: expected abc123, found xyz789",
			violationCount: 3);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.EventsVerified.ShouldBe(5000);
		result.StartDate.ShouldBe(startDate);
		result.EndDate.ShouldBe(endDate);
		result.FirstViolationEventId.ShouldBe("event-corrupt-001");
		result.ViolationDescription.ShouldBe("Hash mismatch detected: expected abc123, found xyz789");
		result.ViolationCount.ShouldBe(3);
	}

	[Fact]
	public void CreateInvalidResultWithSingleViolation()
	{
		// Arrange
		var startDate = DateTimeOffset.UtcNow.AddDays(-7);
		var endDate = DateTimeOffset.UtcNow;

		// Act - default violation count is 1
		var result = AuditIntegrityResult.Invalid(
			eventsVerified: 1000,
			startDate: startDate,
			endDate: endDate,
			firstViolationEventId: "event-bad-001",
			violationDescription: "Broken hash chain");

		// Assert
		result.ViolationCount.ShouldBe(1);
	}

	[Fact]
	public void CreateResultDirectlyWithRequiredProperties()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;

		// Act
		var result = new AuditIntegrityResult
		{
			IsValid = true,
			EventsVerified = 50000,
			StartDate = now.AddMonths(-1),
			EndDate = now,
			VerifiedAt = now
		};

		// Assert
		result.IsValid.ShouldBeTrue();
		result.EventsVerified.ShouldBe(50000);
	}

	[Fact]
	public void SupportRecordEquality()
	{
		// Arrange
		var startDate = DateTimeOffset.UtcNow.AddDays(-7);
		var endDate = DateTimeOffset.UtcNow;
		var verifiedAt = DateTimeOffset.UtcNow;

		var result1 = new AuditIntegrityResult
		{
			IsValid = true,
			EventsVerified = 1000,
			StartDate = startDate,
			EndDate = endDate,
			VerifiedAt = verifiedAt
		};

		var result2 = new AuditIntegrityResult
		{
			IsValid = true,
			EventsVerified = 1000,
			StartDate = startDate,
			EndDate = endDate,
			VerifiedAt = verifiedAt
		};

		// Assert
		result1.ShouldBe(result2);
	}

	[Fact]
	public void HaveDefaultZeroViolationCount()
	{
		// Act
		var result = new AuditIntegrityResult
		{
			IsValid = true,
			EventsVerified = 100,
			StartDate = DateTimeOffset.UtcNow,
			EndDate = DateTimeOffset.UtcNow,
			VerifiedAt = DateTimeOffset.UtcNow
		};

		// Assert
		result.ViolationCount.ShouldBe(0);
	}
}
