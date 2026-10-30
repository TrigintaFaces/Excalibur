// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.SqlServer.DependencyInjection;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Tests.SqlServer;

[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
[Trait("Pattern", "Regression")]
public sealed class SqlServerRegistrationParityShould
{
    private const string Connection = "Server=localhost;Database=Registration;Integrated Security=true;TrustServerCertificate=true";

    [Theory]
    [InlineData("event-options", true, false)]
    [InlineData("event-config", true, false)]
    [InlineData("event-factory", false, false)]
    [InlineData("event-typed", false, false)]
    [InlineData("all-options", true, true)]
    [InlineData("all-config", true, true)]
    [InlineData("all-factory", false, true)]
    [InlineData("all-typed", false, true)]
    public void SupplyReaderAndValidationWithoutInventingConnectionOwnership(string shape, bool owned, bool snapshots)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var db = A.Fake<IDb>();
        using var connection = new SqlConnection(Connection);
        A.CallTo(() => db.Connection).Returns(connection);
        services.AddSingleton(db);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionString"] = Connection,
            ["HealthChecks:RegisterHealthChecks"] = "false",
        }).Build();
        switch (shape)
        {
            case "event-options": services.AddSqlServerEventStore(o => o.ConnectionString = Connection); break;
            case "event-config": services.AddSqlServerEventStore(configuration); break;
            case "event-factory": services.AddSqlServerEventStore(() => new SqlConnection(Connection)); break;
            case "event-typed": services.AddSqlServerEventStore<IDb>(); break;
            case "all-options": services.AddSqlServerEventSourcing(o => { o.ConnectionString = Connection; o.HealthChecks.RegisterHealthChecks = false; }); break;
            case "all-config": services.AddSqlServerEventSourcing(configuration); break;
            case "all-factory": services.AddSqlServerEventSourcing(() => new SqlConnection(Connection)); break;
            case "all-typed": services.AddSqlServerEventSourcing<IDb>(); break;
            default: throw new ArgumentOutOfRangeException(nameof(shape));
        }
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        provider.GetRequiredService<IGlobalStreamQuery>().ShouldBeOfType<ContiguousGlobalStreamQuery>();
        provider.GetServices<IValidateOptions<SqlServerEventSourcingOptions>>().ShouldNotBeEmpty();
        provider.GetService<IStartupValidator>().ShouldNotBeNull();
        var store = provider.GetRequiredKeyedService<IEventStore>("default");
        (store.GetService(typeof(IEventStoreAuthoritativeReader)) is not null).ShouldBe(owned);
        (provider.GetKeyedService<ISnapshotStore>("default") is not null).ShouldBe(snapshots);
    }

    [Fact]
    public void PreserveAnExistingSnapshotRegistrationForEventOnlyCalls()
    {
        var snapshot = A.Fake<ISnapshotStore>();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKeyedSingleton<ISnapshotStore>("default", snapshot);
        services.AddSqlServerEventStore(o => o.ConnectionString = Connection);
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredKeyedService<ISnapshotStore>("default").ShouldBeSameAs(snapshot);
    }

    [Fact]
    public void RejectASecondSourceBeforeChangingItsBinding()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSqlServerEventStore(o => o.ConnectionString = Connection);
        Should.Throw<InvalidOperationException>(() => services.AddSqlServerEventStore(o => o.ConnectionString = Connection))
            .Message.ShouldContain("already registered");
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IGlobalStreamQuery>().ShouldBeOfType<ContiguousGlobalStreamQuery>();
    }

    [Fact]
    public void RejectInvalidFinalOptionsAtStartup()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSqlServerEventSourcing(o => o.ConnectionString = Connection);
        services.PostConfigure<SqlServerEventSourcingOptions>(o => o.ConnectionString = null);
        using var provider = services.BuildServiceProvider();
        Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
    }

    [Fact]
    public void PreserveBoundLocationsAndValidateFinalOptions()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionString"] = Connection, ["EventStoreSchema"] = "events", ["EventStoreTable"] = "History",
            ["SnapshotStoreSchema"] = "snapshots", ["SnapshotStoreTable"] = "State", ["HealthChecks:RegisterHealthChecks"] = "false",
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSqlServerEventSourcing(config);
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<SqlServerEventSourcingOptions>>().Value;
        options.EventStoreSchema.ShouldBe("events");
        options.EventStoreTable.ShouldBe("History");
        options.SnapshotStoreSchema.ShouldBe("snapshots");
        options.SnapshotStoreTable.ShouldBe("State");
        provider.GetRequiredService<IStartupValidator>().Validate();
    }
}
