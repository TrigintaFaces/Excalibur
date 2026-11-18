// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Delivery;

/// <summary>Explicit reason for successful conditional occurrence retirement.</summary>
public enum ScheduleExecutionCompletionDisposition
{
    /// <summary>The caller observed actual successful dispatch.</summary>
    Fulfilled = 0,
    /// <summary>Never-executed first-grant work is omitted by its catchup policy.</summary>
    PolicySkipped = 1,
    /// <summary>Never-executed first-grant work is disabled by its catchup policy.</summary>
    PolicyDisabled = 2,
}
