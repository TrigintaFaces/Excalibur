// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Testing;
using Excalibur.Dispatch.Testing.Tracking;

namespace Excalibur.Dispatch.Testing.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Excalibur.Dispatch.Testing")]
public sealed class DispatchTestHarnessShould
{
	[Fact]
	public void ImplementIAsyncDisposable()
	{
		var harness = new DispatchTestHarness();
		harness.ShouldBeAssignableTo<IAsyncDisposable>();
	}

	[Fact]
	public void ExposeDispatchedMessageLog()
	{
		var harness = new DispatchTestHarness();
		harness.Dispatched.ShouldNotBeNull();
		harness.Dispatched.ShouldBeAssignableTo<IDispatchedMessageLog>();
	}

	[Fact]
	public void ReturnSameDispatchedLogInstance()
	{
		var harness = new DispatchTestHarness();
		var log1 = harness.Dispatched;
		var log2 = harness.Dispatched;
		log1.ShouldBeSameAs(log2);
	}

	[Fact]
	public void AllowFluentConfigureServicesChaining()
	{
		var harness = new DispatchTestHarness();
		var returned = harness.ConfigureServices(_ => { });
		returned.ShouldBeSameAs(harness);
	}

	[Fact]
	public void AllowFluentConfigureDispatchChaining()
	{
		var harness = new DispatchTestHarness();
		var returned = harness.ConfigureDispatch(_ => { });
		returned.ShouldBeSameAs(harness);
	}

	[Fact]
	public void BuildServiceProviderOnFirstDispatcherAccess()
	{
		var harness = new DispatchTestHarness();
		var dispatcher = harness.Dispatcher;
		dispatcher.ShouldNotBeNull();
		dispatcher.ShouldBeAssignableTo<IDispatcher>();
	}

	[Fact]
	public void BuildServiceProviderOnFirstServicesAccess()
	{
		var harness = new DispatchTestHarness();
		var services = harness.Services;
		services.ShouldNotBeNull();
		services.ShouldBeAssignableTo<IServiceProvider>();
	}

	[Fact]
	public void ReturnSameDispatcherOnMultipleAccesses()
	{
		var harness = new DispatchTestHarness();
		var d1 = harness.Dispatcher;
		var d2 = harness.Dispatcher;
		d1.ShouldBeSameAs(d2);
	}

	[Fact]
	public void ThrowWhenConfigureServicesCalledAfterBuild()
	{
		var harness = new DispatchTestHarness();
		_ = harness.Dispatcher; // triggers build

		Should.Throw<InvalidOperationException>(() =>
			harness.ConfigureServices(_ => { }));
	}

	[Fact]
	public void ThrowWhenConfigureDispatchCalledAfterBuild()
	{
		var harness = new DispatchTestHarness();
		_ = harness.Dispatcher; // triggers build

		Should.Throw<InvalidOperationException>(() =>
			harness.ConfigureDispatch(_ => { }));
	}

	[Fact]
	public void ThrowWhenConfigureServicesPassedNull()
	{
		var harness = new DispatchTestHarness();
		Should.Throw<ArgumentNullException>(() =>
			harness.ConfigureServices(null!));
	}

	[Fact]
	public void ThrowWhenConfigureDispatchPassedNull()
	{
		var harness = new DispatchTestHarness();
		Should.Throw<ArgumentNullException>(() =>
			harness.ConfigureDispatch(null!));
	}

	[Fact]
	public async Task DisposeCleanlyWhenNotBuilt()
	{
		var harness = new DispatchTestHarness();
		await harness.DisposeAsync();
		// Should not throw
	}

	[Fact]
	public async Task DisposeCleanlyWhenBuilt()
	{
		var harness = new DispatchTestHarness();
		_ = harness.Dispatcher; // triggers build
		await harness.DisposeAsync();
		// Should not throw
	}

	[Fact]
	public async Task ThrowOnDispatcherAccessAfterDispose()
	{
		var harness = new DispatchTestHarness();
		await harness.DisposeAsync();

		Should.Throw<ObjectDisposedException>(() =>
			_ = harness.Dispatcher);
	}

	[Fact]
	public async Task ThrowOnServicesAccessAfterDispose()
	{
		var harness = new DispatchTestHarness();
		await harness.DisposeAsync();

		Should.Throw<ObjectDisposedException>(() =>
			_ = harness.Services);
	}

