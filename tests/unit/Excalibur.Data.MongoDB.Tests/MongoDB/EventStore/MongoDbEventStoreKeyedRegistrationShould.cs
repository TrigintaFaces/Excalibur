// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.MongoDB;

using Microsoft.Extensions.DependencyInjection;

using MongoDB.Driver;

using Shouldly;

using Xunit;

namespace Excalibur.Data.Tests.MongoDB.EventStore;

/// <summary>
/// Real-DI resolution lock for the MongoDB event store registration. Every sibling provider registers
/// <c>IEventStore</c> as a keyed singleton ("mongodb" + "default"); Mongo previously registered a
/// non-keyed <c>Scoped</c> store, which (a) created a captive-dependency hazard for the singleton
/// forwarder and (b) left Mongo out of every keyed-"default" consumer (GDPR erasure, prereq validator,
/// projections, time-travel). This lock builds a real <c>ServiceProvider</c> via the production
/// registration path for BOTH connection paths and asserts the keyed-"default" store resolves as a
/// singleton. RED on the pre-fix <c>TryAddScoped</c> non-keyed registration (keyed-"default" was null,
/// and resolving from a scope produced a per-scope Scoped instance). No Mongo container is required:
/// the client-path store constructor uses the driver's lazy client handle and the options-path
/// constructor does not connect.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
[Trait("Database", "MongoDB")]
public sealed class MongoDbEventStoreKeyedRegistrationShould
{
    private const string LocalConnectionString = "mongodb://localhost:27017";

    [Fact]
    public async Task UseMongoDB_ClientPath_ResolvesKeyedDefaultAsSingletonForwarder()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();

        _ = services.AddExcalibur(excalibur => excalibur.AddEventSourcing(es =>
            es.UseMongoDB(mongo => mongo
                .Client(new MongoClient(LocalConnectionString))
                .DatabaseName("es_keyed_client"))));

        await AssertKeyedDefaultResolvesAsSingletonAsync(services).ConfigureAwait(false);
    }

    [Fact]
    public async Task UseMongoDB_OptionsPath_ResolvesKeyedDefaultAsSingletonForwarder()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();

        _ = services.AddExcalibur(excalibur => excalibur.AddEventSourcing(es =>
            es.UseMongoDB(mongo => mongo
                .ConnectionString(LocalConnectionString)
                .DatabaseName("es_keyed_options"))));

        await AssertKeyedDefaultResolvesAsSingletonAsync(services).ConfigureAwait(false);
    }

    private static async Task AssertKeyedDefaultResolvesAsSingletonAsync(IServiceCollection services)
    {
        // The resolved singleton store is IAsyncDisposable, so the provider is disposed asynchronously —
        // faithful to how a real host disposes it.
        await using var provider = services.BuildServiceProvider();

        // Liveness: every keyed-"default" consumer (GDPR erasure, prereq validator, projections) resolves
        // a non-null Mongo store — the participation the non-keyed registration silently withheld.
        var keyedDefault = provider.GetKeyedService<IEventStore>("default");
        keyedDefault.ShouldNotBeNull();

        var keyedMongo = provider.GetKeyedService<IEventStore>("mongodb");
        keyedMongo.ShouldNotBeNull();
        keyedDefault.ShouldBeSameAs(keyedMongo);

        // Singleton, resolvable from the root: no captive-dependency (a singleton forwarder capturing a
        // Scoped store) — resolving from a child scope returns the SAME root singleton, not a new instance.
        provider.GetRequiredKeyedService<IEventStore>("default").ShouldBeSameAs(keyedDefault);
        await using var scope = provider.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredKeyedService<IEventStore>("default").ShouldBeSameAs(keyedDefault);
    }
}
