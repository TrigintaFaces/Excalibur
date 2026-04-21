// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.LeaderElection.Postgres;
using Excalibur.Dispatch.LeaderElection.DependencyInjection;

namespace Excalibur.Data.Tests.Postgres;

/// <summary>
/// Unit tests for <see cref="PostgresLeaderElectionBuilderExtensions"/>.
/// Updated for the canonical 5-overload builder pattern (Sprint 767).
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Database", "Postgres")]
public sealed class PostgresLeaderElectionBuilderExtensionsShould
{
    private const string TestConnectionString =
        "Host=localhost;Database=test;Username=test;Password=test";

    [Fact]
    public void UsePostgres_ThrowWhenBuilderIsNull()
    {
        ILeaderElectionBuilder builder = null!;
        Should.Throw<ArgumentNullException>(() =>
            builder.UsePostgres(pg => pg.ConnectionString(TestConnectionString)));
    }

    [Fact]
    public void UsePostgres_ThrowWhenConfigureIsNull()
    {
        var services = new ServiceCollection();
        var builder = A.Fake<ILeaderElectionBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        Should.Throw<ArgumentNullException>(() =>
            builder.UsePostgres((Action<IPostgresLeaderElectionBuilder>)null!));
    }

    [Fact]
    public void UsePostgres_ReturnSameBuilderForChaining()
    {
        var services = new ServiceCollection();
        var builder = A.Fake<ILeaderElectionBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        var result = builder.UsePostgres(pg => pg.ConnectionString(TestConnectionString));
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void UsePostgres_RegisterPostgresLeaderElectionOptions()
    {
        var services = new ServiceCollection();
        var builder = A.Fake<ILeaderElectionBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        builder.UsePostgres(pg => pg.ConnectionString(TestConnectionString));

        services.ShouldContain(sd =>
            sd.ServiceType == typeof(IConfigureOptions<PostgresLeaderElectionOptions>));
    }

    [Fact]
    public void UsePostgres_ConfiguresLockKeyViaBuilder()
    {
        var services = new ServiceCollection();
        var builder = A.Fake<ILeaderElectionBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        builder.UsePostgres(pg => pg.ConnectionString(TestConnectionString).LockKey(99));

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<PostgresLeaderElectionOptions>>();
        options.Value.LockKey.ShouldBe(99);
    }
}
