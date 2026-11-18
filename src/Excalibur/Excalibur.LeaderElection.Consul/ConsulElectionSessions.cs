// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Consul;
using Polly;

namespace Excalibur.LeaderElection.Consul;

/// <summary>Typed Consul session requests and effective-TTL validation.</summary>
/// <remarks>Calls run within the caller's registered operation latch; this helper owns no work or client.</remarks>
internal static class ConsulElectionSessions
{
    internal static async Task<string> CreateAsync(IConsulClient client, ConsulLeaderElectionOptions options,
        ResiliencePipeline retryPolicy, string resourceName, string candidateId, string nonce, CancellationToken token)
    {
        var request = new SessionEntry
        {
            Name = $"leader-election-{resourceName}-{candidateId}-{nonce}",
            Behavior = SessionBehavior.Delete, TTL = options.SessionTTL, LockDelay = options.LockDelay
        };
        if (options.EnableHealthChecks && !string.IsNullOrEmpty(options.HealthCheckId)) { request.Checks = [options.HealthCheckId, "serfHealth"]; }
        var created = await retryPolicy.ExecuteAsync(async ct =>
            await client.Session.Create(request, WriteOptions(options), ct).ConfigureAwait(false), token).ConfigureAwait(false);
        if (string.IsNullOrEmpty(created.Response)) { throw new InvalidOperationException("Consul returned an empty session identifier."); }
        return created.Response;
    }

    internal static async Task<bool> RenewAsync(IConsulClient client, ConsulLeaderElectionOptions options,
        string sessionId, CancellationToken token)
    {
        var response = await client.Session.Renew(sessionId, WriteOptions(options), token).ConfigureAwait(false);
        var session = response.Response;
        if (session is null || !string.Equals(session.ID, sessionId, StringComparison.Ordinal)) { return false; }
        if (session.TTL is not { } ttl) { throw new InvalidOperationException("Consul renewal omitted the session TTL; local timing headroom could not be verified."); }
        var failure = ConsulLeaderElectionOptionsValidator.ValidateEffectiveTtl(options, ttl);
        if (failure is not null) { throw new InvalidOperationException(failure); }
        return true;
    }

    internal static async Task DestroyAsync(IConsulClient client, ConsulLeaderElectionOptions options,
        string sessionId, CancellationToken token)
    {
        _ = await client.Session.Destroy(sessionId, WriteOptions(options), token).ConfigureAwait(false);
    }

    private static WriteOptions WriteOptions(ConsulLeaderElectionOptions options) =>
        new() { Datacenter = options.Datacenter, Token = options.Token };
}
