// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing.Decorators;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Sharding;
using Excalibur.EventSourcing.TieredStorage;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.EventSourcing.Tests.TieredStorage;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "EventSourcing")]
public sealed class TieredStartupCompositionShould
{
    [Theory]
    [InlineData("missing-reader", false)]
    [InlineData("missing-reader", true)]
    [InlineData("replacement", false)]
    [InlineData("replacement", true)]
    [InlineData("other-receipt", false)]
    [InlineData("other-receipt", true)]
    [InlineData("denied", false)]
    [InlineData("denied", true)]
    [InlineData("transient", false)]
    [InlineData("transient", true)]
    public async Task RefuseUnsafeCompositionBeforeArchival(string scenario, bool concurrent)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.Configure<HostOptions>(o => o.ServicesStartConcurrently = concurrent);
        var hot = NewHot();
        var cold = A.Fake<IColdEventStore>();
        var archiveResolved = false;
        builder.Services.AddSingleton<IEventStoreArchive>(_ =>
        {
            archiveResolved = true;
            return (IEventStoreArchive)hot;
        });
        builder.Services.AddKeyedSingleton<IEventStore>("default", hot);
        builder.Services.AddSingleton(cold);
        new ExcaliburEventSourcingBuilder(builder.Services).UseTieredStorage(p => p.MaxAge = TimeSpan.FromDays(1));
        var descriptor = builder.Services.Last(d => d.ServiceType == typeof(IEventStore)
            && d.IsKeyedService && Equals(d.ServiceKey, "default"));

        switch (scenario)
        {
            case "missing-reader":
                A.CallTo(() => hot.GetService(typeof(IEventStoreAuthoritativeReader))).Returns(null);
                break;
            case "replacement":
                // This replacement has a working authoritative capability: rejection must be composition-specific.
                builder.Services.AddKeyedSingleton<IEventStore>("default", NewHot());
                break;
            case "other-receipt":
                builder.Services.AddKeyedSingleton<IEventStore>("default", (sp, _) =>
                    new TieredEventStoreDecorator(hot, cold, NullLogger<TieredEventStoreDecorator>.Instance,
                        sp.GetRequiredService<ITenantContext>()));
                break;
            case "denied":
                builder.Services.AddKeyedSingleton<IEventStore>("default", (sp, _) =>
                    new DenyingStore((IEventStore)descriptor.KeyedImplementationFactory!(sp, "default")));
                break;
            case "transient":
                builder.Services.AddKeyedTransient<IEventStore>("default", (sp, _) =>
                    (IEventStore)descriptor.KeyedImplementationFactory!(sp, "default"));
                break;
        }

        using var host = builder.Build();
        var error = await Should.ThrowAsync<InvalidOperationException>(() => host.StartAsync());
        error.Message.ShouldContain(scenario == "missing-reader" ? "authoritative event reader" : "tiered singleton read composition");
        archiveResolved.ShouldBeFalse("rejection must precede resolution of the archive worker's constructor dependencies");
        Fake.GetCalls(cold).ShouldBeEmpty();
        Fake.GetCalls(hot).Where(c => c.Method.Name == nameof(IEventStoreArchive.TombstoneArchivedEventsUpToVersionAsync))
            .ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StartThroughTenantWrapperPreservingTheExactReceipt(bool concurrent)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.Configure<HostOptions>(o => o.ServicesStartConcurrently = concurrent);
        builder.Services.AddKeyedSingleton<IEventStore>("default", NewHot());
        builder.Services.AddSingleton(A.Fake<IColdEventStore>());
        new ExcaliburEventSourcingBuilder(builder.Services).UseTieredStorage(p => p.MaxAge = TimeSpan.FromDays(1));
        var descriptor = builder.Services.Last(d => d.ServiceType == typeof(IEventStore)
            && d.IsKeyedService && Equals(d.ServiceKey, "default"));
        IEventStore? tiered = null;
        builder.Services.AddKeyedSingleton<IEventStore>("default", (sp, _) =>
        {
            tiered = (IEventStore)descriptor.KeyedImplementationFactory!(sp, "default");
            return new TenantScopedEventStore(tiered, sp.GetRequiredService<ITenantContext>());
        });

        using var host = builder.Build();
        await host.StartAsync();
        var final = host.Services.GetRequiredKeyedService<IEventStore>("default");
        var receipt = final.GetService(typeof(TieredStorageCompositionReceipt));
        receipt.ShouldBeSameAs(tiered!.GetService(typeof(TieredStorageCompositionReceipt)));
        (receipt is IEventStore).ShouldBeFalse();
        host.Services.GetServices<IHostedService>().ShouldContain(s => s is EventArchiveService);
        await host.StopAsync();
    }

    [Fact]
    public async Task BindStatefulHotAndColdFactoriesOnceForReadsAndArchival()
    {
        var builder = Host.CreateApplicationBuilder();
        var hotCreated = new List<IEventStore>();
        var coldCreated = new List<IColdEventStore>();
        builder.Services.AddKeyedTransient<IEventStore>("default", (_, _) =>
        {
            var hot = NewHot();
            hotCreated.Add(hot);
            return hot;
        });
        builder.Services.AddTransient<IColdEventStore>(_ =>
        {
            var cold = A.Fake<IColdEventStore>();
            coldCreated.Add(cold);
            return cold;
        });
        new ExcaliburEventSourcingBuilder(builder.Services).UseTieredStorage(p => p.MaxAge = TimeSpan.FromDays(1));
        using var host = builder.Build();
        await host.StartAsync();
        hotCreated.Count.ShouldBe(1);
        coldCreated.Count.ShouldBe(1);
        host.Services.GetRequiredService<IEventStoreArchive>().ShouldBeSameAs(hotCreated[0]);
        host.Services.GetRequiredService<IEventStoreArchiveReader>().ShouldBeSameAs(hotCreated[0]);
        host.Services.GetRequiredService<IEventStoreArchiveScanner>().ShouldBeSameAs(hotCreated[0]);
        host.Services.GetServices<IHostedService>().ShouldContain(s => s is EventArchiveService);
        await host.StopAsync();
    }

    [Fact]
    public void RefuseDuplicateRegistrationWithoutChangingHotOrConsumerDescriptors()
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IEventStore>("default", NewHot());
        var builder = new ExcaliburEventSourcingBuilder(services);
        builder.UseTieredStorage(_ => { });
        var stores = services.Where(d => d.ServiceType == typeof(IEventStore)).ToArray();

        Should.Throw<InvalidOperationException>(() => builder.UseTieredStorage(_ => { }))
            .Message.ShouldContain("only be registered once");
        services.Where(d => d.ServiceType == typeof(IEventStore)).ShouldBe(stores);
    }

    private static IEventStore NewHot()
    {
        var hot = A.Fake<IEventStore>(o => o.Implements<IEventStoreArchive>().Implements<IEventStoreArchiveReader>().Implements<IEventStoreArchiveScanner>());
        A.CallTo(() => hot.GetService(typeof(IEventStoreArchive))).Returns((IEventStoreArchive)hot);
        A.CallTo(() => hot.GetService(typeof(IEventStoreArchiveReader))).Returns((IEventStoreArchiveReader)hot);
        A.CallTo(() => hot.GetService(typeof(IEventStoreArchiveScanner))).Returns((IEventStoreArchiveScanner)hot);
        A.CallTo(() => hot.GetService(typeof(IEventStoreAuthoritativeReader))).Returns(new TestEventStateReader(hot));
        return hot;
    }

    private sealed class DenyingStore(IEventStore inner) : IsolatingEventStoreDecorator(inner);
}
