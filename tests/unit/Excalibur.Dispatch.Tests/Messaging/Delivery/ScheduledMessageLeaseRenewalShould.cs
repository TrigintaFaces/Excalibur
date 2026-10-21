// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Registry;
using Excalibur.Dispatch.Options.Scheduling;
using Excalibur.Dispatch.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Pattern", "Concurrency")]
public sealed class ScheduledMessageLeaseRenewalShould
{
	[Fact]
	public async Task KeepALongHandlerOwnedAcrossSeveralLeaseDurations()
	{
		await using var host = new RenewalHost();
		var row = await host.StoreRowAsync();
		var entered = Signal(); var finish = Signal();
		host.Cleanup = () => finish.TrySetResult();
		host.Handler.Callback = async ct => { entered.TrySetResult(); await finish.Task.WaitAsync(ct); };
		await host.Service.StartAsync(CancellationToken.None);
		await entered.Task.WaitAsync(Budget);
		for (var index = 0; index < 8; index++)
		{
			await host.Clock.WaitForRenewalDelayAsync(index + 1);
			var renewal = host.Store.NextRenewal();
			host.Clock.Advance(TimeSpan.FromSeconds(1));
			await renewal.WaitAsync(Budget);
			(await host.Store.TryAcquireExecutionAsync(row.Id, host.Store.Generation, row.NextExecutionUtc!.Value, CancellationToken.None)).ShouldBeNull();
		}
		finish.TrySetResult();
		await host.Store.Released.Task.WaitAsync(Budget);
		host.Store.ProgressCalls.ShouldBe(1);
		host.Handler.Calls.ShouldBe(1);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task CancelWithoutAdvancingWhenRenewalRejectsOrThrows(bool throws)
	{
		await using var host = new RenewalHost();
		var row = await host.StoreRowAsync();
		var entered = Signal(); var cancelled = Signal();
		host.Store.Renewal = (_, _) => throws ? Task.FromException<bool>(new IOException("renewal unavailable")) : Task.FromResult(false);
		host.Handler.Callback = async ct =>
		{
			entered.TrySetResult();
			try { await Task.Delay(Timeout.InfiniteTimeSpan, ct); }
			catch (OperationCanceledException) when (ct.IsCancellationRequested) { cancelled.TrySetResult(); }
		};
		await host.Service.StartAsync(CancellationToken.None);
		await entered.Task.WaitAsync(Budget);
		await host.Clock.WaitForRenewalDelayAsync(1);
		host.Clock.Advance(TimeSpan.FromSeconds(1));
		await cancelled.Task.WaitAsync(Budget);
		await host.Store.Released.Task.WaitAsync(Budget);
		host.Store.ProgressCalls.ShouldBe(0);
		var current = (await host.Store.GetAllAsync(CancellationToken.None)).Single();
		current.NextExecutionUtc.ShouldBe(row.NextExecutionUtc);
		current.LastExecutionUtc.ShouldBeNull();
		current.Enabled.ShouldBeTrue();
	}

	[Fact]
	public async Task LatchTimeoutEvenWhenUncooperativeRenewalLaterReturnsTrue()
	{
		await using var host = new RenewalHost();
		await host.StoreRowAsync();
		var entered = Signal(); var cancelled = Signal(); var finish = Signal();
		host.Cleanup = () => finish.TrySetResult();
		var late = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		var renewalEntered = Signal();
		host.Store.Renewal = (_, _) => { renewalEntered.TrySetResult(); return late.Task; };
		host.Handler.Callback = async ct =>
		{
			using var registration = ct.Register(() => cancelled.TrySetResult());
			entered.TrySetResult();
			await finish.Task; // Deliberately ignore cancellation.
		};
		await host.Service.StartAsync(CancellationToken.None);
		await entered.Task.WaitAsync(Budget);
		await host.Clock.WaitForRenewalDelayAsync(1);
		host.Clock.Advance(TimeSpan.FromSeconds(1));
		await renewalEntered.Task.WaitAsync(Budget);
		host.Clock.Advance(TimeSpan.FromSeconds(3));
		await cancelled.Task.WaitAsync(Budget);
		late.TrySetResult(true);
		finish.TrySetResult();
		await host.Store.Released.Task.WaitAsync(Budget);
		host.Store.ProgressCalls.ShouldBe(0);
	}

	[Fact]
	public async Task ContinueRenewingWhileProgressSettlementIsBlocked()
	{
		await using var host = new RenewalHost();
		await host.StoreRowAsync();
		var settling = Signal(); var finish = Signal();
		host.Cleanup = () => finish.TrySetResult();
		host.Store.BeforeProgress = async ct => { settling.TrySetResult(); await finish.Task.WaitAsync(ct); };
		await host.Service.StartAsync(CancellationToken.None);
		await settling.Task.WaitAsync(Budget);
		for (var index = 0; index < 4; index++)
		{
			await host.Clock.WaitForRenewalDelayAsync(index + 1);
			var renewal = host.Store.NextRenewal();
			host.Clock.Advance(TimeSpan.FromSeconds(1));
			await renewal.WaitAsync(Budget);
		}
		finish.TrySetResult();
		await host.Store.Released.Task.WaitAsync(Budget);
		host.Store.Committed.ShouldBeTrue();
	}

	[Fact]
	public async Task RejectExpiredAndAdministrativelyReplacedAuthorityWithoutResurrection()
	{
		await using var host = new RenewalHost();
		var row = await host.StoreRowAsync();
		var lease = (await host.Store.TryAcquireExecutionAsync(row.Id, host.Store.Generation, row.NextExecutionUtc!.Value, CancellationToken.None))!;
		host.Clock.Advance(host.Store.LeaseDuration);
		(await host.Store.TryRenewExecutionAsync(lease, CancellationToken.None)).ShouldBeFalse();
		await host.Store.StoreAsync(row, CancellationToken.None);
		(await host.Store.TryRenewExecutionAsync(lease, CancellationToken.None)).ShouldBeFalse();
		(await host.Store.TryUpdateProgressAsync(lease, new(null, null, true), CancellationToken.None)).ShouldBeFalse();
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(-1)]
	public async Task RejectUnschedulableDurationsBeforeAdmission(long milliseconds)
	{
		await using var host = new RenewalHost();
		await host.StoreRowAsync();
		host.Store.Duration = TimeSpan.FromMilliseconds(milliseconds);
		await host.Service.StartAsync(CancellationToken.None);
		await host.Store.DurationRead.Task.WaitAsync(Budget);
		if (milliseconds > 0)
		{
			// Preserve the former 1ms test case while proving positive provider durations are valid.
			await host.Store.Released.Task.WaitAsync(Budget);
			host.Store.Acquisitions.ShouldBe(1);
			host.Store.ProgressCalls.ShouldBe(1);
			host.Handler.Calls.ShouldBe(1);
			return;
		}
		await host.Service.StopAsync(CancellationToken.None).WaitAsync(Budget);
		host.Store.Acquisitions.ShouldBe(0);
		host.Store.ProgressCalls.ShouldBe(0);
		host.Handler.Calls.ShouldBe(0);
	}

	[Fact]
	public async Task RetainOccurrenceWhenAcquisitionConsumesItsEntireLocalBudget()
	{
		await using var host = new RenewalHost();
		var row = await host.StoreRowAsync();
		host.Store.AfterAcquire = _ => { host.Clock.Advance(host.Store.LeaseDuration); return Task.CompletedTask; };
		await host.Service.StartAsync(CancellationToken.None);
		await host.Store.Released.Task.WaitAsync(Budget);
		host.Handler.Calls.ShouldBe(0);
		host.Store.ProgressCalls.ShouldBe(0);
		(await host.Store.GetAllAsync(CancellationToken.None)).Single().NextExecutionUtc.ShouldBe(row.NextExecutionUtc);
	}

	[Fact]
	public async Task BoundShutdownWhenAnOutstandingRenewalIgnoresCancellation()
	{
		await using var host = new RenewalHost();
		await host.StoreRowAsync();
		var entered = Signal(); var requestStarted = Signal();
		var late = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		host.Store.Renewal = (_, _) => { requestStarted.TrySetResult(); return late.Task; };
		host.Handler.Callback = async ct => { entered.TrySetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, ct); };
		try
		{
			await host.Service.StartAsync(CancellationToken.None);
			await entered.Task.WaitAsync(Budget);
			await host.Clock.WaitForRenewalDelayAsync(1);
			host.Clock.Advance(TimeSpan.FromSeconds(1));
			await requestStarted.Task.WaitAsync(Budget);
			// Cleanup owns one five-second wall-clock deadline before retaining late-provider
			// resources for deferred disposal. Allow bounded scheduling grace around that contract.
			await host.Service.StopAsync(CancellationToken.None).WaitAsync(Budget);
			host.Store.ProgressCalls.ShouldBe(0);
			host.Store.Released.Task.IsCompleted.ShouldBeTrue();
			late.Task.IsCompleted.ShouldBeFalse();
		}
		finally { late.TrySetResult(true); }
	}

