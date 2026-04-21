// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Saga.Abstractions;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Saga.Tests.DependencyInjection;

/// <summary>
/// Unit tests for <see cref="SagaDefinitionServiceCollectionExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SagaDefinitionServiceCollectionExtensionsShould : UnitTestBase
{
	// --- Test types ---

	private sealed class TestSagaData { }
	private sealed class OtherSagaData { }

	private sealed class ConcreteSagaDefinition : ISagaDefinition<TestSagaData>, ISagaDefinitionLifecycle<TestSagaData>
	{
		public string Name => "TestSaga";
		public TimeSpan Timeout => TimeSpan.FromMinutes(5);
		public IReadOnlyList<ISagaStep<TestSagaData>> Steps => [];
		public ISagaRetryPolicy? RetryPolicy => null;
		public Task OnCompletedAsync(ISagaContext<TestSagaData> context, CancellationToken cancellationToken) =>
			Task.CompletedTask;
		public Task OnFailedAsync(ISagaContext<TestSagaData> context, Exception exception, CancellationToken cancellationToken) =>
			Task.CompletedTask;
	}

	private sealed class MultiInterfaceSagaDefinition
		: ISagaDefinition<TestSagaData>, ISagaDefinitionLifecycle<TestSagaData>,
		  ISagaDefinition<OtherSagaData>, ISagaDefinitionLifecycle<OtherSagaData>
	{
		string ISagaDefinition<TestSagaData>.Name => "Multi1";
		TimeSpan ISagaDefinition<TestSagaData>.Timeout => TimeSpan.FromMinutes(1);
		IReadOnlyList<ISagaStep<TestSagaData>> ISagaDefinition<TestSagaData>.Steps => [];
		ISagaRetryPolicy? ISagaDefinition<TestSagaData>.RetryPolicy => null;
		Task ISagaDefinitionLifecycle<TestSagaData>.OnCompletedAsync(ISagaContext<TestSagaData> context, CancellationToken cancellationToken) =>
			Task.CompletedTask;
		Task ISagaDefinitionLifecycle<TestSagaData>.OnFailedAsync(ISagaContext<TestSagaData> context, Exception exception, CancellationToken cancellationToken) =>
			Task.CompletedTask;

		string ISagaDefinition<OtherSagaData>.Name => "Multi2";
		TimeSpan ISagaDefinition<OtherSagaData>.Timeout => TimeSpan.FromMinutes(1);
		IReadOnlyList<ISagaStep<OtherSagaData>> ISagaDefinition<OtherSagaData>.Steps => [];
		ISagaRetryPolicy? ISagaDefinition<OtherSagaData>.RetryPolicy => null;
		Task ISagaDefinitionLifecycle<OtherSagaData>.OnCompletedAsync(ISagaContext<OtherSagaData> context, CancellationToken cancellationToken) =>
			Task.CompletedTask;
		Task ISagaDefinitionLifecycle<OtherSagaData>.OnFailedAsync(ISagaContext<OtherSagaData> context, Exception exception, CancellationToken cancellationToken) =>
			Task.CompletedTask;
	}

	private abstract class AbstractSagaDefinition : ISagaDefinition<TestSagaData>, ISagaDefinitionLifecycle<TestSagaData>
	{
		public abstract string Name { get; }
		public abstract TimeSpan Timeout { get; }
		public abstract IReadOnlyList<ISagaStep<TestSagaData>> Steps { get; }
		public abstract ISagaRetryPolicy? RetryPolicy { get; }
		public abstract Task OnCompletedAsync(ISagaContext<TestSagaData> context, CancellationToken cancellationToken);
		public abstract Task OnFailedAsync(ISagaContext<TestSagaData> context, Exception exception, CancellationToken cancellationToken);
	}

	// --- Null guard tests ---

	[Fact]
	public void ThrowOnNullServices()
	{
		Should.Throw<ArgumentNullException>(() =>
			SagaDefinitionServiceCollectionExtensions.AddSagaDefinitionsFromAssembly(
				null!, typeof(ConcreteSagaDefinition).Assembly));
	}

	[Fact]
	public void ThrowOnNullAssembly()
	{
		var services = new ServiceCollection();
		Should.Throw<ArgumentNullException>(() =>
			services.AddSagaDefinitionsFromAssembly(null!));
	}

	// --- Registration tests ---

	[Fact]
	public void RegisterConcreteDefinitionType()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddSagaDefinitionsFromAssembly(typeof(ConcreteSagaDefinition).Assembly);

		// Assert - concrete type registered
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(ConcreteSagaDefinition) &&
			sd.ImplementationType == typeof(ConcreteSagaDefinition));
	}

	[Fact]
	public void RegisterClosedSagaDefinitionInterface()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddSagaDefinitionsFromAssembly(typeof(ConcreteSagaDefinition).Assembly);

		// Assert - ISagaDefinition<TestSagaData> registered
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(ISagaDefinition<TestSagaData>));
	}

	[Fact]
	public void RegisterMultipleClosedInterfaces()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddSagaDefinitionsFromAssembly(typeof(MultiInterfaceSagaDefinition).Assembly);

		// Assert - both closed interfaces registered for the multi-interface type
		services.Where(sd => sd.ServiceType == typeof(ISagaDefinition<TestSagaData>))
			.ShouldNotBeEmpty();
		services.Where(sd => sd.ServiceType == typeof(ISagaDefinition<OtherSagaData>))
			.ShouldNotBeEmpty();
	}

	[Fact]
	public void SkipAbstractClasses()
	{
		var services = new ServiceCollection();

		services.AddSagaDefinitionsFromAssembly(typeof(AbstractSagaDefinition).Assembly);

		services.ShouldNotContain(sd =>
			sd.ServiceType == typeof(AbstractSagaDefinition));
	}

	// --- Idempotency ---

	[Fact]
	public void NotDuplicateRegistrationsWhenCalledTwice()
	{
		var services = new ServiceCollection();
		var assembly = typeof(ConcreteSagaDefinition).Assembly;

		services.AddSagaDefinitionsFromAssembly(assembly);
		services.AddSagaDefinitionsFromAssembly(assembly);

		services.Count(sd =>
			sd.ServiceType == typeof(ConcreteSagaDefinition) &&
			sd.ImplementationType == typeof(ConcreteSagaDefinition)).ShouldBe(1);
	}

	// --- Lifetime ---

	[Fact]
	public void DefaultToSingletonLifetime()
	{
		var services = new ServiceCollection();

		services.AddSagaDefinitionsFromAssembly(typeof(ConcreteSagaDefinition).Assembly);

		services.ShouldContain(sd =>
			sd.ServiceType == typeof(ConcreteSagaDefinition) &&
			sd.Lifetime == ServiceLifetime.Singleton);
	}

	[Fact]
	public void RespectCustomLifetime()
	{
		var services = new ServiceCollection();

		services.AddSagaDefinitionsFromAssembly(
			typeof(ConcreteSagaDefinition).Assembly,
			ServiceLifetime.Transient);

		services.ShouldContain(sd =>
			sd.ServiceType == typeof(ConcreteSagaDefinition) &&
			sd.Lifetime == ServiceLifetime.Transient);
	}

	// --- Fluent chaining ---

	[Fact]
	public void ReturnSameServiceCollection()
	{
		var services = new ServiceCollection();

		var result = services.AddSagaDefinitionsFromAssembly(
			typeof(ConcreteSagaDefinition).Assembly);

		result.ShouldBeSameAs(services);
	}

	// --- Empty assembly ---

	[Fact]
	public void HandleAssemblyWithNoDefinitionsGracefully()
	{
		var services = new ServiceCollection();
		var emptyAssembly = typeof(int).Assembly;
		var countBefore = services.Count;

		services.AddSagaDefinitionsFromAssembly(emptyAssembly);

		services.Count.ShouldBe(countBefore);
	}
}
