// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Delivery;

/// <summary>Persisted disposition of an admitted failure.</summary>
public enum ScheduleFailureDisposition
{
    /// <summary>Retrying.</summary>
    Retrying = 0,
    /// <summary>Quarantined.</summary>
    Quarantined = 1,
}
