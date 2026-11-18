// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Delivery;

/// <summary>Bounded reason for an unsuccessful scheduled occurrence.</summary>
public enum ScheduleFailureReason
{
    /// <summary>HandlerFailed.</summary>
    HandlerFailed = 0,
    /// <summary>InvalidResult.</summary>
    InvalidResult = 1,
    /// <summary>UnknownType.</summary>
    UnknownType = 2,
    /// <summary>MalformedPayload.</summary>
    MalformedPayload = 3,
    /// <summary>UnsupportedType.</summary>
    UnsupportedType = 4,
    /// <summary>HandlerTimeout.</summary>
    HandlerTimeout = 5,
    /// <summary>AdmissionBudgetExhausted.</summary>
    AdmissionBudgetExhausted = 6,
    /// <summary>Explicit administration retained an interrupted or recovering occurrence without claiming handler failure.</summary>
    AdministrativeCancellation = 7,
}
