// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json.Serialization;

using Excalibur.Dispatch.LeaderElection;

namespace Excalibur.LeaderElection.Consul;

/// <summary>
/// Source-generated serialization context for <see cref="ConsulLeaderElection" /> payloads.
/// </summary>
[JsonSerializable(typeof(ConsulLeaderElection.LeaderInfo))]
[JsonSerializable(typeof(CandidateHealth))]
internal sealed partial class ConsulLeaderElectionJsonContext : JsonSerializerContext
{
	// Intentionally left empty – all configuration is provided through attributes.
}
