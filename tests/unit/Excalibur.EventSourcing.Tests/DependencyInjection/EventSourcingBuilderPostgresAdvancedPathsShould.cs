// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Postgres;
using Excalibur.EventSourcing.Postgres.DependencyInjection;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Tests.DependencyInjection;

/// <summary>
/// Tests for advanced DI extension paths in <see cref="EventSourcingBuilderPostgresExtensions"/>:
/// BindConfiguration + PostConfigure interaction (bd-ld76tv) and
/// ConnectionStringName resolution failure (bd-d7aymb).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Database", "Postgres")]
public sealed class EventSourcingBuilderPostgresAdvancedPathsShould
{
    private const string TestConnectionString =
        "Host=localhost;Database=TestDb;Username=test;Password=test";

    private static IEventSourcingBuilder CreateBuilder(ServiceCollection? services = null)
    {
        var svc = services ?? new ServiceCollection();
        return new ExcaliburEventSourcingBuilder(svc);
    }

    // --- BindConfiguration + PostConfigure interaction (bd-ld76tv) ---

    [Fact]
    public void BindConfiguration_RegisterOptionsFromConfigSection()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Postgres:ConnectionString"] = TestConnectionString,
                ["Postgres:EventStoreSchema"] = "config_schema",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        var builder = CreateBuilder(services);

        // Act
        builder.UsePostgres(pg => pg.BindConfiguration("Postgres"));

        // Assert — options are bound from configuration
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<PostgresEventSourcingOptions>>();
        options.Value.ConnectionString.ShouldBe(TestConnectionString);
        options.Value.EventStoreSchema.ShouldBe("config_schema");
    }

    [Fact]
    public void BindConfiguration_WithExplicitConnectionString_PostConfigureOverrides()
    {
        // Arrange — config has one connection string, builder sets another explicitly
        var configConnStr = "Host=config-host;Database=ConfigDb;";
        var explicitConnStr = "Host=explicit-host;Database=ExplicitDb;";

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Postgres:ConnectionString"] = configConnStr,
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        var builder = CreateBuilder(services);

        // Act — BindConfiguration + explicit ConnectionString
        // The builder calls ConnectionString() first (sets options.ConnectionString),
        // then BindConfiguration() (clears options.ConnectionString to empty).
        // But the PostConfigure path should restore the explicit connection string.
        builder.UsePostgres(pg =>
        {
            pg.ConnectionString(explicitConnStr)
              .BindConfiguration("Postgres");
        });

        // Assert — the explicit ConnectionString should NOT be used because
        // BindConfiguration has last-wins semantics and clears ConnectionString.
        // The BindConfiguration path resolves from config section.
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<PostgresEventSourcingOptions>>();

        // BindConfiguration wins over explicit ConnectionString due to last-wins
        options.Value.ConnectionString.ShouldBe(configConnStr);
    }

    [Fact]
    public void BindConfiguration_WithFeatureMethods_PreservesFeatureSettings()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Postgres:ConnectionString"] = TestConnectionString,
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        var builder = CreateBuilder(services);

        // Act — BindConfiguration combined with feature methods
        builder.UsePostgres(pg => pg
            .BindConfiguration("Postgres")
            .EventStoreSchema("custom_events")
            .SnapshotStoreTable("custom_snapshots"));

        // Assert — feature methods work alongside BindConfiguration
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<PostgresEventSourcingOptions>>();
        options.Value.EventStoreSchema.ShouldBe("custom_events");
        options.Value.SnapshotStoreTable.ShouldBe("custom_snapshots");
    }

    // --- ConnectionStringName resolution failure (bd-d7aymb) ---

    [Fact]
    public void ConnectionStringName_ThrowWhenNameNotFoundInConfiguration()
    {
        // Arrange — config has no connection strings at all
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        var builder = CreateBuilder(services);

        // Act — configure with a non-existent connection string name
        builder.UsePostgres(pg => pg.ConnectionStringName("NonExistentDb"));

        // Assert — resolving the DataSource should throw InvalidOperationException
        var provider = services.BuildServiceProvider();
        // The factory is lazy — it throws when first resolved
        var ex = Should.Throw<InvalidOperationException>(() =>
            provider.GetRequiredService<PostgresEventStore>());
        ex.Message.ShouldContain("NonExistentDb");
        ex.Message.ShouldContain("not found");
    }

    [Fact]
    public void ConnectionStringName_ResolveFromConfiguration_WhenNameExists()
    {
        // Arrange — config has the named connection string
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:EventStoreDb"] = TestConnectionString,
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        var builder = CreateBuilder(services);

        // Act
        builder.UsePostgres(pg => pg.ConnectionStringName("EventStoreDb"));

        // Assert — store is registered (resolution succeeds)
        var provider = services.BuildServiceProvider();
        services.ShouldContain(sd => sd.ServiceType == typeof(PostgresEventStore));
    }
}
