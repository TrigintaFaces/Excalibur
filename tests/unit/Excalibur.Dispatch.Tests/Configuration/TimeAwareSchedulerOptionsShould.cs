// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Options.Scheduling;

namespace Excalibur.Dispatch.Tests.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class TimeAwareSchedulerOptionsShould
{
	[Fact]
	public void DefaultValues_AreCorrect()
	{
		// Act
		var options = new TimeAwareSchedulerOptions();

		// Assert
		options.PollInterval.ShouldBe(TimeSpan.FromSeconds(30));
		options.PastScheduleBehavior.ShouldBe(PastScheduleBehavior.ExecuteImmediately);
		options.MessageTypeSchedulingTimeouts.ShouldNotBeNull();
		options.MessageTypeSchedulingTimeouts.ShouldBeEmpty();
		options.HeavyOperationMultiplier.ShouldBe(2.0);
		options.ComplexOperationMultiplier.ShouldBe(1.5);
		options.Timeouts.ShouldNotBeNull();
		options.Adaptive.ShouldNotBeNull();
	}

	[Fact]
	public void SectionName_IsCorrect()
	{
		// Assert
		TimeAwareSchedulerOptions.SectionName.ShouldBe("Dispatch:TimeAwareScheduler");
	}

	[Fact]
	public void BackwardCompatibleShims_DelegateToTimeouts()
	{
		// Arrange
		var options = new TimeAwareSchedulerOptions();

		// Act
		options.EnableTimeoutPolicies = false;
		options.ScheduleRetrievalTimeout = TimeSpan.FromSeconds(60);
		options.DeserializationTimeout = TimeSpan.FromSeconds(20);
		options.DispatchTimeout = TimeSpan.FromMinutes(5);
		options.ScheduleUpdateTimeout = TimeSpan.FromSeconds(30);
		options.MaxSchedulingTimeout = TimeSpan.FromMinutes(10);
		options.EnableCronTimeouts = false;
		options.EnableTimezoneTimeouts = false;
		options.LogSchedulingTimeouts = false;
		options.IncludeTimeoutMetrics = false;

		// Assert - shims read from sub-options
		options.Timeouts.EnableTimeoutPolicies.ShouldBeFalse();
		options.Timeouts.ScheduleRetrievalTimeout.ShouldBe(TimeSpan.FromSeconds(60));
		options.Timeouts.DeserializationTimeout.ShouldBe(TimeSpan.FromSeconds(20));
		options.Timeouts.DispatchTimeout.ShouldBe(TimeSpan.FromMinutes(5));
		options.Timeouts.ScheduleUpdateTimeout.ShouldBe(TimeSpan.FromSeconds(30));
		options.Timeouts.MaxSchedulingTimeout.ShouldBe(TimeSpan.FromMinutes(10));
		options.Timeouts.EnableCronTimeouts.ShouldBeFalse();
		options.Timeouts.EnableTimezoneTimeouts.ShouldBeFalse();
		options.Timeouts.LogSchedulingTimeouts.ShouldBeFalse();
		options.Timeouts.IncludeTimeoutMetrics.ShouldBeFalse();
	}

	[Fact]
	public void BackwardCompatibleShims_DelegateToAdaptive()
	{
		// Arrange
		var options = new TimeAwareSchedulerOptions();

		// Act
		options.EnableAdaptiveTimeouts = true;
		options.AdaptiveTimeoutPercentile = 90;
		options.MinimumSampleSize = 100;
		options.EnableTimeoutEscalation = false;
		options.TimeoutEscalationMultiplier = 2.0;
		options.MaxTimeoutEscalations = 5;

		// Assert - shims read from sub-options
		options.Adaptive.EnableAdaptiveTimeouts.ShouldBeTrue();
		options.Adaptive.AdaptiveTimeoutPercentile.ShouldBe(90);
		options.Adaptive.MinimumSampleSize.ShouldBe(100);
		options.Adaptive.EnableTimeoutEscalation.ShouldBeFalse();
		options.Adaptive.TimeoutEscalationMultiplier.ShouldBe(2.0);
		options.Adaptive.MaxTimeoutEscalations.ShouldBe(5);
	}

	[Fact]
	public void Validate_ReturnsEmpty_WhenValid()
	{
		// Arrange - defaults have PollInterval=30s == ScheduleRetrievalTimeout=30s,
		// so set PollInterval < ScheduleRetrievalTimeout to avoid that validation error
		var options = new TimeAwareSchedulerOptions
		{
			PollInterval = TimeSpan.FromSeconds(10),
		};

		// Act
		var results = options.Validate().ToList();

		// Assert
		results.ShouldBeEmpty();
	}

	[Fact]
	public void Validate_DetectsDeserializationTimeoutExceedsDispatch()
	{
		// Arrange
		var options = new TimeAwareSchedulerOptions
		{
			DeserializationTimeout = TimeSpan.FromMinutes(3),
			DispatchTimeout = TimeSpan.FromMinutes(2),
		};

		// Act
		var results = options.Validate().ToList();

		// Assert
		results.ShouldNotBeEmpty();
		results.ShouldContain(r => r.MemberNames.Contains("DeserializationTimeout"));
	}

