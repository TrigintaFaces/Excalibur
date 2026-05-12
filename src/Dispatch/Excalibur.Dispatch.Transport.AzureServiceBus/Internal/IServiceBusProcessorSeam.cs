// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Azure.Messaging.ServiceBus;

namespace Excalibur.Dispatch.Transport.AzureServiceBus.Internal;

/// <summary>
/// Narrow internal seam over <see cref="ServiceBusProcessor"/> used by
/// <see cref="ServiceBusTransportSubscriber"/>. Exposes <b>use-case</b>
/// operations so tests can substitute the SDK without depending on which
/// <see cref="ServiceBusProcessor"/> overloads remain virtual in a given SDK
/// minor version. Not a consumer-facing abstraction; do not make this public.
/// </summary>
/// <remarks>
/// <para>
/// Follows the ADR-142 §D7 canonical template. The event handlers
/// (<see cref="ProcessMessageAsync"/> and <see cref="ProcessErrorAsync"/>)
/// are modeled as C# events matching the SDK processor's shape — the seam
/// preserves the push-based subscription contract.
/// </para>
/// <para>
/// Data-shaped SDK types (<see cref="ProcessMessageEventArgs"/>,
/// <see cref="ProcessErrorEventArgs"/>) cross the seam — they carry
/// Args-suffix DTO semantics and are safe per the COMPASS msg 1743
/// refined rubric.
/// </para>
/// </remarks>
internal interface IServiceBusProcessorSeam : IAsyncDisposable
{
	/// <summary>
	/// Event raised when a message is received for processing.
	/// </summary>
	event Func<ProcessMessageEventArgs, Task> ProcessMessageAsync;

	/// <summary>
	/// Event raised when an error occurs during message processing.
	/// </summary>
	event Func<ProcessErrorEventArgs, Task> ProcessErrorAsync;

	/// <summary>
	/// Starts processing messages. Wraps
	/// <see cref="ServiceBusProcessor.StartProcessingAsync(CancellationToken)"/>.
	/// </summary>
	Task StartProcessingAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Stops processing messages. Wraps
	/// <see cref="ServiceBusProcessor.StopProcessingAsync(CancellationToken)"/>.
	/// </summary>
	Task StopProcessingAsync(CancellationToken cancellationToken);
}
