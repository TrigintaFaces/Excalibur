// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using Excalibur.EventSourcing.Subscriptions;
namespace Excalibur.EventSourcing.Tests;
// Test setup for the pre-existing position-CAS arms. Production callers must retain the
// binding they observed before processing; they must never resolve a fresh token to acknowledge old work.
internal static class SubscriptionCheckpointTestSetup
{
    internal static IServiceProvider CreateRebuildProvider<T>() where T : class
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISubscriptionCheckpointStore>(new InMemorySubscriptionCheckpointStore());
        services.AddSingleton<Microsoft.Extensions.Options.IOptions<Excalibur.EventSourcing.Projections.GlobalStreamProjectionOptions>>(
            Microsoft.Extensions.Options.Options.Create(new Excalibur.EventSourcing.Projections.GlobalStreamProjectionOptions()));
        var provider = services.BuildServiceProvider();
        var fake = A.Fake<IServiceProvider>();
        A.CallTo(() => fake.GetService(A<Type>._)).ReturnsLazily((Type type) => provider.GetService(type));
        var factory = A.Fake<IServiceScopeFactory>();
        var scope = A.Fake<IServiceScope>();
        A.CallTo(() => scope.ServiceProvider).Returns(fake);
        A.CallTo(() => factory.CreateScope()).Returns(scope);
        A.CallTo(() => fake.GetService(typeof(IServiceScopeFactory))).Returns(factory);
        A.CallTo(() => fake.GetService(typeof(IProjectionStore<T>))).Returns(new Excalibur.EventSourcing.Projections.InMemoryProjectionStore<T>());
        return fake;
    }

    internal static ISubscriptionCheckpointStore CreateFake()
    {
        var inner = new InMemorySubscriptionCheckpointStore();
        var fake = A.Fake<ISubscriptionCheckpointStore>();
        A.CallTo(() => fake.GetCheckpointAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .ReturnsLazily((string name, string key, CancellationToken ct) => inner.GetCheckpointAsync(name, key, ct));
        A.CallTo(() => fake.BindSubscriptionAsync(A<string>._, A<IReadOnlyList<SubscriptionMember>>._, A<CancellationToken>._))
            .ReturnsLazily(async (string name, IReadOnlyList<SubscriptionMember> members, CancellationToken ct) =>
                await WithCursorsAsync(await inner.BindSubscriptionAsync(name, members, ct), fake, ct));
        A.CallTo(() => fake.ReadSubscriptionAsync(A<string>._, A<CancellationToken>._))
            .ReturnsLazily(async (string name, CancellationToken ct) =>
                await WithCursorsAsync(await inner.ReadSubscriptionAsync(name, ct), fake, ct));
        A.CallTo(() => fake.AdvanceCheckpointAsync(A<string>._, A<string>._, A<Guid>._, A<long?>._, A<long>._, A<CancellationToken>._))
            .ReturnsLazily((string name, string key, Guid binding, long? expected, long next, CancellationToken ct) =>
                inner.AdvanceCheckpointAsync(name, key, binding, expected, next, ct));
        A.CallTo(() => fake.ReleaseSubscriptionMemberAsync(A<string>._, A<string>._, A<Guid>._, A<CancellationToken>._))
            .ReturnsLazily((string name, string key, Guid binding, CancellationToken ct) =>
                inner.ReleaseSubscriptionMemberAsync(name, key, binding, ct));
        return fake;
    }

    private static async Task<SubscriptionRoster> WithCursorsAsync(SubscriptionRoster roster,
        ISubscriptionCheckpointStore fake, CancellationToken cancellationToken)
    {
        var members = new List<SubscriptionMember>();
        foreach (var member in roster.Members)
        {
            members.Add(member with { Position = await fake.GetCheckpointAsync(roster.SubscriptionName, member.ProjectionKey, cancellationToken) });
        }
        return roster with { Members = members };
    }

    internal static async Task<CheckpointAdvanceOutcome> AdvanceBoundCheckpointAsync(
        this ISubscriptionCheckpointStore store, string subscriptionName, string projectionKey,
        long? expectedPosition, long newPosition, CancellationToken cancellationToken)
    {
        var roster = await store.ReadSubscriptionAsync(subscriptionName, cancellationToken).ConfigureAwait(false);
        var member = roster.Members.FirstOrDefault(m => m.ProjectionKey == projectionKey);
        if (member is null)
        {
            roster = await store.BindSubscriptionAsync(subscriptionName,
                [new SubscriptionMember(projectionKey, new string('A', 64), "dest-a")], cancellationToken).ConfigureAwait(false);
            member = roster.Members.Single(m => m.ProjectionKey == projectionKey);
        }
        return await store.AdvanceCheckpointAsync(subscriptionName, projectionKey, member.BindingId,
            expectedPosition, newPosition, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<bool> ReleaseBoundMemberAsync(this ISubscriptionCheckpointStore store,
        string subscriptionName, string projectionKey, CancellationToken cancellationToken)
    {
        var roster = await store.ReadSubscriptionAsync(subscriptionName, cancellationToken).ConfigureAwait(false);
        var member = roster.Members.FirstOrDefault(m => m.ProjectionKey == projectionKey);
        var bindingId = member?.BindingId ?? Guid.NewGuid();
        var released = await store.ReleaseSubscriptionMemberAsync(subscriptionName, projectionKey, bindingId, cancellationToken).ConfigureAwait(false);
        if (released)
        {
            var stale = await store.AdvanceCheckpointAsync(subscriptionName, projectionKey, bindingId, null, 999, cancellationToken).ConfigureAwait(false);
            if (stale != CheckpointAdvanceOutcome.Superseded)
            {
                throw new InvalidOperationException("A retired binding recreated an orphan checkpoint.");
            }
        }
        return released;
    }
}
