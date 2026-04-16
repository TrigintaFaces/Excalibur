// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.DependencyInjection;
using Excalibur.Saga.Postgres;
using Excalibur.Saga.Postgres.DependencyInjection;

namespace Excalibur.Data.Tests.Postgres.Builders.Saga;

/// <summary>
/// Unit tests for <see cref="SagaBuilderPostgresExtensions.UsePostgres"/>.
/// Validates null guards, fluent chaining, and options registration.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Database", "Postgres")]
public sealed class SagaBuilderPostgresExtensionsShould : UnitTestBase
{
    private const string TestConnectionString =
        "Host=localhost;Database=test;Username=test;Password=test";

    [Fact]
    public void UsePostgres_ThrowWhenBuilderIsNull()
    {
        ISagaBuilder builder = null!;
        Should.Throw<ArgumentNullException>(() =>
            builder.UsePostgres(pg => pg.ConnectionString(TestConnectionString)));
    }

    [Fact]
    public void UsePostgres_ThrowWhenConfigureIsNull()
    {
        var services = new ServiceCollection();
        var builder = A.Fake<ISagaBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        Should.Throw<ArgumentNullException>(() =>
            builder.UsePostgres((Action<IPostgresSagaBuilder>)null!));
    }

    [Fact]
    public void UsePostgres_ReturnSameBuilderForChaining()
    {
        var services = new ServiceCollection();
        var builder = A.Fake<ISagaBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        var result = builder.UsePostgres(pg => pg.ConnectionString(TestConnectionString));
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void UsePostgres_RegisterPostgresSagaOptions()
    {
        var services = new ServiceCollection();
        var builder = A.Fake<ISagaBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        builder.UsePostgres(pg => pg.ConnectionString(TestConnectionString));

        services.ShouldContain(sd =>
            sd.ServiceType == typeof(IConfigureOptions<PostgresSagaOptions>));
    }

    [Fact]
    public void UsePostgres_ConfiguresSchemaViaBuilder()
    {
        var services = new ServiceCollection();
        var builder = A.Fake<ISagaBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        builder.UsePostgres(pg => pg.ConnectionString(TestConnectionString).SchemaName("custom"));

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<PostgresSagaOptions>>();
        options.Value.Schema.ShouldBe("custom");
    }

    [Fact]
    public void UsePostgres_ConfiguresTableNameViaBuilder()
    {
        var services = new ServiceCollection();
        var builder = A.Fake<ISagaBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        builder.UsePostgres(pg => pg.ConnectionString(TestConnectionString).TableName("custom_sagas"));

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<PostgresSagaOptions>>();
        options.Value.TableName.ShouldBe("custom_sagas");
    }
}
