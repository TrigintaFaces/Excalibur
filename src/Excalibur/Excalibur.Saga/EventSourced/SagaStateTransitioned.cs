// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Models;

namespace Excalibur.Saga.EventSourced;

/// <summary>
/// Records a saga state transition event.
/// </summary>
/// <remarks>
/// Captured when a saga transitions between states (e.g., Running to Completed,
/// Running to Compensating).
/// </remarks>
public sealed class SagaStateTransitioned : ISagaEvent
{
	/// <inheritdoc />
	public string SagaId { get; init; } = string.Empty;

	/// <inheritdoc />
	public string EventType => "SagaStateTransitioned";

	/// <inheritdoc />
	public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets the status the saga transitioned from.
	/// </summary>
	/// <value>The previous saga status.</value>
	public SagaStatus FromStatus { get; init; }

	/// <summary>
	/// Gets the status the saga transitioned to.
	/// </summary>
	/// <value>The new saga status.</value>
	public SagaStatus ToStatus { get; init; }

	/// <summary>
	/// Gets an optional reason for the transition.
	/// </summary>
	/// <value>The transition reason, or <see langword="null"/> if not specified.</value>
	public string? Reason { get; init; }
}
