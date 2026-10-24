// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics;
using System.Security.Cryptography;
using Excalibur.Compliance;
using Excalibur.Compliance.Configuration;
using Excalibur.Compliance.CryptoShredding;
using Excalibur.Compliance.Encryption;
using Excalibur.Compliance.Erasure;
using Excalibur.EventSourcing.Encryption.Decorators;
using System.Diagnostics.Metrics;
using System.Text.Json.Serialization.Metadata;
using Excalibur.EventSourcing.Diagnostics;
using Excalibur.EventSourcing.InMemory;
using Excalibur.EventSourcing.Sharding;
using Excalibur.Testing.Conformance;

namespace Excalibur.Tests.Testing.Conformance;

/// <summary>Runs the complete event store contract through the shipping decorators.</summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "STORE")]
public sealed class DecoratorEventStoreConformanceTests : EventStoreConformanceTestKit, IDisposable
{
    private readonly Meter _meter = new("EventDecoratorConformance");
    private readonly ActivitySource _activities = new("EventDecoratorConformance");
    private int _decorator;

    protected override void ConfigureProvider(IServiceCollection services, IJsonTypeInfoResolver? eventTypeInfoResolver)
    {
        services.AddExcalibur(x => x.AddEventSourcing(es => es.UseInMemory(null)));
        services.Configure<InMemoryEventStoreOptions>(options => options.EventTypeInfoResolver = eventTypeInfoResolver);
        services.AddComplianceEncryption(builder => builder.WithInMemoryKeyManagement());
        services.AddSingleton<IEncryptionProviderRegistry>(provider =>
        {
            var registry = new EncryptionProviderRegistry();
            registry.Register("conformance", provider.GetRequiredService<IEncryptionProvider>());
            registry.SetPrimary("conformance");
            return registry;
        });
        services.AddCryptoShredding();
        services.AddCryptoShreddingWithoutErasure();
        services.Configure<DataSubjectHashingOptions>(options =>
            options.Pepper = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
        services.AddSingleton<IEventStore>(provider =>
        {
            var inner = provider.GetRequiredKeyedService<IEventStore>("default");
            return _decorator switch
            {
                0 => new EventStoreThroughputMetrics(inner, _meter, "inmemory"),
                1 => new TelemetryEventStore(inner, _meter, _activities, "inmemory"),
                2 => new TenantScopedEventStore(inner, provider.GetRequiredService<ITenantContext>()),
                3 => new EncryptingEventStoreDecorator(inner, provider.GetRequiredService<IEncryptionProviderRegistry>(),
                    provider.GetRequiredService<SubjectFieldCryptor>(), provider.GetRequiredService<IEventSerializer>(),
                    Options.Create(new EncryptionOptions()), provider.GetRequiredService<ITenantContext>()),
                _ => throw new InvalidOperationException("Unknown decorator test case."),
            };
        });
    }

    public void Dispose()
    {
        _activities.Dispose();
        _meter.Dispose();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task AppendAsync_EventTypeTheResolverDoesNotDeclare_ShouldThrowAndWriteNothing_Test(int decorator)
    {
        _decorator = decorator;
        return AppendAsync_EventTypeTheResolverDoesNotDeclare_ShouldThrowAndWriteNothing();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task AppendAsync_AboveTheAtomicLimit_ShouldRefuseWholeOrAppendAtomically_Test(int decorator)
    {
        _decorator = decorator;
        return AppendAsync_AboveTheAtomicLimit_ShouldRefuseWholeOrAppendAtomically();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task AppendAsync_ToNewStream_ShouldSucceed_Test(int decorator)
    {
        _decorator = decorator;
        return AppendAsync_ToNewStream_ShouldSucceed();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task AppendAsync_LargerThanOneStatement_ShouldRemainContiguousAndOrdered_Test(int decorator)
    {
        _decorator = decorator;
        return AppendAsync_LargerThanOneStatement_ShouldRemainContiguousAndOrdered();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task AppendAsync_WithCorrectExpectedVersion_ShouldSucceed_Test(int decorator)
    {
        _decorator = decorator;
        return AppendAsync_WithCorrectExpectedVersion_ShouldSucceed();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task AppendAsync_WithWrongExpectedVersion_ShouldReturnConcurrencyConflict_Test(int decorator)
    {
        _decorator = decorator;
        return AppendAsync_WithWrongExpectedVersion_ShouldReturnConcurrencyConflict();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task AppendAsync_WithConflict_ShouldReportTheMeasuredVersion_Test(int decorator)
    {
        _decorator = decorator;
        return AppendAsync_WithConflict_ShouldReportTheMeasuredVersion();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task AppendAsync_EmptyEvents_ShouldNotChangeVersion_Test(int decorator)
    {
        _decorator = decorator;
        return AppendAsync_EmptyEvents_ShouldNotChangeVersion();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task ConcurrentAppend_SameExpectedVersion_OnlyOneShouldSucceed_Test(int decorator)
    {
        _decorator = decorator;
        return ConcurrentAppend_SameExpectedVersion_OnlyOneShouldSucceed();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task ConcurrentAppend_DifferentAggregates_AllShouldSucceed_Test(int decorator)
    {
        _decorator = decorator;
        return ConcurrentAppend_DifferentAggregates_AllShouldSucceed();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task ReAppendingTheSameEventsReportsSuccessAndDoesNotDuplicate_Test(int decorator)
    {
        _decorator = decorator;
        return ReAppendingTheSameEventsReportsSuccessAndDoesNotDuplicate();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task ReAppendingMidStreamReportsSuccessAndDoesNotDuplicate_Test(int decorator)
    {
        _decorator = decorator;
        return ReAppendingMidStreamReportsSuccessAndDoesNotDuplicate();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task ReAppendingAMultiEventBatchReportsSuccessAndDoesNotDuplicate_Test(int decorator)
    {
        _decorator = decorator;
        return ReAppendingAMultiEventBatchReportsSuccessAndDoesNotDuplicate();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task AppendAsync_UnaddressableAggregateId_ShouldThrow_Test(int decorator)
    {
        _decorator = decorator;
        return AppendAsync_UnaddressableAggregateId_ShouldThrow();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task AppendAsync_WithExpectedVersionBeyondTail_ShouldReturnConcurrencyConflict_Test(int decorator)
    {
        _decorator = decorator;
        return AppendAsync_WithExpectedVersionBeyondTail_ShouldReturnConcurrencyConflict();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task AppendAsync_NonExistentStream_WithWrongExpectedVersion_ShouldReturnConcurrencyConflict_Test(int decorator)
    {
        _decorator = decorator;
        return AppendAsync_NonExistentStream_WithWrongExpectedVersion_ShouldReturnConcurrencyConflict();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task LoadAsync_EmptyStream_ShouldReturnEmpty_Test(int decorator)
    {
        _decorator = decorator;
        return LoadAsync_EmptyStream_ShouldReturnEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task LoadAsync_ExistingStream_ShouldReturnAllEvents_Test(int decorator)
    {
        _decorator = decorator;
        return LoadAsync_ExistingStream_ShouldReturnAllEvents();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task LoadAsync_ShouldReturnEventsInVersionOrder_Test(int decorator)
    {
        _decorator = decorator;
        return LoadAsync_ShouldReturnEventsInVersionOrder();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task LoadAsync_FromVersion_ShouldReturnEventsAfterVersion_Test(int decorator)
    {
        _decorator = decorator;
        return LoadAsync_FromVersion_ShouldReturnEventsAfterVersion();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task LoadAsync_FromVersionBeyondStream_ShouldReturnEmpty_Test(int decorator)
    {
        _decorator = decorator;
        return LoadAsync_FromVersionBeyondStream_ShouldReturnEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task LoadAsync_FromVersionZero_ShouldReturnAllExceptTheFirst_Test(int decorator)
    {
        _decorator = decorator;
        return LoadAsync_FromVersionZero_ShouldReturnAllExceptTheFirst();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task ConcurrentFirstUse_ShouldNotFault_Test(int decorator)
    {
        _decorator = decorator;
        return ConcurrentFirstUse_ShouldNotFault();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task LoadAsync_ShouldIsolateByAggregateType_Test(int decorator)
    {
        _decorator = decorator;
        return LoadAsync_ShouldIsolateByAggregateType();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task LoadAsync_ShouldIsolateByAggregateId_Test(int decorator)
    {
        _decorator = decorator;
        return LoadAsync_ShouldIsolateByAggregateId();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task AppendAndLoad_ShouldPreserveEventData_Test(int decorator)
    {
        _decorator = decorator;
        return AppendAndLoad_ShouldPreserveEventData();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task AppendAndLoad_ShouldPreserveMetadata_Test(int decorator)
    {
        _decorator = decorator;
        return AppendAndLoad_ShouldPreserveMetadata();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task TenantScopedLoad_MustNotSeeAnotherTenantsEvents_Test(int decorator)
    {
        _decorator = decorator;
        return TenantScopedLoad_MustNotSeeAnotherTenantsEvents();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task TenantScopedLoad_MustSeeItsOwnEvents_Test(int decorator)
    {
        _decorator = decorator;
        return TenantScopedLoad_MustSeeItsOwnEvents();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task TenantPartitions_MustVersionTheSameAggregateIndependently_Test(int decorator)
    {
        _decorator = decorator;
        return TenantPartitions_MustVersionTheSameAggregateIndependently();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task UntenantedPartition_MustRoundTripItsOwnEvents_Test(int decorator)
    {
        _decorator = decorator;
        return UntenantedPartition_MustRoundTripItsOwnEvents();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task ConformanceSuite_ShouldWireEveryArm_Test(int decorator)
    {
        _decorator = decorator;
        return ConformanceSuite_ShouldWireEveryArm();
    }

}
