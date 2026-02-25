// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Saga.EventSourced;

/// <summary>
/// Records the failure of a saga step.
/// </summary>
public sealed class SagaStepFailed : ISagaEvent
{
	/// <inheritdoc />
	public string SagaId { get; init; } = string.Empty;

	/// <inheritdoc />
	public string EventType => "SagaStepFailed";

	/// <inheritdoc />
	public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets the name of the failed step.
	/// </summary>
	/// <value>The step name.</value>
	public string StepName { get; init; } = string.Empty;

	/// <summary>
	/// Gets the index of the failed step.
	/// </summary>
	/// <value>The step index (0-based).</value>
	public int StepIndex { get; init; }

	/// <summary>
	/// Gets the error message describing the failure.
	/// </summary>
	/// <value>The error message.</value>
	public string ErrorMessage { get; init; } = string.Empty;

	/// <summary>
	/// Gets the number of retry attempts made before this failure.
	/// </summary>
	/// <value>The retry count.</value>
	public int RetryCount { get; init; }
}
