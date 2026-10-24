// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json.Serialization;
using Excalibur.Dispatch;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Projections;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Tests.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
[Trait("Pattern", "Conformance")]
public sealed class GeneratedProjectionApiShould
{
    [Fact]
    public void RefuseAnInnerGeneratedCapabilityThroughTheTenantDecoratorOrDirectDi()
    {
        var (services, inner, capability) = CreateServices();
        var outer = new Excalibur.EventSourcing.Sharding.TenantScopedProjectionStore<GeneratedReadModel>(
            inner, A.Fake<ITenantContext>());
        outer.GetService(typeof(IAotProjectionStore<GeneratedReadModel>)).ShouldBeNull();
        services.AddSingleton<IProjectionStore<GeneratedReadModel>>(outer);
        services.AddSingleton(capability); // A direct registration must not bypass the outer decorator.
        using var provider = services.BuildServiceProvider();
        Should.Throw<InvalidOperationException>(() => ProjectionStoreOperations<GeneratedReadModel>.Generated(
            provider, GeneratedProjectionJson.Default.GeneratedReadModel));
        A.CallTo(() => capability.GetByIdAsync(A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public void RejectEphemeralRegistrationInsteadOfIgnoringGeneratedMetadata()
    {
        var builder = A.Fake<IEventSourcingBuilder>();
        A.CallTo(() => builder.Services).Returns(new ServiceCollection());
        Should.Throw<NotSupportedException>(() => builder.AddProjection(
            GeneratedProjectionJson.Default.GeneratedReadModel, p => p.Ephemeral()));
    }

    [Fact]
    public async Task ReadThroughTheSafeCapabilityWithoutCallingLegacyPersistence()
    {
        var (services, _, capability) = CreateServices();
        A.CallTo(() => capability.GetByIdAsync("row", A<CancellationToken>._)).Returns(new GeneratedReadModel { Count = 9 });
        await using var provider = services.BuildServiceProvider();
        var reader = new ProjectionSessionReader(provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new ProjectionSessionOptions()), Options.Create(new GlobalStreamProjectionOptions()));
        var result = await reader.ReadAsync("row", new SaveResult(1, null, ProjectionFreshness.NotRequested), GeneratedProjectionJson.Default.GeneratedReadModel, CancellationToken.None);
        result.Projection.ShouldNotBeNull().Count.ShouldBe(9);
        result.Freshness.ShouldBe(ProjectionFreshness.NotRequested);
    }

    [Fact]
    public async Task RefuseMissingCapabilitiesAndDifferentMetadataBeforeReading()
    {
        var (services, store, capability) = CreateServices();
        await using var provider = services.BuildServiceProvider();
        Should.Throw<InvalidOperationException>(() => ProjectionStoreOperations<GeneratedReadModel>.Generated(provider, new GeneratedProjectionJson().GeneratedReadModel));
        A.CallTo(() => store.GetService(typeof(IAotProjectionStore<GeneratedReadModel>))).Returns(null);
        Should.Throw<InvalidOperationException>(() => ProjectionStoreOperations<GeneratedReadModel>.Generated(provider, GeneratedProjectionJson.Default.GeneratedReadModel));
        A.CallTo(() => capability.GetByIdAsync(A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task FoldABatchOnceAndSkipAnAlreadyAppliedBatch()
    {
        var (services, _, capability) = CreateServices();
        var registry = new InMemoryProjectionRegistry();
        var builder = new ProjectionBuilder<GeneratedReadModel>(services);
        builder.Inline().When<GeneratedAdded>((state, e) => state.Count += e.Amount);
        builder.BuildGenerated(registry, GeneratedProjectionJson.Default.GeneratedReadModel);
        await using var provider = services.BuildServiceProvider();
        var apply = registry.GetRegistration(typeof(GeneratedReadModel))!.InlineApply!;
        var events = Enumerable.Range(1, 20).Select(i => new ProjectionEvent(new GeneratedAdded(1), "row", i)).ToArray();
        A.CallTo(() => capability.GetWithPositionAsync("row", A<CancellationToken>._))
            .Returns((null, ProjectionPosition.Unplaceable));
        A.CallTo(() => capability.UpsertAtPositionAsync("row", A<GeneratedReadModel>.That.Matches(x => x.Count == 20), null, 20, A<CancellationToken>._))
            .Returns(new ProjectionAdvanceResult(ProjectionAdvanceOutcome.Applied, 20));
        var context = new EventNotificationContext("row", "Counter", 20, DateTimeOffset.UtcNow, GlobalPosition: 20);
        await apply(events, context, provider, CancellationToken.None);
        A.CallTo(() => capability.GetWithPositionAsync("row", A<CancellationToken>._))
            .Returns((new GeneratedReadModel { Count = 20 }, ProjectionPosition.At(20)));
        await apply(events, context, provider, CancellationToken.None);
        A.CallTo(() => capability.UpsertAtPositionAsync(A<string>._, A<GeneratedReadModel>._, A<long?>._, A<long>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData(ProjectionAdvanceOutcome.Superseded, 2L)]
    [InlineData(ProjectionAdvanceOutcome.Unplaceable, null)]
    public async Task PreserveRefusedWriteFailures(ProjectionAdvanceOutcome outcome, long? position)
    {
        var (services, _, capability) = CreateServices();
        await using var provider = services.BuildServiceProvider();
        var operations = ProjectionStoreOperations<GeneratedReadModel>.Generated(provider, GeneratedProjectionJson.Default.GeneratedReadModel);
        A.CallTo(() => capability.UpsertAtPositionAsync(A<string>._, A<GeneratedReadModel>._, A<long?>._, A<long>._, A<CancellationToken>._))
            .Returns(new ProjectionAdvanceResult(outcome, position));
        await Should.ThrowAsync<InvalidOperationException>(() => PositionedProjectionWriter<GeneratedReadModel>.WriteAsync(
            operations, operations, "row", new GeneratedReadModel(), 1, 3, CancellationToken.None));
    }

    [Fact]
    public async Task ValidateBeforeTheInlineHostCanResolveItsEventSource()
    {
        var (services, store, _) = CreateServices();
        A.CallTo(() => store.GetService(typeof(IAotProjectionStore<GeneratedReadModel>))).Returns(null);
        services.AddSingleton(A.Fake<IEventNotificationBroker>());
        services.AddSingleton<IGeneratedProjectionValidator>(sp => new GeneratedProjectionValidator<GeneratedReadModel>(sp, GeneratedProjectionJson.Default.GeneratedReadModel));
        await using var provider = services.BuildServiceProvider();
        await using var coordinator = new InlineProjectionCoordinator(provider, Options.Create(new EventNotificationOptions()), NullLogger<InlineProjectionCoordinator>.Instance);
        var error = await Should.ThrowAsync<InvalidOperationException>(() => coordinator.StartAsync(CancellationToken.None));
        error.Message.ShouldContain("generated-metadata operations");
    }

    [Fact]
    public async Task RecoverThroughScopedHandlersAndConditionalRefold()
    {
        var (services, _, capability) = CreateServices();
        var registry = new InMemoryProjectionRegistry();
        services.AddScoped<GeneratedScopeDependency>();
        var builder = new ProjectionBuilder<GeneratedReadModel>(services);
        builder.Inline().WhenHandledBy<GeneratedAdded, GeneratedScopedHandler>();
        builder.BuildGenerated(registry, GeneratedProjectionJson.Default.GeneratedReadModel);
        var eventStore = A.Fake<IEventStore>();
        services.AddSingleton<ProjectionRecoveryService>(sp => new ProjectionRecoveryService(registry, eventStore,
            sp.GetRequiredService<IEventSerializer>(), sp, NullLogger<ProjectionRecoveryService>.Instance));
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var serializer = provider.GetRequiredService<IEventSerializer>();
        var stored = new StoredEvent("1", "row", "Counter", serializer.GetTypeName(typeof(GeneratedAdded)),
            serializer.SerializeEvent(new GeneratedAdded(6)), null, 1, DateTimeOffset.UtcNow) { GlobalPosition = 5 };
        A.CallTo(() => eventStore.LoadAsync("row", "Counter", A<CancellationToken>._))
            .Returns(new ValueTask<IReadOnlyList<StoredEvent>>([stored]));
        A.CallTo(() => capability.GetByIdAsync("row", A<CancellationToken>._)).Returns(new GeneratedReadModel());
        A.CallTo(() => capability.GetWithPositionAsync("row", A<CancellationToken>._))
            .Returns((new GeneratedReadModel(), ProjectionPosition.At(5)));
        A.CallTo(() => capability.RefoldAtPositionAsync("row", A<GeneratedReadModel>.That.Matches(x => x.Count == 6), 5, A<CancellationToken>._))
            .Returns(new ProjectionRefoldResult(ProjectionRefoldOutcome.Applied, 5));
        await using var scope = provider.CreateAsyncScope();
        await registry.GetRegistration(typeof(GeneratedReadModel))!.ClearForAggregate!(scope.ServiceProvider, "row", "Counter", CancellationToken.None);
        scope.ServiceProvider.GetRequiredService<GeneratedScopeDependency>().Calls.ShouldBe(1);
        A.CallTo(() => capability.RefoldAtPositionAsync("row", A<GeneratedReadModel>._, 5, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    private static (ServiceCollection Services, IProjectionStore<GeneratedReadModel> Store, IAotProjectionStore<GeneratedReadModel> Capability) CreateServices()
    {
        var services = new ServiceCollection();
        var store = A.Fake<IProjectionStore<GeneratedReadModel>>();
        var capability = A.Fake<IAotProjectionStore<GeneratedReadModel>>();
        A.CallTo(() => capability.TypeInfo).Returns(GeneratedProjectionJson.Default.GeneratedReadModel);
        A.CallTo(() => store.GetService(typeof(IAotProjectionStore<GeneratedReadModel>))).Returns(capability);
        A.CallTo(() => store.GetService(typeof(IPositionedProjectionStore<GeneratedReadModel>))).Returns(A.Fake<IPositionedProjectionStore<GeneratedReadModel>>());
        A.CallTo(() => store.GetByIdAsync(A<string>._, A<CancellationToken>._)).Throws(new InvalidOperationException("Legacy read invoked"));
        A.CallTo(() => store.UpsertAsync(A<string>._, A<GeneratedReadModel>._, A<CancellationToken>._)).Throws(new InvalidOperationException("Legacy write invoked"));
        services.AddSingleton(store);
        services.AddSingleton<IEventSerializer>(new AotJsonEventSerializer(GeneratedProjectionJson.Default, typeof(GeneratedAdded)));
        return (services, store, capability);
    }
}

public sealed class GeneratedReadModel { public int Count { get; set; } }
[MessageName("test.generated-projection.added")]
public sealed record GeneratedAdded(int Amount) : DomainEvent;
public sealed class GeneratedScopeDependency { public int Calls { get; set; } }
public sealed class GeneratedScopedHandler(GeneratedScopeDependency dependency) : IProjectionEventHandler<GeneratedReadModel, GeneratedAdded>
{
    public Task HandleAsync(GeneratedReadModel projection, GeneratedAdded domainEvent, ProjectionHandlerContext context, CancellationToken cancellationToken)
    {
        dependency.Calls++;
        projection.Count += domainEvent.Amount;
        return Task.CompletedTask;
    }
}

[JsonSerializable(typeof(GeneratedReadModel))]
[JsonSerializable(typeof(GeneratedAdded))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal sealed partial class GeneratedProjectionJson : JsonSerializerContext;



