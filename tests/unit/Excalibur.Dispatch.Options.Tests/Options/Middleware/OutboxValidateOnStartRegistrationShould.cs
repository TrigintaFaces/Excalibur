// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Middleware;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Tests.Options.Middleware;

/// <summary>
/// Verifies that OutboxMiddlewareOptionsValidator registration works correctly with
/// <see cref="IValidateOptions{TOptions}"/> and that ValidateOnStart is wired up.
/// Sprint 563 S563.57: ValidateOnStart registration tests.
/// </summary>
/// <remarks>
/// Tests the validator registration pattern used by <c>RegisterDefaultMiddleware</c>
/// in <c>DispatchConfigurationServiceCollectionExtensions</c>. We register directly
/// rather than through <c>AddDefaultDispatchPipelines</c> to avoid pipeline profile
/// side effects in unit tests.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
public sealed class OutboxValidateOnStartRegistrationShould
{
	/// <summary>
	/// Registers the OutboxMiddlewareOptionsValidator the same way as production code.
	/// </summary>
	private static void RegisterOutboxValidation(IServiceCollection services)
	{
		services.AddOptions<OutboxMiddlewareOptions>()
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<OutboxMiddlewareOptions>, OutboxMiddlewareOptionsValidator>());
	}

	[Fact]
	public void RegisterOutboxMiddlewareOptionsValidator()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		RegisterOutboxValidation(services);

		// Assert
		using var provider = services.BuildServiceProvider();
		var validators = provider.GetServices<IValidateOptions<OutboxMiddlewareOptions>>();
		validators.ShouldNotBeEmpty("Registration should register IValidateOptions<OutboxMiddlewareOptions>");
		validators.ShouldContain(v => v is OutboxMiddlewareOptionsValidator);
	}

	[Fact]
	public void ValidOptions_ResolveSuccessfully()
	{
		// Arrange
		var services = new ServiceCollection();
		RegisterOutboxValidation(services);
		services.Configure<OutboxMiddlewareOptions>(options =>
		{
			options.PublishBatchSize = 50;
			options.PublishPollingInterval = TimeSpan.FromSeconds(5);
			options.Retry.MaxRetries = 3;
			options.Retry.RetryDelay = TimeSpan.FromSeconds(30);
		});

		// Act
		using var provider = services.BuildServiceProvider();
		var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<OutboxMiddlewareOptions>>();
		var value = optionsMonitor.CurrentValue;

		// Assert
		value.PublishBatchSize.ShouldBe(50);
		value.Retry.MaxRetries.ShouldBe(3);
	}

	[Fact]
	public void InvalidOptions_ThrowsOnResolve()
	{
		// Arrange
		var services = new ServiceCollection();
		RegisterOutboxValidation(services);
		services.Configure<OutboxMiddlewareOptions>(options =>
		{
			options.PublishBatchSize = 0; // Invalid: must be > 0
		});

		// Act
		using var provider = services.BuildServiceProvider();
		var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<OutboxMiddlewareOptions>>();

		// Assert - accessing the option triggers validation and throws
		_ = Should.Throw<OptionsValidationException>(() => optionsMonitor.CurrentValue);
	}

	[Fact]
	public void DuplicateRegistrations_DoNotDuplicateValidator()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act - register twice
		RegisterOutboxValidation(services);
		RegisterOutboxValidation(services);

		// Assert - TryAddEnumerable prevents duplicate validators
		using var provider = services.BuildServiceProvider();
		var validators = provider.GetServices<IValidateOptions<OutboxMiddlewareOptions>>()
			.Where(v => v is OutboxMiddlewareOptionsValidator)
			.ToList();
		validators.Count.ShouldBe(1, "TryAddEnumerable should prevent duplicate OutboxMiddlewareOptionsValidator registrations");
	}
}
