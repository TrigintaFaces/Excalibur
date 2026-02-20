// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Saga.Faults;

/// <summary>
/// Utility for creating <see cref="ISagaFaultEvent"/> instances when saga steps fail.
/// </summary>
/// <remarks>
/// <para>
/// Use this class to generate fault events that capture failure context including
/// the saga identifier, the failed step name, and the reason for failure.
/// Generated events can be dispatched through the event pipeline for monitoring
/// and alerting.
/// </para>
/// </remarks>
public static class SagaFaultGenerator
{
	/// <summary>
	/// Creates a fault event for a failed saga step.
	/// </summary>
	/// <param name="sagaId">The identifier of the saga instance that faulted.</param>
	/// <param name="failedStepName">The name of the step that failed.</param>
	/// <param name="faultReason">A human-readable description of the failure.</param>
	/// <returns>A new <see cref="ISagaFaultEvent"/> capturing the fault details.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="sagaId"/>, <paramref name="failedStepName"/>,
	/// or <paramref name="faultReason"/> is null or empty.
	/// </exception>
	public static ISagaFaultEvent CreateFaultEvent(
		string sagaId,
		string failedStepName,
		string faultReason)
	{
		ArgumentException.ThrowIfNullOrEmpty(sagaId);
		ArgumentException.ThrowIfNullOrEmpty(failedStepName);
		ArgumentException.ThrowIfNullOrEmpty(faultReason);

		return new SagaFaultEvent
		{
			SagaId = sagaId,
			AggregateId = sagaId,
			FailedStepName = failedStepName,
			FaultReason = faultReason,
			OccurredAt = DateTimeOffset.UtcNow,
		};
	}

	/// <summary>
	/// Creates a fault event for a failed saga step from an exception.
	/// </summary>
	/// <param name="sagaId">The identifier of the saga instance that faulted.</param>
	/// <param name="failedStepName">The name of the step that failed.</param>
	/// <param name="exception">The exception that caused the failure.</param>
	/// <returns>A new <see cref="ISagaFaultEvent"/> capturing the fault details.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="sagaId"/> or <paramref name="failedStepName"/> is null or empty.
	/// </exception>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="exception"/> is null.
	/// </exception>
	public static ISagaFaultEvent CreateFaultEvent(
		string sagaId,
		string failedStepName,
		Exception exception)
	{
		ArgumentException.ThrowIfNullOrEmpty(sagaId);
		ArgumentException.ThrowIfNullOrEmpty(failedStepName);
		ArgumentNullException.ThrowIfNull(exception);

		return new SagaFaultEvent
		{
			SagaId = sagaId,
			AggregateId = sagaId,
			FailedStepName = failedStepName,
			FaultReason = exception.Message,
			OccurredAt = DateTimeOffset.UtcNow,
			Metadata = new Dictionary<string, object>(StringComparer.Ordinal)
			{
				["ExceptionType"] = exception.GetType().FullName ?? exception.GetType().Name,
				["StackTrace"] = exception.StackTrace ?? string.Empty,
			},
		};
	}
}
