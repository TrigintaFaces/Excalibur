// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance.Postgres.Erasure;
using Excalibur.Dispatch.Configuration;
using Excalibur.AuditLogging.Postgres;
using Excalibur.Inbox.Postgres;
using Excalibur.LeaderElection.Postgres;
using Excalibur.Postgres;
using Excalibur.Saga.Postgres;

namespace Excalibur.Postgres.Tests;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class ExcaliburPostgresOptionsShould : UnitTestBase
{
    // ── Default values ──────────────────────────────────────────────

    [Fact]
    public void HaveEmptyConnectionStringByDefault()
    {
        // Arrange & Act
        var sut = new ExcaliburPostgresOptions();

        // Assert
        sut.ConnectionString.ShouldBe(string.Empty);
    }

    [Fact]
    public void HaveUseInboxTrueByDefault()
    {
        var sut = new ExcaliburPostgresOptions();
        sut.UseInbox.ShouldBeTrue();
    }

    [Fact]
    public void HaveUseSagaTrueByDefault()
    {
        var sut = new ExcaliburPostgresOptions();
        sut.UseSaga.ShouldBeTrue();
    }

    [Fact]
    public void HaveUseLeaderElectionTrueByDefault()
    {
        var sut = new ExcaliburPostgresOptions();
        sut.UseLeaderElection.ShouldBeTrue();
    }

    [Fact]
    public void HaveUseAuditLoggingTrueByDefault()
    {
        var sut = new ExcaliburPostgresOptions();
        sut.UseAuditLogging.ShouldBeTrue();
    }

    [Fact]
    public void HaveUseComplianceTrueByDefault()
    {
        var sut = new ExcaliburPostgresOptions();
        sut.UseCompliance.ShouldBeTrue();
    }

    [Fact]
    public void HaveNullConfigurationCallbacksByDefault()
    {
        var sut = new ExcaliburPostgresOptions();

        sut.DispatchConfiguration.ShouldBeNull();
        sut.InboxConfiguration.ShouldBeNull();
        sut.SagaConfiguration.ShouldBeNull();
        sut.LeaderElectionConfiguration.ShouldBeNull();
        sut.AuditLoggingConfiguration.ShouldBeNull();
        sut.ErasureConfiguration.ShouldBeNull();
    }

    // ── ConnectionString ────────────────────────────────────────────

    [Fact]
    public void AllowSettingConnectionString()
    {
        var sut = new ExcaliburPostgresOptions();

        sut.ConnectionString = "Host=localhost;Database=test";

        sut.ConnectionString.ShouldBe("Host=localhost;Database=test");
    }

    // ── ConfigureDispatch ───────────────────────────────────────────

    [Fact]
    public void StoreDispatchCallback()
    {
        // Arrange
        var sut = new ExcaliburPostgresOptions();
        Action<IDispatchBuilder> callback = _ => { };

        // Act
        sut.ConfigureDispatch(callback);

        // Assert
        sut.DispatchConfiguration.ShouldBeSameAs(callback);
    }

    [Fact]
    public void ReturnSameInstanceFromConfigureDispatch()
    {
        var sut = new ExcaliburPostgresOptions();

        var result = sut.ConfigureDispatch(_ => { });

        result.ShouldBeSameAs(sut);
    }

    [Fact]
    public void ThrowOnNullConfigureDispatch()
    {
        var sut = new ExcaliburPostgresOptions();

        Should.Throw<ArgumentNullException>(() => sut.ConfigureDispatch(null!))
            .ParamName.ShouldBe("configure");
    }

    // ── ConfigureInbox ──────────────────────────────────────────────

    [Fact]
    public void StoreInboxCallback()
    {
        var sut = new ExcaliburPostgresOptions();
        Action<IPostgresInboxBuilder> callback = _ => { };

        sut.ConfigureInbox(callback);

        sut.InboxConfiguration.ShouldBeSameAs(callback);
    }

    [Fact]
    public void ReturnSameInstanceFromConfigureInbox()
    {
        var sut = new ExcaliburPostgresOptions();

        var result = sut.ConfigureInbox(_ => { });

        result.ShouldBeSameAs(sut);
    }

    [Fact]
    public void ThrowOnNullConfigureInbox()
    {
        var sut = new ExcaliburPostgresOptions();

        Should.Throw<ArgumentNullException>(() => sut.ConfigureInbox(null!))
            .ParamName.ShouldBe("configure");
    }

    // ── ConfigureSaga ───────────────────────────────────────────────

    [Fact]
    public void StoreSagaCallback()
    {
        var sut = new ExcaliburPostgresOptions();
        Action<IPostgresSagaBuilder> callback = _ => { };

        sut.ConfigureSaga(callback);

        sut.SagaConfiguration.ShouldBeSameAs(callback);
    }

    [Fact]
    public void ReturnSameInstanceFromConfigureSaga()
    {
        var sut = new ExcaliburPostgresOptions();

        var result = sut.ConfigureSaga(_ => { });

        result.ShouldBeSameAs(sut);
    }

