// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;

using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Registry;
using Excalibur.Dispatch.Middleware;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Options.Scheduling;
using Excalibur.Dispatch.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Pattern", "Concurrency")]
public sealed class ScheduledMessageCancellationShould
{
	[Fact]
	public async Task RefuseAdmissionWhenCancellationWinsAfterTheSnapshotRead()
	{
		await using var host = new ScheduleWorkerTestHost();
		var read = ScheduleWorkerTestHost.Signal();
		var resume = ScheduleWorkerTestHost.Signal();
		host.Store.AfterRead = async ct => { read.TrySetResult(); await resume.Task.WaitAsync(ct); };
		var row = await host.StoreRowAsync();
		await host.Service.StartAsync(CancellationToken.None);
		await read.Task.WaitAsync(TimeSpan.FromSeconds(10));
		await host.Scheduler.CancelScheduleAsync(row.Id, CancellationToken.None);
		resume.TrySetResult();
		await host.Store.Admission.Task.WaitAsync(TimeSpan.FromSeconds(10));
		host.Handler.Calls.ShouldBe(0);
		(await host.ReadAsync()).Enabled.ShouldBeFalse();
	}

	[Fact]
	public async Task PreserveCancellationWhenAnAlreadyAdmittedHandlerFinishes()
	{
		await using var host = new ScheduleWorkerTestHost();
		var entered = ScheduleWorkerTestHost.Signal();
		var resume = ScheduleWorkerTestHost.Signal();
		host.Handler.Callback = async ct => { entered.TrySetResult(); await resume.Task.WaitAsync(ct); };
		var row = await host.StoreRowAsync();
		await host.Service.StartAsync(CancellationToken.None);
		await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
		await host.Scheduler.CancelScheduleAsync(row.Id, CancellationToken.None);
		resume.TrySetResult();
		await host.Store.Released.Task.WaitAsync(TimeSpan.FromSeconds(10));
		host.Handler.Calls.ShouldBe(1);
		host.Store.ProgressAccepted.ShouldBe((bool?)false);
		var current = await host.ReadAsync();
		current.Enabled.ShouldBeFalse();
		current.NextExecutionUtc.ShouldBe(row.NextExecutionUtc);
		current.LastExecutionUtc.ShouldBeNull();
	}

	[Fact]
	public async Task PreserveAnAdministrativeReplacementAfterOldWorkFinishes()
	{
		await using var host = new ScheduleWorkerTestHost();
		var entered = ScheduleWorkerTestHost.Signal();
		var resume = ScheduleWorkerTestHost.Signal();
		host.Handler.Callback = async ct => { entered.TrySetResult(); await resume.Task.WaitAsync(ct); };
		var row = await host.StoreRowAsync();
		await host.Service.StartAsync(CancellationToken.None);
		await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
		row.MessageBody = "administrative replacement";
		row.NextExecutionUtc = host.Clock.GetUtcNow().AddDays(1);
		await host.Store.Inner.StoreAsync(row, CancellationToken.None);
		resume.TrySetResult();
		await host.Store.Released.Task.WaitAsync(TimeSpan.FromSeconds(10));
		host.Store.ProgressAccepted.ShouldBe((bool?)false);
		var current = await host.ReadAsync();
		current.Enabled.ShouldBeTrue();
		current.MessageBody.ShouldBe("administrative replacement");
		current.NextExecutionUtc.ShouldBe(row.NextExecutionUtc);
		current.LastExecutionUtc.ShouldBeNull();
	}

	[Fact]
	public async Task RetainDueWorkAndReleaseOwnershipAfterHostShutdown()
	{
		await using var host = new ScheduleWorkerTestHost();
		var entered = ScheduleWorkerTestHost.Signal();
		host.Handler.Callback = async ct => { entered.TrySetResult(); await Task.Delay(Timeout.Infinite, ct); };
		var row = await host.StoreRowAsync();
		var snapshot = (await host.Store.Inner.GetExecutionSnapshotsAsync(CancellationToken.None)).Single();
		await host.Service.StartAsync(CancellationToken.None);
		await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
		await host.Service.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));
		await host.Store.Released.Task.WaitAsync(TimeSpan.FromSeconds(10));
		host.Store.ReleaseTokenWasCancelled.ShouldBeFalse();
		var current = await host.ReadAsync();
		current.Enabled.ShouldBeTrue();
		current.NextExecutionUtc.ShouldBe(row.NextExecutionUtc);
		current.LastExecutionUtc.ShouldBeNull();
		var lease = await host.Store.Inner.TryAcquireExecutionAsync(row.Id, snapshot.Generation,
			row.NextExecutionUtc!.Value, CancellationToken.None);
		lease.ShouldNotBeNull();
		await host.Store.Inner.ReleaseExecutionAsync(lease, CancellationToken.None);
	}

	[Theory]
	[InlineData(false, 1)]
	[InlineData(true, 0)]
	public async Task LeaveStoreDisposalToTheOwningContainer(bool borrowed, int expectedDisposals)
	{
		var store = new ScheduleWorkerTestStore();
		var host = new ScheduleWorkerTestHost(store, borrowed);
		await host.Service.StartAsync(CancellationToken.None);
		await host.Service.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));
		store.Disposals.ShouldBe(0);
		await host.DisposeAsync();
		store.Disposals.ShouldBe(expectedDisposals);
	}
}

