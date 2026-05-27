// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Saga.Orchestration;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Saga.Tests.Orchestration;

/// <summary>
/// Unit tests for <see cref="DispatchOrchestrationExtensions"/>.
/// Verifies service registration behavior.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class DispatchOrchestrationExtensionsShould
{
	#region AddExcaliburOrchestration Tests

	[Fact]
	public void ReturnServiceCollection_ForChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddExcaliburOrchestration();

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void RegisterSagaStore()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburOrchestration();

		// Assert - verify keyed registration exists for ISagaStore
		var descriptor = services.FirstOrDefault(d =>
			d.ServiceType == typeof(ISagaStore) && d.IsKeyedService);
		descriptor.ShouldNotBeNull();

		// Verify the concrete InMemorySagaStore is also registered
		services.ShouldContain(d =>
			d.ServiceType == typeof(InMemorySagaStore) &&
			d.Lifetime == ServiceLifetime.Singleton);
	}

	[Fact]
	public void RegisterSagaCoordinator()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburOrchestration();

		// Assert - verify registration exists
		var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ISagaCoordinator));
		descriptor.ShouldNotBeNull();
		descriptor.ImplementationType.ShouldBe(typeof(SagaCoordinator));
	}

	[Fact]
	public void RegisterSagaHandlingMiddleware()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act
		services.AddExcaliburOrchestration();

		// Assert - verify the middleware is registered in the service collection
		var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IDispatchMiddleware));
		descriptor.ShouldNotBeNull();
		descriptor.ImplementationType.ShouldBe(typeof(SagaHandlingMiddleware));
	}

	[Fact]
	public void RegisterServicesAsSingleton()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburOrchestration();

		// Assert - verify singleton lifetime for keyed ISagaStore
		var storeDescriptor = services.FirstOrDefault(d =>
			d.ServiceType == typeof(ISagaStore) && d.IsKeyedService);
		storeDescriptor.ShouldNotBeNull();
		storeDescriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);

		var coordinatorDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ISagaCoordinator));
		coordinatorDescriptor.ShouldNotBeNull();
		coordinatorDescriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
	}

	[Fact]
	public void NotOverrideExistingSagaStore()
	{
		// Arrange
		var services = new ServiceCollection();
		var fakeSagaStore = A.Fake<ISagaStore>();
		// Pre-register a keyed "default" ISagaStore to prevent override
		services.AddKeyedSingleton<ISagaStore>("default", fakeSagaStore);

		// Act
		services.AddExcaliburOrchestration();

		// Assert - the "default" keyed registration should still be the original fake
		var defaultDescriptors = services.Where(d =>
			d.ServiceType == typeof(ISagaStore) &&
			d.IsKeyedService &&
			Equals(d.ServiceKey, "default")).ToList();
		defaultDescriptors.Count.ShouldBe(1);
		defaultDescriptors[0].KeyedImplementationInstance.ShouldBeSameAs(fakeSagaStore);
	}

	[Fact]
	public void NotOverrideExistingSagaCoordinator()
	{
		// Arrange
		var services = new ServiceCollection();
		var fakeCoordinator = A.Fake<ISagaCoordinator>();
		services.AddSingleton(fakeCoordinator);

		// Act
		services.AddExcaliburOrchestration();

		// Assert - should only have one registration (the original fake)
		var descriptors = services.Where(d => d.ServiceType == typeof(ISagaCoordinator)).ToList();
		descriptors.Count.ShouldBe(1);
		descriptors[0].ImplementationInstance.ShouldBeSameAs(fakeCoordinator);
	}

	[Fact]
	public void NotThrow_WhenCalledMultipleTimes()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert - should not throw
		Should.NotThrow(() =>
		{
			services.AddExcaliburOrchestration();
			services.AddExcaliburOrchestration();
			services.AddExcaliburOrchestration();
		});
	}

	[Fact]
	public void NotDuplicateMiddleware_WhenCalledMultipleTimes()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburOrchestration();
		services.AddExcaliburOrchestration();

		// Assert - verify only one middleware registration
		var middlewareDescriptors = services.Where(d =>
			d.ServiceType == typeof(IDispatchMiddleware) &&
			d.ImplementationType == typeof(SagaHandlingMiddleware)).ToList();
		middlewareDescriptors.Count.ShouldBe(1);
	}

	#endregion
}
