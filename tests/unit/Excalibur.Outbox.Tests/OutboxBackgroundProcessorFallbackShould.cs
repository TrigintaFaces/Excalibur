// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Outbox.Outbox;
using Excalibur.Outbox.Health;
using Microsoft.Extensions.Hosting;

namespace Excalibur.Outbox.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
[Trait("Pattern", "Regression")]
public sealed class OutboxBackgroundProcessorFallbackShould
{
	private static readonly TimeSpan Bound = TimeSpan.FromSeconds(5);

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	public async Task RecordActivityAndKnownSuccessesWithoutResettingObservedFailures(int delivered)
	{
		var state = new State { Delivered = delivered };
		var health = new BackgroundServiceHealthState();
		health.RecordCycle(0, 2);
		var previous = health.LastActivityTime;
		await using var provider = Build(state, services => services.AddSingleton(health));
		var worker = Worker(provider);
		try
		{
			await worker.StartAsync(CancellationToken.None);
			await state.Entered.Task.WaitAsync(Bound);
		}
		finally { await Drain(worker); }
		health.TotalCycles.ShouldBe(2);
		health.TotalProcessed.ShouldBe(delivered);
		health.TotalFailed.ShouldBe(2);
		health.LastActivityTime.ShouldNotBeNull();
		health.LastActivityTime.Value.ShouldBeGreaterThanOrEqualTo(previous!.Value);
	}

	[Fact]
	public async Task DrainProcessorWithoutPublisher()
	{
		var state = new State();
		await using var provider = Build(state);
		var worker = Worker(provider);
		try
		{
			await worker.StartAsync(CancellationToken.None);
			await state.Entered.Task.WaitAsync(Bound);
		}
		finally { await Drain(worker); }
		state.Created.ShouldBe(1);
		state.Calls.ShouldBe(1);
		state.Identity.ShouldNotBeNullOrWhiteSpace();
		state.Disposed.ShouldBe(1);
	}

	[Fact]
	public async Task PreferSuppliedPublisher()
	{
		var state = new State();
		var published = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var publisher = A.Fake<IOutboxPublisher>();
		A.CallTo(() => publisher.PublishPendingMessagesAsync(A<CancellationToken>._))
			.ReturnsLazily(() => { published.TrySetResult(); return Task.FromResult(PublishingResult.Success(1)); });
		await using var provider = Build(state, services => services.AddSingleton(publisher));
		var worker = Worker(provider);
		try
		{
			await worker.StartAsync(CancellationToken.None);
			await published.Task.WaitAsync(Bound);
		}
		finally { await Drain(worker); }
		state.Created.ShouldBe(0);
		A.CallTo(() => publisher.PublishPendingMessagesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task AvoidProcessorWhenDisabled()
	{
		var state = new State();
		await using var provider = Build(state, enabled: false);
		var worker = Worker(provider);
		try
		{
			await worker.StartAsync(CancellationToken.None);
			await worker.ExecuteTask!.WaitAsync(Bound);
		}
		finally { await Drain(worker); }
		state.Created.ShouldBe(0);
	}

	[Fact]
	public async Task AvoidProcessorAfterClosedGateObserved()
	{
		var state = new State();
		var observed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var gate = A.Fake<IProcessingGate>();
		A.CallTo(() => gate.ShouldProcess).ReturnsLazily(() => { observed.TrySetResult(); return false; });
		await using var provider = Build(state, services => services.AddSingleton(gate));
		var worker = Worker(provider);
		try
		{
			await worker.StartAsync(CancellationToken.None);
			await observed.Task.WaitAsync(Bound);
		}
		finally { await Drain(worker); }
		state.Created.ShouldBe(0);
	}

	[Fact]
	public async Task RetainScopeUntilIgnoredCancellationDrainActuallyFinishes()
	{
		var state = new State { Pause = true };
		await using var provider = Build(state);
		var worker = Worker(provider);
		try
		{
			await worker.StartAsync(CancellationToken.None);
			await state.Entered.Task.WaitAsync(Bound);
			using var deadline = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
			try { await worker.StopAsync(deadline.Token).WaitAsync(Bound); }
			catch (OperationCanceledException) when (deadline.IsCancellationRequested) { }
			state.Token.IsCancellationRequested.ShouldBeTrue();
			worker.ExecuteTask!.IsCompleted.ShouldBeFalse();
			state.Exited.ShouldBeFalse();
			state.Disposed.ShouldBe(0);
			state.Calls.ShouldBe(1);
			state.Release.TrySetResult();
			await worker.ExecuteTask.WaitAsync(Bound);
		}
		finally { state.Release.TrySetResult(); await Drain(worker); }
		state.Disposed.ShouldBe(1);
	}

	private static ServiceProvider Build(State state, Action<IServiceCollection>? configure = null, bool enabled = true)
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.Configure<OutboxProcessingOptions>(options =>
		{
			options.Enabled = enabled;
			options.PollingInterval = TimeSpan.FromMinutes(1);
			options.DrainTimeoutSeconds = 60;
			options.ProcessScheduledMessages = false;
			options.RetryFailedMessages = false;
		});
		services.AddScoped(_ => new ScopeSentinel(state));
		services.AddScoped<IOutboxProcessor>(sp =>
		{
			state.Created++;
			_ = sp.GetRequiredService<ScopeSentinel>();
			var processor = A.Fake<IOutboxProcessor>();
			A.CallTo(() => processor.Init(A<string>._)).Invokes(call => state.Identity = call.GetArgument<string>(0));
			A.CallTo(() => processor.DispatchPendingMessagesAsync(A<CancellationToken>._))
				.ReturnsLazily(call => state.RunAsync(call.GetArgument<CancellationToken>(0)));
			return processor;
		});
		configure?.Invoke(services);
		services.AddOutboxHostedService();
		return services.BuildServiceProvider(validateScopes: true);
	}

	private static BackgroundService Worker(IServiceProvider provider) =>
		provider.GetServices<IHostedService>().OfType<BackgroundService>().Single();

	private static async Task Drain(BackgroundService worker)
	{
		await worker.StopAsync(CancellationToken.None).WaitAsync(Bound);
		if (worker.ExecuteTask is { } execution) { await execution.WaitAsync(Bound); }
	}

	private sealed class State
	{
		public readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
		public readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
		public bool Pause;
		public bool Exited;
		public int Created;
		public int Calls;
		public int Delivered = 1;
		public int Disposed;
		public string? Identity;
		public CancellationToken Token;
		public async Task<int> RunAsync(CancellationToken token)
		{
			Calls++;
			Token = token;
			Entered.TrySetResult();
			try { if (Pause) { await Release.Task.ConfigureAwait(false); } return Delivered; }
			finally { Exited = true; }
		}
	}

	private sealed class ScopeSentinel(State state) : IAsyncDisposable
	{
		public ValueTask DisposeAsync()
		{
			state.Exited.ShouldBeTrue("the drain must finish before its scope is disposed");
			state.Disposed++;
			return ValueTask.CompletedTask;
		}
	}
}
