// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.OpenSearch;

using OpenSearch.Client;

namespace Excalibur.Data.Tests.OpenSearch.EntryPoints;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Database", "OpenSearch")]
public sealed class OpenSearchDataExtensionsShould : UnitTestBase
{
    [Fact]
    public void ThrowWhenServicesIsNull()
    {
        IServiceCollection services = null!;

        Should.Throw<ArgumentNullException>(
            () => services.AddExcaliburOpenSearch(_ => { }));
    }

    [Fact]
    public void ThrowWhenConfigureIsNull()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentNullException>(
            () => services.AddExcaliburOpenSearch(null!));
    }

    [Fact]
    public void ReturnSameServiceCollectionForChaining()
    {
        var services = new ServiceCollection();

        var result = services.AddExcaliburOpenSearch(os =>
            os.NodeUri(new Uri("https://localhost:9200")));

        result.ShouldBeSameAs(services);
    }

    [Fact]
    public void InvokeConfigureAction()
    {
        var services = new ServiceCollection();
        var invoked = false;

        services.AddExcaliburOpenSearch(_ => invoked = true);

        invoked.ShouldBeTrue();
    }

    [Fact]
    public void RegisterOpenSearchClientWhenNodeUriConfigured()
    {
        var services = new ServiceCollection();

        services.AddExcaliburOpenSearch(os =>
            os.NodeUri(new Uri("https://localhost:9200")));

        services.ShouldContain(
            d => d.ServiceType == typeof(OpenSearchClient)
                 && d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void RegisterConfigurationOptionsWhenBindConfigurationUsed()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["OpenSearch:Url"] = "https://localhost:9200",
                })
                .Build());

        services.AddExcaliburOpenSearch(os =>
            os.BindConfiguration("OpenSearch"));

        // Options infrastructure should be registered
        var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<IOptions<OpenSearchConfigurationOptions>>();
        options.Value.ShouldNotBeNull();
    }
}
