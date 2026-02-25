// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.AuditLogging;

namespace Excalibur.Dispatch.Security.Tests.AuditLogging;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
[UnitTest]
public sealed class AuditLoggingServiceCollectionExtensionsShould
{
	#region AddAuditLogging (default) Tests

	[Fact]
	public void AddAuditLogging_RegistersDefaultServices()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddAuditLogging();
		using var provider = services.BuildServiceProvider();

		// Assert
		var auditLogger = provider.GetService<IAuditLogger>();
		var auditStore = provider.GetService<IAuditStore>();
		var inMemoryStore = provider.GetService<InMemoryAuditStore>();

		_ = auditLogger.ShouldNotBeNull();
		_ = auditLogger.ShouldBeOfType<DefaultAuditLogger>();
		_ = auditStore.ShouldNotBeNull();
		_ = auditStore.ShouldBeOfType<InMemoryAuditStore>();
		_ = inMemoryStore.ShouldNotBeNull();
	}

	[Fact]
	public void AddAuditLogging_ReturnsSameStoreInstance()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddAuditLogging();
		using var provider = services.BuildServiceProvider();

		// Assert - store should be singleton
		var store1 = provider.GetRequiredService<IAuditStore>();
		var store2 = provider.GetRequiredService<IAuditStore>();

		store1.ShouldBeSameAs(store2);
	}

	[Fact]
	public void AddAuditLogging_LoggerIsScopedDifferentPerScope()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddAuditLogging();
		using var provider = services.BuildServiceProvider();

		// Assert - logger should be scoped
		IAuditLogger logger1;
		IAuditLogger logger2;

		using (var scope1 = provider.CreateScope())
		{
			logger1 = scope1.ServiceProvider.GetRequiredService<IAuditLogger>();
		}

		using (var scope2 = provider.CreateScope())
		{
			logger2 = scope2.ServiceProvider.GetRequiredService<IAuditLogger>();
		}

		// Different scope = different instance
		logger1.ShouldNotBeSameAs(logger2);
	}

	[Fact]
	public void AddAuditLogging_ThrowsOnNullServices()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			AuditLoggingServiceCollectionExtensions.AddAuditLogging(null!));
	}

	[Fact]
	public void AddAuditLogging_IsIdempotent()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act - call twice
		_ = services.AddAuditLogging();
		_ = services.AddAuditLogging();
		using var provider = services.BuildServiceProvider();

		// Assert - should still resolve correctly (TryAdd prevents duplicates)
		var store = provider.GetRequiredService<IAuditStore>();
		_ = store.ShouldBeOfType<InMemoryAuditStore>();
	}

	[Fact]
	public void AddAuditLogging_ReturnsSameServiceCollectionForChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddAuditLogging();

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddAuditLogging_InMemoryStoreAndInterfaceResolveSameInstance()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddAuditLogging();
		using var provider = services.BuildServiceProvider();

		// Assert - concrete type and interface should resolve to the same singleton
		var concreteStore = provider.GetRequiredService<InMemoryAuditStore>();
		var interfaceStore = provider.GetRequiredService<IAuditStore>();

		interfaceStore.ShouldBeSameAs(concreteStore);
	}

	[Fact]
	public void AddAuditLogging_RegistersInMemoryStoreAsSingleton()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddAuditLogging();

		// Assert - verify descriptor lifetime
		var storeDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(InMemoryAuditStore));
		_ = storeDescriptor.ShouldNotBeNull();
		storeDescriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
	}

	[Fact]
	public void AddAuditLogging_RegistersLoggerAsScoped()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddAuditLogging();

		// Assert - verify descriptor lifetime
		var loggerDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IAuditLogger));
		_ = loggerDescriptor.ShouldNotBeNull();
		loggerDescriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
	}

	#endregion AddAuditLogging (default) Tests

	#region AddAuditLogging<T> (generic) Tests

	[Fact]
	public void AddAuditLogging_Generic_RegistersCustomStore()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddAuditLogging<CustomAuditStore>();
		using var provider = services.BuildServiceProvider();

		// Assert
		var auditStore = provider.GetService<IAuditStore>();
		_ = auditStore.ShouldNotBeNull();
		_ = auditStore.ShouldBeOfType<CustomAuditStore>();
	}

	[Fact]
	public void AddAuditLogging_Generic_ThrowsOnNullServices()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			AuditLoggingServiceCollectionExtensions.AddAuditLogging<CustomAuditStore>(null!));
	}

	[Fact]
	public void AddAuditLogging_Generic_RegistersLoggerAsScoped()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddAuditLogging<CustomAuditStore>();

		// Assert
		var loggerDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IAuditLogger));
		_ = loggerDescriptor.ShouldNotBeNull();
		loggerDescriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);

		// Verify the implementation type
		var storeDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IAuditStore));
		_ = storeDescriptor.ShouldNotBeNull();
		storeDescriptor.ImplementationType.ShouldBe(typeof(CustomAuditStore));
	}

	[Fact]
	public void AddAuditLogging_Generic_ReturnsSameServiceCollectionForChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddAuditLogging<CustomAuditStore>();

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddAuditLogging_Generic_RegistersStoreAsSingleton()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddAuditLogging<CustomAuditStore>();

		// Assert
		var storeDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IAuditStore));
		_ = storeDescriptor.ShouldNotBeNull();
		storeDescriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
	}

	[Fact]
	public void AddAuditLogging_Generic_IsIdempotent()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act - call twice
		_ = services.AddAuditLogging<CustomAuditStore>();
		_ = services.AddAuditLogging<CustomAuditStore>();

		// Assert - TryAdd prevents duplicates
		var storeDescriptors = services.Where(d => d.ServiceType == typeof(IAuditStore)).ToList();
		storeDescriptors.Count.ShouldBe(1);
	}

	[Fact]
	public void AddAuditLogging_Generic_StoreIsSingletonAcrossScopes()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddAuditLogging<CustomAuditStore>();
		using var provider = services.BuildServiceProvider();

		// Act
		IAuditStore store1;
		IAuditStore store2;

		using (var scope1 = provider.CreateScope())
		{
			store1 = scope1.ServiceProvider.GetRequiredService<IAuditStore>();
		}

		using (var scope2 = provider.CreateScope())
		{
			store2 = scope2.ServiceProvider.GetRequiredService<IAuditStore>();
		}

		// Assert - singleton across scopes
		store1.ShouldBeSameAs(store2);
	}

	#endregion AddAuditLogging<T> (generic) Tests

	#region AddAuditLogging (factory) Tests

	[Fact]
	public void AddAuditLogging_WithFactory_UsesFactory()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		var customStore = new InMemoryAuditStore();

		// Act
		_ = services.AddAuditLogging(_ => customStore);
		using var provider = services.BuildServiceProvider();

		// Assert
		var resolvedStore = provider.GetRequiredService<IAuditStore>();
		resolvedStore.ShouldBeSameAs(customStore);
	}

	[Fact]
	public void AddAuditLogging_WithFactory_ThrowsOnNullFactory()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddAuditLogging((Func<IServiceProvider, IAuditStore>)null!));
	}

	[Fact]
	public void AddAuditLogging_WithFactory_ThrowsOnNullServices()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			AuditLoggingServiceCollectionExtensions.AddAuditLogging(null!, _ => new InMemoryAuditStore()));
	}

	[Fact]
	public void AddAuditLogging_WithFactory_ReturnsSameServiceCollectionForChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddAuditLogging(_ => new InMemoryAuditStore());

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddAuditLogging_WithFactory_RegistersAsSingleton()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddAuditLogging(_ => new InMemoryAuditStore());

		// Assert
		var storeDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IAuditStore));
		_ = storeDescriptor.ShouldNotBeNull();
		storeDescriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
		storeDescriptor.ImplementationFactory.ShouldNotBeNull();
	}

	[Fact]
	public void AddAuditLogging_WithFactory_RegistersLoggerAsScoped()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddAuditLogging(_ => new InMemoryAuditStore());

		// Assert
		var loggerDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IAuditLogger));
		_ = loggerDescriptor.ShouldNotBeNull();
		loggerDescriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
	}

	[Fact]
	public void AddAuditLogging_WithFactory_FactoryReceivesServiceProvider()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		IServiceProvider? capturedProvider = null;

		// Act
		_ = services.AddAuditLogging(sp =>
		{
			capturedProvider = sp;
			return new InMemoryAuditStore();
		});
		using var provider = services.BuildServiceProvider();
		_ = provider.GetRequiredService<IAuditStore>();

		// Assert
		_ = capturedProvider.ShouldNotBeNull();
	}

	[Fact]
	public void AddAuditLogging_WithFactory_IsIdempotent()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act - call twice
		_ = services.AddAuditLogging(_ => new InMemoryAuditStore());
		_ = services.AddAuditLogging(_ => new InMemoryAuditStore());

		// Assert - TryAdd prevents duplicates
		var storeDescriptors = services.Where(d => d.ServiceType == typeof(IAuditStore)).ToList();
		storeDescriptors.Count.ShouldBe(1);
	}

	[Fact]
	public void AddAuditLogging_WithFactory_StoreIsSingletonAcrossScopes()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddAuditLogging(_ => new InMemoryAuditStore());
		using var provider = services.BuildServiceProvider();

		// Act
		IAuditStore store1;
		IAuditStore store2;

		using (var scope1 = provider.CreateScope())
		{
			store1 = scope1.ServiceProvider.GetRequiredService<IAuditStore>();
		}

		using (var scope2 = provider.CreateScope())
		{
			store2 = scope2.ServiceProvider.GetRequiredService<IAuditStore>();
		}

		// Assert - singleton across scopes
		store1.ShouldBeSameAs(store2);
	}

	[Fact]
	public void AddAuditLogging_WithFactory_CanAccessOtherServicesViaProvider()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act - factory uses the service provider to get other services
		_ = services.AddAuditLogging(sp =>
		{
			// Verify we can resolve logging from the provider
			_ = sp.GetRequiredService<ILoggerFactory>();
			return new InMemoryAuditStore();
		});
		using var provider = services.BuildServiceProvider();

		// Assert - should not throw
		var store = provider.GetRequiredService<IAuditStore>();
		_ = store.ShouldBeOfType<InMemoryAuditStore>();
	}

	#endregion AddAuditLogging (factory) Tests

	#region UseAuditStore Tests

	[Fact]
	public void UseAuditStore_ReplacesExistingRegistration()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddAuditLogging(); // Registers InMemoryAuditStore

		// Act
		_ = services.UseAuditStore<CustomAuditStore>();
		using var provider = services.BuildServiceProvider();

		// Assert
		var store = provider.GetRequiredService<IAuditStore>();
		_ = store.ShouldBeOfType<CustomAuditStore>();
	}

	[Fact]
	public void UseAuditStore_WorksWhenNoExistingRegistration()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.UseAuditStore<CustomAuditStore>();
		using var provider = services.BuildServiceProvider();

		// Assert
		var store = provider.GetRequiredService<IAuditStore>();
		_ = store.ShouldBeOfType<CustomAuditStore>();
	}

	[Fact]
	public void UseAuditStore_ThrowsOnNullServices()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			AuditLoggingServiceCollectionExtensions.UseAuditStore<CustomAuditStore>(null!));
	}

	[Fact]
	public void UseAuditStore_ReturnsSameServiceCollectionForChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.UseAuditStore<CustomAuditStore>();

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void UseAuditStore_ReplacesFactoryRegistration()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddAuditLogging(_ => new InMemoryAuditStore());

		// Act
		_ = services.UseAuditStore<CustomAuditStore>();

		// Assert - the factory descriptor should be replaced
		var descriptors = services.Where(d => d.ServiceType == typeof(IAuditStore)).ToList();
		descriptors.Count.ShouldBe(1);
		descriptors[0].ImplementationType.ShouldBe(typeof(CustomAuditStore));
	}

	[Fact]
	public void UseAuditStore_ReplacesGenericRegistration()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddAuditLogging<InMemoryAuditStore>();

		// Act
		_ = services.UseAuditStore<CustomAuditStore>();

		// Assert
		var descriptors = services.Where(d => d.ServiceType == typeof(IAuditStore)).ToList();
		descriptors.Count.ShouldBe(1);
		descriptors[0].ImplementationType.ShouldBe(typeof(CustomAuditStore));
	}

	[Fact]
	public void UseAuditStore_RemovesOnlyFirstDescriptor()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddSingleton<IAuditStore, InMemoryAuditStore>();
		_ = services.AddSingleton<IAuditStore, CustomAuditStore>();

		// Act - UseAuditStore removes only the first descriptor
		_ = services.UseAuditStore<CustomAuditStore>();

		// Assert - one removed, one kept, one added = 2 total
		var descriptors = services.Where(d => d.ServiceType == typeof(IAuditStore)).ToList();
		descriptors.Count.ShouldBe(2);
	}

	[Fact]
	public void UseAuditStore_RegistersAsSingleton()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.UseAuditStore<CustomAuditStore>();

		// Assert
		var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IAuditStore));
		_ = descriptor.ShouldNotBeNull();
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
	}

	[Fact]
	public void UseAuditStore_ReplacesInstanceRegistration()
	{
		// Arrange
		var services = new ServiceCollection();
		var instance = new InMemoryAuditStore();
		_ = services.AddSingleton<IAuditStore>(instance);

		// Act
		_ = services.UseAuditStore<CustomAuditStore>();

		// Assert
		var descriptors = services.Where(d => d.ServiceType == typeof(IAuditStore)).ToList();
		descriptors.Count.ShouldBe(1);
		descriptors[0].ImplementationType.ShouldBe(typeof(CustomAuditStore));
		descriptors[0].ImplementationInstance.ShouldBeNull(); // Instance registration replaced
	}

	#endregion UseAuditStore Tests

	#region AddRbacAuditStore Tests

	[Fact]
	public void AddRbacAuditStore_ThrowsWhenNoAuditStoreRegistered()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() =>
			services.AddRbacAuditStore());
	}

	[Fact]
	public void AddRbacAuditStore_ThrowsOnNullServices()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			AuditLoggingServiceCollectionExtensions.AddRbacAuditStore(null!));
	}

	[Fact]
	public void AddRbacAuditStore_ReturnsSameServiceCollectionForChaining()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddAuditLogging();
		_ = services.AddScoped<IAuditRoleProvider, TestRoleProvider>();

		// Act
		var result = services.AddRbacAuditStore();

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddRbacAuditStore_ImplementationType_RegistersOriginalStoreByConcreteType()
	{
		// Arrange - AddAuditLogging<T> uses ImplementationType branch
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddAuditLogging<CustomAuditStore>();
		_ = services.AddScoped<IAuditRoleProvider, TestRoleProvider>();

		// Act
		_ = services.AddRbacAuditStore();

		// Assert - the original CustomAuditStore is re-registered under its concrete type
		var concreteDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(CustomAuditStore));
		_ = concreteDescriptor.ShouldNotBeNull();
		concreteDescriptor.ImplementationType.ShouldBe(typeof(CustomAuditStore));

		// And IAuditStore now points to a factory (decorator)
		var decoratorDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IAuditStore));
		_ = decoratorDescriptor.ShouldNotBeNull();
		decoratorDescriptor.ImplementationFactory.ShouldNotBeNull();
	}

	[Fact]
	public void AddRbacAuditStore_InstanceBased_RegistersOriginalInstanceByConcreteType()
	{
		// Arrange - ImplementationInstance branch
		var services = new ServiceCollection();
		_ = services.AddLogging();
		var storeInstance = new InMemoryAuditStore();
		_ = services.AddSingleton<IAuditStore>(storeInstance);
		_ = services.AddScoped<IAuditRoleProvider, TestRoleProvider>();

		// Act
		_ = services.AddRbacAuditStore();

		// Assert - the instance is re-registered under its concrete type
		var concreteDescriptor = services.FirstOrDefault(d =>
			d.ServiceType == typeof(InMemoryAuditStore) && d.ImplementationInstance is not null);
		_ = concreteDescriptor.ShouldNotBeNull();
		concreteDescriptor.ImplementationInstance.ShouldBeSameAs(storeInstance);
		concreteDescriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
	}

	[Fact]
	public void AddRbacAuditStore_FactoryBased_RegistersDecoratorWithFactory()
	{
		// Arrange - ImplementationFactory branch
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddAuditLogging(_ => new InMemoryAuditStore());
		_ = services.AddScoped<IAuditRoleProvider, TestRoleProvider>();

		// Act
		_ = services.AddRbacAuditStore();

		// Assert - factory-based decorator registration preserves original lifetime
		var descriptors = services.Where(d => d.ServiceType == typeof(IAuditStore)).ToList();
		descriptors.Count.ShouldBe(1);
		descriptors[0].ImplementationFactory.ShouldNotBeNull();
		descriptors[0].Lifetime.ShouldBe(ServiceLifetime.Singleton);
	}

	[Fact]
	public void AddRbacAuditStore_ImplementationType_PreservesLifetime()
	{
		// Arrange - verify the decorator preserves the original descriptor's lifetime
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddAuditLogging<CustomAuditStore>(); // Singleton by default

		_ = services.AddScoped<IAuditRoleProvider, TestRoleProvider>();

		// Act
		_ = services.AddRbacAuditStore();

		// Assert - decorator should preserve the original lifetime
		var decoratorDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IAuditStore));
		_ = decoratorDescriptor.ShouldNotBeNull();
		decoratorDescriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
	}

	[Fact]
	public void AddRbacAuditStore_RemovesOriginalDescriptor()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddAuditLogging();
		_ = services.AddScoped<IAuditRoleProvider, TestRoleProvider>();

		var originalDescriptorCount = services.Count(d => d.ServiceType == typeof(IAuditStore));
		originalDescriptorCount.ShouldBe(1);

		// Act
		_ = services.AddRbacAuditStore();

		// Assert - still exactly one IAuditStore descriptor (the decorator replaced the original)
		var newDescriptorCount = services.Count(d => d.ServiceType == typeof(IAuditStore));
		newDescriptorCount.ShouldBe(1);
	}

	[Fact]
	public void AddRbacAuditStore_WithGenericCustomStore_DescriptorCheck()
	{
		// Arrange - AddAuditLogging<T> uses ImplementationType
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddAuditLogging<CustomAuditStore>();
		_ = services.AddScoped<IAuditRoleProvider, TestRoleProvider>();

		// Act
		_ = services.AddRbacAuditStore();

		// Assert - decorator registered with factory
		var decoratorDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IAuditStore));
		_ = decoratorDescriptor.ShouldNotBeNull();
		decoratorDescriptor.ImplementationFactory.ShouldNotBeNull();

		// Original store re-registered by concrete type
		var concreteDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(CustomAuditStore));
		_ = concreteDescriptor.ShouldNotBeNull();
	}

	#endregion AddRbacAuditStore Tests

	#region RbacAuditStore Direct Construction Tests

	// NOTE: These tests use direct construction to avoid the circular DI dependency
	// where AddRbacAuditStore's factory resolves IAuditLogger which depends on IAuditStore.
	// This is a known bug in AddRbacAuditStore (GitHub issue pending).

	[Fact]
	public async Task RbacAuditStore_DelegatesStoreOperationsDirectly()
	{
		// Arrange - construct RbacAuditStore directly
		var innerStore = new InMemoryAuditStore();
		var roleProvider = A.Fake<IAuditRoleProvider>();
		A.CallTo(() => roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.Returns(Task.FromResult(AuditLogRole.Administrator));
		var logger = A.Fake<ILogger<RbacAuditStore>>();

		var rbacStore = new RbacAuditStore(innerStore, roleProvider, logger);

		var auditEvent = new AuditEvent
		{
			EventId = "test-rbac-direct",
			EventType = AuditEventType.DataAccess,
			Action = "Read",
			Outcome = AuditOutcome.Success,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "user-1"
		};

		// Act
		var result = await rbacStore.StoreAsync(auditEvent, CancellationToken.None);

		// Assert
		result.EventId.ShouldBe("test-rbac-direct");
		result.EventHash.ShouldNotBeNullOrWhiteSpace();
		innerStore.Count.ShouldBe(1);
	}

	[Fact]
	public async Task RbacAuditStore_DelegatesQueryOperationsDirectly()
	{
		// Arrange - construct directly
		var innerStore = new InMemoryAuditStore();
		var roleProvider = A.Fake<IAuditRoleProvider>();
		A.CallTo(() => roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.Returns(Task.FromResult(AuditLogRole.Administrator));
		var logger = A.Fake<ILogger<RbacAuditStore>>();

		var rbacStore = new RbacAuditStore(innerStore, roleProvider, logger);

		// Pre-populate the inner store
		_ = await innerStore.StoreAsync(new AuditEvent
		{
			EventId = "query-event-1",
			EventType = AuditEventType.DataAccess,
			Action = "Read",
			Outcome = AuditOutcome.Success,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "user-1"
		}, CancellationToken.None);

		// Act
		var results = await rbacStore.QueryAsync(new AuditQuery(), CancellationToken.None);

		// Assert
		results.Count.ShouldBe(1);
		results[0].EventId.ShouldBe("query-event-1");
	}

	[Fact]
	public async Task RbacAuditStore_DelegatesGetByIdDirectly()
	{
		// Arrange - construct directly
		var innerStore = new InMemoryAuditStore();
		var roleProvider = A.Fake<IAuditRoleProvider>();
		A.CallTo(() => roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.Returns(Task.FromResult(AuditLogRole.Administrator));
		var logger = A.Fake<ILogger<RbacAuditStore>>();

		var rbacStore = new RbacAuditStore(innerStore, roleProvider, logger);

		// Pre-populate
		_ = await innerStore.StoreAsync(new AuditEvent
		{
			EventId = "getbyid-event-1",
			EventType = AuditEventType.DataAccess,
			Action = "Read",
			Outcome = AuditOutcome.Success,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "user-1"
		}, CancellationToken.None);

		// Act
		var result = await rbacStore.GetByIdAsync("getbyid-event-1", CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		result.EventId.ShouldBe("getbyid-event-1");
	}

	[Fact]
	public async Task RbacAuditStore_WithMetaAuditLogger_DelegatesCorrectly()
	{
		// Arrange - construct directly with meta audit logger
		var innerStore = new InMemoryAuditStore();
		var roleProvider = A.Fake<IAuditRoleProvider>();
		A.CallTo(() => roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.Returns(Task.FromResult(AuditLogRole.Administrator));
		var logger = A.Fake<ILogger<RbacAuditStore>>();
		var metaLogger = A.Fake<IAuditLogger>();

		var rbacStore = new RbacAuditStore(innerStore, roleProvider, logger, null, metaLogger);

		var auditEvent = new AuditEvent
		{
			EventId = "meta-logger-test",
			EventType = AuditEventType.DataAccess,
			Action = "Read",
			Outcome = AuditOutcome.Success,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "user-1"
		};

		// Act
		var result = await rbacStore.StoreAsync(auditEvent, CancellationToken.None);

		// Assert
		result.EventId.ShouldBe("meta-logger-test");
		innerStore.Count.ShouldBe(1);
	}

	[Fact]
	public async Task RbacAuditStore_DelegatesCountDirectly()
	{
		// Arrange
		var innerStore = new InMemoryAuditStore();
		var roleProvider = A.Fake<IAuditRoleProvider>();
		A.CallTo(() => roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.Returns(Task.FromResult(AuditLogRole.Administrator));
		var logger = A.Fake<ILogger<RbacAuditStore>>();

		var rbacStore = new RbacAuditStore(innerStore, roleProvider, logger);

		// Pre-populate
		_ = await innerStore.StoreAsync(new AuditEvent
		{
			EventId = "count-event-1",
			EventType = AuditEventType.DataAccess,
			Action = "Read",
			Outcome = AuditOutcome.Success,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "user-1"
		}, CancellationToken.None);

		// Act
		var count = await rbacStore.CountAsync(new AuditQuery(), CancellationToken.None);

		// Assert
		count.ShouldBe(1);
	}

	[Fact]
	public async Task RbacAuditStore_DelegatesVerifyIntegrityDirectly()
	{
		// Arrange
		var innerStore = new InMemoryAuditStore();
		var roleProvider = A.Fake<IAuditRoleProvider>();
		A.CallTo(() => roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.Returns(Task.FromResult(AuditLogRole.Administrator));
		var logger = A.Fake<ILogger<RbacAuditStore>>();

		var rbacStore = new RbacAuditStore(innerStore, roleProvider, logger);

		// Act
		var result = await rbacStore.VerifyChainIntegrityAsync(
			DateTimeOffset.UtcNow.AddDays(-1),
			DateTimeOffset.UtcNow,
			CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
		result.EventsVerified.ShouldBe(0);
	}

	[Fact]
	public async Task RbacAuditStore_DelegatesGetLastEventDirectly()
	{
		// Arrange
		var innerStore = new InMemoryAuditStore();
		var roleProvider = A.Fake<IAuditRoleProvider>();
		A.CallTo(() => roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.Returns(Task.FromResult(AuditLogRole.Administrator));
		var logger = A.Fake<ILogger<RbacAuditStore>>();

		var rbacStore = new RbacAuditStore(innerStore, roleProvider, logger);

		// Pre-populate
		_ = await innerStore.StoreAsync(new AuditEvent
		{
			EventId = "last-event-test",
			EventType = AuditEventType.DataAccess,
			Action = "Read",
			Outcome = AuditOutcome.Success,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "user-1"
		}, CancellationToken.None);

		// Act
		var lastEvent = await rbacStore.GetLastEventAsync(null, CancellationToken.None);

		// Assert
		_ = lastEvent.ShouldNotBeNull();
		lastEvent.EventId.ShouldBe("last-event-test");
	}

	[Fact]
	public async Task RbacAuditStore_FullWorkflow_StoreQueryGetByIdCount()
	{
		// Arrange - full end-to-end workflow with direct construction
		var innerStore = new InMemoryAuditStore();
		var roleProvider = A.Fake<IAuditRoleProvider>();
		A.CallTo(() => roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.Returns(Task.FromResult(AuditLogRole.Administrator));
		var logger = A.Fake<ILogger<RbacAuditStore>>();

		var rbacStore = new RbacAuditStore(innerStore, roleProvider, logger);

		var auditEvent = new AuditEvent
		{
			EventId = "full-e2e-test",
			EventType = AuditEventType.DataAccess,
			Action = "Read",
			Outcome = AuditOutcome.Success,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "user-1"
		};

		// Act - store, query, get by id
		var storeResult = await rbacStore.StoreAsync(auditEvent, CancellationToken.None);
		storeResult.EventId.ShouldBe("full-e2e-test");

		var retrieved = await rbacStore.GetByIdAsync("full-e2e-test", CancellationToken.None);
		_ = retrieved.ShouldNotBeNull();

		var count = await rbacStore.CountAsync(new AuditQuery(), CancellationToken.None);
		count.ShouldBe(1);
	}

	#endregion RbacAuditStore Direct Construction Tests

	#region AddAuditRoleProvider Tests

	[Fact]
	public void AddAuditRoleProvider_RegistersProvider()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddAuditRoleProvider<TestRoleProvider>();
		using var provider = services.BuildServiceProvider();
		using var scope = provider.CreateScope();

		// Assert
		var roleProvider = scope.ServiceProvider.GetService<IAuditRoleProvider>();
		_ = roleProvider.ShouldNotBeNull();
		_ = roleProvider.ShouldBeOfType<TestRoleProvider>();
	}

	[Fact]
	public void AddAuditRoleProvider_ThrowsOnNullServices()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			AuditLoggingServiceCollectionExtensions.AddAuditRoleProvider<TestRoleProvider>(null!));
	}

	[Fact]
	public void AddAuditRoleProvider_RegistersAsScoped()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddAuditRoleProvider<TestRoleProvider>();

		// Assert
		var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IAuditRoleProvider));
		_ = descriptor.ShouldNotBeNull();
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
	}

	[Fact]
	public void AddAuditRoleProvider_ReturnsSameServiceCollectionForChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddAuditRoleProvider<TestRoleProvider>();

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddAuditRoleProvider_OverridesPreviousRegistration()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act - register two different providers
		_ = services.AddAuditRoleProvider<TestRoleProvider>();
		_ = services.AddAuditRoleProvider<AnotherTestRoleProvider>();
		using var provider = services.BuildServiceProvider();
		using var scope = provider.CreateScope();

		// Assert - the last registration wins (AddScoped replaces)
		var roleProvider = scope.ServiceProvider.GetRequiredService<IAuditRoleProvider>();
		_ = roleProvider.ShouldBeOfType<AnotherTestRoleProvider>();
	}

	[Fact]
	public void AddAuditRoleProvider_VerifiesDescriptorLifetime()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddAuditRoleProvider<TestRoleProvider>();

		// Assert
		var descriptors = services.Where(d => d.ServiceType == typeof(IAuditRoleProvider)).ToList();
		descriptors.Count.ShouldBe(1);
		descriptors[0].ImplementationType.ShouldBe(typeof(TestRoleProvider));
		descriptors[0].Lifetime.ShouldBe(ServiceLifetime.Scoped);
	}

	#endregion AddAuditRoleProvider Tests

	#region Full Integration Tests (descriptor checks + direct construction only)

	[Fact]
	public void FullSetup_WithInstanceAndRbac_DescriptorRegistration()
	{
		// Arrange - verify instance + RBAC descriptor wiring
		var services = new ServiceCollection();
		_ = services.AddLogging();
		var storeInstance = new InMemoryAuditStore();
		_ = services.AddSingleton<IAuditStore>(storeInstance);
		_ = services.AddScoped<IAuditRoleProvider, TestRoleProvider>();

		// Act
		_ = services.AddRbacAuditStore();

		// Assert - the instance is re-registered by concrete type
		var concreteDescriptor = services.FirstOrDefault(d =>
			d.ServiceType == typeof(InMemoryAuditStore) && d.ImplementationInstance is not null);
		_ = concreteDescriptor.ShouldNotBeNull();
		concreteDescriptor.ImplementationInstance.ShouldBeSameAs(storeInstance);

		// And IAuditStore is decorated
		var decoratorDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IAuditStore));
		_ = decoratorDescriptor.ShouldNotBeNull();
		decoratorDescriptor.ImplementationFactory.ShouldNotBeNull();
	}

	[Fact]
	public void FullSetup_WithFactoryAndRbac_DescriptorRegistration()
	{
		// Arrange - verify factory + RBAC descriptor wiring
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddAuditLogging(_ => new InMemoryAuditStore());
		_ = services.AddScoped<IAuditRoleProvider, TestRoleProvider>();

		// Act
		_ = services.AddRbacAuditStore();

		// Assert - verify the descriptor chain is correct
		var storeDescriptors = services.Where(d => d.ServiceType == typeof(IAuditStore)).ToList();
		storeDescriptors.Count.ShouldBe(1);
		storeDescriptors[0].ImplementationFactory.ShouldNotBeNull();
	}

	#endregion Full Integration Tests

	/// <summary>
	/// Test role provider that always returns Administrator.
	/// </summary>
	private sealed class TestRoleProvider : IAuditRoleProvider
	{
		public Task<AuditLogRole> GetCurrentRoleAsync(CancellationToken cancellationToken = default)
			=> Task.FromResult(AuditLogRole.Administrator);
	}

	/// <summary>
	/// Another test role provider for verifying provider replacement.
	/// </summary>
	private sealed class AnotherTestRoleProvider : IAuditRoleProvider
	{
		public Task<AuditLogRole> GetCurrentRoleAsync(CancellationToken cancellationToken = default)
			=> Task.FromResult(AuditLogRole.ComplianceOfficer);
	}

	/// <summary>
	/// Custom audit store for testing.
	/// </summary>
	private sealed class CustomAuditStore : IAuditStore
	{
		public Task<AuditEventId> StoreAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
			=> Task.FromResult(new AuditEventId
			{
				EventId = auditEvent.EventId,
				EventHash = "custom-hash",
				SequenceNumber = 1,
				RecordedAt = DateTimeOffset.UtcNow
			});

		public Task<AuditEvent?> GetByIdAsync(string eventId, CancellationToken cancellationToken = default)
			=> Task.FromResult<AuditEvent?>(null);

		public Task<IReadOnlyList<AuditEvent>> QueryAsync(AuditQuery query, CancellationToken cancellationToken = default)
			=> Task.FromResult<IReadOnlyList<AuditEvent>>([]);

		public Task<long> CountAsync(AuditQuery query, CancellationToken cancellationToken = default)
			=> Task.FromResult(0L);

		public Task<AuditIntegrityResult> VerifyChainIntegrityAsync(
			DateTimeOffset startDate,
			DateTimeOffset endDate,
			CancellationToken cancellationToken = default)
			=> Task.FromResult(AuditIntegrityResult.Valid(0, startDate, endDate));

		public Task<AuditEvent?> GetLastEventAsync(string? tenantId = null, CancellationToken cancellationToken = default)
			=> Task.FromResult<AuditEvent?>(null);
	}
}
