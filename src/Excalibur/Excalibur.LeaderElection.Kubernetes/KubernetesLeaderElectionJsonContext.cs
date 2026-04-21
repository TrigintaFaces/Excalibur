// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Text.Json.Serialization;

using Excalibur.Dispatch.LeaderElection;

namespace Excalibur.LeaderElection.Kubernetes;

/// <summary>
/// DTO for health data stored in Kubernetes lease annotations.
/// </summary>
internal sealed class KubernetesHealthAnnotation
{
	[JsonPropertyName("candidateId")]
	public string CandidateId { get; set; } = string.Empty;

	[JsonPropertyName("isHealthy")]
	public bool IsHealthy { get; set; }

	[JsonPropertyName("healthScore")]
	public double HealthScore { get; set; }

	[JsonPropertyName("lastUpdated")]
	public DateTimeOffset LastUpdated { get; set; }

	[JsonPropertyName("metadata")]
	public IDictionary<string, string>? Metadata { get; set; }
}

/// <summary>
/// Source-generated serialization context for Kubernetes leader election payloads.
/// </summary>
[JsonSerializable(typeof(CandidateHealth))]
[JsonSerializable(typeof(KubernetesHealthAnnotation))]
internal sealed partial class KubernetesLeaderElectionJsonContext : JsonSerializerContext
{
	// All configuration is provided via attributes.
}
