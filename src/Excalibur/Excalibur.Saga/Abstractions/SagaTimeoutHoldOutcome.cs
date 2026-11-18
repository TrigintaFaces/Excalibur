// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Saga.Abstractions;

/// <summary>Authoritative result of one conditional hold operation, never a delivery acknowledgment.</summary>
public enum SagaTimeoutHoldOutcome
{
    /// <summary>The provider certifies a current committed hold and supplies its real receipt.</summary>
    Held = 0,
    /// <summary>The same original source/cycle is current, but a newly current claim is required.</summary>
    RetryWithFreshClaim = 1,
    /// <summary>The same bound target proves the original source/cycle can no longer dispatch.</summary>
    ObservationEnded = 2,
    /// <summary>Target/tenant/inputs were rejected; no source-absence conclusion is authorized.</summary>
    Rejected = 3,
}
