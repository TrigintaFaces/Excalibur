// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Tests.Functional.MultiTenant;

/// <summary>
/// End-to-end functional tests for multi-tenant scenarios.
/// Tests demonstrate tenant isolation and per-tenant configuration patterns.
/// </summary>
[Trait("Category", "Functional")]
public sealed class MultiTenantFunctionalShould : FunctionalTestBase
{
	#region Tenant Isolation Tests

	[Fact]
	public async Task MultiTenant_IsolatesDataByTenant()
	{
		// Arrange
		using var host = CreateHost(services =>
		{
			_ = services.AddSingleton<MultiTenantRepository>();
			_ = services.AddSingleton<ITenantDataService, TenantDataService>();
		});
		await host.StartAsync(TestCancellationToken).ConfigureAwait(false);

		var dataService = host.Services.GetRequiredService<ITenantDataService>();

		// Act - Create data for tenant A
		await dataService.CreateDataAsync("tenant-a", "data-a", TestCancellationToken).ConfigureAwait(false);

		// Create data for tenant B
		await dataService.CreateDataAsync("tenant-b", "data-b", TestCancellationToken).ConfigureAwait(false);

		// Query data for each tenant
		var resultA = await dataService.GetDataAsync("tenant-a", TestCancellationToken).ConfigureAwait(false);
		var resultB = await dataService.GetDataAsync("tenant-b", TestCancellationToken).ConfigureAwait(false);

		await host.StopAsync(TestCancellationToken).ConfigureAwait(false);

		// Assert - Each tenant sees only their data
		resultA.ShouldContain("data-a");
		resultA.ShouldNotContain("data-b");

		resultB.ShouldContain("data-b");
		resultB.ShouldNotContain("data-a");
	}

	[Fact]
	public async Task MultiTenant_RejectsMissingTenantId()
	{
		// Arrange
		using var host = CreateHost(services =>
		{
			_ = services.AddSingleton<MultiTenantRepository>();
			_ = services.AddSingleton<ITenantDataService, TenantDataService>();
		});
		await host.StartAsync(TestCancellationToken).ConfigureAwait(false);

		var dataService = host.Services.GetRequiredService<ITenantDataService>();

		await host.StopAsync(TestCancellationToken).ConfigureAwait(false);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(
			() => dataService.CreateDataAsync(null!, "data", TestCancellationToken)).ConfigureAwait(false);

		_ = await Should.ThrowAsync<ArgumentException>(
			() => dataService.CreateDataAsync("", "data", TestCancellationToken)).ConfigureAwait(false);
	}

	[Fact]
	public async Task MultiTenant_SupportsManyTenants()
	{
		// Arrange
		using var host = CreateHost(services =>
		{
			_ = services.AddSingleton<MultiTenantRepository>();
			_ = services.AddSingleton<ITenantDataService, TenantDataService>();
		});
		await host.StartAsync(TestCancellationToken).ConfigureAwait(false);

		var dataService = host.Services.GetRequiredService<ITenantDataService>();

		// Act - Create data for 10 tenants
		for (var i = 0; i < 10; i++)
		{
			await dataService.CreateDataAsync($"tenant-{i}", $"data-{i}", TestCancellationToken).ConfigureAwait(false);
		}

		await host.StopAsync(TestCancellationToken).ConfigureAwait(false);

		// Assert - Each tenant has isolated data
		for (var i = 0; i < 10; i++)
		{
			var data = await dataService.GetDataAsync($"tenant-{i}", TestCancellationToken).ConfigureAwait(false);
			data.Count.ShouldBe(1);
			data.ShouldContain($"data-{i}");
		}
	}

	#endregion

	#region Tenant Configuration Tests

	[Fact]
	public async Task MultiTenant_AppliesTenantSpecificConfiguration()
	{
		// Arrange
		var tenantConfigs = new Dictionary<string, TenantConfig>
		{
			["tenant-premium"] = new TenantConfig { MaxItems = 1000, FeatureFlags = ["premium-feature"] },
			["tenant-basic"] = new TenantConfig { MaxItems = 100, FeatureFlags = [] }
		};

		using var host = CreateHost(services =>
		{
			_ = services.AddSingleton<ITenantConfigProvider>(new InMemoryTenantConfigProvider(tenantConfigs));
			_ = services.AddSingleton<ITenantConfigService, TenantConfigService>();
		});
		await host.StartAsync(TestCancellationToken).ConfigureAwait(false);

		var configService = host.Services.GetRequiredService<ITenantConfigService>();

		// Act
		var premiumConfig = await configService.GetConfigAsync("tenant-premium", TestCancellationToken).ConfigureAwait(false);
		var basicConfig = await configService.GetConfigAsync("tenant-basic", TestCancellationToken).ConfigureAwait(false);

		await host.StopAsync(TestCancellationToken).ConfigureAwait(false);

		// Assert
		_ = premiumConfig.ShouldNotBeNull();
		premiumConfig.MaxItems.ShouldBe(1000);
		premiumConfig.FeatureFlags.ShouldContain("premium-feature");

		_ = basicConfig.ShouldNotBeNull();
		basicConfig.MaxItems.ShouldBe(100);
		basicConfig.FeatureFlags.ShouldBeEmpty();
	}

