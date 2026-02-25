// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Dispatch.Testing;

namespace Excalibur.Dispatch.Testing.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Testing")]
public sealed class SagaTestHarnessShould
{
	#region ConfigureServices

	[Fact]
	public void ReturnSelfFromConfigureServices()
	{
		var harness = new SagaTestHarness<TestSaga>();
		var returned = harness.ConfigureServices(_ => { });
		returned.ShouldBeSameAs(harness);
	}

	[Fact]
	public void ThrowOnNullConfigureServices()
	{
		var harness = new SagaTestHarness<TestSaga>();
		Should.Throw<ArgumentNullException>(() => harness.ConfigureServices(null!));
	}

	[Fact]
	public void ThrowWhenConfigureServicesCalledAfterBuild()
	{
		var harness = new SagaTestHarness<TestSaga>();
		_ = harness.Saga; // triggers build
		Should.Throw<InvalidOperationException>(() => harness.ConfigureServices(_ => { }));
	}

	[Fact]
	public async Task ThrowWhenConfigureServicesCalledAfterDispose()
	{
		var harness = new SagaTestHarness<TestSaga>();
		await harness.DisposeAsync();
		Should.Throw<ObjectDisposedException>(() => harness.ConfigureServices(_ => { }));
	}

	#endregion

	#region Saga property

	[Fact]
	public void ResolveSagaInstance()
	{
		var harness = new SagaTestHarness<TestSaga>();
		harness.Saga.ShouldNotBeNull();
		harness.Saga.ShouldBeOfType<TestSaga>();
	}

	[Fact]
	public async Task ThrowOnSagaAccessAfterDispose()
	{
		var harness = new SagaTestHarness<TestSaga>();
		await harness.DisposeAsync();
		Should.Throw<ObjectDisposedException>(() => _ = harness.Saga);
	}

	#endregion

	#region Services property

	[Fact]
	public void ExposeServiceProvider()
	{
		var harness = new SagaTestHarness<TestSaga>();
		harness.Services.ShouldNotBeNull();
	}

	[Fact]
	public async Task ThrowOnServicesAccessAfterDispose()
	{
		var harness = new SagaTestHarness<TestSaga>();
		await harness.DisposeAsync();
		Should.Throw<ObjectDisposedException>(() => _ = harness.Services);
	}

	#endregion

	#region SendAsync

	[Fact]
	public async Task SendHandledEvent()
	{
		await using var harness = new SagaTestHarness<TestSaga>();
		await harness.SendAsync(new TestSagaEvent("step"));
		harness.Saga.LastEvent.ShouldBe("step");
	}

	[Fact]
	public async Task RecordProcessedEvents()
	{
		await using var harness = new SagaTestHarness<TestSaga>();
		await harness.SendAsync(new TestSagaEvent("a"));
		await harness.SendAsync(new TestSagaEvent("b"));
		harness.ProcessedEvents.Count.ShouldBe(2);
	}

	[Fact]
	public async Task ThrowOnNullEvent()
	{
		await using var harness = new SagaTestHarness<TestSaga>();
		await Should.ThrowAsync<ArgumentNullException>(() => harness.SendAsync(null!));
	}

	[Fact]
	public async Task ThrowWhenSagaDoesNotHandleEvent()
	{
		await using var harness = new SagaTestHarness<TestSaga>();
		await Should.ThrowAsync<InvalidOperationException>(() =>
			harness.SendAsync("unhandled-event-type"));
	}

	[Fact]
	public async Task ThrowOnSendAsyncAfterDispose()
	{
		var harness = new SagaTestHarness<TestSaga>();
		await harness.DisposeAsync();
		await Should.ThrowAsync<ObjectDisposedException>(() =>
			harness.SendAsync(new TestSagaEvent("x")));
	}

	#endregion

	#region ForceSendAsync

	[Fact]
	public async Task ForceSendAnyEvent()
	{
		await using var harness = new SagaTestHarness<TestSaga>();
		await harness.ForceSendAsync(new TestSagaEvent("forced"));
		harness.Saga.LastEvent.ShouldBe("forced");
		harness.ProcessedEvents.Count.ShouldBe(1);
	}

	[Fact]
	public async Task ThrowOnNullForceSend()
	{
		await using var harness = new SagaTestHarness<TestSaga>();
		await Should.ThrowAsync<ArgumentNullException>(() => harness.ForceSendAsync(null!));
	}

	[Fact]
	public async Task ThrowOnForceSendAfterDispose()
	{
		var harness = new SagaTestHarness<TestSaga>();
		await harness.DisposeAsync();
		await Should.ThrowAsync<ObjectDisposedException>(() =>
			harness.ForceSendAsync(new TestSagaEvent("x")));
	}

	#endregion

	#region HandlesEvent

	[Fact]
	public void ReturnTrueForHandledEventType()
	{
		var harness = new SagaTestHarness<TestSaga>();
		harness.HandlesEvent(new TestSagaEvent("x")).ShouldBeTrue();
	}

	[Fact]
	public void ReturnFalseForUnhandledEventType()
	{
		var harness = new SagaTestHarness<TestSaga>();
		harness.HandlesEvent("not-handled").ShouldBeFalse();
	}

	[Fact]
	public void ThrowOnNullHandlesEvent()
	{
		var harness = new SagaTestHarness<TestSaga>();
		Should.Throw<ArgumentNullException>(() => harness.HandlesEvent(null!));
	}

	#endregion

	#region Dispose

	[Fact]
	public async Task DisposeCleanlyWhenNotBuilt()
	{
		var harness = new SagaTestHarness<TestSaga>();
		await harness.DisposeAsync();
	}

	[Fact]
	public async Task DisposeCleanlyWhenBuilt()
	{
		var harness = new SagaTestHarness<TestSaga>();
		_ = harness.Saga;
		await harness.DisposeAsync();
	}

	[Fact]
	public async Task AllowDoubleDispose()
	{
		var harness = new SagaTestHarness<TestSaga>();
		_ = harness.Saga;
		await harness.DisposeAsync();
		await harness.DisposeAsync();
	}

	#endregion

	#region Test doubles

	private sealed record TestSagaEvent(string Name);

	private sealed class TestSaga : ISaga
	{
		public Guid Id { get; } = Guid.NewGuid();
		public bool IsCompleted { get; private set; }
		public string? LastEvent { get; private set; }

		public bool HandlesEvent(object eventMessage) => eventMessage is TestSagaEvent;

		public Task HandleAsync(object eventMessage, CancellationToken cancellationToken)
		{
			if (eventMessage is TestSagaEvent evt)
			{
				LastEvent = evt.Name;
				if (evt.Name == "complete")
				{
					IsCompleted = true;
				}
			}

			return Task.CompletedTask;
		}
	}

	#endregion
}
