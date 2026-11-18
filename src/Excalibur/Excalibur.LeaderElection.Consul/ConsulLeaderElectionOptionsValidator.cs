// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using Microsoft.Extensions.Options;

namespace Excalibur.LeaderElection.Consul;

/// <summary>Validates Consul's actual session TTL and the local renewal configuration.</summary>
/// <remarks>Headroom is configuration validation, not an upper bound on server invalidation or process pauses.</remarks>
public sealed class ConsulLeaderElectionOptionsValidator : IValidateOptions<ConsulLeaderElectionOptions>
{
    private static readonly TimeSpan ClockMargin = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MinimumSessionTtl = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan MaximumSessionTtl = TimeSpan.FromDays(1);

    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, ConsulLeaderElectionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();
        if (options.LeaseDuration <= TimeSpan.Zero) { failures.Add("LeaseDuration must be positive; Consul uses SessionTTL for its actual server timing."); }
        if (options.RetryInterval <= TimeSpan.Zero) { failures.Add("RetryInterval must be positive."); }
        if (options.MaxRetryAttempts < 0) { failures.Add("MaxRetryAttempts must not be negative."); }
        if (!double.IsFinite(options.MinimumHealthScore) || options.MinimumHealthScore < 0 || options.MinimumHealthScore > 1) { failures.Add("MinimumHealthScore must be between zero and one."); }
        if (options.LockDelay < TimeSpan.Zero || options.LockDelay > TimeSpan.FromMinutes(1)) { failures.Add("LockDelay must be between zero and sixty seconds."); }
        // Retain the existing framework policy; this relation is not asserted as a Consul server requirement.
        if (options.LockDelay >= options.SessionTTL) { failures.Add("LockDelay must be less than SessionTTL."); }
        var timing = ValidateEffectiveTtl(options, options.SessionTTL);
        if (timing is not null) { failures.Add(timing); }
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    internal static string? ValidateEffectiveTtl(ConsulLeaderElectionOptions options, TimeSpan ttl)
    {
        if (ttl < MinimumSessionTtl || ttl > MaximumSessionTtl) { return "SessionTTL must be between ten seconds and twenty-four hours."; }
        if (options.RenewInterval <= TimeSpan.Zero || options.GracePeriod <= TimeSpan.Zero) { return "RenewInterval and GracePeriod must be positive."; }
        if (options.RenewInterval >= ttl) { return "RenewInterval must be less than the actual SessionTTL."; }
        var remaining = ttl - options.RenewInterval;
        if (options.GracePeriod >= remaining || ClockMargin >= remaining - options.GracePeriod)
        {
            return "RenewInterval + GracePeriod + one second of reserved headroom must be strictly less than the actual SessionTTL.";
        }
        return null;
    }
}
