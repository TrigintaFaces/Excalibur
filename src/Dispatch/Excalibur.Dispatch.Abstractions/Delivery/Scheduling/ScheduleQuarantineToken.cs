// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Delivery;

/// <summary>Expected state for one retained quarantine; not an execution lease.</summary>
/// <param name="StoreId">StoreId.</param>
/// <param name="QuarantineId">QuarantineId.</param>
/// <param name="Version">Version.</param>
/// <param name="ScheduleId">ScheduleId.</param>
/// <param name="Generation">Generation.</param>
/// <param name="OccurrenceUtc">OccurrenceUtc.</param>
public sealed record ScheduleQuarantineToken(Guid StoreId, Guid QuarantineId, long Version, Guid ScheduleId, Guid Generation, DateTimeOffset OccurrenceUtc);
