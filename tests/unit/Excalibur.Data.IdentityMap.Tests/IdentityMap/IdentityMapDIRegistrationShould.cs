// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.IdentityMap.Tests.IdentityMap;

[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Data.IdentityMap")]
public sealed class IdentityMapDIRegistrationShould
{
	#region AddInMemoryIdentityMap

	[Fact]
	public void AddInMemoryIdentityMap_RegisterStore()
	{
		var services = new ServiceCollection();
		services.AddInMemoryIdentityMap();

		using var provider = services.BuildServiceProvider();
		var store = provider.GetService<IIdentityMapStore>();

		store.ShouldNotBeNull();

		// Telemetry decorator wraps InMemoryIdentityMapStore by default (EnableTelemetry=true)
		store.ShouldBeOfType<Diagnostics.TelemetryIdentityMapStoreDecorator>();
	}

	[Fact]
	public void AddInMemoryIdentityMap_SkipTelemetryWhenDisabled()
	{
		var services = new ServiceCollection();
		services.AddInMemoryIdentityMap();
		services.Configure<IdentityMapOptions>(o => o.EnableTelemetry = false);

		using var provider = services.BuildServiceProvider();
		var store = provider.GetService<IIdentityMapStore>();

		store.ShouldNotBeNull();
		store.ShouldBeOfType<InMemoryIdentityMapStore>();
	}

	[Fact]
	public void AddInMemoryIdentityMap_WrapWithCachingWhenDistributedCacheAvailable()
	{
		var services = new ServiceCollection();
		services.AddInMemoryIdentityMap();
		services.AddSingleton(A.Fake<Microsoft.Extensions.Caching.Distributed.IDistributedCache>());

		using var provider = services.BuildServiceProvider();
		var store = provider.GetService<IIdentityMapStore>();

		store.ShouldNotBeNull();

		// Caching wraps outermost when IDistributedCache is available
		store.ShouldBeOfType<Diagnostics.CachingIdentityMapStoreDecorator>();
	}

	[Fact]
	public void AddInMemoryIdentityMap_RegisterOptions()
	{
		var services = new ServiceCollection();
		services.AddInMemoryIdentityMap();

		using var provider = services.BuildServiceProvider();
		var options = provider.GetService<IOptions<IdentityMapOptions>>();

		options.ShouldNotBeNull();
		options.Value.EnableTelemetry.ShouldBeTrue();
		options.Value.DefaultExternalSystem.ShouldBeNull();
	}

	[Fact]
	public void AddInMemoryIdentityMap_ReturnServiceCollection()
	{
		var services = new ServiceCollection();
		var result = services.AddInMemoryIdentityMap();

		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddInMemoryIdentityMap_ThrowOnNullServices()
	{
		IServiceCollection services = null!;

		Should.Throw<ArgumentNullException>(() => services.AddInMemoryIdentityMap());
	}

	[Fact]
	public void AddInMemoryIdentityMap_BeSingleton()
	{
		var services = new ServiceCollection();
		services.AddInMemoryIdentityMap();

		using var provider = services.BuildServiceProvider();
		var store1 = provider.GetRequiredService<IIdentityMapStore>();
		var store2 = provider.GetRequiredService<IIdentityMapStore>();

		store1.ShouldBeSameAs(store2);
	}

	#endregion

	#region AddIdentityMap Builder

	[Fact]
	public void AddIdentityMap_ThrowOnNullServices()
	{
		IServiceCollection services = null!;

		Should.Throw<ArgumentNullException>(() =>
			services.AddIdentityMap(_ => { }));
	}

	[Fact]
	public void AddIdentityMap_ThrowOnNullConfigure()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(() =>
			services.AddIdentityMap(null!));
	}

	[Fact]
	public void AddIdentityMap_RegisterOptionsViaBuilder()
	{
		var services = new ServiceCollection();
		services.AddIdentityMap(_ => { });

		using var provider = services.BuildServiceProvider();
		var options = provider.GetService<IOptions<IdentityMapOptions>>();

		options.ShouldNotBeNull();
	}

	#endregion

	#region IdentityMapOptions

	[Fact]
	public void IdentityMapOptions_HaveCorrectDefaults()
	{
		var options = new IdentityMapOptions();

		options.EnableTelemetry.ShouldBeTrue();
		options.DefaultExternalSystem.ShouldBeNull();
	}

	[Fact]
	public void IdentityMapOptions_AllowSettingValues()
	{
		var options = new IdentityMapOptions
		{
			EnableTelemetry = false,
			DefaultExternalSystem = "LegacyCore"
		};

		options.EnableTelemetry.ShouldBeFalse();
		options.DefaultExternalSystem.ShouldBe("LegacyCore");
	}

	#endregion

	#region IdentityBindResult

	[Fact]
	public void IdentityBindResult_SupportRecordEquality()
	{
		var result1 = new IdentityBindResult("AGG-001", true);
		var result2 = new IdentityBindResult("AGG-001", true);

		result1.ShouldBe(result2);
	}

	[Fact]
	public void IdentityBindResult_DifferOnWasCreated()
	{
		var result1 = new IdentityBindResult("AGG-001", true);
		var result2 = new IdentityBindResult("AGG-001", false);

		result1.ShouldNotBe(result2);
	}

	#endregion
}
