// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.CloudNative;

namespace Excalibur.Dispatch.Tests.Messaging.CloudNative;

/// <summary>
/// Unit tests for <see cref="AdaptationEvent"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class AdaptationEventShould
{
	[Fact]
	public void HaveDefaultTimestamp()
	{
		// Arrange
		var before = DateTimeOffset.UtcNow;

		// Act
		var adaptationEvent = new AdaptationEvent();
		var after = DateTimeOffset.UtcNow;

		// Assert
		adaptationEvent.Timestamp.ShouldBeGreaterThanOrEqualTo(before);
		adaptationEvent.Timestamp.ShouldBeLessThanOrEqualTo(after);
	}

	[Fact]
	public void HaveEmptyAdaptationTypeByDefault()
	{
		// Arrange & Act
		var adaptationEvent = new AdaptationEvent();

		// Assert
		adaptationEvent.AdaptationType.ShouldBe(string.Empty);
	}

	[Fact]
	public void HaveEmptyReasonByDefault()
	{
		// Arrange & Act
		var adaptationEvent = new AdaptationEvent();

		// Assert
		adaptationEvent.Reason.ShouldBe(string.Empty);
	}

	[Fact]
	public void HaveNullOldValueByDefault()
	{
		// Arrange & Act
		var adaptationEvent = new AdaptationEvent();

		// Assert
		adaptationEvent.OldValue.ShouldBeNull();
	}

	[Fact]
	public void HaveNullNewValueByDefault()
	{
		// Arrange & Act
		var adaptationEvent = new AdaptationEvent();

		// Assert
		adaptationEvent.NewValue.ShouldBeNull();
	}

	[Fact]
	public void HaveMinorImpactByDefault()
	{
		// Arrange & Act
		var adaptationEvent = new AdaptationEvent();

		// Assert
		adaptationEvent.Impact.ShouldBe(AdaptationImpact.Minor);
	}

	[Fact]
	public void AllowSettingTimestamp()
	{
		// Arrange
		var adaptationEvent = new AdaptationEvent();
		var customTime = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

		// Act
		adaptationEvent.Timestamp = customTime;

		// Assert
		adaptationEvent.Timestamp.ShouldBe(customTime);
	}

	[Fact]
	public void AllowSettingAdaptationType()
	{
		// Arrange
		var adaptationEvent = new AdaptationEvent();

		// Act
		adaptationEvent.AdaptationType = "ThreadPoolScaling";

		// Assert
		adaptationEvent.AdaptationType.ShouldBe("ThreadPoolScaling");
	}

	[Fact]
	public void AllowSettingReason()
	{
		// Arrange
		var adaptationEvent = new AdaptationEvent();

		// Act
		adaptationEvent.Reason = "High CPU usage detected";

		// Assert
		adaptationEvent.Reason.ShouldBe("High CPU usage detected");
	}

	[Fact]
	public void AllowSettingOldValue()
	{
		// Arrange
		var adaptationEvent = new AdaptationEvent();

		// Act
		adaptationEvent.OldValue = 10;

		// Assert
		adaptationEvent.OldValue.ShouldBe(10);
	}

	[Fact]
	public void AllowSettingNewValue()
	{
		// Arrange
		var adaptationEvent = new AdaptationEvent();

		// Act
		adaptationEvent.NewValue = 20;

		// Assert
		adaptationEvent.NewValue.ShouldBe(20);
	}

	[Fact]
	public void AllowSettingImpact()
	{
		// Arrange
		var adaptationEvent = new AdaptationEvent();

		// Act
		adaptationEvent.Impact = AdaptationImpact.Major;

		// Assert
		adaptationEvent.Impact.ShouldBe(AdaptationImpact.Major);
	}

	[Fact]
	public void SupportObjectInitializer()
	{
		// Arrange & Act
		var adaptationEvent = new AdaptationEvent
		{
			Timestamp = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero),
			AdaptationType = "RateLimitAdjustment",
			Reason = "Traffic spike detected",
			OldValue = 1000,
			NewValue = 2000,
			Impact = AdaptationImpact.Major,
		};

		// Assert
		adaptationEvent.Timestamp.ShouldBe(new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero));
		adaptationEvent.AdaptationType.ShouldBe("RateLimitAdjustment");
		adaptationEvent.Reason.ShouldBe("Traffic spike detected");
		adaptationEvent.OldValue.ShouldBe(1000);
		adaptationEvent.NewValue.ShouldBe(2000);
		adaptationEvent.Impact.ShouldBe(AdaptationImpact.Major);
	}

	[Theory]
	[InlineData(AdaptationImpact.Minor)]
	[InlineData(AdaptationImpact.Moderate)]
	[InlineData(AdaptationImpact.Major)]
	public void AcceptAllAdaptationImpactLevels(AdaptationImpact impact)
	{
		// Arrange
		var adaptationEvent = new AdaptationEvent();

		// Act
		adaptationEvent.Impact = impact;

		// Assert
		adaptationEvent.Impact.ShouldBe(impact);
	}

	[Theory]
	[InlineData("")]
	[InlineData("ScaleUp")]
	[InlineData("ConnectionPoolAdjustment")]
	[InlineData("CircuitBreakerThresholdModification")]
	public void AcceptVariousAdaptationTypes(string adaptationType)
	{
		// Arrange
		var adaptationEvent = new AdaptationEvent();

		// Act
		adaptationEvent.AdaptationType = adaptationType;

		// Assert
		adaptationEvent.AdaptationType.ShouldBe(adaptationType);
	}

	[Fact]
	public void AllowComplexOldValue()
	{
		// Arrange
		var adaptationEvent = new AdaptationEvent();
		var complexOldValue = new { Rate = 100, Timeout = TimeSpan.FromSeconds(30) };

		// Act
		adaptationEvent.OldValue = complexOldValue;

		// Assert
		adaptationEvent.OldValue.ShouldBe(complexOldValue);
	}

	[Fact]
	public void AllowComplexNewValue()
	{
		// Arrange
		var adaptationEvent = new AdaptationEvent();
		var complexNewValue = new { Rate = 200, Timeout = TimeSpan.FromSeconds(15) };

		// Act
		adaptationEvent.NewValue = complexNewValue;

		// Assert
		adaptationEvent.NewValue.ShouldBe(complexNewValue);
	}

	[Fact]
	public void TrackTypicalAdaptationScenario()
	{
		// Arrange & Act - Simulate a rate limit adaptation
		var adaptationEvent = new AdaptationEvent
		{
			AdaptationType = "RateLimitAdaptation",
			Reason = "Response time degradation detected",
			OldValue = 100, // requests per second
			NewValue = 50,  // reduced rate
			Impact = AdaptationImpact.Moderate,
		};

		// Assert
		adaptationEvent.AdaptationType.ShouldNotBeNullOrEmpty();
		adaptationEvent.Reason.ShouldNotBeNullOrEmpty();
		adaptationEvent.OldValue.ShouldNotBeNull();
		adaptationEvent.NewValue.ShouldNotBeNull();
	}
}
