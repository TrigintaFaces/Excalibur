// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Delivery;

/// <summary>An admitted bearer and the provider-captured automatic budget. Policy fields are observations, not bearer authority.</summary>
/// <param name="Lease">Lease.</param>
/// <param name="CapturedMaxAdmissionAttempts">CapturedMaxAdmissionAttempts.</param>
/// <param name="PolicySelection">PolicySelection.</param>
/// <param name="PolicyDispositionAllowed">Provider observation: first grant with no prior failure or recovered-occurrence lineage; provider rechecks at completion.</param>
public sealed record ScheduleFailureExecutionAdmission(ScheduleExecutionLease Lease, int CapturedMaxAdmissionAttempts, ScheduleAdmissionPolicySelection PolicySelection, bool PolicyDispositionAllowed);
