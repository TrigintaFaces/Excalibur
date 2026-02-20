// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Abstractions.Messaging;
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
	#region AddDispatchOrchestration Tests

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			OrchestrationDispatchBuilderExtensions.AddDispatchOrchestration(null!));
	}

	[Fact]
	public void ReturnBuilder_ForChaining()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		// Act
		var result = builder.AddDispatchOrchestration();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void RegisterSagaStore_ViaBuilder()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		// Act
		builder.AddDispatchOrchestration();

		// Assert - verify registration exists
		var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ISagaStore));
		descriptor.ShouldNotBeNull();
		descriptor.ImplementationType.ShouldBe(typeof(InMemorySagaStore));
	}

	[Fact]
	public void RegisterSagaCoordinator_ViaBuilder()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		// Act
		builder.AddDispatchOrchestration();

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
		builder.AddDispatchOrchestration();

		// Assert - verify the middleware is registered
		var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IDispatchMiddleware));
		descriptor.ShouldNotBeNull();
		descriptor.ImplementationType.ShouldBe(typeof(SagaHandlingMiddleware));
	}

	[Fact]
	public void CallServicesAddDispatchOrchestration()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		// Act
		builder.AddDispatchOrchestration();

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
			builder.AddDispatchOrchestration();
			builder.AddDispatchOrchestration();
		});
	}

	#endregion
}
