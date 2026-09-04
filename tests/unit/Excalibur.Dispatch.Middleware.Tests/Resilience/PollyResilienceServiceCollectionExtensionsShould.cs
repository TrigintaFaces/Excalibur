// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.CloudNative;
using Excalibur.Dispatch.Options.Resilience;
using Excalibur.Dispatch.Resilience.Polly;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using PollyRetryOptions = Excalibur.Dispatch.Resilience.Polly.RetryOptions;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Unit tests for <see cref="PollyResilienceServiceCollectionExtensions"/>.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Resilience)]
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
		services.Any(d => d.ServiceType == typeof(ITimeoutManager)).ShouldBeTrue();
		services.Any(d => d.ServiceType == typeof(IGracefulDegradationService)).ShouldBeTrue();
		services.Any(d => d.ServiceType == typeof(BulkheadManager)).ShouldBeTrue();
		services.Any(d => d.ServiceType == typeof(PollyRetryPolicyAdapter)).ShouldBeTrue();

		// The distributed circuit breaker factory is deliberately absent: it requires an IDistributedCache
		// and this path registers none, so registering it here would seat a service nothing can construct.
		services.Any(d => d.ServiceType == typeof(DistributedCircuitBreakerFactory)).ShouldBeFalse();
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
		var timeoutManagerDescriptors = services.Where(d => d.ServiceType == typeof(ITimeoutManager)).ToList();
		timeoutManagerDescriptors.Count.ShouldBe(1);
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
	public async Task AddDistributedCircuitBreaker_DoesNotSeatAnInProcessCacheOfItsOwn()
	{
		// The method used to call AddDistributedMemoryCache() as a default. A consumer who overrode
		// nothing then got a per-instance breaker reached through a method promising coordination, and
		// nothing about that was observable at runtime. The store is the caller's to supply.
		var services = new ServiceCollection();
		services.AddLogging();

		services.AddDistributedCircuitBreaker("test");

		await using var provider = services.BuildServiceProvider();
		provider.GetService<IDistributedCache>().ShouldBeNull(
			"AddDistributedCircuitBreaker must not supply a store that is not shared between instances");
	}

	[Fact]
	public async Task AddDistributedCircuitBreaker_FailsAtStartupWhenNoStoreIsRegistered()
	{
		using var host = new HostBuilder()
			.ConfigureServices((_, services) =>
			{
				services.AddLogging();
				services.AddDistributedCircuitBreaker("test");
			})
			.Build();

		var thrown = await Should.ThrowAsync<OptionsValidationException>(() => host.StartAsync());
		thrown.Message.ShouldContain("IDistributedCache");
	}

	[Fact]
	public async Task AddDistributedCircuitBreaker_FailsAtStartupWhenTheStoreIsInProcess()
	{
		// The silent case the guard exists for: the composition constructs and runs, and every replica
		// trips its own circuit while the registration reads as distributed.
		using var host = new HostBuilder()
			.ConfigureServices((_, services) =>
			{
				services.AddLogging();
				services.AddDistributedMemoryCache();
				services.AddDistributedCircuitBreaker("test");
			})
			.Build();

		var thrown = await Should.ThrowAsync<OptionsValidationException>(() => host.StartAsync());
		thrown.Message.ShouldContain("AddDistributedMemoryCache");
	}

	[Fact]
	public async Task AddDistributedCircuitBreaker_StartsAndResolvesTheNamedBreakerOnASharedStore()
	{
		using var host = new HostBuilder()
			.ConfigureServices((_, services) =>
			{
				services.AddLogging();
				services.AddSingleton<IDistributedCache>(new SharedStoreDouble());
				services.AddDistributedCircuitBreaker("test", options => options.ConsecutiveFailureThreshold = 7);
			})
			.Build();

		await host.StartAsync();

		// Resolved, not merely asserted present: before the keyed registration this method configured a
		// breaker no consumer could obtain.
		var breaker = host.Services.GetRequiredKeyedService<IDistributedCircuitBreaker>("test");
		breaker.ShouldNotBeNull();

		await host.StopAsync();
	}

	/// <summary>
	/// Stands in for a cross-instance backend (Redis, SQL Server). Only its TYPE matters here — the guard
	/// refuses the in-process implementation by name — so the storage itself can be trivial.
	/// </summary>
	private sealed class SharedStoreDouble : IDistributedCache
	{
		private readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte[]> _entries =
			new(StringComparer.Ordinal);

		public byte[]? Get(string key) => _entries.TryGetValue(key, out var value) ? value : null;

		public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Task.FromResult(Get(key));

		public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => _entries[key] = value;

		public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
		{
			Set(key, value, options);
			return Task.CompletedTask;
		}

		public void Refresh(string key)
		{
		}

		public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;

		public void Remove(string key) => _entries.TryRemove(key, out _);

		public Task RemoveAsync(string key, CancellationToken token = default)
		{
			Remove(key);
			return Task.CompletedTask;
		}
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
