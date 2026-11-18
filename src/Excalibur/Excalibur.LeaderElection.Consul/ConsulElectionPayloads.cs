// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Security.Cryptography;
using System.Text.Json;
using Excalibur.Dispatch.LeaderElection;

namespace Excalibur.LeaderElection.Consul;

/// <summary>Source-generated encoding and tolerant decoding of Consul election payloads.</summary>
internal static class ConsulElectionPayloads
{
    // The private payload owner retains a fresh256-bit CSPRNG nonce for each acquisition attempt.
    internal static string CreateOwnerNonce() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    internal static byte[] EncodeLeader(ConsulLeaderElection.LeaderInfo information) =>
        JsonSerializer.SerializeToUtf8Bytes(information, ConsulLeaderElectionJsonContext.Default.LeaderInfo);

    internal static byte[] EncodeHealth(CandidateHealth health) =>
        JsonSerializer.SerializeToUtf8Bytes(health, ConsulLeaderElectionJsonContext.Default.CandidateHealth);

    internal static ConsulLeaderElection.LeaderInfo? ReadLeader(byte[] payload)
    {
        try { return JsonSerializer.Deserialize(payload, ConsulLeaderElectionJsonContext.Default.LeaderInfo); }
        catch (JsonException) { return null; }
    }

    internal static CandidateHealth? ReadHealth(byte[] payload, out Exception? error)
    {
        error = null;
        try { return JsonSerializer.Deserialize(payload, ConsulLeaderElectionJsonContext.Default.CandidateHealth); }
        catch (JsonException failure) { error = failure; return null; }
    }
}
