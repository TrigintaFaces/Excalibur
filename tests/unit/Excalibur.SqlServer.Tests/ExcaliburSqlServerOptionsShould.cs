// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance.SqlServer;
using Excalibur.Compliance.SqlServer.Erasure;
using Excalibur.Dispatch.Configuration;
using Excalibur.AuditLogging.SqlServer;
using Excalibur.Inbox.SqlServer;
using Excalibur.LeaderElection.SqlServer;
using Excalibur.Saga.SqlServer;
using Excalibur.SqlServer;

namespace Excalibur.SqlServer.Tests;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class ExcaliburSqlServerOptionsShould : UnitTestBase
{
    // ── Default values ──────────────────────────────────────────────

    [Fact]
    public void HaveEmptyConnectionStringByDefault()
    {
        var sut = new ExcaliburSqlServerOptions();

        sut.ConnectionString.ShouldBe(string.Empty);
    }

    [Fact]
    public void HaveUseInboxTrueByDefault()
    {
        var sut = new ExcaliburSqlServerOptions();
        sut.UseInbox.ShouldBeTrue();
    }

    [Fact]
    public void HaveUseSagaTrueByDefault()
    {
        var sut = new ExcaliburSqlServerOptions();
        sut.UseSaga.ShouldBeTrue();
    }

    [Fact]
    public void HaveUseLeaderElectionTrueByDefault()
    {
        var sut = new ExcaliburSqlServerOptions();
        sut.UseLeaderElection.ShouldBeTrue();
    }

    [Fact]
    public void HaveUseAuditLoggingTrueByDefault()
    {
        var sut = new ExcaliburSqlServerOptions();
        sut.UseAuditLogging.ShouldBeTrue();
    }

    [Fact]
    public void HaveUseComplianceTrueByDefault()
    {
        var sut = new ExcaliburSqlServerOptions();
        sut.UseCompliance.ShouldBeTrue();
    }

    [Fact]
    public void HaveNullConfigurationCallbacksByDefault()
    {
        var sut = new ExcaliburSqlServerOptions();

        sut.DispatchConfiguration.ShouldBeNull();
        sut.InboxConfiguration.ShouldBeNull();
        sut.SagaConfiguration.ShouldBeNull();
        sut.LeaderElectionConfiguration.ShouldBeNull();
        sut.AuditLoggingConfiguration.ShouldBeNull();
        sut.KeyEscrowConfiguration.ShouldBeNull();
        sut.ErasureConfiguration.ShouldBeNull();
    }

    // ── ConnectionString ────────────────────────────────────────────

    [Fact]
    public void AllowSettingConnectionString()
    {
        var sut = new ExcaliburSqlServerOptions();

        sut.ConnectionString = "Server=localhost;Database=test";

        sut.ConnectionString.ShouldBe("Server=localhost;Database=test");
    }

    // ── ConfigureDispatch ───────────────────────────────────────────

    [Fact]
    public void StoreDispatchCallback()
    {
        var sut = new ExcaliburSqlServerOptions();
        Action<IDispatchBuilder> callback = _ => { };

        sut.ConfigureDispatch(callback);

        sut.DispatchConfiguration.ShouldBeSameAs(callback);
    }

    [Fact]
    public void ReturnSameInstanceFromConfigureDispatch()
    {
        var sut = new ExcaliburSqlServerOptions();

        var result = sut.ConfigureDispatch(_ => { });

        result.ShouldBeSameAs(sut);
    }

    [Fact]
    public void ThrowOnNullConfigureDispatch()
    {
        var sut = new ExcaliburSqlServerOptions();

        Should.Throw<ArgumentNullException>(() => sut.ConfigureDispatch(null!))
            .ParamName.ShouldBe("configure");
    }

    // ── ConfigureInbox ──────────────────────────────────────────────

