// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Persistence;
using Excalibur.Data.Redis;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.Redis.Tests.Persistence;

/// <summary>
/// Binds this provider to declining the transaction capability at discovery rather than at use.
/// </summary>
/// <remarks>
/// <para>
/// Redis has no client-side transaction; the atomicity it offers is server-side Lua. The provider no
/// longer implements <see cref="IPersistenceProviderTransaction"/> at all, so there are no throwing
/// members left to reach. It previously returned itself from <see cref="IPersistenceProvider.GetService"/>
/// for that capability, which answers "yes" to the only question a consumer can ask about it — so the
/// refusal arrived as an exception from a capability the provider had advertised.
/// </para>
/// <para>
/// Declining transactions no longer costs this provider its connection details. Those live on
/// <see cref="IPersistenceProviderConnection"/>, a separate capability Redis genuinely offers, so the
/// honest answer about transactions no longer withdraws two members it can answer for.
/// </para>
/// <para>
/// <b>Null is the documented way to decline.</b> <c>GetService</c> is specified as returning "the service
/// instance, or null if not supported". Declining at discovery lets a caller branch on the answer;
/// declining at use gives them an exception from something they were told they had.
/// </para>
/// <para>
/// <b>All three arms, deliberately.</b> Asserting only that transactions are declined would be satisfied
/// by a provider that declines everything, including the health and connection capabilities it genuinely
/// implements. The two liveness arms hold that line.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class RedisPersistenceProviderCapabilityShould
{
    private static RedisPersistenceProvider CreateProvider() =>
        new(
            Options.Create(new RedisProviderOptions { ConnectionString = "localhost:6379" }),
            NullLogger<RedisPersistenceProvider>.Instance);

    [Fact]
    public void DeclineTheTransactionCapabilityAtDiscovery()
    {
        using var provider = CreateProvider();

        var transaction = provider.GetService(typeof(IPersistenceProviderTransaction));

        transaction.ShouldBeNull(
            "Redis cannot honour a client-side transaction, so it must decline the capability where a "
            + "caller can act on the answer. Returning the provider here advertises support and defers "
            + "the refusal to a NotSupportedException at the point of use.");
    }

    [Fact]
    public void StillOfferTheHealthCapabilityItActuallyImplements()
    {
        using var provider = CreateProvider();

        var health = provider.GetService(typeof(IPersistenceProviderHealth));

        // The liveness half. Without it, a provider that declined everything would satisfy the arm above
        // while silently withdrawing a capability consumers rely on.
        _ = health.ShouldBeOfType<RedisPersistenceProvider>(
            "health is genuinely implemented and must still be offered; declining the transaction "
            + "capability is a statement about transactions, not a withdrawal of everything.");
    }

    [Fact]
    public void StillOfferTheConnectionCapabilityItActuallyImplements()
    {
        using var provider = CreateProvider();

        var connection = provider.GetService(typeof(IPersistenceProviderConnection));

        // The second liveness arm, and the one the capability split exists to make possible. When the
        // connection string and retry policy lived on the transaction contract, declining transactions
        // withdrew them too: a consumer holding IPersistenceProvider could not discover either, even
        // though Redis supplies both correctly. Being unable to run a transaction is not a reason to
        // stop answering how the store is reached.
        _ = connection.ShouldBeOfType<RedisPersistenceProvider>(
            "the connection capability is genuinely implemented and must be offered independently of "
            + "the transaction capability this provider declines.");
    }
}
