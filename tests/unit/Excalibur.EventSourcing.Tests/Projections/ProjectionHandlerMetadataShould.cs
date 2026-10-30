// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing.Projections;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.Subscriptions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Tests.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
[Trait("Pattern", "Regression")]
public sealed class ProjectionHandlerMetadataShould
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, false, true)]
    public async Task DeliverEachStoredEventsMetadataAcrossStreamsAndWithinAStream(bool inline, bool overrideTarget, bool immutable)
    {
        var registry = new InMemoryProjectionRegistry();
        var services = new ServiceCollection();
        var observed = new List<ProjectionHandlerContext>();
        services.AddSingleton(observed);
        services.AddTransient<RecordingHandler>();
        services.AddSingleton<IProjectionStore<MetadataModel>>(new InMemoryProjectionStore<MetadataModel>());
        if (immutable)
        {
            var builder = new ImmutableProjectionBuilder<MetadataModel>(services);
            if (inline) builder.Inline(); else builder.Async();
            builder.WhenHandledBy<MetadataEvent, RecordingHandler>();
            builder.Build(registry);
        }
        else
        {
            var builder = new ProjectionBuilder<MetadataModel>(registry);
            if (inline) builder.Inline(); else builder.Async();
            builder.WhenHandledBy<MetadataEvent, RecordingHandler>();
            builder.Build();
        }
        var stored = new[]
        {
            Row("a", "Account", 3, 1),
            Row("b", "Branch", 0, 2),
            Row("a", "Account", 4, 3),
            // An unhandled final event must not supply metadata for any earlier handler.
            Row("c", "Customer", 19, 4) with { EventType = "Unhandled" },
        };
        var query = A.Fake<IGlobalStreamQuery>();
        A.CallTo(() => query.GetHeadPositionAsync(A<CancellationToken>._)).Returns(new ValueTask<long>(4));
        A.CallTo(() => query.ReadAllAsync(A<GlobalStreamPosition>._, A<int>._, A<CancellationToken>._))
            .ReturnsLazily((GlobalStreamPosition position, int _, CancellationToken _) =>
                new ValueTask<IReadOnlyList<StoredEvent>>(stored.Where(e => e.GlobalPosition > position.Position).ToArray()));
        services.AddSingleton(query);
        var serializer = A.Fake<IEventSerializer>();
        A.CallTo(() => serializer.ResolveType("Handled")).Returns(typeof(MetadataEvent));
        A.CallTo(() => serializer.ResolveType("Unhandled")).Returns(typeof(UnhandledEvent));
        A.CallTo(() => serializer.DeserializeEvent(A<byte[]>._, typeof(MetadataEvent))).ReturnsLazily(() => new MetadataEvent(overrideTarget));
        A.CallTo(() => serializer.DeserializeEvent(A<byte[]>._, typeof(UnhandledEvent))).ReturnsLazily(() => new UnhandledEvent());
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var host = new AsyncProjectionProcessingHost(registry, serializer, new InMemorySubscriptionCheckpointStore(),
            Options.Create(new GlobalStreamProjectionOptions { SubscriptionScope = "metadata-regression" }),
            provider, NullLogger<AsyncProjectionProcessingHost>.Instance, mode: inline ? ProjectionMode.Inline : ProjectionMode.Async);
        var result = await host.ProcessPendingAsync(TestContext.Current.CancellationToken);
        result.Outcome.ShouldBe(ProjectionProcessingOutcome.CaughtUp);
        observed.Count.ShouldBe(overrideTarget ? 6 : 3);
        for (var i = 0; i < observed.Count; i++)
        {
            var expected = stored[overrideTarget ? i / 2 : i];
            observed[i].AggregateId.ShouldBe(expected.AggregateId);
            observed[i].AggregateType.ShouldBe(expected.AggregateType);
            observed[i].CommittedVersion.ShouldBe(expected.Version);
            observed[i].Timestamp.ShouldBe(expected.Timestamp);
            observed[i].IsReplay.ShouldBeFalse();
        }
    }

    private static StoredEvent Row(string id, string type, long version, long position) =>
        new($"event-{position}", id, type, "Handled", [], null, version, DateTimeOffset.UnixEpoch.AddMinutes(position))
        { GlobalPosition = position };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RetainNotificationMetadataForDirectDeliveryWithoutStoredEnvelope(bool immutable)
    {
        var registry = new InMemoryProjectionRegistry();
        var services = new ServiceCollection();
        var observed = new List<ProjectionHandlerContext>();
        services.AddSingleton(observed);
        services.AddTransient<RecordingHandler>();
        services.AddSingleton<IProjectionStore<MetadataModel>>(new InMemoryProjectionStore<MetadataModel>());
        if (immutable)
        {
            var builder = new ImmutableProjectionBuilder<MetadataModel>(services);
            builder.WhenHandledBy<MetadataEvent, RecordingHandler>();
            builder.Build(registry);
        }
        else
        {
            var builder = new ProjectionBuilder<MetadataModel>(registry);
            builder.WhenHandledBy<MetadataEvent, RecordingHandler>();
            builder.Build();
        }
        using var provider = services.BuildServiceProvider();
        var context = new EventNotificationContext("batch", "Legacy", 7, DateTimeOffset.UnixEpoch, IsReplay: true);
        await registry.GetRegistration(typeof(MetadataModel))!.InlineApply!(
            [new ProjectionEvent(new MetadataEvent(), "event-aggregate", 1)], context, provider, TestContext.Current.CancellationToken);
        observed.Count.ShouldBe(1);
        observed[0].AggregateId.ShouldBe("event-aggregate");
        observed[0].AggregateType.ShouldBe("Legacy");
        observed[0].CommittedVersion.ShouldBe(7);
        observed[0].Timestamp.ShouldBe(DateTimeOffset.UnixEpoch);
        observed[0].IsReplay.ShouldBeTrue();
    }

    [MessageName("test.projection-handler-metadata.event")]
    private sealed record MetadataEvent(bool OverrideTarget = false) : DomainEvent;
    [MessageName("test.projection-handler-metadata.unhandled")]
    private sealed record UnhandledEvent : DomainEvent;
    private sealed class MetadataModel { public int Count { get; set; } }
    private sealed class RecordingHandler(List<ProjectionHandlerContext> observed) : IProjectionEventHandler<MetadataModel, MetadataEvent>, IImmutableProjectionHandler<MetadataModel, MetadataEvent>
    {
        public Task HandleAsync(MetadataModel projection, MetadataEvent @event, ProjectionHandlerContext context, CancellationToken cancellationToken)
        {
            observed.Add(context);
            if (@event.OverrideTarget) context.OverrideProjectionId = "mirror-" + context.AggregateId;
            projection.Count++;
            return Task.CompletedTask;
        }

        public Task<MetadataModel> TransformAsync(MetadataModel? current, MetadataEvent @event, ProjectionHandlerContext context, CancellationToken cancellationToken)
        {
            observed.Add(context);
            return Task.FromResult(new MetadataModel { Count = (current?.Count ?? 0) + 1 });
        }
    }
}
