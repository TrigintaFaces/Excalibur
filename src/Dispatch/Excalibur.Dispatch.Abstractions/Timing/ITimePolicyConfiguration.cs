// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Provides additional timeout configuration properties and methods.
/// Implementations that support these operations should implement this interface
/// alongside <see cref="ITimePolicy"/>.
/// </summary>
public interface ITimePolicyConfiguration
{
	/// <summary>
	/// Gets the timeout for serialization operations.
	/// </summary>
	/// <value>The serialization timeout duration.</value>
	TimeSpan SerializationTimeout { get; }

	/// <summary>
	/// Gets the timeout for transport operations.
	/// </summary>
	/// <value>The transport timeout duration.</value>
	TimeSpan TransportTimeout { get; }

	/// <summary>
	/// Gets the timeout for validation operations.
	/// </summary>
	/// <value>The validation timeout duration.</value>
	TimeSpan ValidationTimeout { get; }

	/// <summary>
	/// Determines if a timeout should be applied based on the operation context.
	/// </summary>
	/// <param name="operationType">The type of operation being timed.</param>
	/// <param name="context">Optional context providing additional information for the decision.</param>
	/// <returns><see langword="true"/> if a timeout should be applied; otherwise, <see langword="false"/>.</returns>
	bool ShouldApplyTimeout(TimeoutOperationType operationType, TimeoutContext? context = null);
}
