// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Data.CosmosDb.Authorization;
using Excalibur.Data.CosmosDb.Projections;
using Excalibur.Data.CosmosDb.Snapshots;
using Excalibur.Dispatch;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.CosmosDb.Tests;

/// <summary>
/// Locks every Cosmos store in this package onto the <see cref="CosmosClient"/> the host registered, and
/// locks each one out of disposing it.
/// </summary>
/// <remarks>
/// <para>
/// The registration has always created a shared client. The stores constructed their own anyway, so a host
/// enabling several Cosmos features opened one connection pool per feature against a single account.
/// </para>
/// <para>
/// The two halves are one change and the order matters. A store that borrows the shared client while still
/// disposing it on its own disposal is worse than the duplication it replaces: the first feature disposed
/// takes the account away from every other feature, which then fails with an ObjectDisposedException naming
/// a client none of them closed. Both halves are asserted here so neither can land alone.
/// </para>
/// <para>
/// No emulator is involved and none is needed. The defect is entirely in constructor selection and disposal
/// ownership, both of which are decided before a single request is issued -- the stores build their client
/// lazily during initialization, so a store that was going to open its own has already been handed the wrong
/// answer by the time it is resolved.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Data")]
[Trait("Database", "CosmosDb")]
public sealed class CosmosStoresBorrowTheRegisteredClientShould
{
    private const string Endpoint = "https://localhost:8081/";

    // The well-known Cosmos emulator key. Not a credential: it is published by Microsoft, identical on every
    // emulator install, and nothing here opens a connection. pragma: allowlist secret
    private const string EmulatorKey =
        "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";

    private static (ServiceProvider Provider, CosmosClient Shared) BuildHost(bool registerSharedClient = true)
    {
        var shared = new CosmosClient(Endpoint, EmulatorKey);

        var services = new ServiceCollection();
        _ = services.AddLogging();

        if (registerSharedClient)
        {
            _ = services.AddSingleton(shared);
        }

        _ = services.Configure<CosmosDbAuthorizationOptions>(o =>
        {
            o.Client.AccountEndpoint = Endpoint;
            o.Client.AccountKey = EmulatorKey;
        });
        _ = services.Configure<CosmosDbSnapshotStoreOptions>(o =>
        {
            o.Client.AccountEndpoint = Endpoint;
            o.Client.AccountKey = EmulatorKey;
            o.DatabaseName = "conformance";
            o.ContainerName = "snapshots";

            // Container creation is the only step of InitializeAsync that issues a request. Turning it
            // off lets the initialization arm below run the ownership decision to completion with no
            // account behind it -- GetDatabase and GetContainer are client-side references.
            o.CreateContainerIfNotExists = false;
        });
        _ = services.Configure<CosmosDbProjectionStoreOptions>(o =>
        {
            o.Client.AccountEndpoint = Endpoint;
            o.Client.AccountKey = EmulatorKey;
        });
        _ = services.Configure<CosmosDbOptions>(o =>
        {
            o.DatabaseName = "conformance";
            o.Client.AccountEndpoint = Endpoint;
            o.Client.AccountKey = EmulatorKey;
        });

        _ = services.AddSingleton<ITenantContext>(UntenantedContext.Instance);
        _ = services.AddSingleton<CosmosDbGrantStore>();
        _ = services.AddSingleton<CosmosDbActivityGroupGrantStore>();
        _ = services.AddSingleton<CosmosDbSnapshotStore>();
        _ = services.AddSingleton<CosmosDbProjectionStore<TestProjection>>();
        _ = services.AddSingleton<CosmosDbPersistenceProvider>();

        return (services.BuildServiceProvider(), shared);
    }