    [Fact]
    public void StoreInboxCallback()
    {
        var sut = new ExcaliburSqlServerOptions();
        Action<ISqlServerInboxBuilder> callback = _ => { };

        sut.ConfigureInbox(callback);

        sut.InboxConfiguration.ShouldBeSameAs(callback);
    }

    [Fact]
    public void ReturnSameInstanceFromConfigureInbox()
    {
        var sut = new ExcaliburSqlServerOptions();

        var result = sut.ConfigureInbox(_ => { });

        result.ShouldBeSameAs(sut);
    }

    [Fact]
    public void ThrowOnNullConfigureInbox()
    {
        var sut = new ExcaliburSqlServerOptions();

        Should.Throw<ArgumentNullException>(() => sut.ConfigureInbox(null!))
            .ParamName.ShouldBe("configure");
    }

    // ── ConfigureSaga ───────────────────────────────────────────────

    [Fact]
    public void StoreSagaCallback()
    {
        var sut = new ExcaliburSqlServerOptions();
        Action<ISqlServerSagaBuilder> callback = _ => { };

        sut.ConfigureSaga(callback);

        sut.SagaConfiguration.ShouldBeSameAs(callback);
    }

    [Fact]
    public void ReturnSameInstanceFromConfigureSaga()
    {
        var sut = new ExcaliburSqlServerOptions();

        var result = sut.ConfigureSaga(_ => { });

        result.ShouldBeSameAs(sut);
    }

    [Fact]
    public void ThrowOnNullConfigureSaga()
    {
        var sut = new ExcaliburSqlServerOptions();

        Should.Throw<ArgumentNullException>(() => sut.ConfigureSaga(null!))
            .ParamName.ShouldBe("configure");
    }

    // ── ConfigureLeaderElection ─────────────────────────────────────

    [Fact]
    public void StoreLeaderElectionCallback()
    {
        var sut = new ExcaliburSqlServerOptions();
        Action<ISqlServerLeaderElectionBuilder> callback = _ => { };

        sut.ConfigureLeaderElection(callback);

        sut.LeaderElectionConfiguration.ShouldBeSameAs(callback);
    }

    [Fact]
    public void ReturnSameInstanceFromConfigureLeaderElection()
    {
        var sut = new ExcaliburSqlServerOptions();

        var result = sut.ConfigureLeaderElection(_ => { });

        result.ShouldBeSameAs(sut);
    }

    [Fact]
    public void ThrowOnNullConfigureLeaderElection()
    {
        var sut = new ExcaliburSqlServerOptions();

        Should.Throw<ArgumentNullException>(() => sut.ConfigureLeaderElection(null!))
            .ParamName.ShouldBe("configure");
    }

    // ── ConfigureAuditLogging ───────────────────────────────────────

    [Fact]
    public void StoreAuditLoggingCallback()
    {
        var sut = new ExcaliburSqlServerOptions();
        Action<SqlServerAuditOptions> callback = _ => { };

        sut.ConfigureAuditLogging(callback);

        sut.AuditLoggingConfiguration.ShouldBeSameAs(callback);
    }

    [Fact]
    public void ReturnSameInstanceFromConfigureAuditLogging()
    {
        var sut = new ExcaliburSqlServerOptions();

        var result = sut.ConfigureAuditLogging(_ => { });

        result.ShouldBeSameAs(sut);
    }

    [Fact]
    public void ThrowOnNullConfigureAuditLogging()
    {
        var sut = new ExcaliburSqlServerOptions();

        Should.Throw<ArgumentNullException>(() => sut.ConfigureAuditLogging(null!))
            .ParamName.ShouldBe("configure");
    }

    // ── ConfigureKeyEscrow ──────────────────────────────────────────

    [Fact]
    public void StoreKeyEscrowCallback()
    {
        var sut = new ExcaliburSqlServerOptions();
        Action<SqlServerKeyEscrowOptions> callback = _ => { };

        sut.ConfigureKeyEscrow(callback);

        sut.KeyEscrowConfiguration.ShouldBeSameAs(callback);
    }

