// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Messaging;

/// <summary>Reports a durable transition result without treating a missing acknowledgement as rollback.</summary>
/// <param name="Outcome">The authoritative outcome, or Unknown.</param>
/// <param name="CommittedVersion">The committed version only when positively established.</param>
public sealed record SagaTransitionCommitResult(SagaTransitionCommitOutcome Outcome, long? CommittedVersion)
{
    /// <summary>Gets the original operation failure when an uncertain commit could not be resolved.</summary>
    /// <remarks>This diagnostic is never persisted as part of the transition receipt or treated as a successful commit.</remarks>
    public Exception? Failure { get; init; }
}
