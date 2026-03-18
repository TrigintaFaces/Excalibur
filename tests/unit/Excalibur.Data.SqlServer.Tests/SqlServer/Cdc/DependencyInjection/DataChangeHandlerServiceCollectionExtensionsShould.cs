// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Cdc.SqlServer;

namespace Excalibur.Data.Tests.SqlServer.Cdc.DependencyInjection;

/// <summary>
/// Unit tests for <see cref="DataChangeHandlerServiceCollectionExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Database", "SqlServer")]
public sealed class DataChangeHandlerServiceCollectionExtensionsShould : UnitTestBase
{
	// --- Test types ---

	private sealed class ConcreteChangeHandlerA : IDataChangeHandler
	{
		public string[] TableNames => ["dbo.Orders"];
		public Task HandleAsync(DataChangeEvent changeEvent, CancellationToken cancellationToken) =>
			Task.CompletedTask;
	}

	private sealed class ConcreteChangeHandlerB : IDataChangeHandler
	{
		public string[] TableNames => ["dbo.Customers"];
		public Task HandleAsync(DataChangeEvent changeEvent, CancellationToken cancellationToken) =>
			Task.CompletedTask;
	}

	private abstract class AbstractChangeHandler : IDataChangeHandler
	{
		public abstract string[] TableNames { get; }
		public abstract Task HandleAsync(DataChangeEvent changeEvent, CancellationToken cancellationToken);
	}

	private interface IExtendedChangeHandler : IDataChangeHandler { }

	// --- Null guards ---

	[Fact]
	public void ThrowOnNullServices()
	{
		Should.Throw<ArgumentNullException>(() =>
			DataChangeHandlerServiceCollectionExtensions.AddDataChangeHandlersFromAssembly(
				null!, typeof(ConcreteChangeHandlerA).Assembly));
	}

	[Fact]
	public void ThrowOnNullAssembly()
	{
		var services = new ServiceCollection();
		Should.Throw<ArgumentNullException>(() =>
			services.AddDataChangeHandlersFromAssembly(null!));
	}

	// --- Registration ---

	[Fact]
	public void RegisterConcreteHandlers()
	{
		var services = new ServiceCollection();

		services.AddDataChangeHandlersFromAssembly(typeof(ConcreteChangeHandlerA).Assembly);

		services.ShouldContain(sd =>
			sd.ServiceType == typeof(ConcreteChangeHandlerA) &&
			sd.ImplementationType == typeof(ConcreteChangeHandlerA));
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(ConcreteChangeHandlerB) &&
			sd.ImplementationType == typeof(ConcreteChangeHandlerB));
	}

	[Fact]
	public void RegisterHandlersAsIDataChangeHandlerForEnumerableResolution()
	{
		var services = new ServiceCollection();

		services.AddDataChangeHandlersFromAssembly(typeof(ConcreteChangeHandlerA).Assembly);

		services.Where(sd => sd.ServiceType == typeof(IDataChangeHandler))
			.Count().ShouldBeGreaterThanOrEqualTo(2);
	}

	[Fact]
	public void SkipAbstractClasses()
	{
		var services = new ServiceCollection();

		services.AddDataChangeHandlersFromAssembly(typeof(AbstractChangeHandler).Assembly);

		services.ShouldNotContain(sd =>
			sd.ServiceType == typeof(AbstractChangeHandler));
	}

	[Fact]
	public void SkipInterfaces()
	{
		var services = new ServiceCollection();

		services.AddDataChangeHandlersFromAssembly(typeof(IExtendedChangeHandler).Assembly);

		services.ShouldNotContain(sd =>
			sd.ServiceType == typeof(IExtendedChangeHandler));
	}

	// --- Idempotency ---

	[Fact]
	public void NotDuplicateRegistrationsWhenCalledTwice()
	{
		var services = new ServiceCollection();
		var assembly = typeof(ConcreteChangeHandlerA).Assembly;

		services.AddDataChangeHandlersFromAssembly(assembly);
		services.AddDataChangeHandlersFromAssembly(assembly);

		services.Count(sd =>
			sd.ServiceType == typeof(ConcreteChangeHandlerA) &&
			sd.ImplementationType == typeof(ConcreteChangeHandlerA)).ShouldBe(1);
	}

	// --- Lifetime ---

	[Fact]
	public void DefaultToSingletonLifetime()
	{
		var services = new ServiceCollection();

		services.AddDataChangeHandlersFromAssembly(typeof(ConcreteChangeHandlerA).Assembly);

		services.ShouldContain(sd =>
			sd.ServiceType == typeof(ConcreteChangeHandlerA) &&
			sd.Lifetime == ServiceLifetime.Singleton);
	}

	[Fact]
	public void RespectCustomLifetime()
	{
		var services = new ServiceCollection();

		services.AddDataChangeHandlersFromAssembly(
			typeof(ConcreteChangeHandlerA).Assembly,
			ServiceLifetime.Transient);

		services.ShouldContain(sd =>
			sd.ServiceType == typeof(ConcreteChangeHandlerA) &&
			sd.Lifetime == ServiceLifetime.Transient);
	}

	// --- Fluent chaining ---

	[Fact]
	public void ReturnSameServiceCollection()
	{
		var services = new ServiceCollection();

		var result = services.AddDataChangeHandlersFromAssembly(
			typeof(ConcreteChangeHandlerA).Assembly);

		result.ShouldBeSameAs(services);
	}

	// --- Empty assembly ---

	[Fact]
	public void HandleAssemblyWithNoHandlersGracefully()
	{
		var services = new ServiceCollection();
		var emptyAssembly = typeof(int).Assembly;
		var countBefore = services.Count;

		services.AddDataChangeHandlersFromAssembly(emptyAssembly);

		services.Count.ShouldBe(countBefore);
	}
}
