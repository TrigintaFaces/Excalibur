// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Caching;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Caching.Tests;

/// <summary>
/// Verifies that <c>AddDispatchCaching()</c> properly registers
/// <see cref="CacheOptionsValidator"/> as an <see cref="IValidateOptions{TOptions}"/>
/// and that ValidateOnStart is wired up.
/// Sprint 563 S563.56: ValidateOnStart registration tests.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Caching")]
public sealed class CacheValidateOnStartRegistrationShould
{
	[Fact]
	public void RegisterCacheOptionsValidator()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDispatchCaching();

		// Assert
		using var provider = services.BuildServiceProvider();
		var validators = provider.GetServices<IValidateOptions<CacheOptions>>();
		validators.ShouldNotBeEmpty("AddDispatchCaching should register IValidateOptions<CacheOptions>");
		validators.ShouldContain(v => v is CacheOptionsValidator);
	}

	[Fact]
	public void ValidOptions_ResolveSuccessfully()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddDispatchCaching(options =>
		{
			options.Behavior.DefaultExpiration = TimeSpan.FromMinutes(30);
			options.Behavior.CacheTimeout = TimeSpan.FromMilliseconds(500);
			options.Behavior.JitterRatio = 0.1;
		});

		// Act
		using var provider = services.BuildServiceProvider();
		var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<CacheOptions>>();
		var value = optionsMonitor.CurrentValue;

		// Assert
		value.Behavior.DefaultExpiration.ShouldBe(TimeSpan.FromMinutes(30));
		value.Behavior.JitterRatio.ShouldBe(0.1);
	}

	[Fact]
	public void InvalidOptions_ThrowsOnResolve()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddDispatchCaching(options =>
		{
			options.Behavior.DefaultExpiration = TimeSpan.Zero; // Invalid: must be > 0
		});

		// Act
		using var provider = services.BuildServiceProvider();
		var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<CacheOptions>>();

		// Assert - accessing the option triggers validation and throws
		_ = Should.Throw<OptionsValidationException>(() => optionsMonitor.CurrentValue);
	}

	[Fact]
	public void DuplicateRegistrations_DoNotDuplicateValidator()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act - register twice
		services.AddDispatchCaching();
		services.AddDispatchCaching();

		// Assert - TryAddEnumerable prevents duplicate validators
		using var provider = services.BuildServiceProvider();
		var validators = provider.GetServices<IValidateOptions<CacheOptions>>()
			.Where(v => v is CacheOptionsValidator)
			.ToList();
		validators.Count.ShouldBe(1, "TryAddEnumerable should prevent duplicate CacheOptionsValidator registrations");
	}
}
