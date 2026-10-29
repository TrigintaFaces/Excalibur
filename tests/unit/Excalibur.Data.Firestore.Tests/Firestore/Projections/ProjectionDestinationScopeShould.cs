// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using Excalibur.EventSourcing;
using Google.Cloud.Firestore;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Data.Tests.Firestore.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "Projections")]
[Trait("Pattern", "Regression")]
public sealed class ProjectionDestinationScopeShould
{
    [Fact]
    public async Task PreserveExactFrameworkClientEndpointInGenerationHandles()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddExcaliburFirestore(f => f.ProjectId("scope-project").EmulatorHost("127.0.0.1:65123"));
        services.AddFirestoreProjectionStore<Row>();
        await using var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IProjectionStore<Row>>();
        var scope = (IProjectionDestinationScope)store.GetService(typeof(IProjectionDestinationScope))!;
        scope.Scope.ShouldContain("127.0.0.1");
        var factory = (IProjectionGenerationReaderFactory<Row>)store.GetService(typeof(IProjectionGenerationReaderFactory<Row>))!;
        var destination = ((IProjectionDestinationIdentity)store.GetService(typeof(IProjectionDestinationIdentity))!).Identity;
        await using var reader = factory.OpenReader(destination);
        (await reader.GetDestinationScopeAsync(CancellationToken.None)).ShouldBe(scope.Scope);
    }

    [Fact]
    public async Task NeverAssignConfiguredEndpointToAPreexistingClient()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new FirestoreDbBuilder
        {
            ProjectId = "scope-project", Endpoint = "127.0.0.1:65124", ChannelCredentials = ChannelCredentials.Insecure,
        }.Build());
        services.AddExcaliburFirestore(f => f.ProjectId("scope-project").EmulatorHost("127.0.0.1:65123"));
        services.AddFirestoreProjectionStore<Row>();
        await using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IProjectionStore<Row>>().GetService(typeof(IProjectionDestinationScope)).ShouldBeNull();
    }

    internal sealed class Row { public string Id { get; set; } = string.Empty; }
}
