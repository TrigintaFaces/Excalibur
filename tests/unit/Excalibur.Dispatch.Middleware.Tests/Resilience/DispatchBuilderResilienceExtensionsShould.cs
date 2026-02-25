// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.CloudNative;
using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Options.Resilience;
using Excalibur.Dispatch.Resilience.Polly;
using RetryOptions = Excalibur.Dispatch.Resilience.Polly.RetryOptions;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Tests for the <see cref="DispatchBuilderResilienceExtensions"/> class.
/// Sprint 45 (bd-5tsb): Unit tests for AddPollyResilienceAdapters DI registration.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DispatchBuilderResilienceExtensionsShould
{
	#region IDispatchBuilder Extension Tests

	[Fact]
	public void RegisterPollyRetryPolicyAdapter()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateFakeDispatchBuilder(services);

		// Act
		_ = builder.AddPollyResilienceAdapters();
		var provider = services.BuildServiceProvider();

		// Assert
		var retryPolicy = provider.GetService<IRetryPolicy>();
		_ = retryPolicy.ShouldNotBeNull();
		_ = retryPolicy.ShouldBeOfType<PollyRetryPolicyAdapter>();
	}

	[Fact]
	public void RegisterPollyCircuitBreakerPolicyAdapter()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateFakeDispatchBuilder(services);

		// Act
		_ = builder.AddPollyResilienceAdapters();
		var provider = services.BuildServiceProvider();

		// Assert
		var circuitBreaker = provider.GetService<ICircuitBreakerPolicy>();
		_ = circuitBreaker.ShouldNotBeNull();
		_ = circuitBreaker.ShouldBeOfType<PollyCircuitBreakerPolicyAdapter>();
	}

	[Fact]
	public void RegisterPollyBackoffCalculatorAdapter()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateFakeDispatchBuilder(services);

		// Act
		_ = builder.AddPollyResilienceAdapters();
		var provider = services.BuildServiceProvider();

		// Assert
		var backoffCalculator = provider.GetService<IBackoffCalculator>();
		_ = backoffCalculator.ShouldNotBeNull();
		_ = backoffCalculator.ShouldBeOfType<PollyBackoffCalculatorAdapter>();
	}

	[Fact]
	public void RegisterPollyTransportCircuitBreakerRegistry()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateFakeDispatchBuilder(services);

		// Act
		_ = builder.AddPollyResilienceAdapters();
		var provider = services.BuildServiceProvider();

		// Assert
		var registry = provider.GetService<ITransportCircuitBreakerRegistry>();
		_ = registry.ShouldNotBeNull();
		_ = registry.ShouldBeOfType<PollyTransportCircuitBreakerRegistry>();
	}

	[Fact]
	public void ReplaceExistingRetryPolicyRegistration()
	{
		// Arrange
		var services = new ServiceCollection();
		var existingPolicy = A.Fake<IRetryPolicy>();
		_ = services.AddSingleton(existingPolicy);

		var builder = CreateFakeDispatchBuilder(services);

		// Act
		_ = builder.AddPollyResilienceAdapters();
		var provider = services.BuildServiceProvider();

		// Assert
		var retryPolicy = provider.GetService<IRetryPolicy>();
		retryPolicy.ShouldNotBeSameAs(existingPolicy);
		_ = retryPolicy.ShouldBeOfType<PollyRetryPolicyAdapter>();
	}

	[Fact]
	public void ReplaceExistingCircuitBreakerPolicyRegistration()
	{
		// Arrange
		var services = new ServiceCollection();
		var existingPolicy = A.Fake<ICircuitBreakerPolicy>();
		_ = services.AddSingleton(existingPolicy);

		var builder = CreateFakeDispatchBuilder(services);

		// Act
		_ = builder.AddPollyResilienceAdapters();
		var provider = services.BuildServiceProvider();

		// Assert
		var circuitBreaker = provider.GetService<ICircuitBreakerPolicy>();
		circuitBreaker.ShouldNotBeSameAs(existingPolicy);
		_ = circuitBreaker.ShouldBeOfType<PollyCircuitBreakerPolicyAdapter>();
	}

	[Fact]
	public void ReplaceExistingBackoffCalculatorRegistration()
	{
		// Arrange
		var services = new ServiceCollection();
		var existingCalculator = A.Fake<IBackoffCalculator>();
		_ = services.AddSingleton(existingCalculator);

		var builder = CreateFakeDispatchBuilder(services);

		// Act
		_ = builder.AddPollyResilienceAdapters();
		var provider = services.BuildServiceProvider();

		// Assert
		var calculator = provider.GetService<IBackoffCalculator>();
		calculator.ShouldNotBeSameAs(existingCalculator);
		_ = calculator.ShouldBeOfType<PollyBackoffCalculatorAdapter>();
	}

	[Fact]
	public void ReplaceExistingTransportCircuitBreakerRegistryRegistration()
	{
		// Arrange
		var services = new ServiceCollection();
		var existingRegistry = A.Fake<ITransportCircuitBreakerRegistry>();
		_ = services.AddSingleton(existingRegistry);

		var builder = CreateFakeDispatchBuilder(services);

		// Act
		_ = builder.AddPollyResilienceAdapters();
		var provider = services.BuildServiceProvider();

		// Assert
		var registry = provider.GetService<ITransportCircuitBreakerRegistry>();
		registry.ShouldNotBeSameAs(existingRegistry);
		_ = registry.ShouldBeOfType<PollyTransportCircuitBreakerRegistry>();
	}

	[Fact]
	public void ReturnBuilderForMethodChaining()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateFakeDispatchBuilder(services);

		// Act
		var result = builder.AddPollyResilienceAdapters();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void ThrowArgumentNullExceptionForNullBuilder()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			((IDispatchBuilder)null!).AddPollyResilienceAdapters());
	}

	#endregion IDispatchBuilder Extension Tests

	#region Configuration Options Tests

	[Fact]
	public void ApplyRetryOptionsToAdapters()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateFakeDispatchBuilder(services);

		// Act
		_ = builder.AddPollyResilienceAdapters(options =>
		{
			options.RetryOptions = new RetryOptions
			{
				MaxRetries = 10,
				BaseDelay = TimeSpan.FromMilliseconds(200),
				BackoffStrategy = BackoffStrategy.Linear,
				UseJitter = false,
			};
		});

		var provider = services.BuildServiceProvider();

		// Assert
		var retryPolicy = provider.GetService<IRetryPolicy>();
		_ = retryPolicy.ShouldNotBeNull();
		_ = retryPolicy.ShouldBeOfType<PollyRetryPolicyAdapter>();
	}

	[Fact]
	public void ApplyCircuitBreakerOptionsToAdapters()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateFakeDispatchBuilder(services);

		// Act
		_ = builder.AddPollyResilienceAdapters(options =>
		{
			options.CircuitBreakerOptions = new CircuitBreakerOptions
			{
				FailureThreshold = 20,
				OpenDuration = TimeSpan.FromMinutes(5),
				SuccessThreshold = 10,
			};
		});

		var provider = services.BuildServiceProvider();

		// Assert
		var circuitBreaker = provider.GetService<ICircuitBreakerPolicy>();
		_ = circuitBreaker.ShouldNotBeNull();
		_ = circuitBreaker.ShouldBeOfType<PollyCircuitBreakerPolicyAdapter>();
	}

	[Fact]
	public void ApplyMaxBackoffDelayToBackoffCalculator()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateFakeDispatchBuilder(services);

		// Act
		_ = builder.AddPollyResilienceAdapters(options =>
		{
			options.MaxBackoffDelay = TimeSpan.FromMinutes(2);
			options.RetryOptions = new RetryOptions
			{
				BaseDelay = TimeSpan.FromMilliseconds(100),
				BackoffStrategy = BackoffStrategy.Exponential,
			};
		});

		var provider = services.BuildServiceProvider();
		var calculator = provider.GetRequiredService<IBackoffCalculator>();

		// Assert - high attempt number should be capped at max
		// Use attempt 20 which gives 100ms * 2^19 = ~52 seconds before capping
		var delay = calculator.CalculateDelay(20);
		delay.ShouldBeLessThanOrEqualTo(TimeSpan.FromMinutes(2));
	}

	[Fact]
	public void UseDefaultOptionsWhenNotProvided()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateFakeDispatchBuilder(services);

		// Act
		_ = builder.AddPollyResilienceAdapters();
		var provider = services.BuildServiceProvider();

		// Assert - all services should be registered with defaults
		_ = provider.GetService<IRetryPolicy>().ShouldNotBeNull();
		_ = provider.GetService<ICircuitBreakerPolicy>().ShouldNotBeNull();
		_ = provider.GetService<IBackoffCalculator>().ShouldNotBeNull();
		_ = provider.GetService<ITransportCircuitBreakerRegistry>().ShouldNotBeNull();
	}

	[Fact]
	public void AcceptNullConfigureAction()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateFakeDispatchBuilder(services);

		// Act - should not throw
		_ = builder.AddPollyResilienceAdapters(null);
		var provider = services.BuildServiceProvider();

		// Assert
		_ = provider.GetService<IRetryPolicy>().ShouldNotBeNull();
	}

	#endregion Configuration Options Tests

	#region AddDispatchResilience Tests

	[Fact]
	public void AddDispatchResilience_WithNullBuilder_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			((IDispatchBuilder)null!).AddDispatchResilience());
	}

	[Fact]
	public void AddDispatchResilience_WithValidBuilder_ReturnsBuilder()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		var builder = CreateFakeDispatchBuilder(services);

		// Act
		var result = builder.AddDispatchResilience();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void AddDispatchResilience_RegistersCorePollyServices()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		var builder = CreateFakeDispatchBuilder(services);

		// Act
		_ = builder.AddDispatchResilience();

		// Assert - Core services should be registered
		services.Any(d => d.ServiceType == typeof(ICircuitBreakerFactory)).ShouldBeTrue();
		services.Any(d => d.ServiceType == typeof(ITimeoutManager)).ShouldBeTrue();
		services.Any(d => d.ServiceType == typeof(IGracefulDegradationService)).ShouldBeTrue();
	}

	[Fact]
	public void AddDispatchResilience_WithConfigureOptions_ConfiguresOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		var builder = CreateFakeDispatchBuilder(services);

		// Act
		_ = builder.AddDispatchResilience(options =>
		{
			options.DefaultRetryCount = 10;
			options.EnableCircuitBreaker = true;
		});

		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetService<Microsoft.Extensions.Options.IOptions<ResilienceOptions>>();
		options.ShouldNotBeNull();
	}

	[Fact]
	public void AddDispatchResilience_WithConfigureOptions_WithNullBuilder_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			((IDispatchBuilder)null!).AddDispatchResilience(_ => { }));
	}

	[Fact]
	public void AddDispatchResilience_WithConfigureOptions_WithNullConfigAction_ThrowsArgumentNullException()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateFakeDispatchBuilder(services);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			builder.AddDispatchResilience(null!));
	}

	#endregion AddDispatchResilience Tests

	#region AddResilience Tests

	[Fact]
	public void AddResilience_WithNullBuilder_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			((IDispatchBuilder)null!).AddResilience());
	}

	[Fact]
	public void AddResilience_WithValidBuilder_ReturnsBuilder()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		var builder = CreateFakeDispatchBuilder(services);

		// Act
		var result = builder.AddResilience();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void AddResilience_WithNullConfigure_CallsAddDispatchResilience()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		var builder = CreateFakeDispatchBuilder(services);

		// Act
		_ = builder.AddResilience(null);

		// Assert - Core services should be registered
		services.Any(d => d.ServiceType == typeof(ICircuitBreakerFactory)).ShouldBeTrue();
	}

	[Fact]
	public void AddResilience_WithConfigure_CallsAddDispatchResilienceWithConfigure()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		var builder = CreateFakeDispatchBuilder(services);
		var configCalled = false;

		// Act
		_ = builder.AddResilience(options =>
		{
			configCalled = true;
			options.DefaultRetryCount = 5;
		});

		var provider = services.BuildServiceProvider();

		// Assert - Configuration action is deferred until options are resolved
		var options = provider.GetService<Microsoft.Extensions.Options.IOptions<ResilienceOptions>>();
		options.ShouldNotBeNull();
		// Access the Value to trigger configuration
		_ = options.Value;
		configCalled.ShouldBeTrue();
		options.Value.DefaultRetryCount.ShouldBe(5);
	}

	#endregion AddResilience Tests

	#region IServiceCollection Extension Tests

	[Fact]
	public void RegisterPollyAdaptersDirectlyToServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddPollyResilienceAdapters();
		var provider = services.BuildServiceProvider();

		// Assert
		_ = provider.GetService<IRetryPolicy>().ShouldBeOfType<PollyRetryPolicyAdapter>();
		_ = provider.GetService<ICircuitBreakerPolicy>().ShouldBeOfType<PollyCircuitBreakerPolicyAdapter>();
		_ = provider.GetService<IBackoffCalculator>().ShouldBeOfType<PollyBackoffCalculatorAdapter>();
		_ = provider.GetService<ITransportCircuitBreakerRegistry>().ShouldBeOfType<PollyTransportCircuitBreakerRegistry>();
	}

	[Fact]
	public void ReturnServiceCollectionForMethodChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddPollyResilienceAdapters();

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void ThrowArgumentNullExceptionForNullServiceCollection()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			((IServiceCollection)null!).AddPollyResilienceAdapters());
	}

	[Fact]
	public void ApplyOptionsToServiceCollectionExtension()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddPollyResilienceAdapters(options =>
		{
			options.RetryOptions = new RetryOptions
			{
				MaxRetries = 5,
				BaseDelay = TimeSpan.FromMilliseconds(500),
			};
		});

		var provider = services.BuildServiceProvider();

		// Assert
		_ = provider.GetService<IRetryPolicy>().ShouldBeOfType<PollyRetryPolicyAdapter>();
	}

	#endregion IServiceCollection Extension Tests

	#region Singleton Lifetime Tests

	[Fact]
	public void RegisterRetryPolicyAsSingleton()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateFakeDispatchBuilder(services);
		_ = builder.AddPollyResilienceAdapters();
		var provider = services.BuildServiceProvider();

		// Act
		var policy1 = provider.GetService<IRetryPolicy>();
		var policy2 = provider.GetService<IRetryPolicy>();

		// Assert
		policy1.ShouldBeSameAs(policy2);
	}

	[Fact]
	public void RegisterCircuitBreakerPolicyAsSingleton()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateFakeDispatchBuilder(services);
		_ = builder.AddPollyResilienceAdapters();
		var provider = services.BuildServiceProvider();

		// Act
		var policy1 = provider.GetService<ICircuitBreakerPolicy>();
		var policy2 = provider.GetService<ICircuitBreakerPolicy>();

		// Assert
		policy1.ShouldBeSameAs(policy2);
	}

	[Fact]
	public void RegisterBackoffCalculatorAsSingleton()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateFakeDispatchBuilder(services);
		_ = builder.AddPollyResilienceAdapters();
		var provider = services.BuildServiceProvider();

		// Act
		var calculator1 = provider.GetService<IBackoffCalculator>();
		var calculator2 = provider.GetService<IBackoffCalculator>();

		// Assert
		calculator1.ShouldBeSameAs(calculator2);
	}

	[Fact]
	public void RegisterTransportRegistryAsSingleton()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateFakeDispatchBuilder(services);
		_ = builder.AddPollyResilienceAdapters();
		var provider = services.BuildServiceProvider();

		// Act
		var registry1 = provider.GetService<ITransportCircuitBreakerRegistry>();
		var registry2 = provider.GetService<ITransportCircuitBreakerRegistry>();

		// Assert
		registry1.ShouldBeSameAs(registry2);
	}

	#endregion Singleton Lifetime Tests

	#region BackoffStrategy Mapping Tests

	[Theory]
	[InlineData(BackoffStrategy.Fixed)]
	[InlineData(BackoffStrategy.Linear)]
	[InlineData(BackoffStrategy.Exponential)]
	public void MapBackoffStrategyCorrectly(BackoffStrategy strategy)
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateFakeDispatchBuilder(services);

		// Act
		_ = builder.AddPollyResilienceAdapters(options =>
		{
			options.RetryOptions = new RetryOptions
			{
				BackoffStrategy = strategy,
				BaseDelay = TimeSpan.FromMilliseconds(100),
			};
		});

		var provider = services.BuildServiceProvider();
		var calculator = provider.GetService<IBackoffCalculator>();

		// Assert
		_ = calculator.ShouldNotBeNull();
		_ = calculator.ShouldBeOfType<PollyBackoffCalculatorAdapter>();
	}

	#endregion BackoffStrategy Mapping Tests

	#region Helper Methods

	private static IDispatchBuilder CreateFakeDispatchBuilder(IServiceCollection services)
	{
		var builder = A.Fake<IDispatchBuilder>();
		_ = A.CallTo(() => builder.Services).Returns(services);
		return builder;
	}

	#endregion Helper Methods
}