    private static CosmosClient? ClientOf(object store) =>
        (CosmosClient?)store.GetType()
            .GetField("_client", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(store);

    private static bool Initialized(object store) =>
        (bool)store.GetType()
            .GetField("_initialized", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(store)!;

    private static bool OwnsClient(object store) =>
        (bool)store.GetType()
            .GetField("_ownsClient", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(store)!;

    /// <summary>
    /// LIVENESS: every store in the package resolves onto the one registered client.
    /// </summary>
    [Fact]
    public void EveryStore_ResolvesOntoTheRegisteredClient()
    {
        var (provider, shared) = BuildHost();
        using var _ = provider;

        object[] stores =
        [
            provider.GetRequiredService<CosmosDbGrantStore>(),
            provider.GetRequiredService<CosmosDbActivityGroupGrantStore>(),
            provider.GetRequiredService<CosmosDbSnapshotStore>(),
            provider.GetRequiredService<CosmosDbProjectionStore<TestProjection>>(),
            provider.GetRequiredService<CosmosDbPersistenceProvider>(),
        ];

        // Assert the population before its members: four stores that all resolved is the claim, and an
        // assertion over a collection that shrank to nothing would pass without checking a single one.
        stores.Length.ShouldBe(5);

        foreach (var store in stores)
        {
            ClientOf(store).ShouldBeSameAs(
                shared,
                $"{store.GetType().Name} built its own CosmosClient while the host had already registered "
                + "one, so a host enabling several Cosmos features opens a connection pool per feature.");
        }
    }

    /// <summary>
    /// SAFETY: a borrowed client is not disposed, so one feature's shutdown does not take the account
    /// away from the others.
    /// </summary>
    [Fact]
    public void DisposingOneStore_LeavesTheSharedClientUsableByTheOthers()
    {
        var (provider, shared) = BuildHost();
        using var host = provider;

        var grants = provider.GetRequiredService<CosmosDbGrantStore>();
        var snapshots = provider.GetRequiredService<CosmosDbSnapshotStore>();

        OwnsClient(grants).ShouldBeFalse("a store handed the host's client must not claim ownership of it.");

        grants.Dispose();

        // The client the other store holds must still be alive. Reading Endpoint on a disposed CosmosClient
        // throws ObjectDisposedException, which is the exact symptom a wrongly-owned client produces.
        Should.NotThrow(() => ClientOf(snapshots)!.Endpoint);
        shared.Endpoint.ShouldNotBeNull();
    }

    /// <summary>
    /// SAFETY: initialization does not turn a borrowed client into an owned one.
    /// </summary>
    /// <remarks>
    /// The arm above resolves the stores and checks ownership straight away, which is a state the
    /// snapshot store reaches before it has decided anything. Ownership is decided inside
    /// InitializeAsync, and the snapshot store used to set the flag twice -- once correctly, inside the
    /// branch that builds a client, and once unconditionally just below it. A store handed the host's
    /// client therefore claimed it, and disposing that one store closed the account for every other
    /// Cosmos feature sharing it. Nothing in the resolution-time arm can see that: the flag is still
    /// false until the store is initialized.
    /// </remarks>
    [Fact]
    public async Task InitializingABorrowingStore_LeavesTheSharedClientOwnedByTheHost()
    {
        var (provider, shared) = BuildHost();
        using var host = provider;

        var snapshots = provider.GetRequiredService<CosmosDbSnapshotStore>();
        var projections = provider.GetRequiredService<CosmosDbProjectionStore<TestProjection>>();

        // LIVENESS: the arm is worthless if initialization did not actually run -- a store that threw or
        // returned early would leave the flag false for a reason that has nothing to do with the fix.
        await snapshots.InitializeAsync(TestContext.Current.CancellationToken);
        Initialized(snapshots).ShouldBeTrue("the ownership decision is made inside InitializeAsync.");

        ClientOf(snapshots).ShouldBeSameAs(shared);
        OwnsClient(snapshots).ShouldBeFalse(
            "an initialized store that was handed the host's client must not claim ownership of it.");

        await snapshots.DisposeAsync();

        // The client every other feature is still holding must be alive.
        Should.NotThrow(() => ClientOf(projections)!.Endpoint);
        shared.Endpoint.ShouldNotBeNull();
    }

    /// <summary>
    /// LIVENESS for the ownership flag itself: a store that had to build its own client still owns it.
    /// </summary>
    /// <remarks>
    /// Without this arm the safety arms above are all satisfied by a store that never claims ownership of
    /// anything -- which leaks a client per store instead of disposing one. Ownership must track which
    /// path ran, not be pinned to either answer.
    /// </remarks>
    [Fact]
    public async Task AStoreWithNoRegisteredClient_BuildsAndOwnsItsOwn()
    {
        var (provider, shared) = BuildHost(registerSharedClient: false);
        using var host = provider;
        using var unregistered = shared;

        var snapshots = provider.GetRequiredService<CosmosDbSnapshotStore>();
        await snapshots.InitializeAsync(TestContext.Current.CancellationToken);

        ClientOf(snapshots).ShouldNotBeNull();
        ClientOf(snapshots).ShouldNotBeSameAs(shared, "no client was registered for it to borrow.");
        OwnsClient(snapshots).ShouldBeTrue(
            "a store that built its own client is the only thing that will ever dispose it.");
    }

    private sealed class TestProjection
    {
        public string Id { get; set; } = string.Empty;
    }
}
