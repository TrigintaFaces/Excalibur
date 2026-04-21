// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3;
using Excalibur.A3.Abstractions.Authorization;
using Excalibur.A3.Authorization.Stores.InMemory;

namespace Excalibur.Tests.A3;

/// <summary>
/// Unit tests for <see cref="A3CoreServiceCollectionExtensions.AddExcaliburA3Core"/>.
/// Verifies DI registration of lightweight A3 core services.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class A3CoreServiceCollectionExtensionsShould
{
	[Fact]
	public void ReturnIA3Builder()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var builder = services.AddExcaliburA3Core();

		// Assert
		builder.ShouldNotBeNull();
		builder.ShouldBeAssignableTo<IA3Builder>();
	}

	[Fact]
	public void ExposeServicesPropertyOnBuilder()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var builder = services.AddExcaliburA3Core();

		// Assert
		builder.Services.ShouldBeSameAs(services);
	}

	[Fact]
	public void ThrowArgumentNullException_WhenServicesIsNull()
	{
		// Act & Assert
		IServiceCollection nullServices = null!;
		Should.Throw<ArgumentNullException>(() => nullServices.AddExcaliburA3Core());
	}

	[Fact]
	public void RegisterInMemoryGrantStore_AsFallback()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburA3Core();

		// Assert
		var provider = services.BuildServiceProvider();
		var store = provider.GetService<IGrantStore>();
		store.ShouldNotBeNull();
		store.ShouldBeOfType<InMemoryGrantStore>();
	}

	[Fact]
	public void RegisterInMemoryActivityGroupStore_AsFallback()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburA3Core();

		// Assert
		var provider = services.BuildServiceProvider();
		var store = provider.GetService<IActivityGroupStore>();
		store.ShouldNotBeNull();
		store.ShouldBeOfType<InMemoryActivityGroupStore>();
	}

	[Fact]
	public void PreserveExistingGrantStore_WhenRegisteredBefore()
	{
		// Arrange -- register custom store BEFORE calling AddExcaliburA3Core
		var services = new ServiceCollection();
		services.AddSingleton<IGrantStore, StubGrantStore>();

		// Act
		services.AddExcaliburA3Core();

		// Assert -- TryAddSingleton should NOT overwrite the existing registration
		var provider = services.BuildServiceProvider();
		var store = provider.GetService<IGrantStore>();
		store.ShouldBeOfType<StubGrantStore>();
	}

	[Fact]
	public void PreserveExistingActivityGroupStore_WhenRegisteredBefore()
	{
		// Arrange -- register custom store BEFORE calling AddExcaliburA3Core
		var services = new ServiceCollection();
		services.AddSingleton<IActivityGroupStore, StubActivityGroupStore>();

		// Act
		services.AddExcaliburA3Core();

		// Assert -- TryAddSingleton should NOT overwrite the existing registration
		var provider = services.BuildServiceProvider();
		var store = provider.GetService<IActivityGroupStore>();
		store.ShouldBeOfType<StubActivityGroupStore>();
	}

	[Fact]
	public void BeIdempotent_WhenCalledMultipleTimes()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburA3Core();
		services.AddExcaliburA3Core();

		// Assert -- should only have one registration per store
		var grantStoreCount = services.Count(sd => sd.ServiceType == typeof(IGrantStore));
		grantStoreCount.ShouldBe(1);

		var activityGroupStoreCount = services.Count(sd => sd.ServiceType == typeof(IActivityGroupStore));
		activityGroupStoreCount.ShouldBe(1);
	}

	[Fact]
	public void RegisterStoresAsSingletons()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburA3Core();

		// Assert
		var grantDescriptor = services.First(sd => sd.ServiceType == typeof(IGrantStore));
		grantDescriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);

		var activityDescriptor = services.First(sd => sd.ServiceType == typeof(IActivityGroupStore));
		activityDescriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
	}

	[Fact]
	public void AllowOverride_ViaBuilderUseGrantStore()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act -- AddExcaliburA3Core then override via builder
		var builder = services.AddExcaliburA3Core();
		builder.UseGrantStore<StubGrantStore>();

		// Assert -- override wins
		var provider = services.BuildServiceProvider();
		provider.GetService<IGrantStore>().ShouldBeOfType<StubGrantStore>();
	}

	[Fact]
	public void AllowOverride_ViaBuilderUseActivityGroupStore()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var builder = services.AddExcaliburA3Core();
		builder.UseActivityGroupStore<StubActivityGroupStore>();

		// Assert -- override wins
		var provider = services.BuildServiceProvider();
		provider.GetService<IActivityGroupStore>().ShouldBeOfType<StubActivityGroupStore>();
	}

	#region Test Doubles

	private sealed class StubGrantStore : IGrantStore
	{
		public Task<Grant?> GetGrantAsync(string userId, string tenantId, string grantType,
			string qualifier, CancellationToken cancellationToken) => Task.FromResult<Grant?>(null);

		public Task<IReadOnlyList<Grant>> GetAllGrantsAsync(string userId, CancellationToken cancellationToken) =>
			Task.FromResult<IReadOnlyList<Grant>>(Array.Empty<Grant>());

		public Task<int> SaveGrantAsync(Grant grant, CancellationToken cancellationToken) => Task.FromResult(0);

		public Task<int> DeleteGrantAsync(string userId, string tenantId, string grantType,
			string qualifier, string? revokedBy, DateTimeOffset? revokedOn,
			CancellationToken cancellationToken) => Task.FromResult(0);

		public Task<bool> GrantExistsAsync(string userId, string tenantId, string grantType,
			string qualifier, CancellationToken cancellationToken) => Task.FromResult(false);

		public object? GetService(Type serviceType) => null;
	}

	private sealed class StubActivityGroupStore : IActivityGroupStore
	{
		public Task<bool> ActivityGroupExistsAsync(string activityGroupName, CancellationToken cancellationToken) =>
			Task.FromResult(false);

		public Task<IReadOnlyDictionary<string, object>> FindActivityGroupsAsync(CancellationToken cancellationToken) =>
			Task.FromResult<IReadOnlyDictionary<string, object>>(new Dictionary<string, object>());

		public Task<int> DeleteAllActivityGroupsAsync(CancellationToken cancellationToken) => Task.FromResult(0);

		public Task<int> CreateActivityGroupAsync(string? tenantId, string name,
			string activityName, CancellationToken cancellationToken) => Task.FromResult(0);

		public object? GetService(Type serviceType) => null;
	}

	#endregion
}
