// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.ErrorHandling;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Tests.Messaging.Configuration;

/// <summary>
/// Verifies that <c>AddPoisonMessageHandling()</c> properly registers
/// ValidateOnStart and DataAnnotation validation for <see cref="PoisonMessageOptions"/>.
/// Sprint 562 S562.53: Dispatch core ValidateOnStart registration tests.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class PoisonMessageValidateOnStartRegistrationShould
{
	[Fact]
	public void RegisterPoisonMessageOptionsValidator()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddPoisonMessageHandling();

		// Assert
		using var provider = services.BuildServiceProvider();
		var validators = provider.GetServices<IValidateOptions<PoisonMessageOptions>>();
		validators.ShouldNotBeEmpty("AddPoisonMessageHandling should register IValidateOptions<PoisonMessageOptions>");
	}

	[Fact]
	public void ValidOptions_ResolveSuccessfully()
	{
		// Arrange
		var services = new ServiceCollection();

		_ = services.AddPoisonMessageHandling(opts =>
		{
			opts.MaxRetryAttempts = 5;
			opts.AlertThreshold = 20;
		});

		// Act
		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<PoisonMessageOptions>>();
		var value = options.Value;

		// Assert
		value.MaxRetryAttempts.ShouldBe(5);
		value.AlertThreshold.ShouldBe(20);
	}

	[Fact]
	public void InvalidMaxRetryAttempts_ThrowsOnResolve()
	{
		// Arrange
		var services = new ServiceCollection();

		_ = services.AddPoisonMessageHandling(opts =>
		{
			opts.MaxRetryAttempts = 0; // Violates [Range(1, int.MaxValue)]
		});

		// Act
		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<PoisonMessageOptions>>();

		// Assert
		_ = Should.Throw<OptionsValidationException>(() => _ = options.Value);
	}

	[Fact]
	public void InvalidAlertThreshold_ThrowsOnResolve()
	{
		// Arrange
		var services = new ServiceCollection();

		_ = services.AddPoisonMessageHandling(opts =>
		{
			opts.AlertThreshold = -1; // Violates [Range(1, int.MaxValue)]
		});

		// Act
		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<PoisonMessageOptions>>();

		// Assert
		_ = Should.Throw<OptionsValidationException>(() => _ = options.Value);
	}

	[Fact]
	public void DefaultValues_ResolveSuccessfully()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddPoisonMessageHandling();

		// Act
		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<PoisonMessageOptions>>();
		var value = options.Value;

		// Assert - defaults should pass validation
		value.MaxRetryAttempts.ShouldBe(3);
		value.AlertThreshold.ShouldBe(10);
		value.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void CustomConfiguration_IsPreservedAfterValidation()
	{
		// Arrange
		var services = new ServiceCollection();

		_ = services.AddPoisonMessageHandling(opts =>
		{
			opts.MaxRetryAttempts = 10;
			opts.MaxProcessingTime = TimeSpan.FromMinutes(10);
			opts.EnableMetrics = false;
			opts.AlertThreshold = 50;
		});

		// Act
		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<PoisonMessageOptions>>().Value;

		// Assert
		options.MaxRetryAttempts.ShouldBe(10);
		options.MaxProcessingTime.ShouldBe(TimeSpan.FromMinutes(10));
		options.EnableMetrics.ShouldBeFalse();
		options.AlertThreshold.ShouldBe(50);
	}
}
