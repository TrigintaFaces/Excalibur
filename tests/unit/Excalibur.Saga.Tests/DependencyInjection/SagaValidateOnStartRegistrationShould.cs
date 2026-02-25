// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Saga.Tests.DependencyInjection;

/// <summary>
/// Verifies that <c>AddExcaliburSaga()</c> properly registers
/// <see cref="SagaOptionsValidator"/> as an <see cref="IValidateOptions{TOptions}"/>
/// and that ValidateOnStart is wired up.
/// Sprint 561 S561.51: ValidateOnStart registration tests.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SagaValidateOnStartRegistrationShould
{
	[Fact]
	public void RegisterSagaOptionsValidator()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburSaga();

		// Assert
		using var provider = services.BuildServiceProvider();
		var validators = provider.GetServices<IValidateOptions<SagaOptions>>();
		validators.ShouldNotBeEmpty("AddExcaliburSaga should register IValidateOptions<SagaOptions>");
		validators.ShouldContain(v => v is SagaOptionsValidator);
	}

	[Fact]
	public void ValidOptions_ResolveSuccessfully()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddExcaliburSaga(opts =>
		{
			opts.MaxRetryAttempts = 5;
			opts.MaxConcurrency = 10;
			opts.DefaultTimeout = TimeSpan.FromMinutes(5);
		});

		// Act
		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<SagaOptions>>();

		// Assert - accessing .Value triggers validation
		var value = options.Value;
		value.MaxRetryAttempts.ShouldBe(5);
		value.MaxConcurrency.ShouldBe(10);
	}

	[Fact]
	public void InvalidOptions_ThrowsOnResolve()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddExcaliburSaga(opts =>
		{
			opts.MaxConcurrency = 0; // Invalid: must be >= 1
		});

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<SagaOptions>>();

		// Act & Assert - accessing .Value should trigger validation failure
		_ = Should.Throw<OptionsValidationException>(() => _ = options.Value);
	}

	[Fact]
	public void CrossPropertyViolation_ThrowsOnResolve()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddExcaliburSaga(opts =>
		{
			opts.MaxRetryAttempts = 3;
			opts.RetryDelay = TimeSpan.FromMinutes(60);
			opts.DefaultTimeout = TimeSpan.FromMinutes(30);
		});

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<SagaOptions>>();

		// Act & Assert - cross-property constraint: RetryDelay > DefaultTimeout when retries enabled
		_ = Should.Throw<OptionsValidationException>(() => _ = options.Value);
	}

	[Fact]
	public void DuplicateRegistrations_DoNotDuplicateValidator()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act - register twice
		_ = services.AddExcaliburSaga();
		_ = services.AddExcaliburSaga();

		// Assert - TryAddEnumerable prevents duplicate validators
		using var provider = services.BuildServiceProvider();
		var validators = provider.GetServices<IValidateOptions<SagaOptions>>()
			.Where(v => v is SagaOptionsValidator)
			.ToList();
		validators.Count.ShouldBe(1, "TryAddEnumerable should prevent duplicate SagaOptionsValidator registrations");
	}
}