	[Fact]
	public void Validate_DetectsScheduleRetrievalExceedsMax()
	{
		// Arrange
		var options = new TimeAwareSchedulerOptions
		{
			ScheduleRetrievalTimeout = TimeSpan.FromMinutes(6),
			MaxSchedulingTimeout = TimeSpan.FromMinutes(5),
		};

		// Act
		var results = options.Validate().ToList();

		// Assert
		results.ShouldNotBeEmpty();
		results.ShouldContain(r => r.MemberNames.Contains("ScheduleRetrievalTimeout"));
	}

	[Fact]
	public void Validate_DetectsScheduleUpdateExceedsMax()
	{
		// Arrange
		var options = new TimeAwareSchedulerOptions
		{
			ScheduleUpdateTimeout = TimeSpan.FromMinutes(6),
			MaxSchedulingTimeout = TimeSpan.FromMinutes(5),
		};

		// Act
		var results = options.Validate().ToList();

		// Assert
		results.ShouldNotBeEmpty();
		results.ShouldContain(r => r.MemberNames.Contains("ScheduleUpdateTimeout"));
	}

	[Fact]
	public void Validate_DetectsDispatchTimeoutExceedsMax()
	{
		// Arrange
		var options = new TimeAwareSchedulerOptions
		{
			DispatchTimeout = TimeSpan.FromMinutes(6),
			MaxSchedulingTimeout = TimeSpan.FromMinutes(5),
		};

		// Act
		var results = options.Validate().ToList();

		// Assert
		results.ShouldNotBeEmpty();
		results.ShouldContain(r => r.MemberNames.Contains("DispatchTimeout"));
	}

	[Fact]
	public void Validate_DetectsPollIntervalExceedsRetrieval()
	{
		// Arrange
		var options = new TimeAwareSchedulerOptions
		{
			PollInterval = TimeSpan.FromMinutes(1),
			ScheduleRetrievalTimeout = TimeSpan.FromSeconds(30),
		};

		// Act
		var results = options.Validate().ToList();

		// Assert
		results.ShouldNotBeEmpty();
		results.ShouldContain(r => r.MemberNames.Contains("PollInterval"));
	}

	[Fact]
	public void Validate_DetectsMessageTypeTimeoutExceedsMax()
	{
		// Arrange
		var options = new TimeAwareSchedulerOptions
		{
			MaxSchedulingTimeout = TimeSpan.FromMinutes(5),
		};
		options.MessageTypeSchedulingTimeouts["MyMessage"] = TimeSpan.FromMinutes(10);

		// Act
		var results = options.Validate().ToList();

		// Assert
		results.ShouldNotBeEmpty();
		results.ShouldContain(r => r.MemberNames.Contains("MessageTypeSchedulingTimeouts"));
	}

	[Fact]
	public void Validate_DetectsMessageTypeTimeoutNonPositive()
	{
		// Arrange
		var options = new TimeAwareSchedulerOptions();
		options.MessageTypeSchedulingTimeouts["MyMessage"] = TimeSpan.Zero;

		// Act
		var results = options.Validate().ToList();

		// Assert
		results.ShouldNotBeEmpty();
		results.ShouldContain(r => r.MemberNames.Contains("MessageTypeSchedulingTimeouts"));
	}

	[Fact]
	public void Validate_DetectsEscalationMultiplierTooLow()
	{
		// Arrange
		var options = new TimeAwareSchedulerOptions
		{
			EnableTimeoutEscalation = true,
			TimeoutEscalationMultiplier = 0.5,
		};

		// Act
		var results = options.Validate().ToList();

		// Assert
		results.ShouldNotBeEmpty();
		results.ShouldContain(r => r.MemberNames.Contains("TimeoutEscalationMultiplier"));
	}

	[Fact]
	public void GetTimeoutFor_ReturnsMappedTimeouts()
	{
		// Arrange
		var options = new TimeAwareSchedulerOptions();

		// Act & Assert
		options.GetTimeoutFor(TimeoutOperationType.Database).ShouldBe(options.ScheduleRetrievalTimeout);
		options.GetTimeoutFor(TimeoutOperationType.Serialization).ShouldBe(options.DeserializationTimeout);
		options.GetTimeoutFor(TimeoutOperationType.Handler).ShouldBe(options.DispatchTimeout);
		options.GetTimeoutFor(TimeoutOperationType.Scheduling).ShouldBe(options.ScheduleUpdateTimeout);
		options.GetTimeoutFor(TimeoutOperationType.Validation).ShouldBe(options.DeserializationTimeout);
		options.GetTimeoutFor(TimeoutOperationType.Transport).ShouldBe(options.DispatchTimeout);
		options.GetTimeoutFor(TimeoutOperationType.Default).ShouldBe(options.ScheduleRetrievalTimeout);
	}

