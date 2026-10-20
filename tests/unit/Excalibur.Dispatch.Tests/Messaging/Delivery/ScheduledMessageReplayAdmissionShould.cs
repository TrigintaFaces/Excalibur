// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Delivery.Registry;
using Excalibur.Dispatch.Options.Scheduling;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Pattern", "Concurrency")]
public sealed class ScheduledMessageReplayAdmissionShould
{
	[Fact]
	public async Task StopCatchUpWhenAHandlerSwallowsItsDeadlineAndReturnsNormally()
	{
		var policy = A.Fake<ITimePolicy>(options => options.Implements<ITimePolicyConfiguration>());
		A.CallTo(() => ((ITimePolicyConfiguration)policy).ShouldApplyTimeout(A<TimeoutOperationType>._, A<TimeoutContext?>._))
			.ReturnsLazily((TimeoutOperationType operation, TimeoutContext? _) => operation == TimeoutOperationType.Handler);
		A.CallTo(() => policy.GetTimeoutFor(TimeoutOperationType.Handler)).Returns(TimeSpan.FromMilliseconds(50));
		await using var host = new ScheduleWorkerTestHost(timePolicy: policy);
		var row = await host.StoreRowAsync("deadline");
		row.CronExpression = "* * * * *";
		row.Interval = null;
		row.NextExecutionUtc = host.Clock.GetUtcNow().AddMinutes(-3);
		row.LastExecutionUtc = host.Clock.GetUtcNow().AddMinutes(-5);
		row.MissedExecutionBehavior = MissedExecutionBehavior.ExecuteAllMissed;
		await host.Store.Inner.StoreAsync(row, CancellationToken.None);
		_ = await host.StoreRowAsync("following-row");
		var entered = ScheduleWorkerTestHost.Signal();
		var following = ScheduleWorkerTestHost.Signal();
		var attempts = 0;
		host.Handler.MessageCallback = async (document, ct) =>
		{
			if (document.Marker != "deadline") { following.TrySetResult(); return; }
			attempts++;
			entered.TrySetResult();
			try { await Task.Delay(Timeout.InfiniteTimeSpan, ct); }
			catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
			// A consumer handler can swallow cancellation. It must not turn deadline expiry into success.
		};
		await host.Service.StartAsync(CancellationToken.None);
		await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
		host.Clock.Advance(TimeSpan.FromMilliseconds(49));
		host.Store.ProgressById.ContainsKey(row.Id).ShouldBeFalse();
		following.Task.IsCompleted.ShouldBeFalse();
		host.Clock.Advance(TimeSpan.FromMilliseconds(1));
		await following.Task.WaitAsync(TimeSpan.FromSeconds(10));
		attempts.ShouldBe(1);
		host.Store.ProgressById[row.Id].ShouldBe(1);
		var current = (await host.Store.Inner.GetAllAsync(CancellationToken.None)).Single(item => item.Id == row.Id);
		current.LastExecutionUtc.ShouldBe(row.LastExecutionUtc);
		current.NextExecutionUtc.ShouldNotBeNull().ShouldBeGreaterThan(host.Clock.GetUtcNow());
		var telemetry = host.Logger.Entries.Single(entry => entry.EventId == DeliveryEventId.ScheduledMissedExecutions
			&& entry.Values.TryGetValue("MessageId", out var id) && Equals(id, row.Id));
		telemetry.Values["Count"].ShouldBe(1);
	}

