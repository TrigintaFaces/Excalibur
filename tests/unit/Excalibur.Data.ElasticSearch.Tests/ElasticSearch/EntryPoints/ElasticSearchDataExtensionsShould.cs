// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch;

namespace Excalibur.Data.Tests.ElasticSearch.EntryPoints;

/// <summary>
/// Unit tests for <see cref="ElasticSearchServiceCollectionExtensions.AddExcaliburElasticSearch"/>.
/// Validates null guards, fluent chaining, configure invocation, and options registration
/// for the <c>Action&lt;IElasticSearchDataBuilder&gt;</c> entry point.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Database", "ElasticSearch")]
public sealed class ElasticSearchDataExtensionsShould : UnitTestBase
{
    private static readonly Uri TestUri = new("http://localhost:9200");

    [Fact]
    public void AddExcaliburElasticSearch_ThrowWhenServicesIsNull()
    {
        // Arrange
        IServiceCollection services = null!;

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            services.AddExcaliburElasticSearch(es =>
                es.NodeUri(TestUri)));
    }

    [Fact]
    public void AddExcaliburElasticSearch_ThrowWhenConfigureIsNull()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            services.AddExcaliburElasticSearch(null!));
    }

    [Fact]
    public void AddExcaliburElasticSearch_ReturnSameServicesForChaining()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddExcaliburElasticSearch(es =>
            es.NodeUri(TestUri));

        // Assert
        result.ShouldBeSameAs(services);
    }

    [Fact]
    public void AddExcaliburElasticSearch_InvokeConfigureAction()
    {
        // Arrange
        var services = new ServiceCollection();
        var configureInvoked = false;

        // Act
        services.AddExcaliburElasticSearch(es =>
        {
            es.NodeUri(TestUri).IndexPrefix("myapp");
            configureInvoked = true;
        });

        // Assert
        configureInvoked.ShouldBeTrue();
    }

    [Fact]
    public void AddExcaliburElasticSearch_RegisterElasticsearchClient()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddExcaliburElasticSearch(es =>
            es.NodeUri(TestUri));

        // Assert
        services.ShouldContain(sd =>
            sd.ServiceType == typeof(ElasticsearchClient));
    }

    [Fact]
    public void AddExcaliburElasticSearch_RegisterOptionsWhenBindConfigurationUsed()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddExcaliburElasticSearch(es =>
            es.BindConfiguration("ElasticSearch"));

        // Assert
        services.ShouldContain(sd =>
            sd.ServiceType == typeof(IConfigureOptions<ElasticsearchConfigurationOptions>));
    }
}
