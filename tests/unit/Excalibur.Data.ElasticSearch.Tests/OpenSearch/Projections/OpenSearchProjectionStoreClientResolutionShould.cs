// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Data.OpenSearch.Projections;

using Excalibur.EventSourcing;

using OpenSearch.Client;

namespace Excalibur.Data.ElasticSearch.Tests.OpenSearch.Projections;

/// <summary>
/// Binds the projection store to the OpenSearch client a consumer registered, rather than one built from
/// the configured node address regardless.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect these arms catch.</b> The registration previously constructed a client from
/// <c>OpenSearchProjectionStoreOptions.NodeUri</c> unconditionally, so a consumer who deliberately
/// registered their own <see cref="IOpenSearchClient" /> — configured for their cluster, their
/// credentials, their transport settings — got a store that quietly talked somewhere else. The default
/// node is a local address, so the substitution surfaces only if nothing happens to be listening on it;
/// where something is, the store reads and writes real documents to the wrong cluster.
/// </para>
/// <para>
/// <b>Why the existing coverage could not catch it.</b> The integration suite registers no client and
/// exercises the node-address path on purpose, so it passes either way. The property that was wrong —
/// that a registered client is preferred — had no arm at all.
/// </para>
/// <para>
/// <b>Both directions are asserted.</b> A registration-preference that always won would be as wrong as
/// one that never did: a host that registers no client must still get a working store from the node
/// address. Asserting only the first arm would be satisfied by an implementation that throws whenever no
/// client is registered.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class OpenSearchProjectionStoreClientResolutionShould
{
    private sealed class TestProjection
    {
        public string Id { get; init; } = string.Empty;
    }

    private static IOpenSearchClient? ClientOf(OpenSearchProjectionStore<TestProjection> store) =>
        (IOpenSearchClient?)typeof(OpenSearchProjectionStore<TestProjection>)
            .GetField("_client", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(store);

    [Fact]
    public void UseTheClientTheConsumerRegistered()
    {
        var registered = A.Fake<IOpenSearchClient>();
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddSingleton(registered);
        _ = services.AddOpenSearchProjectionStore<TestProjection>(
            (Action<OpenSearchProjectionStoreOptions>)(o => o.NodeUri = "https://example.invalid:9200"));

        using var provider = services.BuildServiceProvider();
        var store = (OpenSearchProjectionStore<TestProjection>)provider
            .GetRequiredService<IProjectionStore<TestProjection>>();

        ClientOf(store).ShouldBeSameAs(
            registered,
            "a deliberately registered OpenSearch client must be the one the store uses. Building a client "
            + "from the configured node address regardless makes the registration appear to take effect "
            + "while the store reads and writes against a different cluster entirely.");
    }

    [Fact]
    public void StillBuildAStoreWhenNoClientIsRegistered()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddOpenSearchProjectionStore<TestProjection>(
            (Action<OpenSearchProjectionStoreOptions>)(o => o.NodeUri = "https://example.invalid:9200"));

        using var provider = services.BuildServiceProvider();
        var store = (OpenSearchProjectionStore<TestProjection>)provider
            .GetRequiredService<IProjectionStore<TestProjection>>();

        // The liveness half. Preferring a registered client must not become a requirement for one --
        // configuring the store by node address alone is a supported and common shape.
        store.ShouldNotBeNull(
            "a host that registers no client must still resolve a store built from the node address.");
        ClientOf(store).ShouldNotBeNull("the store must always end up with a client to talk to.");
    }

    /// <summary>
    /// Builds the store through a real container assembled by the production registration entry points,
    /// rather than by hand-registering the client the store is expected to find.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The gap these arms close.</b> The arms above register the client themselves, as
    /// <c>IOpenSearchClient</c>. No shipped entry point registers that service type: every one of them --
    /// the preconfigured-client overload, the node-address overloads, and each branch of the fluent
    /// builder -- registers the concrete <see cref="OpenSearchClient" />. A store that resolved only the
    /// interface would therefore pass the arms above and still leave every documented registration
    /// unused, silently falling back to the configured node address. Asserting against a hand-supplied
    /// dependency says nothing about what the registration produces; these arms resolve the store from a
    /// provider built by the real entry points.
    /// </para>
    /// <para>
    /// <b>Why reference identity is the right assertion.</b> The node-address fallback constructs its own
    /// client inside the constructor, so a store that fell back can never hold the same instance the
    /// container does. Same-reference is therefore exactly the property "this store is not addressing
    /// NodeUri", stated without needing a cluster to talk to.
    /// </para>
    /// </remarks>
    /// <param name="shape">The registration entry point under test.</param>
    [Theory]
    [InlineData(RegistrationShape.AddOpenSearchServicesWithClient)]
    [InlineData(RegistrationShape.AddOpenSearchServicesWithNodeUri)]
    [InlineData(RegistrationShape.AddOpenSearchServicesWithNodeUris)]
    [InlineData(RegistrationShape.BuilderClientInstance)]
    [InlineData(RegistrationShape.BuilderClientFactory)]
    [InlineData(RegistrationShape.BuilderNodeUri)]
    [InlineData(RegistrationShape.BuilderNodeUris)]
    [InlineData(RegistrationShape.BuilderBindConfiguration)]
    public void UseTheClientTheShippedRegistrationEntryPointsProduce(RegistrationShape shape)
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        var expected = Register(services, shape);

        _ = services.AddOpenSearchProjectionStore<TestProjection>(
            (Action<OpenSearchProjectionStoreOptions>)(o => o.NodeUri = "https://example.invalid:9200"));

        using var provider = services.BuildServiceProvider();

        var resolvedClient = provider.GetService<OpenSearchClient>();
        resolvedClient.ShouldNotBeNull(
            $"{shape} must register a client the container can resolve. A registration that binds a "
            + "delegate or an unreachable service type leaves the documented setup path inert.");

        var store = (OpenSearchProjectionStore<TestProjection>)provider
            .GetRequiredService<IProjectionStore<TestProjection>>();

        ClientOf(store).ShouldBeSameAs(
            expected ?? resolvedClient,
            $"a store registered after {shape} must use the client that entry point produced. Falling "
            + "back to a client built from NodeUri makes the documented registration appear to take "
            + "effect while the store reads and writes against a different cluster.");
    }

    /// <summary>The shipped entry points that put an OpenSearch client into the container.</summary>
    public enum RegistrationShape
    {
        /// <summary>services.AddOpenSearchServices(client)</summary>
        AddOpenSearchServicesWithClient,

        /// <summary>services.AddOpenSearchServices(nodeUri)</summary>
        AddOpenSearchServicesWithNodeUri,

        /// <summary>services.AddOpenSearchServices(nodeUris)</summary>
        AddOpenSearchServicesWithNodeUris,

        /// <summary>services.AddExcaliburOpenSearch(os =&gt; os.Client(client))</summary>
        BuilderClientInstance,

        /// <summary>services.AddExcaliburOpenSearch(os =&gt; os.ClientFactory(sp =&gt; client))</summary>
        BuilderClientFactory,

        /// <summary>services.AddExcaliburOpenSearch(os =&gt; os.NodeUri(uri))</summary>
        BuilderNodeUri,

        /// <summary>services.AddExcaliburOpenSearch(os =&gt; os.NodeUris(uris))</summary>
        BuilderNodeUris,

        /// <summary>services.AddExcaliburOpenSearch(os =&gt; os.BindConfiguration(path))</summary>
        BuilderBindConfiguration,
    }

    private static readonly Uri ConsumerNode = new("https://consumer-cluster.invalid:9200");

    private static OpenSearchClient NewConsumerClient() =>
        new(new ConnectionSettings(ConsumerNode));

    /// <summary>
    /// Applies one registration shape, returning the exact client instance the caller handed in where the
    /// shape has one, or <see langword="null" /> where the entry point builds the client itself (in which
    /// case the arm asserts against whatever the container resolves).
    /// </summary>
    private static OpenSearchClient? Register(IServiceCollection services, RegistrationShape shape)
    {
        switch (shape)
        {
            case RegistrationShape.AddOpenSearchServicesWithClient:
            {
                var client = NewConsumerClient();
                _ = services.AddOpenSearchServices(client);
                return client;
            }

            case RegistrationShape.AddOpenSearchServicesWithNodeUri:
                _ = services.AddOpenSearchServices(ConsumerNode.ToString());
                return null;

            case RegistrationShape.AddOpenSearchServicesWithNodeUris:
                _ = services.AddOpenSearchServices(new[] { ConsumerNode });
                return null;

            case RegistrationShape.BuilderClientInstance:
            {
                var client = NewConsumerClient();
                _ = services.AddExcaliburOpenSearch(os => os.Client(client));
                return client;
            }

            case RegistrationShape.BuilderClientFactory:
            {
                var client = NewConsumerClient();
                _ = services.AddExcaliburOpenSearch(os => os.ClientFactory(_ => client));
                return client;
            }

            case RegistrationShape.BuilderNodeUri:
                _ = services.AddExcaliburOpenSearch(os => os.NodeUri(ConsumerNode));
                return null;

            case RegistrationShape.BuilderNodeUris:
                _ = services.AddExcaliburOpenSearch(os => os.NodeUris(new[] { ConsumerNode }));
                return null;

            case RegistrationShape.BuilderBindConfiguration:
                _ = services.AddSingleton<IConfiguration>(
                    new ConfigurationBuilder()
                        .AddInMemoryCollection(new Dictionary<string, string?>
                        {
                            ["OpenSearch:Url"] = ConsumerNode.ToString(),
                        })
                        .Build());
                _ = services.AddExcaliburOpenSearch(os => os.BindConfiguration("OpenSearch"));
                return null;

            default:
                throw new ArgumentOutOfRangeException(nameof(shape), shape, "Unhandled registration shape.");
        }
    }
}
