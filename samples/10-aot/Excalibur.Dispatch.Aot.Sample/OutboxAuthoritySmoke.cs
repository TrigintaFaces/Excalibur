// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Excalibur.Dispatch.Aot.Sample;

/// <summary>Exercises shared outbox authority using real framework storage in a native executable.</summary>
internal static class OutboxAuthoritySmoke
{
    public static async Task RunAsync()
    {
        Require(!RuntimeFeature.IsDynamicCodeSupported, "run the published native executable");
        Require(!JsonSerializer.IsReflectionEnabledByDefault, "reflection-based JSON must remain disabled");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        // Keep this domain alive while the independent-domain control runs.
        await using var shared = await OutboxAuthorityComposition.CreateAsync().ConfigureAwait(false);
        await OutboxAuthorityScenarios.SharedScopesAndNewerTenureAsync(shared, timeout.Token).ConfigureAwait(false);
        await OutboxAuthorityScenarios.IndependentDomainAsync(timeout.Token).ConfigureAwait(false);
        await OutboxAuthorityScenarios.CapturedAuthorityAsync(failTransport: false, timeout.Token).ConfigureAwait(false);
        await OutboxAuthorityScenarios.CapturedAuthorityAsync(failTransport: true, timeout.Token).ConfigureAwait(false);
        GC.KeepAlive(shared);

        Console.WriteLine("Native outbox authority: shared scopes, captured completion/failure, newer tenure and independent domains verified with the framework InMemory store.");
    }

    internal static void Require(bool condition, string detail)
    {
        if (!condition) { throw new InvalidOperationException("Outbox authority smoke: " + detail); }
    }
}
