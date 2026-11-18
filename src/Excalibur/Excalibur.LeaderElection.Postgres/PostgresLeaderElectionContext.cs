// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using Excalibur.Dispatch;
namespace Excalibur.LeaderElection.Postgres;

/// <summary>Optional cross-cutting collaborators for a Postgres election.</summary>
public sealed class PostgresLeaderElectionContext
{
    /// <summary>Gets the optional shared classifier. Permanent failures accelerate local retirement;
    /// other failures do not advance the deadline. A null classifier keeps exceptions ambiguous.</summary>
    public IMessageFailureClassifier? FailureClassifier { get; init; }
}
