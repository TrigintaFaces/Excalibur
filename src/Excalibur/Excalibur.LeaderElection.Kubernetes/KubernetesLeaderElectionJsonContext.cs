// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Text.Json.Serialization;

using Excalibur.Dispatch.LeaderElection;

namespace Excalibur.LeaderElection.Kubernetes;

/// <summary>
/// Source-generated serialization context for Kubernetes leader election payloads.
/// </summary>
[JsonSerializable(typeof(CandidateHealth))]
internal sealed partial class KubernetesLeaderElectionJsonContext : JsonSerializerContext
{
	// All configuration is provided via attributes.
}
