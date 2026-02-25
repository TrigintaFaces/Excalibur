// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Messaging;
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
	#region AddDispatchOrchestration Tests

	[Fact]
	public void ReturnServiceCollection_ForChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddDispatchOrchestration();

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void RegisterSagaStore()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDispatchOrchestration();

		// Assert - verify registration exists
		var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ISagaStore));
		descriptor.ShouldNotBeNull();
		descriptor.ImplementationType.ShouldBe(typeof(InMemorySagaStore));
	}

	[Fact]
	public void RegisterSagaCoordinator()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDispatchOrchestration();

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
		services.AddDispatchOrchestration();

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
		services.AddDispatchOrchestration();

		// Assert - verify singleton lifetime
		var storeDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ISagaStore));
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
		services.AddSingleton(fakeSagaStore);

		// Act
		services.AddDispatchOrchestration();

		// Assert - should only have one registration (the original fake)
		var descriptors = services.Where(d => d.ServiceType == typeof(ISagaStore)).ToList();
		descriptors.Count.ShouldBe(1);
		descriptors[0].ImplementationInstance.ShouldBeSameAs(fakeSagaStore);
	}

	[Fact]
	public void NotOverrideExistingSagaCoordinator()
	{
		// Arrange
		var services = new ServiceCollection();
		var fakeCoordinator = A.Fake<ISagaCoordinator>();
		services.AddSingleton(fakeCoordinator);

		// Act
		services.AddDispatchOrchestration();

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
			services.AddDispatchOrchestration();
			services.AddDispatchOrchestration();
			services.AddDispatchOrchestration();
		});
	}

	[Fact]
	public void NotDuplicateMiddleware_WhenCalledMultipleTimes()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDispatchOrchestration();
		services.AddDispatchOrchestration();

		// Assert - verify only one middleware registration
		var middlewareDescriptors = services.Where(d =>
			d.ServiceType == typeof(IDispatchMiddleware) &&
			d.ImplementationType == typeof(SagaHandlingMiddleware)).ToList();
		middlewareDescriptors.Count.ShouldBe(1);
	}

	#endregion
}
