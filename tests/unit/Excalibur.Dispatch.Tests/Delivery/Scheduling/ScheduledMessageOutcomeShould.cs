// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Registry;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Middleware;
using Excalibur.Dispatch.Options.Scheduling;
using Excalibur.Dispatch.Serialization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Excalibur.Dispatch.Tests.Delivery.Scheduling;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "Regression")]
public sealed class ScheduledMessageOutcomeShould
{
    public enum MessageKind { Action, Event, Document }
    private static readonly MessageKind[] MessageCases = Enum.GetValues<MessageKind>();
    private static readonly MissedExecutionBehavior[] CatchUpCases =
        [MissedExecutionBehavior.ExecuteLatestMissed, MissedExecutionBehavior.ExecuteAllMissed];

    public enum Outcome { Failed, Null, Cancelled, FailedWithoutDetails, Handled, Cached, Duplicate, Background, Deadline, Shutdown }

    public static TheoryData<MessageKind, MissedExecutionBehavior, Outcome> Cases
    {
        get
        {
            var cases = new TheoryData<MessageKind, MissedExecutionBehavior, Outcome>();
            foreach (var kind in MessageCases)
            foreach (var behavior in CatchUpCases)
            foreach (var outcome in Enum.GetValues<Outcome>().Where(o => o != Outcome.Shutdown))
                cases.Add(kind, behavior, outcome);
            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public Task ClassifyReturnedOutcomeAndContinueTheFollowingRow(MessageKind kind, MissedExecutionBehavior behavior, Outcome outcome) =>
        VerifyAsync(kind, behavior, outcome);

    [Theory]
    [InlineData(MessageKind.Action, MissedExecutionBehavior.ExecuteLatestMissed)]
    [InlineData(MessageKind.Event, MissedExecutionBehavior.ExecuteLatestMissed)]
    [InlineData(MessageKind.Action, MissedExecutionBehavior.ExecuteAllMissed)]
    [InlineData(MessageKind.Event, MissedExecutionBehavior.ExecuteAllMissed)]
    [InlineData(MessageKind.Document, MissedExecutionBehavior.ExecuteLatestMissed)]
    [InlineData(MessageKind.Document, MissedExecutionBehavior.ExecuteAllMissed)]
    public Task PreserveHostCancellationWithoutRetiringTheRow(MessageKind kind, MissedExecutionBehavior behavior) =>
        VerifyAsync(kind, behavior, Outcome.Shutdown);

    [Fact]
    public async Task DeliverADocumentThroughTheRealPipelineBeforeRecordingSuccess()
    {
        var now = new DateTimeOffset(3000, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var recorder = new DocumentRecorder();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(recorder);
        services.AddDispatch(d => d.UseMiddleware<DocumentMiddleware>());
        services.AddTransient<IDocumentHandler<OutcomeDocument>, DocumentHandler>();
        await using var provider = services.BuildServiceProvider();
        var serializer = new DispatchJsonSerializer();
        MessageTypeRegistry.RegisterType<OutcomeDocument>();
        var row = new ScheduledMessage
        {
            Id = Guid.NewGuid(), Enabled = true, Interval = TimeSpan.FromMinutes(5),
            NextExecutionUtc = now, TenantId = "scheduled-document-tenant",
            MessageName = typeof(OutcomeDocument).FullName!,
            MessageBody = await serializer.SerializeAsync(new OutcomeDocument("persisted-payload"), typeof(OutcomeDocument))
        };
        var written = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        IScheduledMessage? persisted = null;
        var store = A.Fake<IScheduleStore>();
        A.CallTo(() => store.GetAllAsync(A<CancellationToken>._))
            .Returns(Task.FromResult<IEnumerable<IScheduledMessage>>([row]));
        A.CallTo(() => store.StoreAsync(A<IScheduledMessage>._, A<CancellationToken>._))
            .Invokes((IScheduledMessage updated, CancellationToken _) => { persisted = updated; written.TrySetResult(); }).Returns(Task.CompletedTask);
        var policy = A.Fake<ITimePolicy>(o => o.Implements<ITimePolicyConfiguration>());
        var cronOptions = Microsoft.Extensions.Options.Options.Create(new CronScheduleOptions());
        using var service = new ScheduledMessageService(store, provider.GetRequiredService<IDispatcher>(), serializer,
            new CronScheduler(cronOptions, NullLogger<CronScheduler>.Instance),
            Microsoft.Extensions.Options.Options.Create(new SchedulerOptions { PollInterval = TimeSpan.FromSeconds(1), EnableAdaptivePolling = false }),
            cronOptions, NullLogger<ScheduledMessageService>.Instance, provider.GetRequiredService<IServiceScopeFactory>(), policy, timeProvider: new FakeTimeProvider(now));
        try
        {
            await service.StartAsync(CancellationToken.None);
            await written.Task.WaitAsync(TimeSpan.FromSeconds(10));
            recorder.HandlerPayload.ShouldBe("persisted-payload");
            recorder.MiddlewareTenant.ShouldBe(row.TenantId);
            recorder.HandlerCalls.ShouldBe(1);
            recorder.MiddlewareCalls.ShouldBe(1);
            var saved = persisted.ShouldNotBeNull();
            saved.LastExecutionUtc.ShouldBe(now);
            saved.NextExecutionUtc.ShouldBe(now.AddMinutes(5));
            row.LastExecutionUtc.ShouldBeNull("the worker must not mutate a live inventory row before storage accepts its progress");
        }
        finally
        {
            await service.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));
            var loop = service.ExecuteTask.ShouldNotBeNull();
            try { await loop; }
            catch (OperationCanceledException) when (loop.IsCanceled) { }
        }
    }

    private sealed class DocumentRecorder
    {
        public string? HandlerPayload { get; set; }
        public string? MiddlewareTenant { get; set; }
        public int HandlerCalls { get; set; }
        public int MiddlewareCalls { get; set; }
    }

    private sealed class DocumentHandler(DocumentRecorder recorder) : IDocumentHandler<OutcomeDocument>
    {
        public Task HandleAsync(OutcomeDocument document, CancellationToken cancellationToken)
        {
            recorder.HandlerPayload = document.Marker;
            recorder.HandlerCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class DocumentMiddleware(DocumentRecorder recorder) : IDispatchMiddleware
    {
        public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;
        public ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context,
            DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
        {
            recorder.MiddlewareTenant = context.GetOrCreateIdentityFeature().TenantId;
            recorder.MiddlewareCalls++;
            return nextDelegate(message, context, cancellationToken);
        }
    }

    private static async Task VerifyAsync(MessageKind kind, MissedExecutionBehavior behavior, Outcome outcome)
    {
        var now = new DateTimeOffset(3000, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var serializer = new DispatchJsonSerializer();
        MessageTypeRegistry.RegisterType<OutcomeAction>();
        MessageTypeRegistry.RegisterType<OutcomeEvent>();
        MessageTypeRegistry.RegisterType<OutcomeDocument>();
        var overdue = await CreateRowAsync("overdue", now.AddMinutes(-3));
        overdue.LastExecutionUtc = now.AddMinutes(-5);
        overdue.CronExpression = "* * * * *";
        overdue.MissedExecutionBehavior = behavior;
        var healthy = await CreateRowAsync("healthy", now);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var advanced = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var writes = new ConcurrentQueue<(DateTimeOffset? Last, DateTimeOffset? Next)>();
        var overdueCalls = 0;
        var healthyCalls = 0;
        var store = A.Fake<IScheduleStore>();
        A.CallTo(() => store.GetAllAsync(A<CancellationToken>._))
            .Returns(Task.FromResult<IEnumerable<IScheduledMessage>>([overdue, healthy]));
        A.CallTo(() => store.StoreAsync(A<IScheduledMessage>._, A<CancellationToken>._))
            .Invokes((IScheduledMessage row, CancellationToken _) =>
            {
                if (row.Id == overdue.Id) writes.Enqueue((row.LastExecutionUtc, row.NextExecutionUtc));
                if (row.Id == healthy.Id && row.NextExecutionUtc > now) advanced.TrySetResult();
            }).Returns(Task.CompletedTask);
        var dispatcher = A.Fake<IDispatcher>();
        A.CallTo(() => dispatcher.DispatchAsync(A<IDispatchAction>._, A<IMessageContext>._, A<CancellationToken>._))
            .ReturnsLazily((IDispatchAction action, IMessageContext _, CancellationToken token) => DispatchAsync(((OutcomeAction)action).Marker, token));
        A.CallTo(() => dispatcher.DispatchAsync(A<IDispatchEvent>._, A<IMessageContext>._, A<CancellationToken>._))
            .ReturnsLazily((IDispatchEvent evt, IMessageContext _, CancellationToken token) => DispatchAsync(((OutcomeEvent)evt).Marker, token));
        A.CallTo(() => dispatcher.DispatchAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
            .ReturnsLazily((IDispatchMessage document, IMessageContext _, CancellationToken token) => DispatchAsync(((OutcomeDocument)document).Marker, token));
        var policy = A.Fake<ITimePolicy>(o => o.Implements<ITimePolicyConfiguration>());
        A.CallTo(() => ((ITimePolicyConfiguration)policy).ShouldApplyTimeout(A<TimeoutOperationType>._, A<TimeoutContext?>._))
            .ReturnsLazily((TimeoutOperationType operation, TimeoutContext? _) =>
                outcome == Outcome.Deadline && operation == TimeoutOperationType.Handler);
        A.CallTo(() => policy.GetTimeoutFor(TimeoutOperationType.Handler)).Returns(TimeSpan.FromMilliseconds(50));
        var cronOptions = Microsoft.Extensions.Options.Options.Create(new CronScheduleOptions());
        var clock = new FakeTimeProvider(now);
        using var scopeProvider = new ServiceCollection().BuildServiceProvider();
        using var service = new ScheduledMessageService(store, dispatcher, serializer,
            new CronScheduler(cronOptions, NullLogger<CronScheduler>.Instance),
            Microsoft.Extensions.Options.Options.Create(new SchedulerOptions { PollInterval = TimeSpan.FromSeconds(1), EnableAdaptivePolling = false }),
            cronOptions, NullLogger<ScheduledMessageService>.Instance, scopeProvider.GetRequiredService<IServiceScopeFactory>(), policy, timeProvider: clock);
        try
        {
            await service.StartAsync(CancellationToken.None);
            await Task.WhenAny(entered.Task, advanced.Task).WaitAsync(TimeSpan.FromSeconds(10));
            entered.Task.IsCompleted.ShouldBeTrue("the worker must dispatch the row before recording successful execution");
            if (outcome == Outcome.Deadline)
            {
                clock.Advance(TimeSpan.FromMilliseconds(49));
                advanced.Task.IsCompleted.ShouldBeFalse("the active handler deadline must not expire before its boundary");
                Volatile.Read(ref healthyCalls).ShouldBe(0);
                clock.Advance(TimeSpan.FromMilliseconds(1));
            }
            if (outcome == Outcome.Shutdown)
            {
                await service.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));
                writes.ShouldBeEmpty("host cancellation must not persist failure retirement");
                overdue.LastExecutionUtc.ShouldBe(now.AddMinutes(-5));
                Volatile.Read(ref healthyCalls).ShouldBe(0);
            }
            else
            {
                await advanced.Task.WaitAsync(TimeSpan.FromSeconds(10));
                Volatile.Read(ref healthyCalls).ShouldBe(1);
                var success = outcome is Outcome.Handled or Outcome.Cached or Outcome.Duplicate or Outcome.Background;
                if (success)
                {
                    var expected = behavior == MissedExecutionBehavior.ExecuteLatestMissed ? 1 : 4;
                    Volatile.Read(ref overdueCalls).ShouldBe(expected);
                    var progress = writes.ToArray();
                    progress.Length.ShouldBe(expected, "each actual persisted occurrence has one progress update");
                    progress.All(p => p.Last == now).ShouldBeTrue();
                    progress.Select(p => p.Next).ShouldBe(behavior == MissedExecutionBehavior.ExecuteLatestMissed
                        ? new DateTimeOffset?[] { now.AddMinutes(1) }
                        : Enumerable.Range(1, 4).Select(index => (DateTimeOffset?)now.AddMinutes(-3 + index)).ToArray());
                }
                else
                {
                    Volatile.Read(ref overdueCalls).ShouldBe(1, "an unsuccessful replay must stop catch-up");
                    var written = writes.Single();
                    written.Next.ShouldNotBeNull().ShouldBeGreaterThan(now);
                    written.Last.ShouldBe(now.AddMinutes(-5), "a failed dispatch must not overwrite the prior execution timestamp");
                }
            }
        }
        finally
        {
            await service.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));
            var loop = service.ExecuteTask.ShouldNotBeNull();
            try { await loop; }
            catch (OperationCanceledException) when (loop.IsCanceled) { }
        }

        async Task<IMessageResult> DispatchAsync(string marker, CancellationToken token)
        {
            if (marker == "healthy") { Interlocked.Increment(ref healthyCalls); return MessageResult.Success(); }
            Interlocked.Increment(ref overdueCalls);
            entered.TrySetResult();
            if (outcome is Outcome.Deadline or Outcome.Shutdown)
            {
                try { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { return MessageResult.Cancelled(); }
                throw new InvalidOperationException("An infinite handler completed without cancellation.");
            }
            return outcome switch
            {
                Outcome.Failed => MessageResult.Failed("dispatch rejected"),
                Outcome.Null => null!,
                Outcome.Cancelled => MessageResult.Cancelled(),
                Outcome.FailedWithoutDetails => new ProbeResult(false, MessageDisposition.Handled),
                Outcome.Cached => new ProbeResult(true, MessageDisposition.ServedFromCache),
                Outcome.Duplicate => new ProbeResult(true, MessageDisposition.SuppressedAsDuplicate),
                Outcome.Background => new ProbeResult(true, MessageDisposition.AcceptedForBackgroundExecution),
                _ => MessageResult.Success()
            };
        }

        async Task<ScheduledMessage> CreateRowAsync(string marker, DateTimeOffset due)
        {
            IDispatchMessage message = kind switch
            {
                MessageKind.Event => new OutcomeEvent(marker),
                MessageKind.Document => new OutcomeDocument(marker),
                _ => new OutcomeAction(marker)
            };
            return new ScheduledMessage
            {
                Id = Guid.NewGuid(), Enabled = true, Interval = TimeSpan.FromMinutes(5), NextExecutionUtc = due,
                MessageName = message.GetType().FullName!, MessageBody = await serializer.SerializeAsync(message, message.GetType())
            };
        }
    }

    private sealed record OutcomeAction(string Marker) : IDispatchAction;
    private sealed record OutcomeEvent(string Marker) : IDispatchEvent;
    private sealed record OutcomeDocument(string Marker) : IDispatchDocument;
    private sealed record ProbeResult(bool Succeeded, MessageDisposition Disposition) : IMessageResult
    {
        public string? ErrorMessage => null;
        public object? ValidationResult => null;
        public object? AuthorizationResult => null;
        public IMessageProblemDetails? ProblemDetails => null;
    }
}
