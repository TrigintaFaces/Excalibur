// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing;

using Microsoft.Extensions.DependencyInjection;

using Oracle.ManagedDataAccess.Client;

using Shouldly;

using Xunit;

namespace Excalibur.EventSourcing.Tests.EventStore;

/// <summary>
/// Real-DI resolution lock for the Oracle snapshot store registration. The core resolves GDPR
/// snapshot erasure via <c>GetKeyedService&lt;ISnapshotStore&gt;("default")</c>, so both
/// <c>AddOracleSnapshotStore</c> overloads MUST register the store keyed as "oracle" + "default"
/// (mirroring the sibling <c>IEventStore</c>). RED on the pre-fix non-keyed <c>TryAddSingleton</c>,
/// where the keyed-"default" probe returned null and the payload was never erased on Oracle.
/// A real <c>ServiceProvider</c> is built via the production registration path; no Oracle container is
/// required because both store constructors capture the connection factory lazily.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
[Trait("Database", "Oracle")]
public sealed class OracleSnapshotStoreKeyedRegistrationShould
{
    private const string UnusedConnectionString =
        "Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=unused)(PORT=1521))"
        + "(CONNECT_DATA=(SERVICE_NAME=FREE)));User Id=x;Password=y;";

    [Fact]
    public void AddOracleSnapshotStore_ConnectionFactoryOverload_ResolvesKeyedDefaultAsSingletonForwarder()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();

        _ = services.AddOracleSnapshotStore(
            () => new OracleConnection(UnusedConnectionString),
            schema: "EXCALIBUR",
            table: "EVENTSTORESNAPSHOTS");

        using var provider = services.BuildServiceProvider();

        // Liveness: the keyed-"default" consumer (GDPR snapshot erasure) resolves a non-null store.
        var keyedDefault = provider.GetKeyedService<ISnapshotStore>("default");
        keyedDefault.ShouldNotBeNull();

        // The provider-specific key resolves too, and "default" forwards to the SAME singleton instance.
        var keyedOracle = provider.GetKeyedService<ISnapshotStore>("oracle");
        keyedOracle.ShouldNotBeNull();
        keyedDefault.ShouldBeSameAs(keyedOracle);

        // Singleton: resolving "default" twice returns the same instance (no captive-dependency hazard).
        provider.GetRequiredKeyedService<ISnapshotStore>("default")
            .ShouldBeSameAs(keyedDefault);
    }

    [Fact]
    public void AddOracleSnapshotStore_OptionsOverload_ResolvesKeyedDefaultAsSingletonForwarder()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();

        _ = services.AddOracleSnapshotStore(options =>
        {
            options.ConnectionString = UnusedConnectionString;
            options.Schema = "EXCALIBUR";
            options.Table = "EVENTSTORESNAPSHOTS";
        });

        using var provider = services.BuildServiceProvider();

        var keyedDefault = provider.GetKeyedService<ISnapshotStore>("default");
        keyedDefault.ShouldNotBeNull();
        provider.GetKeyedService<ISnapshotStore>("oracle").ShouldNotBeNull();
        keyedDefault.ShouldBeSameAs(provider.GetRequiredKeyedService<ISnapshotStore>("oracle"));
    }
}
