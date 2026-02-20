// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Scheduling;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Tests.Messaging.Configuration;

/// <summary>
/// Verifies that <c>AddTimeAwareScheduling()</c> properly registers
/// ValidateOnStart and DataAnnotation validation for <see cref="TimeAwareSchedulerOptions"/>.
/// Sprint 562 S562.54: Dispatch core ValidateOnStart registration tests.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SchedulingValidateOnStartRegistrationShould
{
	[Fact]
	public void RegisterSchedulerOptionsValidator()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddTimeAwareScheduling();

		// Assert
		using var provider = services.BuildServiceProvider();
		var validators = provider.GetServices<IValidateOptions<TimeAwareSchedulerOptions>>();
		validators.ShouldNotBeEmpty("AddTimeAwareScheduling should register IValidateOptions<TimeAwareSchedulerOptions>");
	}

	[Fact]
	public void DefaultOptions_ResolveSuccessfully()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddTimeAwareScheduling();

		// Act
		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<TimeAwareSchedulerOptions>>();
		var value = options.Value;

		// Assert - defaults should pass validation
		value.HeavyOperationMultiplier.ShouldBe(2.0);
		value.ComplexOperationMultiplier.ShouldBe(1.5);
		value.AdaptiveTimeoutPercentile.ShouldBe(95);
		value.MinimumSampleSize.ShouldBe(50);
	}

	[Fact]
	public void InvalidHeavyOperationMultiplier_ThrowsOnResolve()
	{
		// Arrange
		var services = new ServiceCollection();

		_ = services.AddTimeAwareScheduling(opts =>
		{
			opts.HeavyOperationMultiplier = 0.5; // Violates [Range(1.0, 5.0)]
		});

		// Act
		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<TimeAwareSchedulerOptions>>();

		// Assert
		_ = Should.Throw<OptionsValidationException>(() => _ = options.Value);
	}

	[Fact]
	public void InvalidComplexOperationMultiplier_ThrowsOnResolve()
	{
		// Arrange
		var services = new ServiceCollection();

		_ = services.AddTimeAwareScheduling(opts =>
		{
			opts.ComplexOperationMultiplier = 4.0; // Violates [Range(1.0, 3.0)]
		});

		// Act
		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<TimeAwareSchedulerOptions>>();

		// Assert
		_ = Should.Throw<OptionsValidationException>(() => _ = options.Value);
	}

	[Fact]
	public void ShimProperties_DelegateToSubOptions()
	{
		// Arrange - shim properties on TimeAwareSchedulerOptions delegate to SchedulerAdaptiveOptions.
		// The [Range] attributes live on SchedulerAdaptiveOptions, not on the shim properties,
		// so ValidateDataAnnotations does not validate them at the top level.
		// This test verifies the delegation works correctly.
		var services = new ServiceCollection();

		_ = services.AddTimeAwareScheduling(opts =>
		{
			opts.AdaptiveTimeoutPercentile = 90;
			opts.MinimumSampleSize = 200;
			opts.MaxTimeoutEscalations = 7;
		});

		// Act
		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<TimeAwareSchedulerOptions>>().Value;

		// Assert - values set via shim properties should be reflected in sub-options
		options.Adaptive.AdaptiveTimeoutPercentile.ShouldBe(90);
		options.Adaptive.MinimumSampleSize.ShouldBe(200);
		options.Adaptive.MaxTimeoutEscalations.ShouldBe(7);
	}

	[Fact]
	public void CustomConfiguration_IsPreservedAfterValidation()
	{
		// Arrange
		var services = new ServiceCollection();

		_ = services.AddTimeAwareScheduling(opts =>
		{
			opts.HeavyOperationMultiplier = 3.0;
			opts.ComplexOperationMultiplier = 2.0;
			opts.AdaptiveTimeoutPercentile = 90;
			opts.MinimumSampleSize = 100;
			opts.MaxTimeoutEscalations = 5;
		});

		// Act
		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<TimeAwareSchedulerOptions>>().Value;

		// Assert
		options.HeavyOperationMultiplier.ShouldBe(3.0);
		options.ComplexOperationMultiplier.ShouldBe(2.0);
		options.AdaptiveTimeoutPercentile.ShouldBe(90);
		options.MinimumSampleSize.ShouldBe(100);
		options.MaxTimeoutEscalations.ShouldBe(5);
	}
}
