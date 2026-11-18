// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using Microsoft.Extensions.Options;

namespace Excalibur.LeaderElection.Kubernetes;

// Construction-only immutable configuration and deployment namespace discovery.
internal static class KubernetesElectionConfiguration
{
    internal static string DetermineNamespace(string? configured, Action<Exception, string> failure)
    {
        if (!string.IsNullOrEmpty(configured)) { return configured; }
        const string path = "/var/run/secrets/kubernetes.io/serviceaccount/namespace";
        if (!File.Exists(path)) { return "default"; }
        try { return File.ReadAllText(path).Trim(); }
        catch (Exception error) { failure(error, path); return "default"; }
    }
    internal static KubernetesLeaderElectionOptions Snapshot(KubernetesLeaderElectionOptions source)
    {
        var copy = new KubernetesLeaderElectionOptions
        {
            LeaseDuration = source.LeaseDuration, RenewInterval = source.RenewInterval, RetryInterval = source.RetryInterval,
            GracePeriod = source.GracePeriod, InstanceId = source.InstanceId, EnableHealthChecks = source.EnableHealthChecks,
            MinimumHealthScore = source.MinimumHealthScore, StepDownWhenUnhealthy = source.StepDownWhenUnhealthy,
            Namespace = source.Namespace, LeaseName = source.LeaseName, MaxRetries = source.MaxRetries, MaxRetryDelay = source.MaxRetryDelay
        };
        foreach (var item in source.CandidateMetadata) { copy.CandidateMetadata.Add(item.Key, item.Value); }
        var validation = new KubernetesLeaderElectionOptionsValidator().Validate(null, copy);
        if (validation.Failed) { throw new OptionsValidationException(nameof(KubernetesLeaderElectionOptions), typeof(KubernetesLeaderElectionOptions), validation.Failures); }
        try { _ = KubernetesLeaseProtocol.ToNativeSeconds(copy.LeaseDuration); }
        catch (ArgumentOutOfRangeException)
        { throw new OptionsValidationException(nameof(KubernetesLeaderElectionOptions), typeof(KubernetesLeaderElectionOptions), ["LeaseDuration exceeds native Int32 seconds."]); }
        return copy;
    }
}