	[Fact]
	public void GetTimeoutForMessageType_ThrowsOnNull()
	{
		// Arrange
		var options = new TimeAwareSchedulerOptions();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			options.GetTimeoutForMessageType(null!, TimeoutOperationType.Handler));
	}

	[Fact]
	public void GetTimeoutForMessageType_ReturnsOverrideWhenConfigured()
	{
		// Arrange
		var options = new TimeAwareSchedulerOptions();
		var messageType = typeof(string);
		var messageTypeName = messageType.FullName!;
		options.MessageTypeSchedulingTimeouts[messageTypeName] = TimeSpan.FromMinutes(3);

		// Act
		var timeout = options.GetTimeoutForMessageType(messageType, TimeoutOperationType.Handler);

		// Assert
		timeout.ShouldBe(TimeSpan.FromMinutes(3));
	}

	[Fact]
	public void GetTimeoutForMessageType_FallsBackToOperationType()
	{
		// Arrange
		var options = new TimeAwareSchedulerOptions();

		// Act
		var timeout = options.GetTimeoutForMessageType(typeof(string), TimeoutOperationType.Handler);

		// Assert
		timeout.ShouldBe(options.DispatchTimeout);
	}

	[Fact]
	public void ApplyComplexityMultiplier_SimpleReduces()
	{
		// Arrange
		var options = new TimeAwareSchedulerOptions();
		var baseTimeout = TimeSpan.FromSeconds(100);

		// Act
		var result = options.ApplyComplexityMultiplier(baseTimeout, OperationComplexity.Simple);

		// Assert - 0.8 multiplier
		result.ShouldBe(TimeSpan.FromSeconds(80));
	}

	[Fact]
	public void ApplyComplexityMultiplier_NormalKeepsSame()
	{
		// Arrange
		var options = new TimeAwareSchedulerOptions();
		var baseTimeout = TimeSpan.FromSeconds(100);

		// Act
		var result = options.ApplyComplexityMultiplier(baseTimeout, OperationComplexity.Normal);

		// Assert - 1.0 multiplier
		result.ShouldBe(TimeSpan.FromSeconds(100));
	}

	[Fact]
	public void ApplyComplexityMultiplier_ComplexIncreases()
	{
		// Arrange
		var options = new TimeAwareSchedulerOptions
		{
			ComplexOperationMultiplier = 1.5,
		};
		var baseTimeout = TimeSpan.FromSeconds(100);

		// Act
		var result = options.ApplyComplexityMultiplier(baseTimeout, OperationComplexity.Complex);

		// Assert
		result.ShouldBe(TimeSpan.FromSeconds(150));
	}

	[Fact]
	public void ApplyComplexityMultiplier_HeavyIncreases()
	{
		// Arrange
		var options = new TimeAwareSchedulerOptions
		{
			HeavyOperationMultiplier = 2.0,
		};
		var baseTimeout = TimeSpan.FromSeconds(100);

		// Act
		var result = options.ApplyComplexityMultiplier(baseTimeout, OperationComplexity.Heavy);

		// Assert
		result.ShouldBe(TimeSpan.FromSeconds(200));
	}

	[Fact]
	public void ApplyComplexityMultiplier_CapsAtMaxSchedulingTimeout()
	{
		// Arrange
		var options = new TimeAwareSchedulerOptions
		{
			MaxSchedulingTimeout = TimeSpan.FromMinutes(5),
			HeavyOperationMultiplier = 5.0,
		};
		var baseTimeout = TimeSpan.FromMinutes(3);

		// Act
		var result = options.ApplyComplexityMultiplier(baseTimeout, OperationComplexity.Heavy);

		// Assert - 3 * 5 = 15 minutes, capped at 5 minutes
		result.ShouldBe(TimeSpan.FromMinutes(5));
	}

	// --- SchedulerTimeoutOptions ---

	[Fact]
	public void SchedulerTimeoutOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new SchedulerTimeoutOptions();

		// Assert
		options.EnableTimeoutPolicies.ShouldBeTrue();
		options.ScheduleRetrievalTimeout.ShouldBe(TimeSpan.FromSeconds(30));
		options.DeserializationTimeout.ShouldBe(TimeSpan.FromSeconds(10));
		options.DispatchTimeout.ShouldBe(TimeSpan.FromMinutes(2));
		options.ScheduleUpdateTimeout.ShouldBe(TimeSpan.FromSeconds(15));
		options.MaxSchedulingTimeout.ShouldBe(TimeSpan.FromMinutes(5));
		options.EnableCronTimeouts.ShouldBeTrue();
		options.EnableTimezoneTimeouts.ShouldBeTrue();
		options.LogSchedulingTimeouts.ShouldBeTrue();
		options.IncludeTimeoutMetrics.ShouldBeTrue();
	}

	// --- SchedulerAdaptiveOptions ---

	[Fact]
	public void SchedulerAdaptiveOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new SchedulerAdaptiveOptions();

		// Assert
		options.EnableAdaptiveTimeouts.ShouldBeFalse();
		options.AdaptiveTimeoutPercentile.ShouldBe(95);
		options.MinimumSampleSize.ShouldBe(50);
		options.EnableTimeoutEscalation.ShouldBeTrue();
		options.TimeoutEscalationMultiplier.ShouldBe(1.5);
		options.MaxTimeoutEscalations.ShouldBe(3);
	}
}
