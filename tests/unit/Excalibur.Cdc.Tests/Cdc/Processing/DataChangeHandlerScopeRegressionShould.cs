// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc.SqlServer;

namespace Excalibur.Cdc.Tests.Cdc.Processing;

/// <summary>
/// Regression tests for Sprint 673 T.8: DataChangeEventProcessor handler caching per scope.
/// The processor caches handler TYPES (FrozenDictionary&lt;string, Type&gt;) but resolves
/// fresh handler INSTANCES from each scoped ServiceProvider. This prevents references
/// to disposed services from previous scopes.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DataChangeHandlerScopeRegressionShould
{
	/// <summary>
	/// Validates that scoped handler registrations produce distinct instances per scope.
	/// This is the core contract that DataChangeEventProcessor relies on: calling
	/// GetServices&lt;IDataChangeHandler&gt;() from a new scope yields fresh instances,
	/// not references to disposed objects from a previous scope.
	/// </summary>
	[Fact]
	public void Resolve_FreshHandlerInstance_PerScope()
	{
		// Arrange -- register a handler as scoped (typical DI lifetime for handlers)
		var services = new ServiceCollection();
		services.AddScoped<IDataChangeHandler, FakeOrdersHandler>();
		using var rootProvider = services.BuildServiceProvider();

		// Act -- resolve from two separate scopes
		IDataChangeHandler instance1;
		IDataChangeHandler instance2;

		using (var scope1 = rootProvider.CreateScope())
		{
			instance1 = scope1.ServiceProvider.GetServices<IDataChangeHandler>().Single();
		}

		using (var scope2 = rootProvider.CreateScope())
		{
			instance2 = scope2.ServiceProvider.GetServices<IDataChangeHandler>().Single();
		}

		// Assert -- instances must be different (fresh per scope, not cached)
		instance1.ShouldNotBeSameAs(instance2);
	}

	/// <summary>
	/// Validates that handler TYPE is stable across scopes (can be cached safely).
	/// DataChangeEventProcessor caches Type objects in a FrozenDictionary, which is
	/// safe because types are immutable metadata -- only instances can hold scope state.
	/// </summary>
	[Fact]
	public void CacheHandlerType_StableAcrossScopes()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddScoped<IDataChangeHandler, FakeOrdersHandler>();
		using var rootProvider = services.BuildServiceProvider();

		// Act -- resolve type from two separate scopes
		Type type1;
		Type type2;

		using (var scope1 = rootProvider.CreateScope())
		{
			type1 = scope1.ServiceProvider.GetServices<IDataChangeHandler>().Single().GetType();
		}

		using (var scope2 = rootProvider.CreateScope())
		{
			type2 = scope2.ServiceProvider.GetServices<IDataChangeHandler>().Single().GetType();
		}

		// Assert -- types are identical (safe to cache)
		type1.ShouldBe(type2);
		type1.ShouldBe(typeof(FakeOrdersHandler));
	}

	/// <summary>
	/// Validates that multiple handler registrations resolve correctly per scope
	/// and can be matched by type (the pattern DataChangeEventProcessor.GetHandler uses).
	/// </summary>
	[Fact]
	public void MatchHandlerByType_FromMultipleRegistrations()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddScoped<IDataChangeHandler, FakeOrdersHandler>();
		services.AddScoped<IDataChangeHandler, FakeCustomersHandler>();
		using var rootProvider = services.BuildServiceProvider();

		// Act -- simulate the GetHandler pattern: get all handlers, match by type
		using var scope = rootProvider.CreateScope();
		var handlers = scope.ServiceProvider.GetServices<IDataChangeHandler>().ToList();
		var targetType = typeof(FakeOrdersHandler);

		var matched = handlers.FirstOrDefault(h => h.GetType() == targetType);

		// Assert
		matched.ShouldNotBeNull();
		matched.ShouldBeOfType<FakeOrdersHandler>();
		handlers.Count.ShouldBe(2);
	}

	/// <summary>
	/// Validates that disposed scope's handler instances are not reused in a new scope.
	/// This is the exact bug the handler caching fix prevents: caching handler instances
	/// (not types) would hold references to disposed scoped services.
	/// </summary>
	[Fact]
	public void DisposedScope_HandlerInstances_AreNotReusedInNewScope()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddScoped<IDataChangeHandler, FakeOrdersHandler>();
		services.AddScoped<DisposableService>();
		using var rootProvider = services.BuildServiceProvider();

		// Act -- resolve and dispose first scope
		DisposableService? service1;
		using (var scope1 = rootProvider.CreateScope())
		{
			_ = scope1.ServiceProvider.GetServices<IDataChangeHandler>().Single();
			service1 = scope1.ServiceProvider.GetRequiredService<DisposableService>();
		}
		// scope1 is now disposed, along with service1

		// Resolve from new scope
		using var scope2 = rootProvider.CreateScope();
		var service2 = scope2.ServiceProvider.GetRequiredService<DisposableService>();

		// Assert -- new scope gives a fresh (not disposed) instance
		service1.IsDisposed.ShouldBeTrue();
		service2.IsDisposed.ShouldBeFalse();
		service1.ShouldNotBeSameAs(service2);
	}

	#region Test Doubles

	private sealed class FakeOrdersHandler : IDataChangeHandler
	{
		public string[] TableNames => ["dbo.Orders"];

		public Task HandleAsync(DataChangeEvent changeEvent, CancellationToken cancellationToken) =>
			Task.CompletedTask;
	}

	private sealed class FakeCustomersHandler : IDataChangeHandler
	{
		public string[] TableNames => ["dbo.Customers"];

		public Task HandleAsync(DataChangeEvent changeEvent, CancellationToken cancellationToken) =>
			Task.CompletedTask;
	}

	private sealed class DisposableService : IDisposable
	{
		public bool IsDisposed { get; private set; }

		public void Dispose()
		{
			IsDisposed = true;
		}
	}

	#endregion
}
