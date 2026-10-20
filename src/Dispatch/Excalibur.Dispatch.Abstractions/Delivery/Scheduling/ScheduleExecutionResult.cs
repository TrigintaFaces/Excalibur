// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Delivery;

/// <summary>Bounded progress changes allowed for an admitted execution.</summary>
/// <param name="LastExecutionUtc">Last execution timestamp.</param>
/// <param name="NextExecutionUtc">Next occurrence, or null when none remains.</param>
/// <param name="Disable">Whether to disable the schedule; false never reactivates it.</param>
public sealed record ScheduleExecutionResult(DateTimeOffset? LastExecutionUtc, DateTimeOffset? NextExecutionUtc, bool Disable = false);
