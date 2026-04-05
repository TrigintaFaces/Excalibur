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
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
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
		value.Adaptive.AdaptiveTimeoutPercentile.ShouldBe(95);
		value.Adaptive.MinimumSampleSize.ShouldBe(50);
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
	public void SubOptionProperties_DelegateToSubOptions()
	{
		// Arrange - sub-option properties on TimeAwareSchedulerOptions are accessed via Timeouts/Adaptive.
		// The [Range] attributes live on SchedulerAdaptiveOptions, not on the parent,
		// so ValidateDataAnnotations does not validate them at the top level.
		// This test verifies the sub-options work correctly.
		var services = new ServiceCollection();

		_ = services.AddTimeAwareScheduling(opts =>
		{
			opts.Adaptive.AdaptiveTimeoutPercentile = 90;
			opts.Adaptive.MinimumSampleSize = 200;
			opts.Adaptive.MaxTimeoutEscalations = 7;
		});

		// Act
		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<TimeAwareSchedulerOptions>>().Value;

		// Assert - values set via sub-options should be reflected
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
			opts.Adaptive.AdaptiveTimeoutPercentile = 90;
			opts.Adaptive.MinimumSampleSize = 100;
			opts.Adaptive.MaxTimeoutEscalations = 5;
		});

		// Act
		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<TimeAwareSchedulerOptions>>().Value;

		// Assert
		options.HeavyOperationMultiplier.ShouldBe(3.0);
		options.ComplexOperationMultiplier.ShouldBe(2.0);
		options.Adaptive.AdaptiveTimeoutPercentile.ShouldBe(90);
		options.Adaptive.MinimumSampleSize.ShouldBe(100);
		options.Adaptive.MaxTimeoutEscalations.ShouldBe(5);
	}
}
