// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Inbox.DependencyInjection;
using Excalibur.Inbox.ElasticSearch;

namespace Excalibur.Data.Tests.ElasticSearch.EntryPoints;

/// <summary>
/// Unit tests for <see cref="InboxBuilderElasticsearchExtensions.UseElasticSearch(IInboxBuilder, Action{IElasticSearchInboxBuilder})"/>.
/// Validates null guards, fluent chaining, and options registration
/// for the <c>Action&lt;IElasticSearchInboxBuilder&gt;</c> entry point.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Database", "ElasticSearch")]
public sealed class InboxBuilderElasticSearchExtensionsShould : UnitTestBase
{
    private static readonly Uri TestUri = new("http://localhost:9200");

    [Fact]
    public void UseElasticSearch_ThrowWhenBuilderIsNull()
    {
        // Arrange
        IInboxBuilder builder = null!;

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            builder.UseElasticSearch(es =>
                es.NodeUri(TestUri)));
    }

    [Fact]
    public void UseElasticSearch_ThrowWhenConfigureIsNull()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<IInboxBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            builder.UseElasticSearch((Action<IElasticSearchInboxBuilder>)null!));
    }

    [Fact]
    public void UseElasticSearch_ReturnSameBuilderForChaining()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<IInboxBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act
        var result = builder.UseElasticSearch(es =>
            es.NodeUri(TestUri));

        // Assert
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void UseElasticSearch_RegisterInboxOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<IInboxBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act
        builder.UseElasticSearch(es =>
            es.NodeUri(TestUri));

        // Assert
        services.ShouldContain(sd =>
            sd.ServiceType == typeof(IConfigureOptions<ElasticsearchInboxOptions>));
    }

    [Fact]
    public void UseElasticSearch_InvokeConfigureAction()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<IInboxBuilder>();
        A.CallTo(() => builder.Services).Returns(services);
        var configureInvoked = false;

        // Act
        builder.UseElasticSearch(es =>
        {
            es.NodeUri(TestUri)
              .IndexName("custom-inbox");
            configureInvoked = true;
        });

        // Assert
        configureInvoked.ShouldBeTrue();
    }

    [Fact]
    public void UseElasticSearch_RegisterElasticsearchClient()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<IInboxBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act
        builder.UseElasticSearch(es =>
            es.NodeUri(TestUri));

        // Assert
        services.ShouldContain(sd =>
            sd.ServiceType == typeof(ElasticsearchClient));
    }
}
