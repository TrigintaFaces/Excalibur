// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Abstractions.Tests.Timing;

/// <summary>
/// Unit tests for <see cref="TimePolicyOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Timing")]
[Trait("Priority", "0")]
public sealed class TimePolicyOptionsShould
{
	#region Section Name Tests

	[Fact]
	public void SectionName_HasExpectedValue()
	{
		// Assert
		TimePolicyOptions.SectionName.ShouldBe("Dispatch:TimePolicy");
	}

	#endregion

	#region Default Values Tests

	[Fact]
	public void Default_DefaultTimeoutIs30Seconds()
	{
		// Arrange & Act
		var options = new TimePolicyOptions();

		// Assert
		options.DefaultTimeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void Default_MaxTimeoutIs5Minutes()
	{
		// Arrange & Act
		var options = new TimePolicyOptions();

		// Assert
		options.MaxTimeout.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void Default_HandlerTimeoutIs2Minutes()
	{
		// Arrange & Act
		var options = new TimePolicyOptions();

		// Assert
		options.HandlerTimeout.ShouldBe(TimeSpan.FromMinutes(2));
	}

	[Fact]
	public void Default_SerializationTimeoutIs10Seconds()
	{
		// Arrange & Act
		var options = new TimePolicyOptions();

		// Assert
		options.SerializationTimeout.ShouldBe(TimeSpan.FromSeconds(10));
	}

	[Fact]
	public void Default_TransportTimeoutIs1Minute()
	{
		// Arrange & Act
		var options = new TimePolicyOptions();

		// Assert
		options.TransportTimeout.ShouldBe(TimeSpan.FromMinutes(1));
	}

	[Fact]
	public void Default_ValidationTimeoutIs5Seconds()
	{
		// Arrange & Act
		var options = new TimePolicyOptions();

		// Assert
		options.ValidationTimeout.ShouldBe(TimeSpan.FromSeconds(5));
	}

	[Fact]
	public void Default_ComplexityMultiplierIs2()
	{
		// Arrange & Act
		var options = new TimePolicyOptions();

		// Assert
		options.ComplexityMultiplier.ShouldBe(2.0);
	}

	[Fact]
	public void Default_HeavyOperationMultiplierIs3()
	{
		// Arrange & Act
		var options = new TimePolicyOptions();

		// Assert
		options.HeavyOperationMultiplier.ShouldBe(3.0);
	}

	[Fact]
	public void Default_EnforceTimeoutsIsTrue()
	{
		// Arrange & Act
		var options = new TimePolicyOptions();

		// Assert
		options.EnforceTimeouts.ShouldBeTrue();
	}

	[Fact]
	public void Default_UseAdaptiveTimeoutsIsFalse()
	{
		// Arrange & Act
		var options = new TimePolicyOptions();

		// Assert
		options.UseAdaptiveTimeouts.ShouldBeFalse();
	}

	[Fact]
	public void Default_AdaptiveTimeoutPercentileIs95()
	{
		// Arrange & Act
		var options = new TimePolicyOptions();

		// Assert
		options.AdaptiveTimeoutPercentile.ShouldBe(95);
	}

	[Fact]
	public void Default_MinimumSampleSizeIs100()
	{
		// Arrange & Act
		var options = new TimePolicyOptions();

		// Assert
		options.MinimumSampleSize.ShouldBe(100);
	}

	[Fact]
	public void Default_CustomTimeoutsIsEmpty()
	{
		// Arrange & Act
		var options = new TimePolicyOptions();

		// Assert
		_ = options.CustomTimeouts.ShouldNotBeNull();
		options.CustomTimeouts.ShouldBeEmpty();
	}

	[Fact]
	public void Default_MessageTypeTimeoutsIsEmpty()
	{
		// Arrange & Act
		var options = new TimePolicyOptions();

		// Assert
		_ = options.MessageTypeTimeouts.ShouldNotBeNull();
		options.MessageTypeTimeouts.ShouldBeEmpty();
	}

	[Fact]
	public void Default_HandlerTypeTimeoutsIsEmpty()
	{
		// Arrange & Act
		var options = new TimePolicyOptions();

		// Assert
		_ = options.HandlerTypeTimeouts.ShouldNotBeNull();
		options.HandlerTypeTimeouts.ShouldBeEmpty();
	}

	[Fact]
	public void Default_LogTimeoutEventsIsTrue()
	{
		// Arrange & Act
		var options = new TimePolicyOptions();

		// Assert
		options.LogTimeoutEvents.ShouldBeTrue();
	}

	[Fact]
	public void Default_IncludeTimeoutMetricsIsTrue()
	{
		// Arrange & Act
		var options = new TimePolicyOptions();

		// Assert
		options.IncludeTimeoutMetrics.ShouldBeTrue();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void DefaultTimeout_CanBeSet()
	{
		// Arrange
		var options = new TimePolicyOptions();

		// Act
		options.DefaultTimeout = TimeSpan.FromSeconds(15);

		// Assert
		options.DefaultTimeout.ShouldBe(TimeSpan.FromSeconds(15));
	}

	[Fact]
	public void MaxTimeout_CanBeSet()
	{
		// Arrange
		var options = new TimePolicyOptions();

		// Act
		options.MaxTimeout = TimeSpan.FromMinutes(10);

		// Assert
		options.MaxTimeout.ShouldBe(TimeSpan.FromMinutes(10));
	}

	[Fact]
	public void CustomTimeouts_CanAddEntry()
	{
		// Arrange
		var options = new TimePolicyOptions();

		// Act
		options.CustomTimeouts[TimeoutOperationType.Handler] = TimeSpan.FromMinutes(3);

		// Assert
		options.CustomTimeouts[TimeoutOperationType.Handler].ShouldBe(TimeSpan.FromMinutes(3));
	}

	[Fact]
	public void MessageTypeTimeouts_CanAddEntry()
	{
		// Arrange
		var options = new TimePolicyOptions();

		// Act
		options.MessageTypeTimeouts["MyNamespace.MyMessage"] = TimeSpan.FromSeconds(45);

		// Assert
		options.MessageTypeTimeouts["MyNamespace.MyMessage"].ShouldBe(TimeSpan.FromSeconds(45));
	}

	[Fact]
	public void HandlerTypeTimeouts_CanAddEntry()
	{
		// Arrange
		var options = new TimePolicyOptions();

		// Act
		options.HandlerTypeTimeouts["MyNamespace.MyHandler"] = TimeSpan.FromMinutes(1);

		// Assert
		options.HandlerTypeTimeouts["MyNamespace.MyHandler"].ShouldBe(TimeSpan.FromMinutes(1));
	}

	#endregion

	#region Validate Tests

	[Fact]
	public void Validate_WithValidDefaults_ReturnsNoErrors()
	{
		// Arrange
		var options = new TimePolicyOptions();

		// Act
		var results = options.Validate();

		// Assert
		results.ShouldBeEmpty();
	}

	[Fact]
	public void Validate_WithDefaultTimeoutGreaterThanMaxTimeout_ReturnsError()
	{
		// Arrange
		var options = new TimePolicyOptions
		{
			DefaultTimeout = TimeSpan.FromMinutes(10),
			MaxTimeout = TimeSpan.FromMinutes(5),
		};

		// Act
		var results = options.Validate().ToList();

		// Assert
		results.ShouldNotBeEmpty();
		results.ShouldContain(r => r.ErrorMessage.Contains("DefaultTimeout must be less than MaxTimeout"));
	}

	[Fact]
	public void Validate_WithDefaultTimeoutEqualToMaxTimeout_ReturnsError()
	{
		// Arrange
		var options = new TimePolicyOptions
		{
			DefaultTimeout = TimeSpan.FromMinutes(5),
			MaxTimeout = TimeSpan.FromMinutes(5),
		};

		// Act
		var results = options.Validate().ToList();

		// Assert
		results.ShouldNotBeEmpty();
		results.ShouldContain(r => r.ErrorMessage.Contains("DefaultTimeout must be less than MaxTimeout"));
	}

	[Fact]
	public void Validate_WithHandlerTimeoutGreaterThanMaxTimeout_ReturnsError()
	{
		// Arrange
		var options = new TimePolicyOptions
		{
			HandlerTimeout = TimeSpan.FromMinutes(10),
			MaxTimeout = TimeSpan.FromMinutes(5),
		};

		// Act
		var results = options.Validate().ToList();

		// Assert
		results.ShouldNotBeEmpty();
		results.ShouldContain(r => r.ErrorMessage.Contains("HandlerTimeout cannot exceed MaxTimeout"));
	}

	[Fact]
	public void Validate_WithTransportTimeoutGreaterThanMaxTimeout_ReturnsError()
	{
		// Arrange
		var options = new TimePolicyOptions
		{
			TransportTimeout = TimeSpan.FromMinutes(10),
			MaxTimeout = TimeSpan.FromMinutes(5),
		};

		// Act
		var results = options.Validate().ToList();

		// Assert
		results.ShouldNotBeEmpty();
		results.ShouldContain(r => r.ErrorMessage.Contains("TransportTimeout cannot exceed MaxTimeout"));
	}

	[Fact]
	public void Validate_WithSerializationTimeoutGreaterThanOrEqualToHandlerTimeout_ReturnsError()
	{
		// Arrange
		var options = new TimePolicyOptions
		{
			SerializationTimeout = TimeSpan.FromMinutes(2),
			HandlerTimeout = TimeSpan.FromMinutes(2),
		};

		// Act
		var results = options.Validate().ToList();

		// Assert
		results.ShouldNotBeEmpty();
		results.ShouldContain(r => r.ErrorMessage.Contains("SerializationTimeout should be less than HandlerTimeout"));
	}

	[Fact]
	public void Validate_WithValidationTimeoutGreaterThanOrEqualToHandlerTimeout_ReturnsError()
	{
		// Arrange
		var options = new TimePolicyOptions
		{
			ValidationTimeout = TimeSpan.FromMinutes(3),
			HandlerTimeout = TimeSpan.FromMinutes(2),
		};

		// Act
		var results = options.Validate().ToList();

		// Assert
		results.ShouldNotBeEmpty();
		results.ShouldContain(r => r.ErrorMessage.Contains("ValidationTimeout should be less than HandlerTimeout"));
	}

	[Fact]
	public void Validate_WithCustomTimeoutExceedingMaxTimeout_ReturnsError()
	{
		// Arrange
		var options = new TimePolicyOptions
		{
			MaxTimeout = TimeSpan.FromMinutes(5),
		};
		options.CustomTimeouts[TimeoutOperationType.Handler] = TimeSpan.FromMinutes(10);

		// Act
		var results = options.Validate().ToList();

		// Assert
		results.ShouldNotBeEmpty();
		results.ShouldContain(r => r.ErrorMessage.Contains("Custom timeout for Handler cannot exceed MaxTimeout"));
	}

	[Fact]
	public void Validate_WithMessageTypeTimeoutExceedingMaxTimeout_ReturnsError()
	{
		// Arrange
		var options = new TimePolicyOptions
		{
			MaxTimeout = TimeSpan.FromMinutes(5),
		};
		options.MessageTypeTimeouts["MyNamespace.MyMessage"] = TimeSpan.FromMinutes(10);

		// Act
		var results = options.Validate().ToList();

		// Assert
		results.ShouldNotBeEmpty();
		results.ShouldContain(r => r.ErrorMessage.Contains("Message type timeout for MyNamespace.MyMessage cannot exceed MaxTimeout"));
	}

	[Fact]
	public void Validate_WithHandlerTypeTimeoutExceedingMaxTimeout_ReturnsError()
	{
		// Arrange
		var options = new TimePolicyOptions
		{
			MaxTimeout = TimeSpan.FromMinutes(5),
		};
		options.HandlerTypeTimeouts["MyNamespace.MyHandler"] = TimeSpan.FromMinutes(10);

		// Act
		var results = options.Validate().ToList();

		// Assert
		results.ShouldNotBeEmpty();
		results.ShouldContain(r => r.ErrorMessage.Contains("Handler type timeout for MyNamespace.MyHandler cannot exceed MaxTimeout"));
	}

	[Fact]
	public void Validate_WithMultipleViolations_ReturnsAllErrors()
	{
		// Arrange
		var options = new TimePolicyOptions
		{
			DefaultTimeout = TimeSpan.FromMinutes(10),
			MaxTimeout = TimeSpan.FromMinutes(5),
			HandlerTimeout = TimeSpan.FromMinutes(10),
		};

		// Act
		var results = options.Validate().ToList();

		// Assert
		results.Count.ShouldBeGreaterThanOrEqualTo(2);
	}

	#endregion
}
