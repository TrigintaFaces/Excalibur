// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Delivery;

/// <summary>Origin of the immutable admission policy returned by the authority.</summary>
public enum ScheduleAdmissionPolicySelection
{
    /// <summary>CapturedNew.</summary>
    CapturedNew = 0,
    /// <summary>UsedExisting.</summary>
    UsedExisting = 1,
}
