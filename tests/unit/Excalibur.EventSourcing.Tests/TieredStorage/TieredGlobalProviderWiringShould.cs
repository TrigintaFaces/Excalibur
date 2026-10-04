// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing.Decorators;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Postgres;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.Sharding;
using Excalibur.EventSourcing.SqlServer;
using Excalibur.EventSourcing.TieredStorage;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Hosting;

namespace Excalibur.EventSourcing.Tests.TieredStorage;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "EventSourcing")]
public sealed class TieredGlobalProviderWiringShould
{
    private const string SqlConnection = "Server=localhost;Database=SourceIdentity;Integrated Security=true;TrustServerCertificate=true";
    private const string PgConnection = "Host=localhost;Database=source_identity;Username=postgres;Password=unused";

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ResolveMatchingProviderWithoutOpeningConnections(bool postgres, bool tenantWrapped)
    {
        var services = Services(postgres);
        var cold = A.Fake<IColdEventStore>();
        services.AddSingleton(cold);
        new ExcaliburEventSourcingBuilder(services).UseTieredStorage(p => p.MaxAge = TimeSpan.FromDays(1));
        if (tenantWrapped)
        {
            var descriptor = DefaultDescriptor(services);
            services.AddKeyedSingleton<IEventStore>("default", (sp, _) => new TenantScopedEventStore(
                (IEventStore)descriptor.KeyedImplementationFactory!(sp, "default"), sp.GetRequiredService<ITenantContext>()));
        }

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IGlobalStreamQuery>().ShouldBeOfType<TieredGlobalStreamQuery>();
        provider.GetRequiredService<IEventStoreArchive>().ShouldNotBeNull();
        provider.GetServices<IHostedService>().ShouldContain(service => service is EventArchiveService);
        Fake.GetCalls(cold).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RejectAReplacementSourceEvenWithTheSameProviderAndConnectionSettings(bool postgres)
    {
        var services = Services(postgres);
        var cold = A.Fake<IColdEventStore>();
        services.AddSingleton(cold);
        services.AddKeyedSingleton<IEventStore>("default", (sp, _) => postgres
            ? new PostgresEventStore(PgConnection, NullLogger<PostgresEventStore>.Instance, sp.GetRequiredService<ITenantContext>())
            : new SqlServerEventStore(SqlConnection, NullLogger<SqlServerEventStore>.Instance, sp.GetRequiredService<ITenantContext>()));
        new ExcaliburEventSourcingBuilder(services).UseTieredStorage(p => p.MaxAge = TimeSpan.FromDays(1));

        using var provider = services.BuildServiceProvider();
        // Tiering itself is valid; source alignment with the earlier global registration is not.
        provider.GetRequiredKeyedService<IEventStore>("default").ShouldBeOfType<TieredEventStoreDecorator>();
        Should.Throw<InvalidOperationException>(() => provider.GetRequiredService<IGlobalStreamQuery>())
            .Message.ShouldContain("same provider binding");
        Fake.GetCalls(cold).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RejectAWrapperThatAllowsAuthoritativeReadsButDeniesSourceIdentity(bool postgres)
    {
        var services = Services(postgres);
        var original = DefaultDescriptor(services);
        services.AddKeyedSingleton<IEventStore>("default", (sp, _) =>
            new SourceDenyingStore((IEventStore)original.KeyedImplementationFactory!(sp, "default")));
        services.AddSingleton(A.Fake<IColdEventStore>());
        new ExcaliburEventSourcingBuilder(services).UseTieredStorage(p => p.MaxAge = TimeSpan.FromDays(1));

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredKeyedService<IEventStore>("default").ShouldBeOfType<TieredEventStoreDecorator>();
        Should.Throw<InvalidOperationException>(() => provider.GetRequiredService<IGlobalStreamQuery>())
            .Message.ShouldContain("same provider binding");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RespectArchiveCapabilityDenialEvenWhenSourceAndReaderAreAvailable(bool postgres)
    {
        var services = Services(postgres);
        var original = DefaultDescriptor(services);
        services.AddKeyedSingleton<IEventStore>("default", (sp, _) =>
            new ArchiveDenyingStore((IEventStore)original.KeyedImplementationFactory!(sp, "default")));
        var cold = A.Fake<IColdEventStore>();
        services.AddSingleton(cold);
        new ExcaliburEventSourcingBuilder(services).UseTieredStorage(p => p.MaxAge = TimeSpan.FromDays(1));
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IGlobalStreamQuery>().ShouldBeOfType<TieredGlobalStreamQuery>();
        Should.Throw<InvalidOperationException>(() => provider.GetServices<IHostedService>().ToArray())
            .Message.ShouldContain("does not expose IEventStoreArchive");
        Fake.GetCalls(cold).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RejectFinalConsumerReplacementBeforeGlobalHydration(bool postgres)
    {
        var services = Services(postgres);
        services.AddSingleton(A.Fake<IColdEventStore>());
        new ExcaliburEventSourcingBuilder(services).UseTieredStorage(p => p.MaxAge = TimeSpan.FromDays(1));
        services.AddKeyedSingleton<IEventStore>("default", A.Fake<IEventStore>());

        using var provider = services.BuildServiceProvider();
        Should.Throw<InvalidOperationException>(() => provider.GetRequiredService<IGlobalStreamQuery>())
            .Message.ShouldContain("tiered singleton read composition");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreserveUntieredProviderQueriesAndHideSourceOperations(bool postgres)
    {
        var services = Services(postgres);
        using var provider = services.BuildServiceProvider();
        var query = provider.GetRequiredService<IGlobalStreamQuery>();
        if (postgres)
        {
            query.ShouldBeOfType<PostgresGlobalStreamQuery>();
        }
        else
        {
            query.ShouldBeOfType<ContiguousGlobalStreamQuery>();
        }
        var store = provider.GetRequiredKeyedService<IEventStore>("default");
        var identity = store.GetService(typeof(EventStoreSourceIdentity));
        identity.ShouldBeOfType<EventStoreSourceIdentity>();
        (identity is IEventStore).ShouldBeFalse();
        (identity is IEventStoreAuthoritativeReader).ShouldBeFalse();
        new TenantScopedEventStore(store, provider.GetRequiredService<ITenantContext>())
            .GetService(typeof(EventStoreSourceIdentity)).ShouldBeSameAs(identity);
        new SourceDenyingStore(store).GetService(typeof(EventStoreSourceIdentity)).ShouldBeNull();
    }

    [Fact]
    public void ResolveTieredSqlWithAnOwnedFactoryWithoutOpeningAConnection()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var builder = new ExcaliburEventSourcingBuilder(services);
        builder.UseSqlServer(b => b.OwnedPrimaryConnectionFactory(_ =>
            () => throw new InvalidOperationException("Resolving the query must not open a connection.")));
        services.AddSingleton(A.Fake<IColdEventStore>());
        builder.UseTieredStorage(p => p.MaxAge = TimeSpan.FromDays(1));
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IGlobalStreamQuery>().ShouldBeOfType<TieredGlobalStreamQuery>();
    }

    [Fact]
    public void RefuseLegacySqlFactoryWhenTieringRequiresAuthoritativeObservations()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var builder = new ExcaliburEventSourcingBuilder(services);
        builder.UseSqlServer(b => b.ConnectionFactory(_ => () => throw new InvalidOperationException("must not open")));
        services.AddSingleton(A.Fake<IColdEventStore>());
        builder.UseTieredStorage(p => p.MaxAge = TimeSpan.FromDays(1));
        using var provider = services.BuildServiceProvider();
        Should.Throw<InvalidOperationException>(() => provider.GetRequiredService<IGlobalStreamQuery>())
            .Message.ShouldContain("authoritative event reader");
    }

    private static ServiceCollection Services(bool postgres)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var builder = new ExcaliburEventSourcingBuilder(services);
        if (postgres)
        {
            builder.UsePostgres(b => b.ConnectionString(PgConnection));
        }
        else
        {
            builder.UseSqlServer(b => b.ConnectionString(SqlConnection));
        }
        return services;
    }

    private static ServiceDescriptor DefaultDescriptor(IServiceCollection services) => services.Last(d =>
        d.ServiceType == typeof(IEventStore) && d.IsKeyedService && Equals(d.ServiceKey, "default"));

    private sealed class SourceDenyingStore(IEventStore inner) : IsolatingEventStoreDecorator(inner)
    {
        protected override object? WrapCapability(Type serviceType) => serviceType == typeof(IEventStoreAuthoritativeReader)
            ? Inner.GetService(serviceType) : null;
    }

    private sealed class ArchiveDenyingStore(IEventStore inner) : IsolatingEventStoreDecorator(inner)
    {
        protected override object? WrapCapability(Type serviceType) =>
            serviceType == typeof(IEventStoreAuthoritativeReader) || serviceType == typeof(EventStoreSourceIdentity)
                ? Inner.GetService(serviceType) : null;
    }
}
