// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Resilience;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Verifies that <c>AddPollyCircuitBreaker()</c> properly registers
/// <see cref="CircuitBreakerOptionsValidator"/> as an <see cref="IValidateOptions{TOptions}"/>
/// and that ValidateOnStart is wired up.
/// Sprint 561 S561.51: ValidateOnStart registration tests.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Resilience")]
public sealed class CircuitBreakerValidateOnStartRegistrationShould
{
	[Fact]
	public void RegisterCircuitBreakerOptionsValidator()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddPollyCircuitBreaker("test-breaker");

		// Assert
		using var provider = services.BuildServiceProvider();
		var validators = provider.GetServices<IValidateOptions<CircuitBreakerOptions>>();
		validators.ShouldNotBeEmpty("AddPollyCircuitBreaker should register IValidateOptions<CircuitBreakerOptions>");
		validators.ShouldContain(v => v is CircuitBreakerOptionsValidator);
	}

	[Fact]
	public void ValidOptions_ResolveSuccessfully()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		_ = services.AddPollyCircuitBreaker("valid-breaker", options =>
		{
			options.FailureThreshold = 10;
			options.SuccessThreshold = 2;
			options.OpenDuration = TimeSpan.FromSeconds(60);
			options.OperationTimeout = TimeSpan.FromSeconds(10);
			options.MaxHalfOpenTests = 5;
		});

		// Act
		using var provider = services.BuildServiceProvider();
		var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<CircuitBreakerOptions>>();
		var value = optionsMonitor.Get("valid-breaker");

		// Assert
		value.FailureThreshold.ShouldBe(10);
		value.SuccessThreshold.ShouldBe(2);
	}

	[Fact]
	public void InvalidOptions_ThrowsOnResolve()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		_ = services.AddPollyCircuitBreaker("invalid-breaker", options =>
		{
			options.FailureThreshold = 0; // Invalid: must be >= 1
		});

		// Act
		using var provider = services.BuildServiceProvider();
		var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<CircuitBreakerOptions>>();

		// Assert - accessing the named option triggers validation and throws
		_ = Should.Throw<OptionsValidationException>(() => optionsMonitor.Get("invalid-breaker"));
	}

	[Fact]
	public void DuplicateRegistrations_DoNotDuplicateValidator()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act - register circuit breakers twice
		_ = services.AddPollyCircuitBreaker("breaker-1");
		_ = services.AddPollyCircuitBreaker("breaker-2");

		// Assert - TryAddEnumerable prevents duplicate validators
		using var provider = services.BuildServiceProvider();
		var validators = provider.GetServices<IValidateOptions<CircuitBreakerOptions>>()
			.Where(v => v is CircuitBreakerOptionsValidator)
			.ToList();
		validators.Count.ShouldBe(1, "TryAddEnumerable should prevent duplicate CircuitBreakerOptionsValidator registrations");
	}
}
