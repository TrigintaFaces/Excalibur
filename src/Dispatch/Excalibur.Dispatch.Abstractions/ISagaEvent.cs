// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Marker interface for events that are part of a saga orchestration.
/// </summary>
/// <remarks>
/// Events implementing this interface are typically used in saga pattern implementations to coordinate long-running business processes.
/// </remarks>
public interface ISagaEvent : IDispatchEvent
{
	/// <summary>
	/// Gets the saga identifier that this event is associated with.
	/// </summary>
	/// <value> The saga identifier. </value>
	string SagaId { get; }

	/// <summary>
	/// Gets the step identifier within the saga workflow.
	/// </summary>
	/// <value> The saga step identifier or <see langword="null" />. </value>
	string? StepId { get; }
}
