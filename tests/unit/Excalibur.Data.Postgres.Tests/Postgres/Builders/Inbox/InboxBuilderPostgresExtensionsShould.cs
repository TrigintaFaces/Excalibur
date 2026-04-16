// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Inbox.DependencyInjection;
using Excalibur.Inbox.Postgres;

namespace Excalibur.Data.Tests.Postgres.Builders.Inbox;

/// <summary>
/// Unit tests for <see cref="InboxBuilderPostgresExtensions.UsePostgres"/>.
/// Validates null guards, fluent chaining, and options registration.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Database", "Postgres")]
public sealed class InboxBuilderPostgresExtensionsShould : UnitTestBase
{
    private const string TestConnectionString =
        "Host=localhost;Database=test;Username=test;Password=test";

    [Fact]
    public void UsePostgres_ThrowWhenBuilderIsNull()
    {
        IInboxBuilder builder = null!;
        Should.Throw<ArgumentNullException>(() =>
            builder.UsePostgres(pg => pg.ConnectionString(TestConnectionString)));
    }

    [Fact]
    public void UsePostgres_ThrowWhenConfigureIsNull()
    {
        var services = new ServiceCollection();
        var builder = A.Fake<IInboxBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        Should.Throw<ArgumentNullException>(() =>
            builder.UsePostgres((Action<IPostgresInboxBuilder>)null!));
    }

    [Fact]
    public void UsePostgres_ReturnSameBuilderForChaining()
    {
        var services = new ServiceCollection();
        var builder = A.Fake<IInboxBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        var result = builder.UsePostgres(pg => pg.ConnectionString(TestConnectionString));
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void UsePostgres_RegisterPostgresInboxOptions()
    {
        var services = new ServiceCollection();
        var builder = A.Fake<IInboxBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        builder.UsePostgres(pg => pg.ConnectionString(TestConnectionString));

        services.ShouldContain(sd =>
            sd.ServiceType == typeof(IConfigureOptions<PostgresInboxOptions>));
    }

    [Fact]
    public void UsePostgres_ConfiguresSchemaViaBuilder()
    {
        var services = new ServiceCollection();
        var builder = A.Fake<IInboxBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        builder.UsePostgres(pg => pg.ConnectionString(TestConnectionString).SchemaName("messaging"));

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<PostgresInboxOptions>>();
        options.Value.SchemaName.ShouldBe("messaging");
    }

    [Fact]
    public void UsePostgres_ConfiguresMaxRetryCountViaBuilder()
    {
        var services = new ServiceCollection();
        var builder = A.Fake<IInboxBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        builder.UsePostgres(pg => pg.ConnectionString(TestConnectionString).MaxRetryCount(10));

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<PostgresInboxOptions>>();
        options.Value.MaxRetryCount.ShouldBe(10);
    }
}