	[Theory]
	[InlineData(MissedExecutionBehavior.ExecuteLatestMissed, 10, 1)]
	[InlineData(MissedExecutionBehavior.ExecuteAllMissed, 2, 2)]
	[InlineData(MissedExecutionBehavior.ExecuteAllMissed, 0, 0)]
	[InlineData(MissedExecutionBehavior.SkipMissed, 10, 0)]
	[InlineData(MissedExecutionBehavior.DisableSchedule, 10, 0)]
	public async Task RespectReplayPolicyThroughTheLegacyStoreInterface(
		MissedExecutionBehavior behavior, int maximum, int expected)
	{
		await using var host = new LegacyScheduleWorkerTestHost(maximum);
		var row = await host.StoreRowAsync("backlog");
		row.CronExpression = "* * * * *";
		row.Interval = null;
		row.NextExecutionUtc = host.Clock.GetUtcNow().AddMinutes(-4);
		row.LastExecutionUtc = row.NextExecutionUtc!.Value.AddMinutes(-1);
		row.MissedExecutionBehavior = behavior;
		await host.Store.StoreAsync(row, CancellationToken.None);
		_ = await host.StoreRowAsync("following-row");
		var reachedFollowing = ScheduleWorkerTestHost.Signal();
		var backlogCalls = 0;
		host.Handler.MessageCallback = (document, _) =>
		{
			if (document.Marker == "backlog") { backlogCalls++; }
			else { reachedFollowing.TrySetResult(); }
			return Task.CompletedTask;
		};
		await host.Service.StartAsync(CancellationToken.None);
		await reachedFollowing.Task.WaitAsync(TimeSpan.FromSeconds(10));
		backlogCalls.ShouldBe(expected);
		var current = (await host.Store.GetAllAsync(CancellationToken.None)).Single(s => s.Id == row.Id);
		if (behavior == MissedExecutionBehavior.DisableSchedule) { current.Enabled.ShouldBeFalse(); }
		else if (behavior == MissedExecutionBehavior.ExecuteAllMissed && maximum > 0)
		{
			current.NextExecutionUtc.ShouldBe(row.NextExecutionUtc!.Value.AddMinutes(expected));
		}
		else { current.NextExecutionUtc.ShouldNotBeNull().ShouldBeGreaterThan(host.Clock.GetUtcNow()); }
	}

	[Fact]
	public async Task PreserveAnOverdueOneTimeDeliveryThroughTheLegacyStoreInterface()
	{
		await using var host = new LegacyScheduleWorkerTestHost(0);
		await host.VerifyDirectDispatchAsync();
		var row = await host.StoreRowAsync("one-time");
		row.Interval = null;
		row.NextExecutionUtc = host.Clock.GetUtcNow().AddMinutes(-4);
		row.MissedExecutionBehavior = MissedExecutionBehavior.SkipMissed;
		await host.Store.StoreAsync(row, CancellationToken.None);
		_ = await host.StoreRowAsync("following-row");
		var reachedFollowing = ScheduleWorkerTestHost.Signal();
		var calls = 0;
		host.Handler.MessageCallback = (document, _) =>
		{
			if (document.Marker == "one-time") { calls++; }
			else { reachedFollowing.TrySetResult(); }
			return Task.CompletedTask;
		};
		await host.Service.StartAsync(CancellationToken.None);
		await reachedFollowing.Task.WaitAsync(TimeSpan.FromSeconds(10));
		calls.ShouldBe(1);
		(await host.Store.GetAllAsync(CancellationToken.None)).Single(s => s.Id == row.Id).Enabled.ShouldBeFalse();
	}

	[Fact]
	public async Task KeepLiveLegacyInventoryUnchangedWhenShutdownPreventsStorage()
	{
		var liveStore = new LiveReferenceScheduleWorkerTestStore();
		await using var host = new LegacyScheduleWorkerTestHost(10, liveStore);
		var row = await host.StoreRowAsync("live-row");
		row.LastExecutionUtc = host.Clock.GetUtcNow().AddMinutes(-1);
		liveStore.Row = row;
		var entered = ScheduleWorkerTestHost.Signal();
		var hostCancelled = ScheduleWorkerTestHost.Signal();
		var resume = ScheduleWorkerTestHost.Signal();
		host.Handler.Callback = async ct =>
		{
			using var registration = ct.Register(() => hostCancelled.TrySetResult());
			entered.TrySetResult();
			await resume.Task;
		};
		await host.Service.StartAsync(CancellationToken.None);
		await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
		var stop = host.Service.StopAsync(CancellationToken.None);
		await hostCancelled.Task.WaitAsync(TimeSpan.FromSeconds(10));
		resume.TrySetResult();
		await stop.WaitAsync(TimeSpan.FromSeconds(10));
		liveStore.Writes.ShouldBe(0);
		liveStore.Row.ShouldBeSameAs(row);
		row.LastExecutionUtc.ShouldBe(host.Clock.GetUtcNow().AddMinutes(-1));
		row.NextExecutionUtc.ShouldBe(host.Clock.GetUtcNow());
		row.Enabled.ShouldBeTrue();
	}

