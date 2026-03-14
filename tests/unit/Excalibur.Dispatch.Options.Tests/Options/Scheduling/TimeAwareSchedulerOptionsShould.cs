// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Options.Scheduling;

namespace Excalibur.Dispatch.Tests.Options.Scheduling;

/// <summary>
/// Unit tests for <see cref="TimeAwareSchedulerOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class TimeAwareSchedulerOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_PollInterval_Is30Seconds()
	{
		// Arrange & Act
		var options = new TimeAwareSchedulerOptions();

		// Assert
		options.PollInterval.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void Default_PastScheduleBehavior_IsExecuteImmediately()
	{
		// Arrange & Act
		var options = new TimeAwareSchedulerOptions();

		// Assert
		options.PastScheduleBehavior.ShouldBe(PastScheduleBehavior.ExecuteImmediately);
	}

	[Fact]
	public void Default_Timeouts_EnableTimeoutPolicies_IsTrue()
	{
		// Arrange & Act
		var options = new TimeAwareSchedulerOptions();

		// Assert
		options.Timeouts.EnableTimeoutPolicies.ShouldBeTrue();
	}

	[Fact]
	public void Default_Adaptive_EnableAdaptiveTimeouts_IsFalse()
	{
		// Arrange & Act
		var options = new TimeAwareSchedulerOptions();

		// Assert
		options.Adaptive.EnableAdaptiveTimeouts.ShouldBeFalse();
	}

	[Fact]
	public void Default_Timeouts_ScheduleRetrievalTimeout_Is30Seconds()
	{
		// Arrange & Act
		var options = new TimeAwareSchedulerOptions();

		// Assert
		options.Timeouts.ScheduleRetrievalTimeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void Default_Timeouts_DeserializationTimeout_Is10Seconds()
	{
		// Arrange & Act
		var options = new TimeAwareSchedulerOptions();

		// Assert
		options.Timeouts.DeserializationTimeout.ShouldBe(TimeSpan.FromSeconds(10));
	}

	[Fact]
	public void Default_Timeouts_DispatchTimeout_Is2Minutes()
	{
		// Arrange & Act
		var options = new TimeAwareSchedulerOptions();

		// Assert
		options.Timeouts.DispatchTimeout.ShouldBe(TimeSpan.FromMinutes(2));
	}

	[Fact]
	public void Default_Timeouts_ScheduleUpdateTimeout_Is15Seconds()
	{
		// Arrange & Act
		var options = new TimeAwareSchedulerOptions();

		// Assert
		options.Timeouts.ScheduleUpdateTimeout.ShouldBe(TimeSpan.FromSeconds(15));
	}

	[Fact]
	public void Default_Timeouts_MaxSchedulingTimeout_Is5Minutes()
	{
		// Arrange & Act
		var options = new TimeAwareSchedulerOptions();

		// Assert
		options.Timeouts.MaxSchedulingTimeout.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void Default_Timeouts_EnableCronTimeouts_IsTrue()
	{
		// Arrange & Act
		var options = new TimeAwareSchedulerOptions();

		// Assert
		options.Timeouts.EnableCronTimeouts.ShouldBeTrue();
	}

	[Fact]
	public void Default_HeavyOperationMultiplier_Is2()
	{
		// Arrange & Act
		var options = new TimeAwareSchedulerOptions();

		// Assert
		options.HeavyOperationMultiplier.ShouldBe(2.0);
	}

	[Fact]
	public void Default_ComplexOperationMultiplier_Is1_5()
	{
		// Arrange & Act
		var options = new TimeAwareSchedulerOptions();

		// Assert
		options.ComplexOperationMultiplier.ShouldBe(1.5);
	}

	[Fact]
	public void Default_MessageTypeSchedulingTimeouts_IsEmpty()
	{
		// Arrange & Act
		var options = new TimeAwareSchedulerOptions();

		// Assert
		_ = options.MessageTypeSchedulingTimeouts.ShouldNotBeNull();
		options.MessageTypeSchedulingTimeouts.ShouldBeEmpty();
	}

	[Fact]
	public void Default_Timeouts_LogSchedulingTimeouts_IsTrue()
	{
		// Arrange & Act
		var options = new TimeAwareSchedulerOptions();

		// Assert
		options.Timeouts.LogSchedulingTimeouts.ShouldBeTrue();
	}

	[Fact]
	public void Default_Adaptive_AdaptiveTimeoutPercentile_Is95()
	{
		// Arrange & Act
		var options = new TimeAwareSchedulerOptions();

		// Assert
		options.Adaptive.AdaptiveTimeoutPercentile.ShouldBe(95);
	}

	[Fact]
	public void Default_Adaptive_MinimumSampleSize_Is50()
	{
		// Arrange & Act
		var options = new TimeAwareSchedulerOptions();

		// Assert
		options.Adaptive.MinimumSampleSize.ShouldBe(50);
	}

	[Fact]
	public void Default_Adaptive_EnableTimeoutEscalation_IsTrue()
	{
		// Arrange & Act
		var options = new TimeAwareSchedulerOptions();

		// Assert
		options.Adaptive.EnableTimeoutEscalation.ShouldBeTrue();
	}

	[Fact]
	public void Default_Adaptive_TimeoutEscalationMultiplier_Is1_5()
	{
		// Arrange & Act
		var options = new TimeAwareSchedulerOptions();

		// Assert
		options.Adaptive.TimeoutEscalationMultiplier.ShouldBe(1.5);
	}

	[Fact]
	public void Default_Adaptive_MaxTimeoutEscalations_Is3()
	{
		// Arrange & Act
		var options = new TimeAwareSchedulerOptions();

		// Assert
		options.Adaptive.MaxTimeoutEscalations.ShouldBe(3);
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void PollInterval_CanBeSet()
	{
		// Arrange
		var options = new TimeAwareSchedulerOptions();

		// Act
		options.PollInterval = TimeSpan.FromMinutes(1);

		// Assert
		options.PollInterval.ShouldBe(TimeSpan.FromMinutes(1));
	}

	[Fact]
	public void Adaptive_EnableAdaptiveTimeouts_CanBeSet()
	{
		// Arrange
		var options = new TimeAwareSchedulerOptions();

		// Act
		options.Adaptive.EnableAdaptiveTimeouts = true;

		// Assert
		options.Adaptive.EnableAdaptiveTimeouts.ShouldBeTrue();
	}

	[Fact]
	public void MessageTypeSchedulingTimeouts_CanAddItems()
	{
		// Arrange
		var options = new TimeAwareSchedulerOptions();

		// Act
		options.MessageTypeSchedulingTimeouts["MyNamespace.MyMessage"] = TimeSpan.FromMinutes(3);

		// Assert
		options.MessageTypeSchedulingTimeouts.Count.ShouldBe(1);
		options.MessageTypeSchedulingTimeouts["MyNamespace.MyMessage"].ShouldBe(TimeSpan.FromMinutes(3));
	}

	#endregion

	#region Validation Tests

	[Fact]
	public void Validate_ReturnsEmptyCollection_ForValidOptions()
	{
		// Arrange - Create valid options where PollInterval < ScheduleRetrievalTimeout
		var options = new TimeAwareSchedulerOptions
		{
			PollInterval = TimeSpan.FromSeconds(15),
		};

		// Act
		var results = options.Validate().ToList();

		// Assert
		results.ShouldBeEmpty();
	}

	[Fact]
	public void Validate_ReturnsError_WhenPollIntervalEqualsScheduleRetrievalTimeout()
	{
		// Arrange - Default values have PollInterval == ScheduleRetrievalTimeout (both 30s)
		var options = new TimeAwareSchedulerOptions();

		// Act
		var results = options.Validate().ToList();

		// Assert
		results.ShouldNotBeEmpty();
	}

	[Fact]
	public void Validate_ReturnsError_WhenDeserializationTimeoutExceedsDispatchTimeout()
	{
		// Arrange
		var options = new TimeAwareSchedulerOptions();
		options.Timeouts.DeserializationTimeout = TimeSpan.FromMinutes(5);
		options.Timeouts.DispatchTimeout = TimeSpan.FromMinutes(2);

		// Act
		var results = options.Validate().ToList();

		// Assert
		results.ShouldNotBeEmpty();
	}

	[Fact]
	public void Validate_ReturnsError_WhenScheduleRetrievalTimeoutExceedsMaxSchedulingTimeout()
	{
		// Arrange
		var options = new TimeAwareSchedulerOptions();
		options.Timeouts.ScheduleRetrievalTimeout = TimeSpan.FromMinutes(10);
		options.Timeouts.MaxSchedulingTimeout = TimeSpan.FromMinutes(5);

		// Act
		var results = options.Validate().ToList();

		// Assert
		results.ShouldNotBeEmpty();
	}

	[Fact]
	public void Validate_ReturnsError_WhenMessageTypeTimeoutExceedsMax()
	{
		// Arrange
		var options = new TimeAwareSchedulerOptions();
		options.Timeouts.MaxSchedulingTimeout = TimeSpan.FromMinutes(5);
		options.MessageTypeSchedulingTimeouts["Test.Message"] = TimeSpan.FromMinutes(10);

		// Act
		var results = options.Validate().ToList();

		// Assert
		results.ShouldNotBeEmpty();
	}

	[Fact]
	public void Validate_ReturnsError_WhenMessageTypeTimeoutIsZeroOrNegative()
	{
		// Arrange
		var options = new TimeAwareSchedulerOptions();
		options.MessageTypeSchedulingTimeouts["Test.Message"] = TimeSpan.Zero;

		// Act
		var results = options.Validate().ToList();

		// Assert
		results.ShouldNotBeEmpty();
	}

	#endregion

	#region GetTimeoutFor Tests

	[Fact]
	public void GetTimeoutFor_Database_ReturnsScheduleRetrievalTimeout()
	{
		// Arrange
		var options = new TimeAwareSchedulerOptions();
		options.Timeouts.ScheduleRetrievalTimeout = TimeSpan.FromSeconds(45);

		// Act
		var result = options.GetTimeoutFor(TimeoutOperationType.Database);

		// Assert
		result.ShouldBe(TimeSpan.FromSeconds(45));
	}

	[Fact]
	public void GetTimeoutFor_Serialization_ReturnsDeserializationTimeout()
	{
		// Arrange
		var options = new TimeAwareSchedulerOptions();
		options.Timeouts.DeserializationTimeout = TimeSpan.FromSeconds(15);

		// Act
		var result = options.GetTimeoutFor(TimeoutOperationType.Serialization);

		// Assert
		result.ShouldBe(TimeSpan.FromSeconds(15));
	}

	[Fact]
	public void GetTimeoutFor_Handler_ReturnsDispatchTimeout()
	{
		// Arrange
		var options = new TimeAwareSchedulerOptions();
		options.Timeouts.DispatchTimeout = TimeSpan.FromMinutes(3);

		// Act
		var result = options.GetTimeoutFor(TimeoutOperationType.Handler);

		// Assert
		result.ShouldBe(TimeSpan.FromMinutes(3));
	}

	#endregion

	#region GetTimeoutForMessageType Tests

	[Fact]
	public void GetTimeoutForMessageType_ReturnsOverride_WhenConfigured()
	{
		// Arrange
		var options = new TimeAwareSchedulerOptions();
		var messageType = typeof(string);
		options.MessageTypeSchedulingTimeouts[messageType.FullName] = TimeSpan.FromMinutes(4);

		// Act
		var result = options.GetTimeoutForMessageType(messageType, TimeoutOperationType.Handler);

		// Assert
		result.ShouldBe(TimeSpan.FromMinutes(4));
	}

	[Fact]
	public void GetTimeoutForMessageType_ReturnsDefault_WhenNoOverride()
	{
		// Arrange
		var options = new TimeAwareSchedulerOptions();
		options.Timeouts.DispatchTimeout = TimeSpan.FromMinutes(2);
		var messageType = typeof(string);

		// Act
		var result = options.GetTimeoutForMessageType(messageType, TimeoutOperationType.Handler);

		// Assert
		result.ShouldBe(TimeSpan.FromMinutes(2));
	}

	#endregion

	#region ApplyComplexityMultiplier Tests

	[Fact]
	public void ApplyComplexityMultiplier_Simple_ReducesTimeout()
	{
		// Arrange
		var options = new TimeAwareSchedulerOptions();
		var baseTimeout = TimeSpan.FromSeconds(100);

		// Act
		var result = options.ApplyComplexityMultiplier(baseTimeout, OperationComplexity.Simple);

		// Assert
		result.ShouldBeLessThan(baseTimeout);
	}

	[Fact]
	public void ApplyComplexityMultiplier_Normal_ReturnsBaseTimeout()
	{
		// Arrange
		var options = new TimeAwareSchedulerOptions();
		var baseTimeout = TimeSpan.FromSeconds(100);

		// Act
		var result = options.ApplyComplexityMultiplier(baseTimeout, OperationComplexity.Normal);

		// Assert
		result.ShouldBe(baseTimeout);
	}

	[Fact]
	public void ApplyComplexityMultiplier_Complex_IncreasesTimeout()
	{
		// Arrange
		var options = new TimeAwareSchedulerOptions
		{
			ComplexOperationMultiplier = 1.5,
		};
		options.Timeouts.MaxSchedulingTimeout = TimeSpan.FromMinutes(10);
		var baseTimeout = TimeSpan.FromSeconds(100);

		// Act
		var result = options.ApplyComplexityMultiplier(baseTimeout, OperationComplexity.Complex);

		// Assert
		result.ShouldBeGreaterThan(baseTimeout);
	}

	[Fact]
	public void ApplyComplexityMultiplier_Heavy_IncreasesTimeoutMore()
	{
		// Arrange
		var options = new TimeAwareSchedulerOptions
		{
			HeavyOperationMultiplier = 2.0,
		};
		options.Timeouts.MaxSchedulingTimeout = TimeSpan.FromMinutes(10);
		var baseTimeout = TimeSpan.FromSeconds(100);

		// Act
		var result = options.ApplyComplexityMultiplier(baseTimeout, OperationComplexity.Heavy);

		// Assert
		result.ShouldBeGreaterThan(baseTimeout);
		result.ShouldBe(TimeSpan.FromSeconds(200));
	}

	[Fact]
	public void ApplyComplexityMultiplier_DoesNotExceedMaxTimeout()
	{
		// Arrange
		var options = new TimeAwareSchedulerOptions
		{
			HeavyOperationMultiplier = 10.0,
		};
		options.Timeouts.MaxSchedulingTimeout = TimeSpan.FromMinutes(5);
		var baseTimeout = TimeSpan.FromMinutes(2);

		// Act
		var result = options.ApplyComplexityMultiplier(baseTimeout, OperationComplexity.Heavy);

		// Assert
		result.ShouldBeLessThanOrEqualTo(TimeSpan.FromMinutes(5));
	}

	#endregion
}