    [Fact]
    public void ReturnSameInstanceFromConfigureKeyEscrow()
    {
        var sut = new ExcaliburSqlServerOptions();

        var result = sut.ConfigureKeyEscrow(_ => { });

        result.ShouldBeSameAs(sut);
    }

    [Fact]
    public void ThrowOnNullConfigureKeyEscrow()
    {
        var sut = new ExcaliburSqlServerOptions();

        Should.Throw<ArgumentNullException>(() => sut.ConfigureKeyEscrow(null!))
            .ParamName.ShouldBe("configure");
    }

    // ── ConfigureErasure ────────────────────────────────────────────

    [Fact]
    public void StoreErasureCallback()
    {
        var sut = new ExcaliburSqlServerOptions();
        Action<SqlServerErasureStoreOptions> callback = _ => { };

        sut.ConfigureErasure(callback);

        sut.ErasureConfiguration.ShouldBeSameAs(callback);
    }

    [Fact]
    public void ReturnSameInstanceFromConfigureErasure()
    {
        var sut = new ExcaliburSqlServerOptions();

        var result = sut.ConfigureErasure(_ => { });

        result.ShouldBeSameAs(sut);
    }

    [Fact]
    public void ThrowOnNullConfigureErasure()
    {
        var sut = new ExcaliburSqlServerOptions();

        Should.Throw<ArgumentNullException>(() => sut.ConfigureErasure(null!))
            .ParamName.ShouldBe("configure");
    }

    // ── Fluent chaining ─────────────────────────────────────────────

    [Fact]
    public void SupportFullFluentChaining()
    {
        var sut = new ExcaliburSqlServerOptions();

        var result = sut
            .ConfigureDispatch(_ => { })
            .ConfigureInbox(_ => { })
            .ConfigureSaga(_ => { })
            .ConfigureLeaderElection(_ => { })
            .ConfigureAuditLogging(_ => { })
            .ConfigureKeyEscrow(_ => { })
            .ConfigureErasure(_ => { });

        result.ShouldBeSameAs(sut);
    }

    [Fact]
    public void OverwritePreviousCallbackOnSecondConfigure()
    {
        var sut = new ExcaliburSqlServerOptions();
        Action<IDispatchBuilder> first = _ => { };
        Action<IDispatchBuilder> second = _ => { };

        sut.ConfigureDispatch(first);
        sut.ConfigureDispatch(second);

        sut.DispatchConfiguration.ShouldBeSameAs(second);
    }

    // ── AddExcaliburSqlServer entry point ────────────────────────────

    [Fact]
    public void ThrowOnNullServicesForAddExcaliburSqlServer()
    {
        IServiceCollection? services = null;

        Should.Throw<ArgumentNullException>(() =>
            services!.AddExcaliburSqlServer(_ => { }))
            .ParamName.ShouldBe("services");
    }

    [Fact]
    public void ThrowOnNullConfigureForAddExcaliburSqlServer()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentNullException>(() =>
            services.AddExcaliburSqlServer((Action<ExcaliburSqlServerOptions>)null!))
            .ParamName.ShouldBe("configure");
    }

    [Fact]
    public void ReturnSameServiceCollectionFromAddExcaliburSqlServer()
    {
        var services = new ServiceCollection();

        var result = services.AddExcaliburSqlServer(sql =>
            sql.ConnectionString = "Server=localhost;Database=test");

        result.ShouldBeSameAs(services);
    }

    [Fact]
    public void InvokeConfigureActionInAddExcaliburSqlServer()
    {
        var services = new ServiceCollection();
        var invoked = false;

        services.AddExcaliburSqlServer(sql =>
        {
            invoked = true;
            sql.ConnectionString = "Server=localhost;Database=test";
        });

        invoked.ShouldBeTrue();
    }
}
