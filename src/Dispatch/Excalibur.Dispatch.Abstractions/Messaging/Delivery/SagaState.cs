// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Messaging;

/// <summary>
/// Base class for saga state management, providing fundamental properties for workflow persistence and tracking. This abstract class serves
/// as the foundation for all saga state implementations, ensuring consistent identity management and completion tracking across different
/// workflow types.
/// </summary>
public abstract class SagaState
{
	/// <summary>
	/// Gets or sets the unique identifier for this saga instance. This identifier is used for saga correlation, state persistence, and
	/// event routing throughout the workflow lifecycle.
	/// </summary>
	/// <value>
	/// The unique identifier for this saga instance. This identifier is used for saga correlation, state persistence, and
	/// event routing throughout the workflow lifecycle.
	/// </value>
	public Guid SagaId { get; set; } = Guid.NewGuid();

	/// <summary>
	/// Gets or sets a value indicating whether this saga workflow has completed successfully. When set to true, the saga will not process
	/// further events and may be eligible for cleanup operations.
	/// </summary>
	/// <value>The current <see cref="Completed"/> value.</value>
	public bool Completed { get; set; }
}