	[Theory]
	[InlineData(MissedExecutionBehavior.SkipMissed, 10, 0)]
	[InlineData(MissedExecutionBehavior.ExecuteLatestMissed, 10, 1)]
	[InlineData(MissedExecutionBehavior.ExecuteAllMissed, 2, 2)]
	[InlineData(MissedExecutionBehavior.ExecuteAllMissed, 0, 0)]
	[InlineData(MissedExecutionBehavior.DisableSchedule, 10, 0)]
	public async Task AdmitEachPersistedOccurrenceAndRespectTheReplayPolicy(
		MissedExecutionBehavior behavior, int maximum, int expected)
	{
		await using var host = new ScheduleWorkerTestHost();
		host.CronOptions.MaxMissedExecutions = maximum;
		var row = await host.StoreRowAsync("backlog");
		row.CronExpression = "* * * * *";
		row.Interval = null;
		row.NextExecutionUtc = host.Clock.GetUtcNow().AddMinutes(-4);
		row.LastExecutionUtc = row.NextExecutionUtc!.Value.AddMinutes(-1);
		row.MissedExecutionBehavior = behavior;
		await host.Store.Inner.StoreAsync(row, CancellationToken.None);
		_ = await host.StoreRowAsync("following-row");
		var reachedFollowing = ScheduleWorkerTestHost.Signal();
		var backlogCalls = 0;
		host.Handler.MessageCallback = (document, _) =>
		{
			if (document.Marker == "backlog") { backlogCalls++; }
			else { reachedFollowing.TrySetResult(); }
			return Task.CompletedTask;
		};
		await host.Service.StartAsync(CancellationToken.None);
		await reachedFollowing.Task.WaitAsync(TimeSpan.FromSeconds(10));
		backlogCalls.ShouldBe(expected);
		var current = (await host.Store.Inner.GetAllAsync(CancellationToken.None)).Single(s => s.Id == row.Id);
		if (behavior == MissedExecutionBehavior.DisableSchedule)
		{
			current.Enabled.ShouldBeFalse();
			current.LastExecutionUtc.ShouldBe(row.LastExecutionUtc);
		}
		else if (behavior == MissedExecutionBehavior.ExecuteAllMissed && maximum > 0)
		{
			// The remainder remains due for the next bounded poll; it is not silently discarded.
			current.NextExecutionUtc.ShouldBe(row.NextExecutionUtc!.Value.AddMinutes(expected));
			current.Enabled.ShouldBeTrue();
		}
		else { current.NextExecutionUtc.ShouldNotBeNull().ShouldBeGreaterThan(host.Clock.GetUtcNow()); }
		// One admission/progress/release for each replay, or one fenced no-dispatch advance.
		host.Store.ProgressById[row.Id].ShouldBe(expected == 0 ? 1 : expected);
		host.Store.ReleasesById[row.Id].ShouldBe(expected == 0 ? 1 : expected);
		var admitted = host.Store.Admitted.Where(lease => lease.ScheduleId == row.Id).Select(lease => lease.OccurrenceUtc).ToArray();
		admitted.ShouldBe(Enumerable.Range(0, expected == 0 ? 1 : expected)
			.Select(index => row.NextExecutionUtc!.Value.AddMinutes(index)).ToArray());
	}

	[Fact]
	public async Task StillDeliverAnOnTimeOccurrenceWhenCatchUpMaximumIsZero()
	{
		await using var host = new ScheduleWorkerTestHost();
		host.CronOptions.MaxMissedExecutions = 0;
		var row = await host.StoreRowAsync();
		row.MissedExecutionBehavior = MissedExecutionBehavior.ExecuteAllMissed;
		await host.Store.Inner.StoreAsync(row, CancellationToken.None);
		await host.Service.StartAsync(CancellationToken.None);
		await host.Store.Released.Task.WaitAsync(TimeSpan.FromSeconds(10));
		host.Handler.Calls.ShouldBe(1);
		(await host.ReadAsync()).NextExecutionUtc.ShouldBe(host.Clock.GetUtcNow().AddMinutes(1));
	}

