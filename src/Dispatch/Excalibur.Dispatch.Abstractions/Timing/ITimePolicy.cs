// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Defines time-based policies for message processing timeouts and scheduling. R7.4: Configurable timeout handling with proper error management.
/// </summary>
public interface ITimePolicy
{
	/// <summary>
	/// Gets the default timeout for message processing operations.
	/// </summary>
	/// <value> The default timeout for message processing operations. </value>
	TimeSpan DefaultTimeout { get; }

	/// <summary>
	/// Gets the maximum allowed timeout for any operation.
	/// </summary>
	/// <value> The maximum timeout allowed. </value>
	TimeSpan MaxTimeout { get; }

	/// <summary>
	/// Gets the timeout for handler execution.
	/// </summary>
	/// <value> The handler execution timeout. </value>
	TimeSpan HandlerTimeout { get; }

	/// <summary>
	/// Gets the timeout for serialization operations.
	/// </summary>
	/// <value> The serialization timeout. </value>
	TimeSpan SerializationTimeout { get; }

	/// <summary>
	/// Gets the timeout for transport operations.
	/// </summary>
	/// <value> The transport timeout. </value>
	TimeSpan TransportTimeout { get; }

	/// <summary>
	/// Gets the timeout for validation operations.
	/// </summary>
	/// <value> The validation timeout. </value>
	TimeSpan ValidationTimeout { get; }

	/// <summary>
	/// Determines the appropriate timeout for a specific operation type.
	/// </summary>
	/// <param name="operationType"> The type of operation being performed. </param>
	/// <returns> The timeout duration for the operation. </returns>
	TimeSpan GetTimeoutFor(TimeoutOperationType operationType);

	/// <summary>
	/// Determines if a timeout should be applied based on the operation context.
	/// </summary>
	/// <param name="operationType"> The type of operation being performed. </param>
	/// <param name="context"> Additional context for the decision. </param>
	/// <returns> <see langword="true" /> if a timeout should be applied; otherwise, <see langword="false" />. </returns>
	bool ShouldApplyTimeout(TimeoutOperationType operationType, TimeoutContext? context = null);

	/// <summary>
	/// Creates a cancellation token with the appropriate timeout for an operation.
	/// </summary>
	/// <param name="operationType"> The type of operation being performed. </param>
	/// <param name="parentToken"> Optional parent cancellation token to link with. </param>
	/// <returns> A cancellation token with the configured timeout. </returns>
	CancellationToken CreateTimeoutToken(TimeoutOperationType operationType, CancellationToken parentToken);
}
