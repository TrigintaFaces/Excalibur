// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.CloudNative;
using Excalibur.Dispatch.Options.Resilience;
using Excalibur.Dispatch.Resilience.Polly;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using PollyRetryOptions = Excalibur.Dispatch.Resilience.Polly.RetryOptions;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Unit tests for <see cref="PollyResilienceServiceCollectionExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Resilience")]
public sealed class PollyResilienceServiceCollectionExtensionsShould : UnitTestBase
{
	#region AddPollyResilience Tests

	[Fact]
	public void AddPollyResilience_WithoutConfiguration_RegistersCoreServices()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddPollyResilience();

		// Assert
		result.ShouldBeSameAs(services);

		// Verify service descriptors are registered (not resolving, which requires logging)
		services.Any(d => d.ServiceType == typeof(ICircuitBreakerFactory)).ShouldBeTrue();
		services.Any(d => d.ServiceType == typeof(ITimeoutManager)).ShouldBeTrue();
		services.Any(d => d.ServiceType == typeof(IGracefulDegradationService)).ShouldBeTrue();
		services.Any(d => d.ServiceType == typeof(BulkheadManager)).ShouldBeTrue();
		services.Any(d => d.ServiceType == typeof(DistributedCircuitBreakerFactory)).ShouldBeTrue();
		services.Any(d => d.ServiceType == typeof(PollyRetryPolicyAdapter)).ShouldBeTrue();
	}

	[Fact]
	public async Task AddPollyResilience_WithConfiguration_BindsSettings()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Resilience:Timeouts:DefaultTimeout"] = "00:00:45",
				["Resilience:GracefulDegradation:EnableAutoAdjustment"] = "true",
				["Resilience:DistributedCircuitBreaker:ConsecutiveFailureThreshold"] = "10"
			})
			.Build();

		// Act
		var result = services.AddPollyResilience(configuration);

		// Assert
		result.ShouldBeSameAs(services);

		await using var provider = services.BuildServiceProvider();

		var timeoutOptions = provider.GetService<IOptions<TimeoutManagerOptions>>();
		timeoutOptions.ShouldNotBeNull();
		timeoutOptions.Value.DefaultTimeout.ShouldBe(TimeSpan.FromSeconds(45));

		var degradationOptions = provider.GetService<IOptions<GracefulDegradationOptions>>();
		degradationOptions.ShouldNotBeNull();
		degradationOptions.Value.EnableAutoAdjustment.ShouldBeTrue();

		var distributedBreakerOptions = provider.GetService<IOptions<DistributedCircuitBreakerOptions>>();
		distributedBreakerOptions.ShouldNotBeNull();
		distributedBreakerOptions.Value.ConsecutiveFailureThreshold.ShouldBe(10);
	}

	[Fact]
	public void AddPollyResilience_CalledMultipleTimes_DoesNotDuplicateServices()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddPollyResilience();
		services.AddPollyResilience();
		services.AddPollyResilience();

		// Assert - TryAddSingleton should prevent duplicates
		var factoryDescriptors = services.Where(d => d.ServiceType == typeof(ICircuitBreakerFactory)).ToList();
		factoryDescriptors.Count.ShouldBe(1);
	}

	#endregion

	#region AddPollyCircuitBreaker Tests

	[Fact]
	public void AddPollyCircuitBreaker_WithNullServices_ThrowsArgumentNullException()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddPollyCircuitBreaker("test"));
	}

	[Fact]
	public void AddPollyCircuitBreaker_WithNullName_ThrowsArgumentNullException()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddPollyCircuitBreaker(null!));
	}

	[Fact]
	public async Task AddPollyCircuitBreaker_WithValidParameters_RegistersNamedOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		const string name = "MyCircuitBreaker";

		// Act
		var result = services.AddPollyCircuitBreaker(name, options =>
		{
			options.FailureThreshold = 10;
			options.OpenDuration = TimeSpan.FromMinutes(2);
		});

		// Assert
		result.ShouldBeSameAs(services);

		await using var provider = services.BuildServiceProvider();
		var optionsMonitor = provider.GetService<IOptionsMonitor<CircuitBreakerOptions>>();
		optionsMonitor.ShouldNotBeNull();

		var namedOptions = optionsMonitor.Get(name);
		namedOptions.FailureThreshold.ShouldBe(10);
		namedOptions.OpenDuration.ShouldBe(TimeSpan.FromMinutes(2));
	}

	[Fact]
	public void AddPollyCircuitBreaker_WithNullConfigureOptions_DoesNotThrow()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert - Should not throw even with null configure action
		var result = services.AddPollyCircuitBreaker("test", null);
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddPollyCircuitBreaker_AlsoCallsAddPollyResilience()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddPollyCircuitBreaker("test");

		// Assert - Core services should be registered (check descriptors, not resolution)
		services.Any(d => d.ServiceType == typeof(ITimeoutManager)).ShouldBeTrue();
	}

	#endregion

	#region AddPollyRetryPolicy Tests

	[Fact]
	public void AddPollyRetryPolicy_WithNullServices_ThrowsArgumentNullException()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddPollyRetryPolicy("test"));
	}

	[Fact]
	public void AddPollyRetryPolicy_WithNullName_ThrowsArgumentNullException()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddPollyRetryPolicy(null!));
	}

	[Fact]
	public async Task AddPollyRetryPolicy_WithValidParameters_RegistersNamedOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		const string name = "MyRetryPolicy";

		// Act
		var result = services.AddPollyRetryPolicy(name, options =>
		{
			options.MaxRetries = 5;
			options.BaseDelay = TimeSpan.FromSeconds(2);
		});

		// Assert
		result.ShouldBeSameAs(services);

		await using var provider = services.BuildServiceProvider();
		var optionsMonitor = provider.GetService<IOptionsMonitor<PollyRetryOptions>>();
		optionsMonitor.ShouldNotBeNull();

		var namedOptions = optionsMonitor.Get(name);
		namedOptions.MaxRetries.ShouldBe(5);
		namedOptions.BaseDelay.ShouldBe(TimeSpan.FromSeconds(2));
	}

	[Fact]
	public void AddPollyRetryPolicy_WithNullConfigureOptions_DoesNotThrow()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		var result = services.AddPollyRetryPolicy("test", null);
		result.ShouldBeSameAs(services);
	}

	#endregion

	#region AddRetryPolicyWithJitter Tests

	[Fact]
	public void AddRetryPolicyWithJitter_WithNullServices_ThrowsArgumentNullException()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddRetryPolicyWithJitter("test"));
	}

	[Fact]
	public void AddRetryPolicyWithJitter_WithNullName_ThrowsArgumentNullException()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddRetryPolicyWithJitter(null!));
	}

	[Fact]
	public async Task AddRetryPolicyWithJitter_WithValidParameters_SetsJitterDefaults()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		const string name = "JitterRetryPolicy";

		// Act
		var result = services.AddRetryPolicyWithJitter(name);

		// Assert
		result.ShouldBeSameAs(services);

		await using var provider = services.BuildServiceProvider();
		var optionsMonitor = provider.GetService<IOptionsMonitor<PollyRetryOptions>>();
		optionsMonitor.ShouldNotBeNull();

		var namedOptions = optionsMonitor.Get(name);
		namedOptions.UseJitter.ShouldBeTrue();
		namedOptions.JitterStrategy.ShouldBe(JitterStrategy.Equal);
	}

	[Fact]
	public async Task AddRetryPolicyWithJitter_WithCustomConfiguration_AppliesAfterDefaults()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		const string name = "CustomJitterPolicy";

		// Act
		var result = services.AddRetryPolicyWithJitter(name, options =>
		{
			options.JitterStrategy = JitterStrategy.Full;
			options.MaxRetries = 7;
		});

		// Assert
		await using var provider = services.BuildServiceProvider();
		var optionsMonitor = provider.GetService<IOptionsMonitor<PollyRetryOptions>>();
		var namedOptions = optionsMonitor.Get(name);

		// Custom config should override defaults
		namedOptions.JitterStrategy.ShouldBe(JitterStrategy.Full);
		namedOptions.MaxRetries.ShouldBe(7);
		// UseJitter is set before custom config, so it stays true unless explicitly changed
		namedOptions.UseJitter.ShouldBeTrue();
	}

	[Fact]
	public void AddRetryPolicyWithJitter_RegistersRetryPolicy()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddRetryPolicyWithJitter("test");

		// Assert - Verify descriptor is registered
		services.Any(d => d.ServiceType == typeof(RetryPolicy)).ShouldBeTrue();
	}

	#endregion

	#region AddBulkhead Tests

	[Fact]
	public void AddBulkhead_WithNullServices_ThrowsArgumentNullException()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddBulkhead("test"));
	}

	[Fact]
	public void AddBulkhead_WithNullResourceName_ThrowsArgumentNullException()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddBulkhead(null!));
	}

	[Fact]
	public async Task AddBulkhead_WithValidParameters_RegistersNamedOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		const string resourceName = "DatabaseConnections";

		// Act
		var result = services.AddBulkhead(resourceName, options =>
		{
			options.MaxConcurrency = 20;
			options.MaxQueueLength = 100;
		});

		// Assert
		result.ShouldBeSameAs(services);

		await using var provider = services.BuildServiceProvider();
		var optionsMonitor = provider.GetService<IOptionsMonitor<BulkheadOptions>>();
		optionsMonitor.ShouldNotBeNull();

		var namedOptions = optionsMonitor.Get(resourceName);
		namedOptions.MaxConcurrency.ShouldBe(20);
		namedOptions.MaxQueueLength.ShouldBe(100);
	}

	[Fact]
	public void AddBulkhead_WithNullConfigureOptions_DoesNotThrow()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		var result = services.AddBulkhead("test", null);
		result.ShouldBeSameAs(services);
	}

	#endregion

	#region AddDistributedCircuitBreaker Tests

	[Fact]
	public void AddDistributedCircuitBreaker_WithNullServices_ThrowsArgumentNullException()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddDistributedCircuitBreaker("test"));
	}

	[Fact]
	public void AddDistributedCircuitBreaker_WithNullName_ThrowsArgumentNullException()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddDistributedCircuitBreaker(null!));
	}

	[Fact]
	public async Task AddDistributedCircuitBreaker_WithValidParameters_RegistersNamedOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		const string name = "DistributedBreaker";

		// Act
		var result = services.AddDistributedCircuitBreaker(name, options =>
		{
			options.ConsecutiveFailureThreshold = 15;
			options.SamplingDuration = TimeSpan.FromMinutes(5);
		});

		// Assert
		result.ShouldBeSameAs(services);

		await using var provider = services.BuildServiceProvider();
		var optionsMonitor = provider.GetService<IOptionsMonitor<DistributedCircuitBreakerOptions>>();
		optionsMonitor.ShouldNotBeNull();

		var namedOptions = optionsMonitor.Get(name);
		namedOptions.ConsecutiveFailureThreshold.ShouldBe(15);
		namedOptions.SamplingDuration.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public async Task AddDistributedCircuitBreaker_RegistersDistributedCache()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act
		services.AddDistributedCircuitBreaker("test");

		// Assert
		await using var provider = services.BuildServiceProvider();
		var cache = provider.GetService<IDistributedCache>();
		cache.ShouldNotBeNull();
	}

	[Fact]
	public void AddDistributedCircuitBreaker_WithNullConfigureOptions_DoesNotThrow()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		var result = services.AddDistributedCircuitBreaker("test", null);
		result.ShouldBeSameAs(services);
	}

	#endregion

	#region ConfigureTimeoutManager Tests

	[Fact]
	public void ConfigureTimeoutManager_WithNullServices_ThrowsArgumentNullException()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.ConfigureTimeoutManager(_ => { }));
	}

	[Fact]
	public void ConfigureTimeoutManager_WithNullConfigureOptions_ThrowsArgumentNullException()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.ConfigureTimeoutManager(null!));
	}

	[Fact]
	public async Task ConfigureTimeoutManager_WithValidParameters_ConfiguresOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act
		var result = services.ConfigureTimeoutManager(options =>
		{
			options.DefaultTimeout = TimeSpan.FromSeconds(60);
			options.DatabaseTimeout = TimeSpan.FromSeconds(30);
			options.HttpTimeout = TimeSpan.FromSeconds(120);
		});

		// Assert
		result.ShouldBeSameAs(services);

		await using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<TimeoutManagerOptions>>();
		options.Value.DefaultTimeout.ShouldBe(TimeSpan.FromSeconds(60));
		options.Value.DatabaseTimeout.ShouldBe(TimeSpan.FromSeconds(30));
		options.Value.HttpTimeout.ShouldBe(TimeSpan.FromSeconds(120));
	}

	[Fact]
	public void ConfigureTimeoutManager_AlsoCallsAddPollyResilience()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.ConfigureTimeoutManager(_ => { });

		// Assert - Core services should be registered (check descriptors)
		services.Any(d => d.ServiceType == typeof(ICircuitBreakerFactory)).ShouldBeTrue();
	}

	#endregion

	#region ConfigureGracefulDegradation Tests

	[Fact]
	public void ConfigureGracefulDegradation_WithNullServices_ThrowsArgumentNullException()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.ConfigureGracefulDegradation(_ => { }));
	}

	[Fact]
	public void ConfigureGracefulDegradation_WithNullConfigureOptions_ThrowsArgumentNullException()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.ConfigureGracefulDegradation(null!));
	}

	[Fact]
	public async Task ConfigureGracefulDegradation_WithValidParameters_ConfiguresOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act
		var result = services.ConfigureGracefulDegradation(options =>
		{
			options.EnableAutoAdjustment = true;
			options.Levels[0] = new DegradationLevelConfig("Minor", 30, 0.01, 60, 60);
			options.HealthCheckInterval = TimeSpan.FromMinutes(5);
		});

		// Assert
		result.ShouldBeSameAs(services);

		await using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<GracefulDegradationOptions>>();
		options.Value.EnableAutoAdjustment.ShouldBeTrue();
		options.Value.GetPriorityThreshold(DegradationLevel.Minor).ShouldBe(30);
		options.Value.HealthCheckInterval.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void ConfigureGracefulDegradation_AlsoCallsAddPollyResilience()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.ConfigureGracefulDegradation(_ => { });

		// Assert - Core services should be registered (check descriptors)
		services.Any(d => d.ServiceType == typeof(IGracefulDegradationService)).ShouldBeTrue();
	}

	#endregion

	#region Method Chaining Tests

	[Fact]
	public void AllExtensionMethods_SupportFluentChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act - Chain all extension methods
		var result = services
			.AddPollyResilience()
			.AddPollyCircuitBreaker("breaker1")
			.AddPollyRetryPolicy("retry1")
			.AddRetryPolicyWithJitter("jitterRetry")
			.AddBulkhead("bulkhead1")
			.AddDistributedCircuitBreaker("distBreaker")
			.ConfigureTimeoutManager(opts => opts.DefaultTimeout = TimeSpan.FromSeconds(30))
			.ConfigureGracefulDegradation(opts => opts.EnableAutoAdjustment = true);

		// Assert
		result.ShouldBeSameAs(services);
	}

	#endregion
}
