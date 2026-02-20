// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Saga.Faults;

/// <summary>
/// Represents a domain event generated when a saga step fails.
/// Extends <see cref="IDomainEvent"/> with fault-specific metadata.
/// </summary>
/// <remarks>
/// <para>
/// Saga fault events are raised by <see cref="SagaFaultGenerator"/> when a saga step
/// encounters an unrecoverable error. Consumers can subscribe to these events to
/// implement custom error handling, alerting, or compensation workflows.
/// </para>
/// <para>
/// The <see cref="SagaId"/> property identifies the saga instance that faulted,
/// while <see cref="FailedStepName"/> identifies the specific step that failed.
/// </para>
/// </remarks>
public interface ISagaFaultEvent : IDomainEvent
{
	/// <summary>
	/// Gets the reason for the fault.
	/// </summary>
	/// <value>A human-readable description of why the saga step failed.</value>
	string FaultReason { get; }

	/// <summary>
	/// Gets the name of the saga step that failed.
	/// </summary>
	/// <value>The name of the step that caused the fault.</value>
	string FailedStepName { get; }

	/// <summary>
	/// Gets the identifier of the saga instance that faulted.
	/// </summary>
	/// <value>The unique identifier of the faulted saga instance.</value>
	string SagaId { get; }
}