	[Fact]
	public async Task StopReplayWhenCancellationRejectsTheFirstProgressCommit()
	{
		await using var host = new ScheduleWorkerTestHost();
		host.CronOptions.MaxMissedExecutions = 10;
		var row = await host.StoreRowAsync("backlog");
		row.NextExecutionUtc = host.Clock.GetUtcNow().AddMinutes(-5);
		row.MissedExecutionBehavior = MissedExecutionBehavior.ExecuteAllMissed;
		await host.Store.Inner.StoreAsync(row, CancellationToken.None);
		_ = await host.StoreRowAsync("following-row");
		var entered = ScheduleWorkerTestHost.Signal();
		var resume = ScheduleWorkerTestHost.Signal();
		var following = ScheduleWorkerTestHost.Signal();
		var backlogCalls = 0;
		host.Handler.MessageCallback = async (document, ct) =>
		{
			if (document.Marker == "backlog")
			{
				backlogCalls++;
				entered.TrySetResult();
				await resume.Task.WaitAsync(ct);
			}
			else { following.TrySetResult(); }
		};
		await host.Service.StartAsync(CancellationToken.None);
		await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
		await host.Scheduler.CancelScheduleAsync(row.Id, CancellationToken.None);
		resume.TrySetResult();
		await following.Task.WaitAsync(TimeSpan.FromSeconds(10));
		backlogCalls.ShouldBe(1);
		var current = (await host.Store.Inner.GetAllAsync(CancellationToken.None)).Single(s => s.Id == row.Id);
		current.Enabled.ShouldBeFalse();
		current.NextExecutionUtc.ShouldBe(row.NextExecutionUtc);
		current.LastExecutionUtc.ShouldBeNull();
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task AdvanceUnknownOrFailedMessagesOnlyThroughAdmittedProgress(bool failedHandler)
	{
		await using var host = new ScheduleWorkerTestHost();
		var row = await host.StoreRowAsync("bad-row");
		if (!failedHandler) { row.MessageName = "unregistered.scheduler.document"; }
		else { host.Handler.Callback = _ => throw new InvalidOperationException("handler failure"); }
		await host.Store.Inner.StoreAsync(row, CancellationToken.None);
		await host.Service.StartAsync(CancellationToken.None);
		await host.Store.Released.Task.WaitAsync(TimeSpan.FromSeconds(10));
		host.Store.Acquisitions.ShouldBe(1);
		host.Store.ProgressWrites.ShouldBe(1);
		host.Store.ProgressAccepted.ShouldBe((bool?)true);
		(await host.ReadAsync()).NextExecutionUtc.ShouldBe(host.Clock.GetUtcNow().AddMinutes(1));
	}

	[Fact]
	public async Task UseTheCompletionClockForAnOrdinaryIntervalExecution()
	{
		await using var host = new ScheduleWorkerTestHost();
		var row = await host.StoreRowAsync();
		host.Handler.Callback = _ => { host.Clock.Advance(TimeSpan.FromMinutes(2)); return Task.CompletedTask; };
		await host.Service.StartAsync(CancellationToken.None);
		await host.Store.Released.Task.WaitAsync(TimeSpan.FromSeconds(10));
		(await host.ReadAsync()).NextExecutionUtc.ShouldBe(row.NextExecutionUtc!.Value.AddMinutes(3));
	}
}

// This adapter deliberately exposes ONLY the legacy store API. Its underlying row storage is real;
// there is no atomic admission capability and no claim that worker cancellation races are fenced.
internal sealed class LegacyScheduleWorkerTestStore : IScheduleStore
{
	private readonly InMemoryScheduleStore _inner = new();
	public Task<IEnumerable<IScheduledMessage>> GetAllAsync(CancellationToken ct) => _inner.GetAllAsync(ct);
	public Task StoreAsync(IScheduledMessage message, CancellationToken ct) => _inner.StoreAsync(message, ct);
	public Task CompleteAsync(Guid id, CancellationToken ct) => _inner.CompleteAsync(id, ct);
}

internal sealed class LiveReferenceScheduleWorkerTestStore : IScheduleStore
{
	public IScheduledMessage? Row { get; set; }
	public int Writes { get; private set; }
	public Task<IEnumerable<IScheduledMessage>> GetAllAsync(CancellationToken ct) =>
		Task.FromResult<IEnumerable<IScheduledMessage>>(Row is { } row ? [row] : []);
	public Task StoreAsync(IScheduledMessage message, CancellationToken ct)
	{
		ct.ThrowIfCancellationRequested();
		Row = message;
		Writes++;
		return Task.CompletedTask;
	}
	public Task CompleteAsync(Guid id, CancellationToken ct)
	{
		ct.ThrowIfCancellationRequested();
		if (Row is { } row && row.Id == id) { row.Enabled = false; }
		return Task.CompletedTask;
	}
}

internal sealed class LegacyScheduleWorkerTestHost : IAsyncDisposable
{
	private readonly ServiceProvider _provider;
	private DispatchJsonSerializer Serializer => _provider.GetRequiredService<DispatchJsonSerializer>();
	public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(3000, 1, 1, 0, 0, 0, TimeSpan.Zero));
	public LegacyScheduleWorkerTestStore Store { get; } = new();
	public ScheduleWorkerTestHandler Handler { get; } = new();
	public ScheduledMessageService Service { get; }
	public LegacyScheduleWorkerTestHost(int maximum, IScheduleStore? configuredStore = null)
	{
		MessageTypeRegistry.RegisterType<ScheduleWorkerTestDocument>();
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddDispatch(dispatch => dispatch.UseMiddleware<ScheduleWorkerTestMiddleware>());
		services.AddSingleton<DispatchJsonSerializer>();
		services.AddSingleton(Handler);
		services.AddSingleton<IDocumentHandler<ScheduleWorkerTestDocument>>(Handler);
		_provider = services.BuildServiceProvider();
		var options = Microsoft.Extensions.Options.Options.Create(new SchedulerOptions
			{ PollInterval = TimeSpan.FromSeconds(1), EnableAdaptivePolling = false });
		var cronOptions = Microsoft.Extensions.Options.Options.Create(new CronScheduleOptions { MaxMissedExecutions = maximum });
		Service = new ScheduledMessageService(configuredStore ?? Store, _provider.GetRequiredService<IDispatcher>(), Serializer,
			new CronScheduler(cronOptions, NullLogger<CronScheduler>.Instance), options, cronOptions,
			new ScheduleWorkerTestLogger(), _provider.GetRequiredService<IServiceScopeFactory>(), timeProvider: Clock);
	}
	public async Task VerifyDirectDispatchAsync()
	{
		var before = Handler.Calls;
		var result = await _provider.GetRequiredService<IDispatcher>().DispatchAsync(
			new ScheduleWorkerTestDocument("direct-diagnostic-probe"), DispatchContextInitializer.CreateDefaultContext(), CancellationToken.None);
		if (!result.Succeeded || Handler.Calls != before + 1)
		{
			throw new InvalidOperationException($"Direct dispatch diagnostic: Succeeded={result.Succeeded}; Error={result.ErrorMessage}; "
				+ $"Expected handler calls={before + 1}; observed={Handler.Calls}; concrete handler registered={_provider.GetService<ScheduleWorkerTestHandler>() is not null}.");
		}
	}
	public async Task<ScheduledMessage> StoreRowAsync(string marker)
	{
		var row = new ScheduledMessage
		{
			Id = Guid.NewGuid(), Enabled = true, NextExecutionUtc = Clock.GetUtcNow(), Interval = TimeSpan.FromMinutes(1),
			MessageName = typeof(ScheduleWorkerTestDocument).FullName!,
			MessageBody = await Serializer.SerializeAsync(new ScheduleWorkerTestDocument(marker), typeof(ScheduleWorkerTestDocument))
		};
		await Store.StoreAsync(row, CancellationToken.None);
		return row;
	}
	public async ValueTask DisposeAsync()
	{
		await Service.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));
		Service.Dispose();
		await _provider.DisposeAsync();
	}
}
