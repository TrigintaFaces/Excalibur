// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Saga.Faults;

/// <summary>
/// Default implementation of <see cref="ISagaFaultEvent"/>.
/// </summary>
/// <remarks>
/// This record provides immutable fault event data created by <see cref="SagaFaultGenerator"/>.
/// </remarks>
public sealed record SagaFaultEvent : ISagaFaultEvent
{
	/// <inheritdoc />
	public string FaultReason { get; init; } = string.Empty;

	/// <inheritdoc />
	public string FailedStepName { get; init; } = string.Empty;

	/// <inheritdoc />
	public string SagaId { get; init; } = string.Empty;

	/// <inheritdoc />
	public string EventId { get; init; } = Guid.NewGuid().ToString("N");

	/// <inheritdoc />
	public string AggregateId { get; init; } = string.Empty;

	/// <inheritdoc />
	public long Version { get; init; }

	/// <inheritdoc />
	public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

	/// <inheritdoc />
	public string EventType { get; init; } = nameof(SagaFaultEvent);

	/// <inheritdoc />
	public IDictionary<string, object>? Metadata { get; init; }
}
