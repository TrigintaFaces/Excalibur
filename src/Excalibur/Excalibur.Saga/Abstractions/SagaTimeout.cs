// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Saga.Abstractions;

/// <summary>
/// Represents a scheduled timeout for a saga instance. Timeouts are used to trigger
/// actions after a specified delay when a saga is waiting for external events.
/// </summary>
/// <param name="TimeoutId">Unique identifier for this timeout instance.</param>
/// <param name="SagaId">Identifier of the saga that requested this timeout.</param>
/// <param name="SagaType">Assembly-qualified type name of the saga for routing.</param>
/// <param name="TimeoutType">Assembly-qualified type name of the timeout message type.</param>
/// <param name="TimeoutData">Serialized timeout data (MemoryPack format), or null for parameterless timeouts.</param>
/// <param name="DueAt">UTC timestamp when this timeout should be delivered.</param>
/// <param name="ScheduledAt">UTC timestamp when this timeout was originally scheduled.</param>
public sealed record SagaTimeout(
	string TimeoutId,
	string SagaId,
	string SagaType,
	string TimeoutType,
	byte[]? TimeoutData,
	DateTimeOffset DueAt,
	DateTimeOffset ScheduledAt);
