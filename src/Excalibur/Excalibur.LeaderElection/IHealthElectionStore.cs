// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using Excalibur.Dispatch.LeaderElection;
namespace Excalibur.LeaderElection;
// Typed native-operation seam; public constructors keep the real SQL/PG implementations.
internal interface IHealthElectionStore
{
    Task CreateAsync(CancellationToken token);
    Task UpsertAsync(string candidateId, bool healthy, string metadataJson, bool leader, CancellationToken token);
    Task<IEnumerable<CandidateHealth>> ReadAsync(CancellationToken token);
    Task RemoveAsync(string candidateId, CancellationToken token);
}