	[Fact]
	public async Task RetainDueOccurrenceWhenOwnershipIsLostDuringProgressSettlement()
	{
		await using var host = new RenewalHost();
		var row = await host.StoreRowAsync();
		var settling = Signal();
		host.Store.BeforeProgress = async ct => { settling.TrySetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, ct); };
		host.Store.Renewal = (_, _) => Task.FromResult(false);
		await host.Service.StartAsync(CancellationToken.None);
		await settling.Task.WaitAsync(Budget);
		await host.Clock.WaitForRenewalDelayAsync(1);
		host.Clock.Advance(TimeSpan.FromSeconds(1));
		await host.Store.Released.Task.WaitAsync(Budget);
		host.Store.Committed.ShouldBeFalse();
		var current = (await host.Store.GetAllAsync(CancellationToken.None)).Single();
		current.NextExecutionUtc.ShouldBe(row.NextExecutionUtc);
		current.LastExecutionUtc.ShouldBeNull();
	}

	[Fact]
	public async Task AcceptALeaseDurationBeyondTheSystemTimerMaximum()
	{
		await using var host = new RenewalHost();
		await host.StoreRowAsync();
		host.Store.Duration = TimeSpan.FromDays(100);
		var entered = Signal();
		host.Handler.Callback = async ct => { entered.TrySetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, ct); };
		await host.Service.StartAsync(CancellationToken.None);
		await entered.Task.WaitAsync(Budget);
		await host.Service.StopAsync(CancellationToken.None).WaitAsync(Budget);
		host.Store.Acquisitions.ShouldBe(1);
		host.Store.ProgressCalls.ShouldBe(0);
	}

	[Fact]
	public async Task StopEachRenewalLoopBeforeAdmittingTheNextCatchUpOccurrence()
	{
		await using var host = new RenewalHost();
		var row = await host.StoreRowAsync();
		row.NextExecutionUtc = host.Clock.GetUtcNow().AddMinutes(-2);
		row.MissedExecutionBehavior = MissedExecutionBehavior.ExecuteAllMissed;
		await host.Store.StoreAsync(row, CancellationToken.None);
		var finished = Signal();
		host.Store.AfterRelease = count => { if (count == 3) { finished.TrySetResult(); } };
		var cadence = 0;
		host.Handler.Callback = async _ =>
		{
			for (var index = 0; index < 4; index++)
			{
				await host.Clock.WaitForRenewalDelayAsync(++cadence);
				var renewed = host.Store.NextRenewal();
				host.Clock.Advance(TimeSpan.FromSeconds(1));
				await renewed.WaitAsync(Budget);
			}
		};
		await host.Service.StartAsync(CancellationToken.None);
		await finished.Task.WaitAsync(Budget);
		host.Handler.Calls.ShouldBe(3);
		host.Store.ProgressCalls.ShouldBe(3);
		host.Store.ReleaseCount.ShouldBe(3);
		var renewalCount = host.Store.RenewalCount;
		host.Clock.Advance(TimeSpan.FromSeconds(4));
		host.Store.RenewalCount.ShouldBe(renewalCount);
	}

	[Fact]
	public async Task BoundHelperCleanupAndObserveALateBlockedCancellationCallbackFault()
	{
		await using var host = new RenewalHost();
		var row = await host.StoreRowAsync();
		var lease = (await host.Store.TryAcquireExecutionAsync(row.Id, host.Store.Generation, row.NextExecutionUtc!.Value, CancellationToken.None))!;
		using var release = new ManualResetEventSlim();
		var callbackEntered = Signal(); var callbackFinished = Signal(); var faultObserved = Signal();
		ScheduleExecutionRenewal? renewal = null;
		CancellationTokenRegistration registration = default;
		Task advance = Task.CompletedTask;
		try
		{
			renewal = new(host.Store, lease, host.Store.LeaseDuration, host.Clock.GetTimestamp(), host.Clock,
				ex => { if (ex is not TimeoutException) { faultObserved.TrySetResult(); } }, CancellationToken.None);
			registration = renewal.Token.Register(() =>
			{
				callbackEntered.TrySetResult();
				release.Wait();
				callbackFinished.TrySetResult();
				throw new InvalidOperationException("late callback fault");
			});
			// Advancing the old implementation's timer would block this thread inside Cancel().
			advance = Task.Run(() => host.Clock.Advance(host.Store.LeaseDuration));
			await callbackEntered.Task.WaitAsync(Budget);
			await advance.WaitAsync(Budget);
			await renewal.DisposeAsync().AsTask().WaitAsync(Budget);
			renewal = null;
			callbackFinished.Task.IsCompleted.ShouldBeFalse();
		}
		finally
		{
			release.Set();
			await advance.WaitAsync(Budget);
			if (renewal is not null) { await renewal.DisposeAsync(); }
			await registration.DisposeAsync();
		}
		await callbackFinished.Task.WaitAsync(Budget);
		await faultObserved.Task.WaitAsync(Budget);
	}

	[Fact]
	public async Task RetainAValidInFlightCommitAfterLocalRenewalUncertaintyWithoutStartingAnotherOccurrence()
	{
		await using var host = new RenewalHost();
		var row = await host.StoreRowAsync();
		row.NextExecutionUtc = host.Clock.GetUtcNow().AddMinutes(-2);
		row.MissedExecutionBehavior = MissedExecutionBehavior.ExecuteAllMissed;
		await host.Store.StoreAsync(row, CancellationToken.None);
		var committed = Signal(); var returnCommit = Signal(); var ownershipLost = Signal();
		host.Cleanup = () => returnCommit.TrySetResult();
		host.Store.AfterProgress = async () =>
		{
			using var registration = host.Store.ProgressToken.Register(() => ownershipLost.TrySetResult());
			committed.TrySetResult();
			await returnCommit.Task;
		};
		host.Store.Renewal = (_, _) => Task.FromResult(false);
		await host.Service.StartAsync(CancellationToken.None);
		await committed.Task.WaitAsync(Budget);
		await host.Clock.WaitForRenewalDelayAsync(1);
		host.Clock.Advance(TimeSpan.FromSeconds(1));
		await ownershipLost.Task.WaitAsync(Budget);
		returnCommit.TrySetResult();
		await host.Store.Released.Task.WaitAsync(Budget);
		host.Store.Committed.ShouldBeTrue();
		host.Store.ProgressCalls.ShouldBe(1);
		host.Handler.Calls.ShouldBe(1);
		(await host.Store.GetAllAsync(CancellationToken.None)).Single().NextExecutionUtc.ShouldBe(row.NextExecutionUtc!.Value.AddMinutes(1));
	}

	[Fact]
	public async Task RetainProviderTokenResourcesUntilALateRequestFinishesAndObserveItsFaultOnce()
	{
		await using var host = new RenewalHost();
		var row = await host.StoreRowAsync();
		var lease = (await host.Store.TryAcquireExecutionAsync(row.Id, host.Store.Generation, row.NextExecutionUtc!.Value, CancellationToken.None))!;
		var requestEntered = Signal(); var finishRequest = Signal(); var faultObserved = Signal();
		var observations = 0;
		var resourcesUsable = false;
		host.Store.Renewal = async (_, ct) =>
		{
			requestEntered.TrySetResult();
			await finishRequest.Task;
			// WaitHandle throws when its originating CTS has been disposed. The request still owns
			// this token even after the caller's bounded cleanup deadline has returned.
			resourcesUsable = ct.WaitHandle.WaitOne(0);
			using var registration = ct.Register(static () => { });
			throw new IOException("late provider fault");
		};
		ScheduleExecutionRenewal? renewal = null;
		try
		{
			renewal = new(host.Store, lease, host.Store.LeaseDuration, host.Clock.GetTimestamp(), host.Clock,
				ex => { if (ex is IOException) { Interlocked.Increment(ref observations); faultObserved.TrySetResult(); } }, CancellationToken.None);
			await host.Clock.WaitForRenewalDelayAsync(1);
			host.Clock.Advance(TimeSpan.FromSeconds(1));
			await requestEntered.Task.WaitAsync(Budget);
			await renewal.DisposeAsync().AsTask().WaitAsync(Budget);
			renewal = null;
			finishRequest.TrySetResult();
			await faultObserved.Task.WaitAsync(Budget);
			resourcesUsable.ShouldBeTrue();
			observations.ShouldBe(1);
		}
		finally
		{
			finishRequest.TrySetResult();
			if (renewal is not null) { await renewal.DisposeAsync(); }
		}
	}

	[Fact]
	public async Task RefuseQueuedMaintenanceWhenDisposalWinsBeforeAdmission()
	{
		await using var host = new RenewalHost();
		var row = await host.StoreRowAsync();
		var lease = (await host.Store.TryAcquireExecutionAsync(row.Id, host.Store.Generation, row.NextExecutionUtc!.Value, CancellationToken.None))!;
		using var resume = new ManualResetEventSlim();
		var beforeAdmission = Signal();
		ScheduleExecutionRenewal? renewal = null;
		Task disposal = Task.CompletedTask;
		Task advance = Task.CompletedTask;
		try
		{
			renewal = new(host.Store, lease, host.Store.LeaseDuration, host.Clock.GetTimestamp(), host.Clock, _ => { }, CancellationToken.None);
			await host.Clock.WaitForRenewalDelayAsync(1);
			host.Clock.BeforeNextTimestamp(() => { beforeAdmission.TrySetResult(); resume.Wait(); });
			advance = Task.Run(() => host.Clock.Advance(TimeSpan.FromSeconds(1)));
			await beforeAdmission.Task.WaitAsync(Budget);
			disposal = renewal.DisposeAsync().AsTask();
			renewal = null;
			resume.Set();
			await disposal.WaitAsync(Budget);
			host.Store.RenewalCalls.ShouldBe(0);
		}
		finally
		{
			resume.Set();
			await advance.WaitAsync(Budget);
			await disposal.WaitAsync(Budget);
			if (renewal is not null) { await renewal.DisposeAsync(); }
		}
	}

	[Fact]
	public async Task RefuseMaintenanceAfterExpiryEvenWhenTheWatchdogCallbackHasNotRun()
	{
		await using var host = new RenewalHost();
		var row = await host.StoreRowAsync();
		var lease = (await host.Store.TryAcquireExecutionAsync(row.Id, host.Store.Generation, row.NextExecutionUtc!.Value, CancellationToken.None))!;
		using var resume = new ManualResetEventSlim();
		var beforeAdmission = Signal();
		ScheduleExecutionRenewal? renewal = null;
		Task advance = Task.CompletedTask;
		try
		{
			renewal = new(host.Store, lease, host.Store.LeaseDuration, host.Clock.GetTimestamp(), host.Clock, _ => { }, CancellationToken.None);
			await host.Clock.WaitForRenewalDelayAsync(1);
			host.Clock.SuppressDeadlineCallbacks = true;
			host.Clock.BeforeNextTimestamp(() => { beforeAdmission.TrySetResult(); resume.Wait(); });
			advance = Task.Run(() => host.Clock.Advance(TimeSpan.FromSeconds(1)));
			await beforeAdmission.Task.WaitAsync(Budget);
			// The captured RPC lower bound is still 1s, but the current grant is now expired.
			// Delayed watchdog delivery must not allow that stale timestamp to authorize maintenance.
			host.Clock.Advance(TimeSpan.FromSeconds(3));
			var cancelled = Signal();
			using var registration = renewal.Token.Register(() => cancelled.TrySetResult());
			resume.Set();
			await cancelled.Task.WaitAsync(Budget);
			await advance.WaitAsync(Budget);
			host.Store.RenewalCalls.ShouldBe(0);
		}
		finally
		{
			resume.Set();
			await advance.WaitAsync(Budget);
			if (renewal is not null) { await renewal.DisposeAsync(); }
		}
	}

	// Real-time ceiling for a fake-clock signal, generous against CI load. DisposeAsync bounds
	// blocked consumer callbacks on the SYSTEM clock at 5s, so this must clear that with slack:
	// the arms assert cleanup is BOUNDED, not that it is fast.
	private static readonly TimeSpan Budget = TimeSpan.FromSeconds(30);

	private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

	private sealed class RenewalHost : IAsyncDisposable
	{
		private readonly ServiceProvider _provider;
		public RenewalClock Clock { get; } = new();
		public ExpiringStore Store { get; }
		public ScheduleWorkerTestHandler Handler { get; } = new();
		public ScheduledMessageService Service { get; }
		public Action? Cleanup { get; set; }
		public RenewalHost()
		{
			Store = new(Clock);
			MessageTypeRegistry.RegisterType<ScheduleWorkerTestDocument>();
			var services = new ServiceCollection();
			services.AddLogging();
			services.AddDispatch(dispatch => dispatch.UseMiddleware<ScheduleWorkerTestMiddleware>());
			services.AddSingleton<DispatchJsonSerializer>();
			services.AddSingleton(Handler);
			services.AddSingleton<IDocumentHandler<ScheduleWorkerTestDocument>>(Handler);
			_provider = services.BuildServiceProvider();
			var options = Microsoft.Extensions.Options.Options.Create(new SchedulerOptions { PollInterval = TimeSpan.FromSeconds(1), EnableAdaptivePolling = false });
			var cronOptions = Microsoft.Extensions.Options.Options.Create(new CronScheduleOptions());
			Service = new(Store, _provider.GetRequiredService<IDispatcher>(), _provider.GetRequiredService<DispatchJsonSerializer>(),
				new CronScheduler(cronOptions, NullLogger<CronScheduler>.Instance), options, cronOptions,
				NullLogger<ScheduledMessageService>.Instance, _provider.GetRequiredService<IServiceScopeFactory>(), timeProvider: Clock);
		}
		public async Task<ScheduledMessage> StoreRowAsync()
		{
			var row = new ScheduledMessage { Id = Guid.NewGuid(), Enabled = true, NextExecutionUtc = Clock.GetUtcNow(),
				Interval = TimeSpan.FromMinutes(1), MessageName = typeof(ScheduleWorkerTestDocument).FullName!,
				MessageBody = await _provider.GetRequiredService<DispatchJsonSerializer>().SerializeAsync(new ScheduleWorkerTestDocument("renewal"), typeof(ScheduleWorkerTestDocument)) };
			await Store.StoreAsync(row, CancellationToken.None);
			return row;
		}
		public async ValueTask DisposeAsync()
		{
			Cleanup?.Invoke();
			await Service.StopAsync(CancellationToken.None).WaitAsync(Budget);
			Service.Dispose();
			await _provider.DisposeAsync();
		}
	}

	// A deterministic authoritative store: expiry is checked under the same lock as generation and ownership.
	private sealed class ExpiringStore(TimeProvider clock) : IScheduleExecutionLeaseRenewal
	{
		private readonly System.Threading.Lock _gate = new();
		private readonly Guid _storeId = Guid.NewGuid();
		private ScheduledMessage? _row;
		private ScheduleExecutionLease? _owner;
		private DateTimeOffset _expires;
		private TaskCompletionSource _renewed = Signal();
		public Guid Generation { get; private set; }
		public TimeSpan Duration { get; set; } = TimeSpan.FromSeconds(3);
		public TaskCompletionSource DurationRead { get; } = Signal();
		public TimeSpan LeaseDuration { get { DurationRead.TrySetResult(); return Duration; } }
		public int Acquisitions { get; private set; }
		public int ReleaseCount { get; private set; }
		public int RenewalCount { get; private set; }
		public int RenewalCalls { get; private set; }
		public Action<int>? AfterRelease { get; set; }
		public Func<CancellationToken, Task>? AfterAcquire { get; set; }
		public Func<ScheduleExecutionLease, CancellationToken, Task<bool>>? Renewal { get; set; }
		public Func<CancellationToken, Task>? BeforeProgress { get; set; }
		public Func<Task>? AfterProgress { get; set; }
		public CancellationToken ProgressToken { get; private set; }
		public TaskCompletionSource Released { get; } = Signal();
		public int ProgressCalls { get; private set; }
		public bool Committed { get; private set; }
		public Task NextRenewal() { lock (_gate) { return _renewed.Task; } }
		public Task StoreAsync(IScheduledMessage row, CancellationToken ct)
		{
			lock (_gate) { ct.ThrowIfCancellationRequested(); _row = ScheduleExecutionSnapshot.Copy(row); Generation = Guid.NewGuid(); _owner = null; Committed = false; }
			return Task.CompletedTask;
		}
		public Task CompleteAsync(Guid id, CancellationToken ct)
		{
			lock (_gate) { ct.ThrowIfCancellationRequested(); if (_row?.Id == id) { _row.Enabled = false; Generation = Guid.NewGuid(); _owner = null; } }
			return Task.CompletedTask;
		}
		public Task<IEnumerable<IScheduledMessage>> GetAllAsync(CancellationToken ct)
		{
			lock (_gate) { ct.ThrowIfCancellationRequested(); return Task.FromResult<IEnumerable<IScheduledMessage>>(_row is null ? [] : [ScheduleExecutionSnapshot.Copy(_row)]); }
		}
		public Task<IReadOnlyList<ScheduleExecutionSnapshot>> GetExecutionSnapshotsAsync(CancellationToken ct)
		{
			lock (_gate) { ct.ThrowIfCancellationRequested(); return Task.FromResult<IReadOnlyList<ScheduleExecutionSnapshot>>(_row is null ? [] : [new(Generation, _row)]); }
		}
		public async Task<ScheduleExecutionLease?> TryAcquireExecutionAsync(Guid id, Guid generation, DateTimeOffset occurrence, CancellationToken ct)
		{
			ScheduleExecutionLease lease;
			lock (_gate)
			{
				ct.ThrowIfCancellationRequested();
				Acquisitions++;
				if (_row is null || !_row.Enabled || _row.Id != id || Generation != generation || _row.NextExecutionUtc != occurrence || (_owner is not null && clock.GetUtcNow() < _expires)) { return null; }
				_owner = new(_storeId, id, generation, occurrence, Guid.NewGuid());
				_expires = clock.GetUtcNow().Add(LeaseDuration); Committed = false;
				lease = _owner;
			}
			if (AfterAcquire is { } barrier) { await barrier(ct); }
			return lease;
		}
		public async Task<bool> TryRenewExecutionAsync(ScheduleExecutionLease lease, CancellationToken ct)
		{
			RenewalCalls++;
			if (Renewal is { } custom)
			{
				var result = await custom(lease, ct);
				return result;
			}
			lock (_gate)
			{
				ct.ThrowIfCancellationRequested();
				if (!Matches(lease)) { return false; }
				RenewalCount++;
				_expires = clock.GetUtcNow().Add(LeaseDuration);
				var signal = _renewed; _renewed = Signal(); signal.TrySetResult(); return true;
			}
		}
		public async Task<bool> TryUpdateProgressAsync(ScheduleExecutionLease lease, ScheduleExecutionResult result, CancellationToken ct)
		{
			ProgressToken = ct;
			ProgressCalls++;
			if (BeforeProgress is { } barrier) { await barrier(ct); }
			lock (_gate)
			{
				ct.ThrowIfCancellationRequested(); if (!Matches(lease) || Committed) { return false; }
				_row!.LastExecutionUtc = result.LastExecutionUtc; _row.NextExecutionUtc = result.NextExecutionUtc;
				if (result.Disable) { _row.Enabled = false; }
				Committed = true;
			}
			if (AfterProgress is { } after) { await after(); }
			return true;
		}
		public Task<bool> ReleaseExecutionAsync(ScheduleExecutionLease lease, CancellationToken ct)
		{
			lock (_gate)
			{
				ct.ThrowIfCancellationRequested(); var matches = _owner == lease && Generation == lease.Generation;
				if (matches) { _owner = null; }
				ReleaseCount++; AfterRelease?.Invoke(ReleaseCount);
				Released.TrySetResult(); return Task.FromResult(matches);
			}
		}
		private bool Matches(ScheduleExecutionLease lease) => _owner == lease && Generation == lease.Generation && clock.GetUtcNow() < _expires;
	}

	// Observe timer registration rather than sleeping or racing the renewal task's asynchronous continuation.
	private sealed class RenewalClock : TimeProvider
	{
		private readonly FakeTimeProvider _inner = new(new DateTimeOffset(3000, 1, 1, 0, 0, 0, TimeSpan.Zero));
		private readonly System.Threading.Lock _gate = new();
		private int _delays;
		private Action? _beforeTimestamp;
		public bool SuppressDeadlineCallbacks { get; set; }
		private TaskCompletionSource _changed = Signal();
		public override long TimestampFrequency => _inner.TimestampFrequency;
		public override long GetTimestamp()
		{
			var timestamp = _inner.GetTimestamp();
			Interlocked.Exchange(ref _beforeTimestamp, null)?.Invoke();
			return timestamp;
		}
		public void BeforeNextTimestamp(Action callback) => _beforeTimestamp = callback;
		public override DateTimeOffset GetUtcNow() => _inner.GetUtcNow();
		public void Advance(TimeSpan duration) => _inner.Advance(duration);
		public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
		{
			var deadline = dueTime == Timeout.InfiniteTimeSpan && period == Timeout.InfiniteTimeSpan;
			var timer = _inner.CreateTimer(value =>
			{
				if (!deadline || !SuppressDeadlineCallbacks) { callback(value); }
			}, state, dueTime, period);
			if (dueTime == TimeSpan.FromSeconds(1))
			{
				lock (_gate) { _delays++; var changed = _changed; _changed = Signal(); changed.TrySetResult(); }
			}
			return timer;
		}
		public async Task WaitForRenewalDelayAsync(int count)
		{
			while (true)
			{
				Task changed;
				lock (_gate) { if (_delays >= count) { return; } changed = _changed.Task; }
				await changed.WaitAsync(Budget);
			}
		}
	}
}
