// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Registry;
using Excalibur.Dispatch.Options.Scheduling;
using Excalibur.Dispatch.Serialization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Excalibur.Dispatch.Tests.Delivery.Scheduling;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "Regression")]
public sealed class DisabledMissedScheduleShould
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PersistTheDisabledRowWithoutDispatchingItAndContinueTheHealthyRow(bool disableStoreFails)
    {
        var now = new DateTimeOffset(3000, 1, 1, 0, 0, 0, TimeSpan.Zero);
        using var serializer = new DispatchJsonSerializer();
        MessageTypeRegistry.RegisterType<MissedScheduleAction>();
        var disabled = await CreateRowAsync("disabled", now.AddMinutes(-10));
        disabled.LastExecutionUtc = now.AddMinutes(-15);
        disabled.MissedExecutionBehavior = MissedExecutionBehavior.DisableSchedule;
        var healthy = await CreateRowAsync("healthy", now);
        var originalLast = disabled.LastExecutionUtc;
        var originalNext = disabled.NextExecutionUtc;
        var dispatched = new ConcurrentQueue<string>();
        var writes = new ConcurrentQueue<(Guid Id, bool Enabled, DateTimeOffset? Last, DateTimeOffset? Next)>();
        var healthyAdvanced = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var disableAttempts = 0;
        var accepted = new ConcurrentDictionary<Guid, IScheduledMessage>();
        accepted[disabled.Id] = disabled;
        accepted[healthy.Id] = healthy;
        var store = A.Fake<IScheduleStore>();
        A.CallTo(() => store.GetAllAsync(A<CancellationToken>._))
            .ReturnsLazily(() => Task.FromResult<IEnumerable<IScheduledMessage>>([accepted[disabled.Id], accepted[healthy.Id]]));
        A.CallTo(() => store.StoreAsync(A<IScheduledMessage>._, A<CancellationToken>._))
            .Invokes((IScheduledMessage row, CancellationToken _) =>
            {
                writes.Enqueue((row.Id, row.Enabled, row.LastExecutionUtc, row.NextExecutionUtc));
                if (row.Id == disabled.Id)
                {
                    disableAttempts++;
                    if (disableStoreFails) { throw new IOException("Disable persistence rejected"); }
                }
                // Accepted writes become authoritative; rejected writes leave the prior row intact.
                accepted[row.Id] = ScheduleExecutionSnapshot.Copy(row);
                if (row.Id == healthy.Id && dispatched.Contains("healthy") && row.NextExecutionUtc > now)
                {
                    healthyAdvanced.TrySetResult();
                }
            }).Returns(Task.CompletedTask);
        var dispatcher = A.Fake<IDispatcher>();
        A.CallTo(() => dispatcher.DispatchAsync(A<IDispatchAction>._, A<IMessageContext>._, A<CancellationToken>._))
            .Invokes((IDispatchAction action, IMessageContext _, CancellationToken _) =>
                dispatched.Enqueue(((MissedScheduleAction)action).Marker))
            .Returns(Task.FromResult(MessageResult.Success()));
        using var scopeProvider = new ServiceCollection().BuildServiceProvider();
		using var service = new ScheduledMessageService(store, dispatcher, serializer, A.Fake<ICronScheduler>(),
            Microsoft.Extensions.Options.Options.Create(new SchedulerOptions
            {
                PollInterval = TimeSpan.FromSeconds(1), EnableAdaptivePolling = false
            }), Microsoft.Extensions.Options.Options.Create(new CronScheduleOptions()),
            NullLogger<ScheduledMessageService>.Instance, scopeProvider.GetRequiredService<IServiceScopeFactory>(), timeProvider: new FakeTimeProvider(now));
        try
        {
            await service.StartAsync(CancellationToken.None);
            var loop = service.ExecuteTask.ShouldNotBeNull();
            var finished = await Task.WhenAny(healthyAdvanced.Task, loop).WaitAsync(TimeSpan.FromSeconds(30));
            if (finished == loop)
            {
                await loop;
                throw new InvalidOperationException("Scheduler ended before the healthy row dispatched and advanced.");
            }
            await healthyAdvanced.Task;
        }
        finally
        {
            await service.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));
        }
        dispatched.ToArray().ShouldBe(["healthy"]);
        var disabledWrites = writes.Where(write => write.Id == disabled.Id).ToArray();
        disabledWrites.Length.ShouldBe(1, "a rejected progress write must not trigger a second administrative upsert");
        disableAttempts.ShouldBe(1);
        disabledWrites[0].Enabled.ShouldBeFalse();
        disabledWrites[0].Last.ShouldBe(originalLast);
        disabledWrites[0].Next.ShouldBe(originalNext);
        var storedDisabled = accepted[disabled.Id];
        storedDisabled.Enabled.ShouldBe(disableStoreFails);
        storedDisabled.LastExecutionUtc.ShouldBe(originalLast);
        storedDisabled.NextExecutionUtc.ShouldBe(originalNext);
        disabled.Enabled.ShouldBeTrue("the worker cannot mutate a live inventory row before storage accepts progress");
        disabled.LastExecutionUtc.ShouldBe(originalLast);
        disabled.NextExecutionUtc.ShouldBe(originalNext);
        var storedHealthy = accepted[healthy.Id];
        storedHealthy.Enabled.ShouldBeTrue();
        storedHealthy.LastExecutionUtc.ShouldBe(now);
        storedHealthy.NextExecutionUtc.ShouldBe(now.AddMinutes(5));
        healthy.LastExecutionUtc.ShouldBeNull();
        healthy.NextExecutionUtc.ShouldBe(now);

        async Task<ScheduledMessage> CreateRowAsync(string marker, DateTimeOffset due) => new()
        {
            Id = Guid.NewGuid(), Enabled = true, Interval = TimeSpan.FromMinutes(5), CronExpression = string.Empty,
            NextExecutionUtc = due, MessageName = typeof(MissedScheduleAction).FullName!,
            MessageBody = await serializer.SerializeAsync(new MissedScheduleAction(marker), typeof(MissedScheduleAction))
        };
    }

    private sealed record MissedScheduleAction(string Marker) : IDispatchAction;
}
