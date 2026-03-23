// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions.Outbox;

/// <summary>
/// Stages outbound messages for reliable delivery via the outbox pattern.
/// </summary>
/// <remarks>
/// <para>
/// Inject this interface into handlers to stage outbound messages. The consistency
/// guarantee (eventually-consistent vs. transactional) is determined by configuration,
/// not by which API you call.
/// </para>
/// <para>
/// <strong>Eventually-consistent mode:</strong> Messages are buffered during handler
/// execution and staged in the outbox <em>after</em> the handler and its transaction
/// complete. If the process crashes between transaction commit and outbox staging,
/// messages may be lost.
/// </para>
/// <para>
/// <strong>Transactional mode:</strong> Messages are written to the outbox store
/// within the ambient transaction, guaranteeing atomicity with the business state
/// change.
/// </para>
/// </remarks>
public interface IOutboxWriter
{
	/// <summary>
	/// Writes a message to the outbox for reliable delivery.
	/// </summary>
	/// <param name="message">The message to stage.</param>
	/// <param name="destination">Optional destination/topic for the message.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation.</param>
	/// <returns>A task representing the write operation.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="message"/> is null.</exception>
	/// <exception cref="InvalidOperationException">
	/// Thrown in transactional mode when no ambient transaction is available.
	/// </exception>
	ValueTask WriteAsync(
		IDispatchMessage message,
		string? destination,
		CancellationToken cancellationToken);
}
