// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Abstractions.Audit;

/// <summary>
/// Unit tests for <see cref="AuditIntegrityResult"/> record.
/// </summary>
/// <remarks>
/// The load-bearing property of this type is that a successful verification over zero events cannot be
/// constructed at all. Every provider used to be able to report an unexamined window as a pass, which put
/// an unearned assurance into compliance evidence. These tests bind both halves: the guards reject the
/// impossible results (safety) and the three legitimate results are still constructible (liveness).
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Compliance)]
[Trait("Feature", "Audit")]
public sealed class AuditIntegrityResultShould : UnitTestBase
{
	[Fact]
	public void CreateVerifiedResultWithFactoryMethod()
	{
		// Arrange
		var startDate = DateTimeOffset.UtcNow.AddDays(-30);
		var endDate = DateTimeOffset.UtcNow;
		var before = DateTimeOffset.UtcNow;

		// Act
		var result = AuditIntegrityResult.Verified(10000, startDate, endDate, isHashChained: true);

		var after = DateTimeOffset.UtcNow;

		// Assert
		result.Outcome.ShouldBe(AuditIntegrityOutcome.Verified);
		result.EventsVerified.ShouldBe(10000);
		result.StartDate.ShouldBe(startDate);
		result.EndDate.ShouldBe(endDate);
		result.VerifiedAt.ShouldBeGreaterThanOrEqualTo(before);
		result.VerifiedAt.ShouldBeLessThanOrEqualTo(after);
		result.FirstViolationEventId.ShouldBeNull();
		result.ViolationDescription.ShouldBeNull();
		result.CompromisedChainCount.ShouldBe(0);
	}

	[Fact]
	public void CreateViolationsDetectedResultWithFactoryMethod()
	{
		// Arrange
		var startDate = DateTimeOffset.UtcNow.AddDays(-30);
		var endDate = DateTimeOffset.UtcNow;

		// Act
		var result = AuditIntegrityResult.ViolationsDetected(
			eventsVerified: 5000,
			startDate: startDate,
			endDate: endDate,
			firstViolationEventId: "event-corrupt-001",
			violationDescription: "Hash mismatch detected: expected abc123, found xyz789",
			compromisedChainCount: 3, isHashChained: true);

		// Assert
		result.Outcome.ShouldBe(AuditIntegrityOutcome.ViolationsDetected);
		result.EventsVerified.ShouldBe(5000);
		result.StartDate.ShouldBe(startDate);
		result.EndDate.ShouldBe(endDate);
		result.FirstViolationEventId.ShouldBe("event-corrupt-001");
		result.ViolationDescription.ShouldBe("Hash mismatch detected: expected abc123, found xyz789");
		result.CompromisedChainCount.ShouldBe(3);
	}

	[Fact]
	public void CreateViolationsDetectedResultWithSingleViolation()
	{
		// Arrange
		var startDate = DateTimeOffset.UtcNow.AddDays(-7);
		var endDate = DateTimeOffset.UtcNow;

		// Act - default violation count is 1
		var result = AuditIntegrityResult.ViolationsDetected(
			eventsVerified: 1000,
			startDate: startDate,
			endDate: endDate,
			firstViolationEventId: "event-bad-001",
			violationDescription: "Broken hash chain", compromisedChainCount: 1, isHashChained: true);

		// Assert
		result.CompromisedChainCount.ShouldBe(1);
	}

	[Fact]
	public void CreateNoEventsInScopeResultWithZeroEventsAndNoViolationDetail()
	{
		// Arrange
		var startDate = DateTimeOffset.UtcNow.AddDays(-1);
		var endDate = DateTimeOffset.UtcNow;

		// Act
		var result = AuditIntegrityResult.NoEventsInScope(startDate, endDate);

		// Assert - an unexamined window is its own outcome, distinct from a pass.
		result.Outcome.ShouldBe(AuditIntegrityOutcome.NoEventsInScope);
		result.Outcome.ShouldNotBe(AuditIntegrityOutcome.Verified);
		result.EventsVerified.ShouldBe(0);
		result.StartDate.ShouldBe(startDate);
		result.EndDate.ShouldBe(endDate);
		result.FirstViolationEventId.ShouldBeNull();
		result.ViolationDescription.ShouldBeNull();
		result.CompromisedChainCount.ShouldBe(0);
	}

	[Theory]
	[InlineData(0L)]
	[InlineData(-1L)]
	public void RefuseToReportVerifiedOverNoEvents(long eventsVerified)
	{
		// A verification that examined nothing establishes nothing. This is the defect the type exists to
		// prevent: every audit store used to be able to return a passing result over an empty window.
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			AuditIntegrityResult.Verified(eventsVerified, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow, isHashChained: true));
	}

	[Fact]
	public void RefuseToReportViolationsOverNoEvents()
	{
		// A violation cannot be detected in an event that was never examined.
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			AuditIntegrityResult.ViolationsDetected(
				eventsVerified: 0,
				startDate: DateTimeOffset.UtcNow.AddDays(-1),
				endDate: DateTimeOffset.UtcNow,
				firstViolationEventId: "event-1",
				violationDescription: "Broken hash chain", compromisedChainCount: 1, isHashChained: true));
	}

	[Fact]
	public void RefuseToReportViolationsWithNoCompromisedChainCount()
	{
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			AuditIntegrityResult.ViolationsDetected(
				eventsVerified: 10,
				startDate: DateTimeOffset.UtcNow.AddDays(-1),
				endDate: DateTimeOffset.UtcNow,
				firstViolationEventId: "event-1",
				violationDescription: "Broken hash chain",
				compromisedChainCount: 0, isHashChained: true));
	}

	[Theory]
	[InlineData(null, "description")]
	[InlineData("", "description")]
	[InlineData("   ", "description")]
	[InlineData("event-1", null)]
	[InlineData("event-1", "")]
	[InlineData("event-1", "   ")]
	public void RefuseToReportAViolationThatDoesNotIdentifyItself(string? eventId, string? description)
	{
		// A reported violation with no event id or no description is not actionable evidence.
		_ = Should.Throw<ArgumentException>(() =>
			AuditIntegrityResult.ViolationsDetected(
				eventsVerified: 10,
				startDate: DateTimeOffset.UtcNow.AddDays(-1),
				endDate: DateTimeOffset.UtcNow,
				firstViolationEventId: eventId!,
				violationDescription: description!, compromisedChainCount: 1, isHashChained: true));
	}

	[Fact]
	public void ExposeNoPublicConstructor()
	{
		// Structural lock. If a public constructor existed, a caller could bypass the factories entirely and
		// build the exact result the guards above reject - a Verified outcome over zero events.
		typeof(AuditIntegrityResult)
			.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
			.ShouldBeEmpty("AuditIntegrityResult must be constructible only through its three factories.");
	}

	[Fact]
	public void ExposeNoPublicSetters()
	{
		// Structural lock, second half. Public init setters would let a caller mutate a factory-produced
		// result into an impossible one via an object initializer or a with-expression.
		typeof(AuditIntegrityResult)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance)
			.Where(p => p.SetMethod is { IsPublic: true })
			.Select(p => p.Name)
			.ShouldBeEmpty("AuditIntegrityResult properties must be read-only outside the type.");
	}
}