internal sealed record ScheduleWorkerTestDocument(string Marker) : IDispatchDocument;

internal sealed class ScheduleWorkerTestMiddleware : IDispatchMiddleware
{
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;
	public ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context,
		DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken) => nextDelegate(message, context, cancellationToken);
}

internal sealed class ScheduleWorkerTestLogger : ILogger<ScheduledMessageService>
{
	public ConcurrentQueue<(int EventId, IReadOnlyDictionary<string, object?> Values)> Entries { get; } = new();
	public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
	public bool IsEnabled(LogLevel logLevel) => true;
	public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
		Func<TState, Exception?, string> formatter)
	{
		var values = state is IEnumerable<KeyValuePair<string, object?>> structured
			? structured.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)
			: new Dictionary<string, object?>();
		Entries.Enqueue((eventId.Id, values));
		Console.Error.WriteLine($"Scheduler diagnostic [{logLevel}] {formatter(state, exception)} {exception}");
	}
}

internal sealed class ScheduleWorkerTestHandler : IDocumentHandler<ScheduleWorkerTestDocument>
{
	private int _calls;
	public int Calls => Volatile.Read(ref _calls);
	public Func<CancellationToken, Task>? Callback { get; set; }
	public Func<ScheduleWorkerTestDocument, CancellationToken, Task>? MessageCallback { get; set; }
	public async Task HandleAsync(ScheduleWorkerTestDocument document, CancellationToken cancellationToken)
	{
		Interlocked.Increment(ref _calls);
		if (MessageCallback is { } messageCallback) { await messageCallback(document, cancellationToken); }
		if (Callback is { } callback) { await callback(cancellationToken); }
	}
}

// Barriers wrap the real authority; no mutation, generation check or ownership check is mocked.
internal sealed class ScheduleWorkerTestStore(TimeProvider? clock = null) : IScheduleExecutionStore, IAsyncDisposable
{
	// The backing store judges occurrence due-ness against its OWN clock, so it has to be the same clock
	// the host drives. Left on TimeProvider.System it sits in the real present while these harnesses
	// schedule in the year 3000, and every occurrence is permanently "not yet due" -- which surfaces as a
	// dispatch that never happens and a test that times out rather than asserts.
	public InMemoryScheduleStore Inner { get; } = new(clock);
	public TaskCompletionSource Admission { get; } = ScheduleWorkerTestHost.Signal();
	public TaskCompletionSource Released { get; } = ScheduleWorkerTestHost.Signal();
	public Func<CancellationToken, Task>? AfterRead { get; set; }
	public bool? ProgressAccepted { get; private set; }
	public bool ReleaseTokenWasCancelled { get; private set; }
	public int Disposals { get; private set; }
	public int Acquisitions { get; private set; }
	public int ProgressWrites { get; private set; }
	public int Releases { get; private set; }
	public ConcurrentDictionary<Guid, int> ProgressById { get; } = new();
	public ConcurrentDictionary<Guid, int> ReleasesById { get; } = new();
	public ConcurrentQueue<ScheduleExecutionLease> Admitted { get; } = new();
	public async Task<IReadOnlyList<ScheduleExecutionSnapshot>> GetExecutionSnapshotsAsync(CancellationToken ct)
	{
		var snapshots = await Inner.GetExecutionSnapshotsAsync(ct);
		if (AfterRead is { } barrier) { await barrier(ct); }
		return snapshots;
	}
	public async Task<ScheduleExecutionLease?> TryAcquireExecutionAsync(Guid id, Guid generation, DateTimeOffset occurrence, CancellationToken ct)
	{
		var lease = await Inner.TryAcquireExecutionAsync(id, generation, occurrence, ct);
		Acquisitions++;
		if (lease is not null) { Admitted.Enqueue(lease); }
		Admission.TrySetResult();
		return lease;
	}
	public async Task<bool> TryUpdateProgressAsync(ScheduleExecutionLease lease, ScheduleExecutionResult result, CancellationToken ct)
	{
		ProgressAccepted = await Inner.TryUpdateProgressAsync(lease, result, ct);
		ProgressWrites++;
		ProgressById.AddOrUpdate(lease.ScheduleId, 1, static (_, count) => count + 1);
		return ProgressAccepted.Value;
	}
	public async Task<bool> ReleaseExecutionAsync(ScheduleExecutionLease lease, CancellationToken ct)
	{
		ReleaseTokenWasCancelled = ct.IsCancellationRequested;
		var released = await Inner.ReleaseExecutionAsync(lease, ct);
		Releases++;
		ReleasesById.AddOrUpdate(lease.ScheduleId, 1, static (_, count) => count + 1);
		Released.TrySetResult();
		return released;
	}
	public Task<IEnumerable<IScheduledMessage>> GetAllAsync(CancellationToken ct) => Inner.GetAllAsync(ct);
	public Task StoreAsync(IScheduledMessage message, CancellationToken ct) => Inner.StoreAsync(message, ct);
	public Task CompleteAsync(Guid id, CancellationToken ct) => Inner.CompleteAsync(id, ct);
	public ValueTask DisposeAsync() { Disposals++; return ValueTask.CompletedTask; }
}

