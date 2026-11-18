// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json;
using Excalibur.Dispatch.LeaderElection;
namespace Excalibur.LeaderElection.Kubernetes;
internal static class KubernetesLeaseHealthCodec
{
    internal static string Serialize(KubernetesHealthAnnotation value) => JsonSerializer.Serialize(value, KubernetesLeaderElectionJsonContext.Default.KubernetesHealthAnnotation);
    internal static KubernetesHealthAnnotation? Deserialize(string value) => JsonSerializer.Deserialize(value, KubernetesLeaderElectionJsonContext.Default.KubernetesHealthAnnotation);

    internal static string SerializeHealth(string candidateId, bool isHealthy, DateTimeOffset lastUpdated, IDictionary<string, string>? metadata) =>
        Serialize(new KubernetesHealthAnnotation
        {
            CandidateId = candidateId, IsHealthy = isHealthy, HealthScore = isHealthy ? 1 : 0,
            LastUpdated = lastUpdated, Metadata = metadata is null ? null : new Dictionary<string, string>(metadata, StringComparer.Ordinal)
        });

    internal static IReadOnlyList<CandidateHealth> ReadCandidates(k8s.Models.V1Lease lease, Action<Exception, string> reportParseFailure)
    {
        var result = new List<CandidateHealth>();
        if (lease.Metadata?.Annotations is null) { return result; }
        foreach (var item in lease.Metadata.Annotations)
        {
            if (!item.Key.StartsWith("leader-election.excalibur.io/health-", StringComparison.Ordinal)) { continue; }
            try
            {
                var health = KubernetesLeaseHealthCodec.Deserialize(item.Value);
                if (health is null) { continue; }
                result.Add(new CandidateHealth
                {
                    CandidateId = health.CandidateId, IsHealthy = health.IsHealthy, HealthScore = health.HealthScore,
                    LastUpdated = health.LastUpdated, IsLeader = lease.Spec?.HolderIdentity == health.CandidateId,
                    Metadata = health.Metadata is null ? new Dictionary<string, string>(StringComparer.Ordinal) : new Dictionary<string, string>(health.Metadata, StringComparer.Ordinal)
                });
            }
            catch (Exception error) { reportParseFailure(error, item.Key); }
        }
        return result;
    }

}
