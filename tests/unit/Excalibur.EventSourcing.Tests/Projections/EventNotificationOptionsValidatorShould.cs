// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Tests.Projections;

/// <summary>
/// Tests for <c>EventNotificationOptionsValidator</c> (internal), exercised via reflection
/// since the class is internal to EventSourcing without InternalsVisibleTo.
/// Also tests <see cref="EventNotificationOptions"/> defaults (R27.4).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class EventNotificationOptionsValidatorShould
{
	private static IValidateOptions<EventNotificationOptions> CreateValidator()
	{
		// The validator is internal sealed in Excalibur.EventSourcing -- use reflection to instantiate it.
		var validatorType = typeof(ExcaliburEventSourcingBuilder).Assembly
			.GetType("Excalibur.EventSourcing.Projections.EventNotificationOptionsValidator")!;
		return (IValidateOptions<EventNotificationOptions>)Activator.CreateInstance(validatorType)!;
	}

	[Fact]
	public void AcceptDefaultOptions()
	{
		// Arrange
		var validator = CreateValidator();
		var options = new EventNotificationOptions();

		// Act
		var result = validator.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void AcceptMinimumThreshold()
	{
		// Arrange -- 1ms is the minimum valid threshold
		var validator = CreateValidator();
		var options = new EventNotificationOptions
		{
			InlineProjectionWarningThreshold = TimeSpan.FromMilliseconds(1)
		};

		// Act
		var result = validator.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void AcceptMaximumThreshold()
	{
		// Arrange -- 10 minutes is the maximum valid threshold
		var validator = CreateValidator();
		var options = new EventNotificationOptions
		{
			InlineProjectionWarningThreshold = TimeSpan.FromMinutes(10)
		};

		// Act
		var result = validator.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void RejectThresholdBelowMinimum()
	{
		// Arrange -- zero is below the 1ms minimum
		var validator = CreateValidator();
		var options = new EventNotificationOptions
		{
			InlineProjectionWarningThreshold = TimeSpan.Zero
		};

		// Act
		var result = validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(EventNotificationOptions.InlineProjectionWarningThreshold));
	}

	[Fact]
	public void RejectNegativeThreshold()
	{
		// Arrange
		var validator = CreateValidator();
		var options = new EventNotificationOptions
		{
			InlineProjectionWarningThreshold = TimeSpan.FromMilliseconds(-1)
		};

		// Act
		var result = validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(EventNotificationOptions.InlineProjectionWarningThreshold));
	}

	[Fact]
	public void RejectThresholdAboveMaximum()
	{
		// Arrange -- exceeds the 10-minute maximum
		var validator = CreateValidator();
		var options = new EventNotificationOptions
		{
			InlineProjectionWarningThreshold = TimeSpan.FromMinutes(11)
		};

		// Act
		var result = validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(EventNotificationOptions.InlineProjectionWarningThreshold));
	}

	[Fact]
	public void ThrowOnNullOptions()
	{
		var validator = CreateValidator();
		Should.Throw<ArgumentNullException>(() => validator.Validate(null, null!));
	}

	[Fact]
	public void DefaultFailurePolicyIsPropagatePerR274()
	{
		// Assert -- R27.4 specifies Propagate as default
		var options = new EventNotificationOptions();
		options.FailurePolicy.ShouldBe(NotificationFailurePolicy.Propagate);
	}

	[Fact]
	public void DefaultWarningThresholdIs100Ms()
	{
		var options = new EventNotificationOptions();
		options.InlineProjectionWarningThreshold.ShouldBe(TimeSpan.FromMilliseconds(100));
	}

	[Fact]
	public void AcceptMidRangeThreshold()
	{
		// Arrange -- 5 minutes is a valid mid-range value
		var validator = CreateValidator();
		var options = new EventNotificationOptions
		{
			InlineProjectionWarningThreshold = TimeSpan.FromMinutes(5)
		};

		// Act
		var result = validator.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void AcceptLogAndContinueFailurePolicy()
	{
		// Arrange -- both failure policies are valid
		var validator = CreateValidator();
		var options = new EventNotificationOptions
		{
			FailurePolicy = NotificationFailurePolicy.LogAndContinue
		};

		// Act
		var result = validator.Validate(null, options);

		// Assert -- validator only checks threshold, not policy
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void RejectJustBelowMinimum()
	{
		// Arrange -- just below 1ms boundary
		var validator = CreateValidator();
		var options = new EventNotificationOptions
		{
			InlineProjectionWarningThreshold = TimeSpan.FromTicks(9_999) // 0.9999ms
		};

		// Act
		var result = validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
	}

	[Fact]
	public void RejectJustAboveMaximum()
	{
		// Arrange -- just above 10 minutes boundary
		var validator = CreateValidator();
		var options = new EventNotificationOptions
		{
			InlineProjectionWarningThreshold = TimeSpan.FromMinutes(10).Add(TimeSpan.FromTicks(1))
		};

		// Act
		var result = validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
	}

	[Fact]
	public void IncludeActualValueInFailureMessage()
	{
		// Arrange
		var validator = CreateValidator();
		var threshold = TimeSpan.FromMinutes(15);
		var options = new EventNotificationOptions
		{
			InlineProjectionWarningThreshold = threshold
		};

		// Act
		var result = validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(threshold.ToString());
	}

	[Fact]
	public void ValidatorIsRegisteredInDiByUseEventNotification()
	{
		// Verifies that UseEventNotification registers EventNotificationOptionsValidator
		// so that ValidateOnStart can run it at startup.
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddMetrics();

		var fakeBuilder = A.Fake<IEventSourcingBuilder>();
		A.CallTo(() => fakeBuilder.Services).Returns(services);
		fakeBuilder.UseEventNotification();

		var sp = services.BuildServiceProvider();
		var validators = sp.GetServices<IValidateOptions<EventNotificationOptions>>().ToList();

		// Fixed: validator is now registered via TryAddEnumerable in UseEventNotification
		validators.ShouldNotBeEmpty();
		validators.Count.ShouldBe(1);
	}
}
