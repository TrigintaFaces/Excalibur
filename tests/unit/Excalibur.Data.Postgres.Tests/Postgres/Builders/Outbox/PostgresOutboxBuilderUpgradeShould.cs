// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Outbox.Postgres;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

using Npgsql;

namespace Excalibur.Data.Tests.Postgres.Builders.Outbox;

/// <summary>
/// Unit tests for the Sprint 768 upgrades to <see cref="PostgresOutboxBuilder"/> —
/// new ConnectionStringName, BindConfiguration, DataSource, DataSourceFactory overloads.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Database", "Postgres")]
public sealed class PostgresOutboxBuilderUpgradeShould : UnitTestBase
{
    private const string TestConnectionString =
        "Host=localhost;Database=TestDb;Username=test;Password=test";

    private static PostgresOutboxBuilder CreateBuilder() =>
        new(new PostgresOutboxStoreOptions());

    [Theory]
    [InlineData("named")]
    [InlineData("configuration")]
    [InlineData("source")]
    [InlineData("factory")]
    public void Register_Advertised_Connection_Mode_Without_Opening_A_Connection(string mode)
    {
        using var dataSource = NpgsqlDataSource.Create(TestConnectionString);
        var services = new ServiceCollection();
        var factoryCalls = 0;

        services.AddExcaliburOutbox(outbox => outbox.UsePostgres(postgres =>
        {
            switch (mode)
            {
                case "named":
                    postgres.ConnectionStringName("OutboxStore");
                    break;
                case "configuration":
                    postgres.BindConfiguration("Outbox:Postgres");
                    break;
                case "source":
                    postgres.DataSource(dataSource);
                    break;
                case "factory":
                    postgres.DataSourceFactory(_ =>
                    {
                        factoryCalls++;
                        return dataSource;
                    });
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode));
            }
        }));

        factoryCalls.ShouldBe(0);
        services.ShouldContain(descriptor => descriptor.ServiceType == typeof(PostgresOutboxStore));
    }

    // --- New connection overloads (happy path) ---

    [Fact]
    public void Bind_Late_Configuration_And_Preserve_Fluent_Ordering()
    {
        var services = new ServiceCollection();
        services.AddExcaliburOutbox(outbox => outbox.UsePostgres(postgres =>
            postgres.SchemaName("before").MaxAttempts(4)
                .BindConfiguration("Outbox:Postgres").TableName("after")));
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Outbox:Postgres:SchemaName"] = "configured",
                ["Outbox:Postgres:OutboxTableName"] = "configured_table",
                ["Outbox:Postgres:FenceTableName"] = "configured_fence",
            }).Build());
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<PostgresOutboxStoreOptions>>().Value;
        options.SchemaName.ShouldBe("configured");
        options.OutboxTableName.ShouldBe("after");
        options.FenceTableName.ShouldBe("configured_fence");
        options.MaxAttempts.ShouldBe(4);
    }

    [Theory]
    [InlineData("named")]
    [InlineData("configuration")]
    public void Reject_Missing_Configuration_Before_Opening_A_Connection(string mode)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddExcaliburOutbox(outbox => outbox.UsePostgres(postgres =>
        {
            if (mode == "named")
            {
                postgres.ConnectionStringName("MissingOutbox");
            }
            else
            {
                postgres.BindConfiguration("Outbox:Missing");
            }
        }));
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();

        Should.Throw<InvalidOperationException>(() => provider.GetRequiredService<PostgresOutboxStore>())
            .Message.ShouldContain("missing or empty");
    }

    [Fact]
    public void Reset_Overrides_On_Second_Bind_And_Apply_All_Later_Fluent_Options()
    {
        var services = new ServiceCollection();
        services.AddExcaliburOutbox(outbox => outbox.UsePostgres(postgres =>
            postgres.BindConfiguration("Discarded").SchemaName("discarded_override")
                .BindConfiguration("Selected").TableName("selected_table")
                .DeadLetterTableName("selected_dead").CommandTimeout(TimeSpan.FromSeconds(11))
                .ReservationTimeout(TimeSpan.FromSeconds(13)).MaxAttempts(7)));
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Discarded:SchemaName"] = "discarded_config",
                ["Selected:SchemaName"] = "selected_schema",
                ["Selected:OutboxTableName"] = "config_table",
                ["Selected:DeadLetterTableName"] = "config_dead",
                ["Selected:BatchProcessing:BatchProcessingTimeout"] = "00:00:22",
                ["Selected:ReservationTimeout"] = "23",
                ["Selected:MaxAttempts"] = "9",
                ["Selected:FailureBackoffFloorSeconds"] = "31",
            }).Build());
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<PostgresOutboxStoreOptions>>().Value;
        options.SchemaName.ShouldBe("selected_schema");
        options.OutboxTableName.ShouldBe("selected_table");
        options.DeadLetterTableName.ShouldBe("selected_dead");
        options.BatchProcessing.BatchProcessingTimeout.ShouldBe(TimeSpan.FromSeconds(11));
        options.ReservationTimeout.ShouldBe(13);
        options.MaxAttempts.ShouldBe(7);
        options.FailureBackoffFloorSeconds.ShouldBe(31);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Release_Failed_Connections_And_Respect_Source_Ownership(bool ownsSource)
    {
        await using var source = NpgsqlDataSource.Create(
            "Host=127.0.0.1;Port=1;Database=unavailable;Username=test;Password=test;Timeout=1;Pooling=false");
        Should.Throw<NpgsqlException>(() => PostgresOutboxConnectionOwner.Create(source, ownsSource));
        if (ownsSource)
        {
            await Should.ThrowAsync<ObjectDisposedException>(async () =>
            {
                await using var connection = await source.OpenConnectionAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);
        }
        else
        {
            await using var connection = source.CreateConnection();
            connection.State.ShouldBe(System.Data.ConnectionState.Closed);
        }
    }

    [Fact]
    public void ConnectionStringName_StoreName()
    {
        var builder = CreateBuilder();
        builder.ConnectionStringName("OutboxStore");
        builder.ConnectionStringNameValue.ShouldBe("OutboxStore");
    }

    [Fact]
    public void BindConfiguration_StorePath()
    {
        var builder = CreateBuilder();
        builder.BindConfiguration("Outbox:Postgres");
        builder.BindConfigurationPath.ShouldBe("Outbox:Postgres");
    }

    [Fact]
    public void DataSource_StoreInstance()
    {
        var builder = CreateBuilder();
        using var dataSource = NpgsqlDataSource.Create(TestConnectionString);
        builder.DataSource(dataSource);
        builder.DataSourceInstance.ShouldBe(dataSource);
    }

    [Fact]
    public void DataSourceFactory_StoreFactory()
    {
        var builder = CreateBuilder();
        Func<IServiceProvider, NpgsqlDataSource> factory = _ => NpgsqlDataSource.Create(TestConnectionString);
        builder.DataSourceFactory(factory);
        builder.DataSourceFactoryFunc.ShouldBe(factory);
    }

    // --- Last-wins semantics for new overloads ---

    [Fact]
    public void ConnectionStringName_ClearAllOtherConnections()
    {
        var builder = CreateBuilder();
        builder.ConnectionString(TestConnectionString);
        builder.ConnectionStringName("OutboxStore");

        builder.ConfiguredConnectionString.ShouldBeNull();
        builder.ConfiguredDbFactory.ShouldBeNull();
        builder.DataSourceInstance.ShouldBeNull();
        builder.DataSourceFactoryFunc.ShouldBeNull();
        builder.BindConfigurationPath.ShouldBeNull();
        builder.ConnectionStringNameValue.ShouldBe("OutboxStore");
    }

    [Fact]
    public void BindConfiguration_ClearAllOtherConnections()
    {
        var builder = CreateBuilder();
        using var ds = NpgsqlDataSource.Create(TestConnectionString);
        builder.DataSource(ds);
        builder.BindConfiguration("Outbox:Postgres");

        builder.ConfiguredConnectionString.ShouldBeNull();
        builder.ConfiguredDbFactory.ShouldBeNull();
        builder.DataSourceInstance.ShouldBeNull();
        builder.DataSourceFactoryFunc.ShouldBeNull();
        builder.ConnectionStringNameValue.ShouldBeNull();
        builder.BindConfigurationPath.ShouldBe("Outbox:Postgres");
    }

    [Fact]
    public void DataSource_ClearAllOtherConnections()
    {
        var builder = CreateBuilder();
        using var ds = NpgsqlDataSource.Create(TestConnectionString);
        builder.ConnectionString(TestConnectionString);
        builder.DataSource(ds);

        builder.ConfiguredConnectionString.ShouldBeNull();
        builder.ConfiguredDbFactory.ShouldBeNull();
        builder.DataSourceFactoryFunc.ShouldBeNull();
        builder.ConnectionStringNameValue.ShouldBeNull();
        builder.BindConfigurationPath.ShouldBeNull();
        builder.DataSourceInstance.ShouldBe(ds);
    }

    [Fact]
    public void DataSourceFactory_ClearAllOtherConnections()
    {
        var builder = CreateBuilder();
        builder.ConnectionString(TestConnectionString);
        Func<IServiceProvider, NpgsqlDataSource> factory = _ => NpgsqlDataSource.Create(TestConnectionString);
        builder.DataSourceFactory(factory);

        builder.ConfiguredConnectionString.ShouldBeNull();
        builder.ConfiguredDbFactory.ShouldBeNull();
        builder.DataSourceInstance.ShouldBeNull();
        builder.ConnectionStringNameValue.ShouldBeNull();
        builder.BindConfigurationPath.ShouldBeNull();
        builder.DataSourceFactoryFunc.ShouldBe(factory);
    }

    // --- Validation guards for new overloads ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ConnectionStringName_ThrowOnInvalidValue(string? invalidValue)
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.ConnectionStringName(invalidValue!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BindConfiguration_ThrowOnInvalidValue(string? invalidValue)
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.BindConfiguration(invalidValue!));
    }

    [Fact]
    public void DataSource_ThrowOnNull()
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentNullException>(() => builder.DataSource(null!));
    }

    [Fact]
    public void DataSourceFactory_ThrowOnNull()
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentNullException>(() => builder.DataSourceFactory(null!));
    }

    // --- Fluent chaining ---

    [Fact]
    public void NewOverloads_ReturnBuilderForChaining()
    {
        var builder = CreateBuilder();
        using var ds = NpgsqlDataSource.Create(TestConnectionString);

        builder.ConnectionStringName("OutboxStore").ShouldBeSameAs(builder);
        builder.BindConfiguration("Outbox:Postgres").ShouldBeSameAs(builder);
        builder.DataSource(ds).ShouldBeSameAs(builder);
        builder.DataSourceFactory(_ => NpgsqlDataSource.Create(TestConnectionString)).ShouldBeSameAs(builder);
    }
}