	[Fact]
	public async Task MultiTenant_ReturnsDefaultConfigForUnknownTenant()
	{
		// Arrange
		var tenantConfigs = new Dictionary<string, TenantConfig>
		{
			["known-tenant"] = new TenantConfig { MaxItems = 500, FeatureFlags = ["feature-a"] }
		};

		using var host = CreateHost(services =>
		{
			_ = services.AddSingleton<ITenantConfigProvider>(new InMemoryTenantConfigProvider(tenantConfigs));
			_ = services.AddSingleton<ITenantConfigService, TenantConfigService>();
		});
		await host.StartAsync(TestCancellationToken).ConfigureAwait(false);

		var configService = host.Services.GetRequiredService<ITenantConfigService>();

		// Act
		var unknownConfig = await configService.GetConfigAsync("unknown-tenant", TestCancellationToken).ConfigureAwait(false);

		await host.StopAsync(TestCancellationToken).ConfigureAwait(false);

		// Assert - Returns default config
		_ = unknownConfig.ShouldNotBeNull();
		unknownConfig.MaxItems.ShouldBe(TenantConfig.Default.MaxItems);
	}

	#endregion

	#region Cross-Tenant Tests

	[Fact]
	public async Task MultiTenant_PreventsCrossTenantAccess()
	{
		// Arrange
		using var host = CreateHost(services =>
		{
			_ = services.AddSingleton<MultiTenantRepository>();
			_ = services.AddSingleton<ITenantDataService, TenantDataService>();
		});
		await host.StartAsync(TestCancellationToken).ConfigureAwait(false);

		var dataService = host.Services.GetRequiredService<ITenantDataService>();

		// Create data for tenant-x
		await dataService.CreateDataAsync("tenant-x", "secret-data", TestCancellationToken).ConfigureAwait(false);

		// Act - Attempt to access tenant-x data from tenant-y
		var canAccess = await dataService.CanAccessAsync("tenant-y", "tenant-x", TestCancellationToken).ConfigureAwait(false);

		await host.StopAsync(TestCancellationToken).ConfigureAwait(false);

		// Assert - Cross-tenant access should be denied
		canAccess.ShouldBeFalse();
	}

	[Fact]
	public async Task MultiTenant_AllowsSameTenantAccess()
	{
		// Arrange
		using var host = CreateHost(services =>
		{
			_ = services.AddSingleton<MultiTenantRepository>();
			_ = services.AddSingleton<ITenantDataService, TenantDataService>();
		});
		await host.StartAsync(TestCancellationToken).ConfigureAwait(false);

		var dataService = host.Services.GetRequiredService<ITenantDataService>();

		// Create data for tenant-z
		await dataService.CreateDataAsync("tenant-z", "my-data", TestCancellationToken).ConfigureAwait(false);

		// Act - Access own tenant data
		var canAccess = await dataService.CanAccessAsync("tenant-z", "tenant-z", TestCancellationToken).ConfigureAwait(false);

		await host.StopAsync(TestCancellationToken).ConfigureAwait(false);

		// Assert
		canAccess.ShouldBeTrue();
	}

	#endregion

	#region Test Interfaces and Implementations

	private interface ITenantDataService
	{
		Task CreateDataAsync(string tenantId, string data, CancellationToken cancellationToken);
		Task<List<string>> GetDataAsync(string tenantId, CancellationToken cancellationToken);
		Task<bool> CanAccessAsync(string requestingTenantId, string targetTenantId, CancellationToken cancellationToken);
	}

	private interface ITenantConfigService
	{
		Task<TenantConfig> GetConfigAsync(string tenantId, CancellationToken cancellationToken);
	}

	private interface ITenantConfigProvider
	{
		TenantConfig? GetConfig(string tenantId);
	}

	private sealed class TenantConfig
	{
		public static TenantConfig Default { get; } = new() { MaxItems = 50, FeatureFlags = [] };

		public int MaxItems { get; set; }
		public List<string> FeatureFlags { get; set; } = [];
	}

	private sealed class MultiTenantRepository
	{
		private readonly Dictionary<string, List<string>> _tenantData = [];

		public void Add(string tenantId, string data)
		{
			if (!_tenantData.TryGetValue(tenantId, out var list))
			{
				list = [];
				_tenantData[tenantId] = list;
			}

			list.Add(data);
		}

		public List<string> GetForTenant(string tenantId) =>
			_tenantData.GetValueOrDefault(tenantId, []);

		public bool HasDataForTenant(string tenantId) =>
			_tenantData.ContainsKey(tenantId) && _tenantData[tenantId].Count > 0;
	}

	private sealed class InMemoryTenantConfigProvider(Dictionary<string, TenantConfig> configs) : ITenantConfigProvider
	{
		public TenantConfig? GetConfig(string tenantId) => configs.GetValueOrDefault(tenantId);
	}

	private sealed class TenantDataService(MultiTenantRepository repository) : ITenantDataService
	{
		public Task CreateDataAsync(string tenantId, string data, CancellationToken cancellationToken)
		{
			if (string.IsNullOrWhiteSpace(tenantId))
			{
				throw new ArgumentException("Tenant ID is required", nameof(tenantId));
			}

			repository.Add(tenantId, data);
			return Task.CompletedTask;
		}

		public Task<List<string>> GetDataAsync(string tenantId, CancellationToken cancellationToken)
		{
			if (string.IsNullOrWhiteSpace(tenantId))
			{
				return Task.FromResult(new List<string>());
			}

			return Task.FromResult(repository.GetForTenant(tenantId));
		}

		public Task<bool> CanAccessAsync(string requestingTenantId, string targetTenantId, CancellationToken cancellationToken)
		{
			// Deny if trying to access different tenant's data
			if (requestingTenantId != targetTenantId)
			{
				return Task.FromResult(false);
			}

			return Task.FromResult(repository.HasDataForTenant(targetTenantId));
		}
	}

	private sealed class TenantConfigService(ITenantConfigProvider configProvider) : ITenantConfigService
	{
		public Task<TenantConfig> GetConfigAsync(string tenantId, CancellationToken cancellationToken)
		{
			var config = configProvider.GetConfig(tenantId) ?? TenantConfig.Default;
			return Task.FromResult(config);
		}
	}

	#endregion
}
