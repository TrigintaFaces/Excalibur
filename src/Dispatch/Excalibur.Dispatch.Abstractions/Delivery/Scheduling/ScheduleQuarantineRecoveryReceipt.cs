// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Delivery;

/// <summary>Immutable durable result of an explicit recovery command.</summary>
/// <param name="RecoveryCommandId">RecoveryCommandId.</param>
/// <param name="OriginalQuarantine">OriginalQuarantine.</param>
/// <param name="FreshGeneration">FreshGeneration.</param>
/// <param name="OccurrenceUtc">OccurrenceUtc.</param>
/// <param name="MaxAdmissionAttempts">MaxAdmissionAttempts.</param>
/// <param name="RecordedUtc">RecordedUtc.</param>
public sealed record ScheduleQuarantineRecoveryReceipt(Guid RecoveryCommandId, ScheduleQuarantineToken OriginalQuarantine, Guid FreshGeneration, DateTimeOffset OccurrenceUtc, int MaxAdmissionAttempts, DateTimeOffset RecordedUtc);
