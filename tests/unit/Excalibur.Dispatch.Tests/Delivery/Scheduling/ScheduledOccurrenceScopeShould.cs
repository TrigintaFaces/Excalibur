// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;

using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Registry;
using Excalibur.Dispatch.Options.Scheduling;
using Excalibur.Dispatch.Serialization;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Delivery.Scheduling;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "Regression")]
public sealed class ScheduledOccurrenceScopeShould
{
	[Fact]
	public async Task KeepDependenciesAliveThroughProgressAndReleaseAndAwaitAsyncDisposal()
	{
		await using var host = new ScopeHost("success", blockProgress: true, blockDisposal: true);
		await host.StoreAsync();
		await host.Service.StartAsync(CancellationToken.None);
		await host.Store.ProgressEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
		var probe = host.Probes.Single();
		probe.Provider.ShouldNotBeSameAs(host.Provider);
		probe.DisposalEntered.Task.IsCompleted.ShouldBeFalse();
		host.Store.AllowProgress.TrySetResult();
		await probe.DisposalEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
		host.Store.Releases.ShouldBe(1);
		probe.Disposed.Task.IsCompleted.ShouldBeFalse();
		probe.DisposedBeforeRelease.ShouldBeFalse();
		host.AllowDisposal.TrySetResult();
		await probe.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(10));
	}

	[Fact]
	public async Task GiveEachCatchUpOccurrenceAnIndependentDisposedScope()
	{
		await using var host = new ScopeHost("success", targetCalls: 3);
		await host.StoreAsync(catchUp: true);
		await host.Service.StartAsync(CancellationToken.None);
		await host.TargetReached.Task.WaitAsync(TimeSpan.FromSeconds(10));
		var probes = host.Probes.ToArray();
		await Task.WhenAll(probes.Select(static probe => probe.Disposed.Task)).WaitAsync(TimeSpan.FromSeconds(10));
		await host.Service.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));
		probes.Length.ShouldBe(3);
		probes.Select(static probe => probe.Provider).Distinct(ReferenceEqualityComparer.Instance).Count().ShouldBe(3);
		probes.ShouldAllBe(static probe => !probe.DisposedBeforeRelease);
		host.Store.Releases.ShouldBe(3);
	}

	[Theory]
	[InlineData("failure")]
	[InlineData("cancellation")]
	public async Task DisposeTheOccurrenceScopeOnHandlerFailureAndHostCancellation(string outcome)
	{
		await using var host = new ScopeHost(outcome);
		await host.StoreAsync();
		await host.Service.StartAsync(CancellationToken.None);
		await host.TargetReached.Task.WaitAsync(TimeSpan.FromSeconds(10));
		if (outcome == "cancellation")
		{
			await host.Service.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));
		}
		var probe = host.Probes.Single();
		await probe.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(10));
		probe.DisposedBeforeRelease.ShouldBeFalse();
		host.Store.Releases.ShouldBe(1);
	}

	private sealed record ScopeAction(string Payload) : IDispatchAction;

	private sealed class ScopeHost : IAsyncDisposable
	{
		private readonly DispatchJsonSerializer _serializer = new();
		private readonly string _outcome;
		private readonly int _targetCalls;
		private int _calls;
		public ServiceProvider Provider { get; }
		public ObservingStore Store { get; }
		public ScheduledMessageService Service { get; }
		public ConcurrentQueue<ScopedProbe> Probes { get; } = new();
		public TaskCompletionSource TargetReached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
		public TaskCompletionSource AllowDisposal { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
		public ScopeHost(string outcome, bool blockProgress = false, bool blockDisposal = false, int targetCalls = 1)
		{
			_outcome = outcome;
			_targetCalls = targetCalls;
			Store = new ObservingStore(blockProgress);
			if (!blockDisposal) { AllowDisposal.TrySetResult(); }
			var services = new ServiceCollection();
			services.AddScoped(provider => new ScopedProbe(provider, Store, AllowDisposal.Task));
			Provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
			var dispatcher = A.Fake<IDispatcher>();
			A.CallTo(() => dispatcher.DispatchAsync(A<IDispatchAction>._, A<IMessageContext>._, A<CancellationToken>._))
				.ReturnsLazily((IDispatchAction _, IMessageContext context, CancellationToken token) => DispatchAsync(context, token));
			var cron = Microsoft.Extensions.Options.Options.Create(new CronScheduleOptions { MaxMissedExecutions = 3 });
			Service = new ScheduledMessageService(Store, dispatcher, _serializer,
				new CronScheduler(cron, NullLogger<CronScheduler>.Instance),
				Microsoft.Extensions.Options.Options.Create(new SchedulerOptions { PollInterval = TimeSpan.FromSeconds(1) }),
				cron, NullLogger<ScheduledMessageService>.Instance, Provider.GetRequiredService<IServiceScopeFactory>());
			MessageTypeRegistry.RegisterType<ScopeAction>();
		}
		public Task StoreAsync(bool catchUp = false) => Store.StoreAsync(new ScheduledMessage
		{
			Enabled = true, MessageName = typeof(ScopeAction).FullName!,
			MessageBody = _serializer.Serialize(new ScopeAction("payload")),
			NextExecutionUtc = DateTimeOffset.UtcNow.AddSeconds(catchUp ? -25 : -1),
			Interval = catchUp ? TimeSpan.FromSeconds(10) : null,
			MissedExecutionBehavior = MissedExecutionBehavior.ExecuteAllMissed
		}, CancellationToken.None);
		private async Task<IMessageResult> DispatchAsync(IMessageContext context, CancellationToken token)
		{
			var probe = context.RequestServices!.GetRequiredService<ScopedProbe>();
			Probes.Enqueue(probe);
			if (Interlocked.Increment(ref _calls) == _targetCalls) { TargetReached.TrySetResult(); }
			if (_outcome == "failure") { throw new InvalidOperationException("A real handler failure must still dispose its occurrence scope."); }
			if (_outcome == "cancellation") { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
			return MessageResult.Success();
		}
		public async ValueTask DisposeAsync()
		{
			Store.AllowProgress.TrySetResult();
			AllowDisposal.TrySetResult();
			await Service.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));
			Service.Dispose();
			await Provider.DisposeAsync();
			_serializer.Dispose();
		}
	}

	private sealed class ScopedProbe(IServiceProvider provider, ObservingStore store, Task allowDisposal) : IAsyncDisposable
	{
		private readonly int _requiredReleaseCount = store.Releases + 1;
		public IServiceProvider Provider { get; } = provider;
		public bool DisposedBeforeRelease { get; private set; }
		public TaskCompletionSource DisposalEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
		public TaskCompletionSource Disposed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
		public async ValueTask DisposeAsync()
		{
			DisposedBeforeRelease = store.Releases < _requiredReleaseCount;
			DisposalEntered.TrySetResult();
			await allowDisposal;
			Disposed.TrySetResult();
		}
	}

	private sealed class ObservingStore : IScheduleExecutionStore
	{
		private readonly InMemoryScheduleStore _inner = new();
		private int _releases;
		public int Releases => Volatile.Read(ref _releases);
		public TaskCompletionSource ProgressEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
		public TaskCompletionSource AllowProgress { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
		public ObservingStore(bool blockProgress)
		{
			if (!blockProgress) { AllowProgress.TrySetResult(); }
		}
		public Task<IEnumerable<IScheduledMessage>> GetAllAsync(CancellationToken token) => _inner.GetAllAsync(token);
		public Task StoreAsync(IScheduledMessage message, CancellationToken token) => _inner.StoreAsync(message, token);
		public Task CompleteAsync(Guid id, CancellationToken token) => _inner.CompleteAsync(id, token);
		public Task<IReadOnlyList<ScheduleExecutionSnapshot>> GetExecutionSnapshotsAsync(CancellationToken token) => _inner.GetExecutionSnapshotsAsync(token);
		public Task<ScheduleExecutionLease?> TryAcquireExecutionAsync(Guid id, Guid generation, DateTimeOffset occurrence, CancellationToken token) =>
			_inner.TryAcquireExecutionAsync(id, generation, occurrence, token);
		public async Task<bool> TryUpdateProgressAsync(ScheduleExecutionLease lease, ScheduleExecutionResult result, CancellationToken token)
		{
			ProgressEntered.TrySetResult();
			await AllowProgress.Task.WaitAsync(token);
			return await _inner.TryUpdateProgressAsync(lease, result, token);
		}
		public async Task<bool> ReleaseExecutionAsync(ScheduleExecutionLease lease, CancellationToken token)
		{
			var released = await _inner.ReleaseExecutionAsync(lease, token);
			if (released) { Interlocked.Increment(ref _releases); }
			return released;
		}
	}
}