    [Fact]
    public void ThrowOnNullConfigureSaga()
    {
        var sut = new ExcaliburPostgresOptions();

        Should.Throw<ArgumentNullException>(() => sut.ConfigureSaga(null!))
            .ParamName.ShouldBe("configure");
    }

    // ── ConfigureLeaderElection ─────────────────────────────────────

    [Fact]
    public void StoreLeaderElectionCallback()
    {
        var sut = new ExcaliburPostgresOptions();
        Action<IPostgresLeaderElectionBuilder> callback = _ => { };

        sut.ConfigureLeaderElection(callback);

        sut.LeaderElectionConfiguration.ShouldBeSameAs(callback);
    }

    [Fact]
    public void ReturnSameInstanceFromConfigureLeaderElection()
    {
        var sut = new ExcaliburPostgresOptions();

        var result = sut.ConfigureLeaderElection(_ => { });

        result.ShouldBeSameAs(sut);
    }

    [Fact]
    public void ThrowOnNullConfigureLeaderElection()
    {
        var sut = new ExcaliburPostgresOptions();

        Should.Throw<ArgumentNullException>(() => sut.ConfigureLeaderElection(null!))
            .ParamName.ShouldBe("configure");
    }

    // ── ConfigureAuditLogging ───────────────────────────────────────

    [Fact]
    public void StoreAuditLoggingCallback()
    {
        var sut = new ExcaliburPostgresOptions();
        Action<PostgresAuditOptions> callback = _ => { };

        sut.ConfigureAuditLogging(callback);

        sut.AuditLoggingConfiguration.ShouldBeSameAs(callback);
    }

    [Fact]
    public void ReturnSameInstanceFromConfigureAuditLogging()
    {
        var sut = new ExcaliburPostgresOptions();

        var result = sut.ConfigureAuditLogging(_ => { });

        result.ShouldBeSameAs(sut);
    }

    [Fact]
    public void ThrowOnNullConfigureAuditLogging()
    {
        var sut = new ExcaliburPostgresOptions();

        Should.Throw<ArgumentNullException>(() => sut.ConfigureAuditLogging(null!))
            .ParamName.ShouldBe("configure");
    }

    // ── ConfigureErasure ────────────────────────────────────────────

    [Fact]
    public void StoreErasureCallback()
    {
        var sut = new ExcaliburPostgresOptions();
        Action<PostgresErasureStoreOptions> callback = _ => { };

        sut.ConfigureErasure(callback);

        sut.ErasureConfiguration.ShouldBeSameAs(callback);
    }

    [Fact]
    public void ReturnSameInstanceFromConfigureErasure()
    {
        var sut = new ExcaliburPostgresOptions();

        var result = sut.ConfigureErasure(_ => { });

        result.ShouldBeSameAs(sut);
    }

    [Fact]
    public void ThrowOnNullConfigureErasure()
    {
        var sut = new ExcaliburPostgresOptions();

        Should.Throw<ArgumentNullException>(() => sut.ConfigureErasure(null!))
            .ParamName.ShouldBe("configure");
    }

    // ── Fluent chaining ─────────────────────────────────────────────

    [Fact]
    public void SupportFullFluentChaining()
    {
        var sut = new ExcaliburPostgresOptions();

        var result = sut
            .ConfigureDispatch(_ => { })
            .ConfigureInbox(_ => { })
            .ConfigureSaga(_ => { })
            .ConfigureLeaderElection(_ => { })
            .ConfigureAuditLogging(_ => { })
            .ConfigureErasure(_ => { });

        result.ShouldBeSameAs(sut);
    }

    [Fact]
    public void OverwritePreviousCallbackOnSecondConfigure()
    {
        var sut = new ExcaliburPostgresOptions();
        Action<IDispatchBuilder> first = _ => { };
        Action<IDispatchBuilder> second = _ => { };

        sut.ConfigureDispatch(first);
        sut.ConfigureDispatch(second);

        sut.DispatchConfiguration.ShouldBeSameAs(second);
    }

    // ── AddExcaliburPostgres entry point ────────────────────────────

    [Fact]
    public void ThrowOnNullServicesForAddExcaliburPostgres()
    {
        IServiceCollection? services = null;

        Should.Throw<ArgumentNullException>(() =>
            services!.AddExcaliburPostgres(_ => { }))
            .ParamName.ShouldBe("services");
    }

    [Fact]
    public void ThrowOnNullConfigureForAddExcaliburPostgres()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentNullException>(() =>
            services.AddExcaliburPostgres((Action<ExcaliburPostgresOptions>)null!))
            .ParamName.ShouldBe("configure");
    }

    [Fact]
    public void ReturnSameServiceCollectionFromAddExcaliburPostgres()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddExcaliburPostgres(pg =>
            pg.ConnectionString = "Host=localhost;Database=test");

        // Assert
        result.ShouldBeSameAs(services);
    }

    [Fact]
    public void InvokeConfigureActionInAddExcaliburPostgres()
    {
        // Arrange
        var services = new ServiceCollection();
        var invoked = false;

        // Act
        services.AddExcaliburPostgres(pg =>
        {
            invoked = true;
            pg.ConnectionString = "Host=localhost;Database=test";
        });

        // Assert
        invoked.ShouldBeTrue();
    }
}
