// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Messaging;
using Excalibur.Saga.Orchestration;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Saga.Tests.Orchestration;

/// <summary>
/// Unit tests for <see cref="OrchestrationDispatchBuilderExtensions"/>.
/// Verifies dispatch builder extension behavior.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class OrchestrationDispatchBuilderExtensionsShould
{
	#region AddExcaliburOrchestration Tests

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			OrchestrationDispatchBuilderExtensions.AddExcaliburOrchestration(null!));
	}

	[Fact]
	public void ReturnBuilder_ForChaining()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		// Act
		var result = builder.AddExcaliburOrchestration();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void NotRegisterSagaStoreImplicitly_ViaBuilder()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		// Act
		builder.AddExcaliburOrchestration();

		// Assert (iuv3s1) - the builder orchestration path registers NO in-memory store implicitly
		// (delegates to services.AddExcaliburOrchestration()). RED on the pre-fix code.
		services.ShouldNotContain(d => d.ServiceType == typeof(ISagaStore) && d.IsKeyedService);
		services.ShouldNotContain(d => d.ServiceType == typeof(InMemorySagaStore));
	}

	[Fact]
	public void RegisterSagaCoordinator_ViaBuilder()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		// Act
		builder.AddExcaliburOrchestration();

		// Assert - verify registration exists
		var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ISagaCoordinator));
		descriptor.ShouldNotBeNull();
		descriptor.ImplementationType.ShouldBe(typeof(SagaCoordinator));
	}

	[Fact]
	public void RegisterSagaHandlingMiddleware_ViaBuilder()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		// Act
		builder.AddExcaliburOrchestration();

		// Assert - verify the middleware is registered
		var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IDispatchMiddleware));
		descriptor.ShouldNotBeNull();
		descriptor.ImplementationType.ShouldBe(typeof(SagaHandlingMiddleware));
	}

	[Fact]
	public void CallServicesAddExcaliburOrchestration()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		// Act
		builder.AddExcaliburOrchestration();

		// Assert - verify services were accessed
		A.CallTo(() => builder.Services).MustHaveHappened();
	}

	[Fact]
	public void NotThrow_WhenCalledMultipleTimes()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		// Act & Assert - should not throw
		Should.NotThrow(() =>
		{
			builder.AddExcaliburOrchestration();
			builder.AddExcaliburOrchestration();
		});
	}

	#endregion
}
