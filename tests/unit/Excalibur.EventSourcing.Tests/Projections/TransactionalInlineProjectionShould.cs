// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using System.Text.Json;
using System.Text.Json.Serialization;
using Excalibur.Dispatch;
using Excalibur.EventSourcing.Projections;
using Excalibur.EventSourcing.SqlServer;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Subscriptions;
using Excalibur.EventSourcing.Implementation;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Tests.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
[Trait("Pattern", "TransactionalInline")]
public sealed class TransactionalInlineProjectionShould
{
    [Fact]
    public void PreserveLegacyCustomBuilderCompatibilityAndRejectUnsupportedTransactionalMode()
    {
        IProjectionBuilder<TransactionalCountModel> legacy = new LegacyBuilder<TransactionalCountModel>();
        legacy.Inline().ShouldBeSameAs(legacy);
        legacy.Async().ShouldBeSameAs(legacy);
        Should.Throw<NotSupportedException>(() => legacy.TransactionalInline("Order"));
    }

    [Fact]
    public async Task RejectExplicitTransactionalOutboxWithoutItsWriterBeforeAppend()
    {
        var coordinator = A.Fake<ITransactionalProjectionCoordinator>();
        A.CallTo(() => coordinator.Handles(A<string>._)).Returns(true);
        var store = A.Fake<IEventStore>();
        var repository = new EventSourcedRepository<EventSourcedRepositoryShould.TestAggregate>(store,
            A.Fake<IEventSerializer>(), _ => new EventSourcedRepositoryShould.TestAggregate(),
            Options.Create(new EventSourcedRepositoryOptions { OutboxStagingStrategy = OutboxStagingStrategy.Transactional }))
        {
            TransactionalProjections = coordinator
        };
        var aggregate = new EventSourcedRepositoryShould.TestAggregate("transactional-order");
        aggregate.Create("pending");
        await Should.ThrowAsync<InvalidOperationException>(() => repository.SaveAsync(aggregate, CancellationToken.None));
        aggregate.GetUncommittedEvents().Count.ShouldBe(1);
        FakeItEasy.Fake.GetCalls(store).ShouldBeEmpty();
    }

    [Fact]
    public void ValidateDestinationCapabilityWithoutStartingAHost()
    {
        var registry = new InMemoryProjectionRegistry();
        var builder = new ProjectionBuilder<TransactionalCountModel>(registry);
        builder.TransactionalInline("Order");
        builder.Build();
        var store = A.Fake<IEventStore>();
        A.CallTo(() => store.GetService(typeof(ITransactionalProjectionEventStore)))
            .Returns(A.Fake<ITransactionalProjectionEventStore>());
        using var services = new ServiceCollection().AddSingleton<IProjectionRegistry>(registry)
            .AddKeyedSingleton("default", store)
            .AddSingleton(A.Fake<IProjectionStore<TransactionalCountModel>>()).BuildServiceProvider();
        Should.Throw<InvalidOperationException>(() => new TransactionalProjectionStartup(services).Validate())
            .Message.ShouldContain("TransactionalInline");
    }

