// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Testing;

namespace Excalibur.Dispatch.Testing.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Testing")]
public sealed class HandlerTestHarnessShould
{
	#region ConfigureServices

	[Fact]
	public void ReturnSelfFromConfigureServices()
	{
		var harness = new HandlerTestHarness<TestVoidHandler>();
		var returned = harness.ConfigureServices(_ => { });
		returned.ShouldBeSameAs(harness);
	}

	[Fact]
	public void ThrowOnNullConfigureServices()
	{
		var harness = new HandlerTestHarness<TestVoidHandler>();
		Should.Throw<ArgumentNullException>(() => harness.ConfigureServices(null!));
	}

	[Fact]
	public void ThrowWhenConfigureServicesCalledAfterBuild()
	{
		var harness = new HandlerTestHarness<TestVoidHandler>();
		_ = harness.Handler; // triggers build
		Should.Throw<InvalidOperationException>(() => harness.ConfigureServices(_ => { }));
	}

	[Fact]
	public async Task ThrowWhenConfigureServicesCalledAfterDispose()
	{
		var harness = new HandlerTestHarness<TestVoidHandler>();
		await harness.DisposeAsync();
		Should.Throw<ObjectDisposedException>(() => harness.ConfigureServices(_ => { }));
	}

	#endregion

	#region Handler property

	[Fact]
	public void ResolveHandlerInstance()
	{
		var harness = new HandlerTestHarness<TestVoidHandler>();
		harness.Handler.ShouldNotBeNull();
		harness.Handler.ShouldBeOfType<TestVoidHandler>();
	}

	[Fact]
	public async Task ThrowOnHandlerAccessAfterDispose()
	{
		var harness = new HandlerTestHarness<TestVoidHandler>();
		await harness.DisposeAsync();
		Should.Throw<ObjectDisposedException>(() => _ = harness.Handler);
	}

	#endregion

	#region Services property

	[Fact]
	public void ExposeServiceProvider()
	{
		var harness = new HandlerTestHarness<TestVoidHandler>();
		harness.Services.ShouldNotBeNull();
	}

	[Fact]
	public async Task ThrowOnServicesAccessAfterDispose()
	{
		var harness = new HandlerTestHarness<TestVoidHandler>();
		await harness.DisposeAsync();
		Should.Throw<ObjectDisposedException>(() => _ = harness.Services);
	}

	#endregion

	#region HandleAsync (void)

	[Fact]
	public async Task HandleVoidAction()
	{
		// Use shared state to verify handler was called, since Handler is transient
		var handled = new SharedState();
		var harness = new HandlerTestHarness<TestVoidHandlerWithSharedState>()
			.ConfigureServices(s => s.AddSingleton(handled));
		await harness.HandleAsync(new TestVoidAction());
		handled.WasCalled.ShouldBeTrue();
	}

	[Fact]
	public async Task ThrowOnNullVoidAction()
	{
		var harness = new HandlerTestHarness<TestVoidHandler>();
		await Should.ThrowAsync<ArgumentNullException>(() =>
			harness.HandleAsync<TestVoidAction>(null!));
	}

	[Fact]
	public async Task ThrowWhenHandlerDoesNotImplementVoidAction()
	{
		var harness = new HandlerTestHarness<TestVoidHandler>();
		await Should.ThrowAsync<InvalidOperationException>(() =>
			harness.HandleAsync(new UnhandledAction()));
	}

	#endregion

	#region HandleAsync (with result)

	[Fact]
	public async Task HandleActionWithResult()
	{
		var harness = new HandlerTestHarness<TestResultHandler>();
		var result = await harness.HandleAsync<TestResultAction, string>(new TestResultAction());
		result.ShouldBe("test-result");
	}

	[Fact]
	public async Task ThrowOnNullResultAction()
	{
		var harness = new HandlerTestHarness<TestResultHandler>();
		await Should.ThrowAsync<ArgumentNullException>(() =>
			harness.HandleAsync<TestResultAction, string>(null!));
	}

	[Fact]
	public async Task ThrowWhenHandlerDoesNotImplementResultAction()
	{
		var harness = new HandlerTestHarness<TestVoidHandler>();
		await Should.ThrowAsync<InvalidOperationException>(() =>
			harness.HandleAsync<TestResultAction, string>(new TestResultAction()));
	}

	#endregion

	#region CreateContextBuilder

	[Fact]
	public void CreateContextBuilder()
	{
		var harness = new HandlerTestHarness<TestVoidHandler>();
		var builder = harness.CreateContextBuilder();
		builder.ShouldNotBeNull();
		builder.ShouldBeOfType<MessageContextBuilder>();
	}

	#endregion

	#region Dispose

	[Fact]
	public async Task DisposeCleanlyWhenNotBuilt()
	{
		var harness = new HandlerTestHarness<TestVoidHandler>();
		await harness.DisposeAsync();
	}

	[Fact]
	public async Task DisposeCleanlyWhenBuilt()
	{
		var harness = new HandlerTestHarness<TestVoidHandler>();
		_ = harness.Handler;
		await harness.DisposeAsync();
	}

	[Fact]
	public async Task AllowDoubleDispose()
	{
		var harness = new HandlerTestHarness<TestVoidHandler>();
		_ = harness.Handler;
		await harness.DisposeAsync();
		await harness.DisposeAsync();
	}

	#endregion

	#region Custom service registration

	[Fact]
	public void ResolveCustomRegisteredService()
	{
		var harness = new HandlerTestHarness<TestHandlerWithDep>()
			.ConfigureServices(s => s.AddSingleton<ITestDependency, TestDependency>());

		harness.Handler.Dependency.ShouldNotBeNull();
	}

	#endregion

	#region Test doubles

	private sealed class TestVoidAction : IDispatchAction;
	private sealed class TestResultAction : IDispatchAction<string>;
	private sealed class UnhandledAction : IDispatchAction;

	private sealed class SharedState
	{
		public bool WasCalled { get; set; }
	}

	private sealed class TestVoidHandler : IActionHandler<TestVoidAction>
	{
		public bool Handled { get; private set; }

		public Task HandleAsync(TestVoidAction action, CancellationToken cancellationToken)
		{
			Handled = true;
			return Task.CompletedTask;
		}
	}

	private sealed class TestVoidHandlerWithSharedState(SharedState state) : IActionHandler<TestVoidAction>
	{
		public Task HandleAsync(TestVoidAction action, CancellationToken cancellationToken)
		{
			state.WasCalled = true;
			return Task.CompletedTask;
		}
	}

	private sealed class TestResultHandler : IActionHandler<TestResultAction, string>
	{
		public Task<string> HandleAsync(TestResultAction action, CancellationToken cancellationToken)
		{
			return Task.FromResult("test-result");
		}
	}

	private interface ITestDependency
	{
		string Value { get; }
	}

	private sealed class TestDependency : ITestDependency
	{
		public string Value => "dep-value";
	}

	private sealed class TestHandlerWithDep(ITestDependency dependency) : IActionHandler<TestVoidAction>
	{
		public ITestDependency Dependency { get; } = dependency;

		public Task HandleAsync(TestVoidAction action, CancellationToken cancellationToken)
			=> Task.CompletedTask;
	}

	#endregion
}