	[Fact]
	public async Task ThrowOnConfigureServicesAfterDispose()
	{
		var harness = new DispatchTestHarness();
		await harness.DisposeAsync();

		Should.Throw<ObjectDisposedException>(() =>
			harness.ConfigureServices(_ => { }));
	}

	[Fact]
	public async Task AllowDoubleDispose()
	{
		var harness = new DispatchTestHarness();
		_ = harness.Dispatcher; // triggers build
		await harness.DisposeAsync();
		await harness.DisposeAsync(); // Should not throw
	}

	[Fact]
	public void AllowResolvingCustomServices()
	{
		var harness = new DispatchTestHarness()
			.ConfigureServices(services =>
				services.AddSingleton<ICustomTestService, CustomTestService>());

		var service = harness.Services.GetService<ICustomTestService>();
		service.ShouldNotBeNull();
		service.ShouldBeOfType<CustomTestService>();
	}

	[Fact]
	public void ResolveTrackingMiddlewareAsDispatchMiddleware()
	{
		var harness = new DispatchTestHarness();
		var middleware = harness.Services.GetService<IDispatchMiddleware>();
		middleware.ShouldNotBeNull();
		middleware.ShouldBeOfType<TestTrackingMiddleware>();
	}

	[Fact]
	public void CreateScopeReturnsNewScope()
	{
		var harness = new DispatchTestHarness();
		using var scope = harness.CreateScope();
		scope.ShouldNotBeNull();
		scope.ServiceProvider.ShouldNotBeNull();
	}

	[Fact]
	public void CreateScopeTriggersBuilt()
	{
		var harness = new DispatchTestHarness();
		using var scope = harness.CreateScope();

		// After CreateScope, the harness is built and Dispatcher should be accessible
		var dispatcher = harness.Dispatcher;
		dispatcher.ShouldNotBeNull();
	}

	[Fact]
	public void ProvideScopedServiceIsolationBetweenScopes()
	{
		var harness = new DispatchTestHarness()
			.ConfigureServices(services =>
				services.AddScoped<IScopedTestService, ScopedTestService>());

		using var scope1 = harness.CreateScope();
		using var scope2 = harness.CreateScope();

		var svc1 = scope1.ServiceProvider.GetRequiredService<IScopedTestService>();
		var svc2 = scope2.ServiceProvider.GetRequiredService<IScopedTestService>();

		// Different scopes should provide different instances of scoped services
		svc1.ShouldNotBeSameAs(svc2);
	}

	[Fact]
	public void ReturnSameScopedInstanceWithinOneScope()
	{
		var harness = new DispatchTestHarness()
			.ConfigureServices(services =>
				services.AddScoped<IScopedTestService, ScopedTestService>());

		using var scope = harness.CreateScope();

		var svc1 = scope.ServiceProvider.GetRequiredService<IScopedTestService>();
		var svc2 = scope.ServiceProvider.GetRequiredService<IScopedTestService>();

		// Same scope should return the same instance
		svc1.ShouldBeSameAs(svc2);
	}

	[Fact]
	public void ShareSingletonServiceAcrossScopes()
	{
		var harness = new DispatchTestHarness()
			.ConfigureServices(services =>
				services.AddSingleton<ICustomTestService, CustomTestService>());

		using var scope1 = harness.CreateScope();
		using var scope2 = harness.CreateScope();

		var svc1 = scope1.ServiceProvider.GetRequiredService<ICustomTestService>();
		var svc2 = scope2.ServiceProvider.GetRequiredService<ICustomTestService>();

		// Singletons should be the same instance across scopes
		svc1.ShouldBeSameAs(svc2);
	}

	[Fact]
	public async Task ThrowOnCreateScopeAfterDispose()
	{
		var harness = new DispatchTestHarness();
		await harness.DisposeAsync();

		Should.Throw<ObjectDisposedException>(() =>
			harness.CreateScope());
	}

	[Fact]
	public void ResolveDispatcherFromScope()
	{
		var harness = new DispatchTestHarness();

		using var scope = harness.CreateScope();
		var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
		dispatcher.ShouldNotBeNull();
	}

	private interface ICustomTestService
	{
		string Name { get; }
	}

	private sealed class CustomTestService : ICustomTestService
	{
		public string Name => "Test";
	}

	private interface IScopedTestService
	{
		Guid InstanceId { get; }
	}

	private sealed class ScopedTestService : IScopedTestService
	{
		public Guid InstanceId { get; } = Guid.NewGuid();
	}
}