internal sealed class ScheduleWorkerTestHost : IAsyncDisposable
{
	private readonly ServiceProvider _provider;
	public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(3000, 1, 1, 0, 0, 0, TimeSpan.Zero));
	public ScheduleWorkerTestStore Store { get; }
	public ScheduleWorkerTestHandler Handler { get; } = new();
	public DispatchJsonSerializer Serializer => _provider.GetRequiredService<DispatchJsonSerializer>();
	public ScheduledMessageService Service { get; }
	public RecurringDispatchScheduler Scheduler { get; }
	public CronScheduleOptions CronOptions { get; } = new();
	public ScheduleWorkerTestLogger Logger { get; } = new();
	public ScheduleWorkerTestHost(ScheduleWorkerTestStore? store = null, bool borrowed = false, ITimePolicy? timePolicy = null)
	{
		Store = store ?? new ScheduleWorkerTestStore(Clock);
		MessageTypeRegistry.RegisterType<ScheduleWorkerTestDocument>();
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddDispatch(dispatch => dispatch.UseMiddleware<ScheduleWorkerTestMiddleware>());
		services.AddSingleton<DispatchJsonSerializer>();
		services.AddSingleton(Handler);
		services.AddSingleton<IDocumentHandler<ScheduleWorkerTestDocument>>(Handler);
		if (borrowed) { services.AddSingleton<IScheduleStore>(Store); }
		else { services.AddSingleton<IScheduleStore>(_ => Store); }
		_provider = services.BuildServiceProvider();
		var configuredStore = _provider.GetRequiredService<IScheduleStore>();
		var options = Microsoft.Extensions.Options.Options.Create(new SchedulerOptions
			{ PollInterval = TimeSpan.FromSeconds(1), EnableAdaptivePolling = false });
		var cronOptions = Microsoft.Extensions.Options.Options.Create(CronOptions);
		var cron = new CronScheduler(cronOptions, NullLogger<CronScheduler>.Instance);
		Service = new ScheduledMessageService(configuredStore, _provider.GetRequiredService<IDispatcher>(),
			Serializer, cron, options, cronOptions, Logger, _provider.GetRequiredService<IServiceScopeFactory>(), timePolicy: timePolicy, timeProvider: Clock);
		Scheduler = new RecurringDispatchScheduler(configuredStore, Serializer, options, cron, cronOptions,
			NullLogger<RecurringDispatchScheduler>.Instance, Clock);
	}
	public async Task<ScheduledMessage> StoreRowAsync(string marker = "persisted")
	{
		var row = new ScheduledMessage
		{
			Id = Guid.NewGuid(), Enabled = true, NextExecutionUtc = Clock.GetUtcNow(), Interval = TimeSpan.FromMinutes(1),
			MessageName = typeof(ScheduleWorkerTestDocument).FullName!,
			MessageBody = await Serializer.SerializeAsync(new ScheduleWorkerTestDocument(marker), typeof(ScheduleWorkerTestDocument))
		};
		await Store.Inner.StoreAsync(row, CancellationToken.None);
		return row;
	}
	public async Task<IScheduledMessage> ReadAsync() => (await Store.Inner.GetAllAsync(CancellationToken.None)).Single();
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
	public static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
	public async ValueTask DisposeAsync()
	{
		await Service.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));
		Service.Dispose();
		await _provider.DisposeAsync();
	}
}