    [Fact]
    public async Task RejectGlobalSessionReadsInsteadOfReportingPermanentLag()
    {
        var registry = new InMemoryProjectionRegistry();
        var builder = new ProjectionBuilder<TransactionalCountModel>(registry);
        builder.TransactionalInline("Order");
        builder.Build();
        await using var services = new ServiceCollection()
            .AddSingleton(A.Fake<IProjectionStore<TransactionalCountModel>>()).BuildServiceProvider();
        var reader = new ProjectionSessionReader(services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new ProjectionSessionOptions()), Options.Create(new GlobalStreamProjectionOptions()), registry: registry);
        await Should.ThrowAsync<NotSupportedException>(async () => await reader.ReadAsync<TransactionalCountModel>(
            "row", new SaveResult(1, 10, ProjectionFreshness.NotRequested), CancellationToken.None));
    }

    [Fact]
    public void KeepExistingModeValuesAndDelegatesUnchanged()
    {
        ((int)ProjectionMode.Async).ShouldBe(0);
        ((int)ProjectionMode.Ephemeral).ShouldBe(1);
        ((int)ProjectionMode.Inline).ShouldBe(2);
        var registry = new InMemoryProjectionRegistry();
        var builder = new ProjectionBuilder<TransactionalCountModel>(registry);
        builder.TransactionalInline("Order").When<Added>((model, value) => model.Total += value.Amount);
        builder.Build();
        var registration = registry.GetRegistration(typeof(TransactionalCountModel))!;
        registration.TransactionalAggregateType.ShouldBe("Order");
        registration.TransactionalApply.ShouldNotBeNull();
        registration.InlineApply.ShouldBeNull();
    }

    [Fact]
    public void RejectCrossStreamKeys()
    {
        var builder = new ProjectionBuilder<TransactionalCountModel>(new InMemoryProjectionRegistry());
        builder.TransactionalInline("Order").When<Added>((model, value) => model.Total += value.Amount)
            .KeyedBy<Added>(_ => "shared");
        Should.Throw<NotSupportedException>(() => builder.Build());
    }

    [Fact]
    public async Task FoldCompleteHistoryThroughTheCanonicalDeserializer()
    {
        var fixture = new Fixture();
        fixture.History = [new StoredEvent("old", "row", "Order", "Added", [1], null, 0, DateTimeOffset.UtcNow)];
        var calls = 0;
        var result = await fixture.Coordinator.AppendAsync(fixture.Store, "row", "Order", [new Added(7)], 0,
            _ => { calls++; return new Added(5); }, null, CancellationToken.None);
        result.Success.ShouldBeTrue();
        calls.ShouldBe(1);
        fixture.Written.ShouldBe("12");
        fixture.Coordinator.Handles("Other").ShouldBeFalse();
    }

    [Theory]
    [InlineData(1, "Added")]
    [InlineData(0, "$erased")]
    public async Task RefuseIncompleteOrErasedHistoryBeforeWriting(long version, string type)
    {
        var fixture = new Fixture();
        fixture.History = [new StoredEvent("old", "row", "Order", type, [1], null, version, DateTimeOffset.UtcNow)];
        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await fixture.Coordinator.AppendAsync(fixture.Store, "row", "Order", [new Added(7)], 0,
                _ => new Added(5), null, CancellationToken.None));
        fixture.Written.ShouldBeNull();
    }

    [Fact]
    public async Task RejectUnsupportedEventStoreBeforeWriting()
    {
        var fixture = new Fixture();
        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await fixture.Coordinator.AppendAsync(A.Fake<IEventStore>(), "row", "Order", [new Added(7)], -1,
                _ => new Added(5), null, CancellationToken.None));
        fixture.Written.ShouldBeNull();
    }

    [Fact]
    public void SerializeGeneratedModelsUsingTheStoreWireFormat()
    {
        var tenant = A.Fake<ITenantContext>();
        A.CallTo(() => tenant.TenantId).Returns("tenant");
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
        options.Converters.Add(new CountConverter());
        var store = new SqlServerProjectionStore<TransactionalCountModel>("Server=unused;Database=unused", NullLogger<SqlServerProjectionStore<TransactionalCountModel>>.Instance, tenant, jsonOptions: options);
        var json = store.Serialize(new TransactionalCountModel { Total = 42 }, TransactionalModelJson.Default.TransactionalCountModel);
        JsonSerializer.Deserialize<TransactionalCountModel>(json, options).ShouldNotBeNull().Total.ShouldBe(42);
        json.ShouldBe("\"count:42\"");
    }

    [MessageName("test.transactional-inline.added")]
    internal sealed record Added(int Amount) : DomainEvent;

    private sealed class CountConverter : JsonConverter<TransactionalCountModel>
    {
        public override TransactionalCountModel Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            new() { Total = int.Parse(reader.GetString()![6..], System.Globalization.CultureInfo.InvariantCulture) };
        public override void Write(Utf8JsonWriter writer, TransactionalCountModel value, JsonSerializerOptions options) =>
            writer.WriteStringValue("count:" + value.Total.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

	private sealed class LegacyBuilder<TProjection> : IProjectionBuilder<TProjection>
		where TProjection : class, new()
	{

		public bool InlineCalled { get; private set; }
		public IProjectionBuilder<TProjection> Inline() { InlineCalled = true; return this; }

		public bool AsyncCalled { get; private set; }

		public bool EphemeralCalled { get; private set; }

		public IProjectionBuilder<TProjection> Async()
		{
			AsyncCalled = true;
			return this;
		}

		public IProjectionBuilder<TProjection> Ephemeral()
		{
			EphemeralCalled = true;
			return this;
		}

		public IProjectionBuilder<TProjection> When<TEvent>(Action<TProjection, TEvent> handler)
			where TEvent : IDomainEvent => this;

		public IProjectionBuilder<TProjection> When<TEvent>(Action<TProjection, TEvent, ProjectionContext> handler)
			where TEvent : IDomainEvent => this;

		public IProjectionBuilder<TProjection> WhenHandledBy<TEvent,
			[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>()
			where TEvent : IDomainEvent
			where THandler : IProjectionEventHandler<TProjection, TEvent> => this;

		public IProjectionBuilder<TProjection> AddProjectionHandlersFromAssembly(System.Reflection.Assembly assembly) => this;

		public IProjectionBuilder<TProjection> KeyedBy<TEvent>(Func<TEvent, string> keySelector)
			where TEvent : IDomainEvent => this;

		public IProjectionBuilder<TProjection> WithCacheTtl(TimeSpan ttl) => this;

		public IProjectionBuilder<TProjection> WhenDeleted(Func<string, CancellationToken, Task> deleteAction) => this;

		public IProjectionBuilder<TProjection> WithStore<TStore>()
			where TStore : class, IProjectionStore<TProjection> => this;

		public IProjectionBuilder<TProjection> WithOptions(Action<ProjectionOptions> configure) => this;

		public IProjectionBuilder<TProjection> WithSearchText(
			Func<TProjection, string> computeSearchText,
			Action<TProjection, string> setSearchText) => this;
	}

    private sealed class Fixture
    {
        public IReadOnlyList<StoredEvent> History { get; set; } = [];
        public string? Written { get; private set; }
        public IEventStore Store { get; } = A.Fake<IEventStore>();
        public TransactionalProjectionCoordinator Coordinator { get; }

        public Fixture()
        {
            var registry = new InMemoryProjectionRegistry();
            var builder = new ProjectionBuilder<TransactionalCountModel>(registry);
            builder.TransactionalInline("Order").When<Added>((model, value) => model.Total += value.Amount);
            builder.Build();
            var destination = A.Fake<IProjectionStore<TransactionalCountModel>>();
            var writer = A.Fake<ITransactionalProjectionStore<TransactionalCountModel>>();
            A.CallTo(() => destination.GetService(typeof(ITransactionalProjectionStore<TransactionalCountModel>))).Returns(writer);
            A.CallTo(() => writer.Serialize(A<TransactionalCountModel>._)).ReturnsLazily((TransactionalCountModel model) => model.Total.ToString(System.Globalization.CultureInfo.InvariantCulture));
            A.CallTo(() => writer.WriteAsync(A<ProjectionTransaction>._, A<string>._, A<string>._, A<string>._, A<CancellationToken>._))
                .Invokes(call => Written = call.GetArgument<string>(3)).Returns(ValueTask.CompletedTask);
            var services = new ServiceCollection().AddSingleton(destination).BuildServiceProvider();
            Coordinator = new TransactionalProjectionCoordinator(registry, services.GetRequiredService<IServiceScopeFactory>());
            var capability = A.Fake<ITransactionalProjectionEventStore>();
            A.CallTo(() => Store.GetService(typeof(ITransactionalProjectionEventStore))).Returns(capability);
            A.CallTo(() => capability.AppendWithProjectionsAsync(A<string>._, A<string>._, A<IReadOnlyList<IDomainEvent>>._, A<long>._,
                A<Func<ProjectionTransaction, CancellationToken, ValueTask>>._, A<CancellationToken>._))
                .ReturnsLazily(call => Apply(call.GetArgument<Func<ProjectionTransaction, CancellationToken, ValueTask>>(4)!));
        }

        private async ValueTask<AppendResult> Apply(Func<ProjectionTransaction, CancellationToken, ValueTask> callback)
        {
            await callback(new ProjectionTransaction(A.Fake<IDbTransaction>(), History, "tenant", "source"), CancellationToken.None);
            return AppendResult.CreateSuccess(History.Count, 1, History.Count + 1);
        }
    }
}

public sealed class TransactionalCountModel { public int Total { get; set; } }

[JsonSerializable(typeof(TransactionalCountModel))]
internal partial class TransactionalModelJson : JsonSerializerContext;
