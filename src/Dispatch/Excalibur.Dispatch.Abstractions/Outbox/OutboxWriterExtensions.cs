// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Dispatch.Outbox;

/// <summary>
/// Extension methods for <see cref="IOutboxWriter"/>.
/// </summary>
public static class OutboxWriterExtensions
{
	/// <summary>
	/// Writes a message to the outbox for scheduled delivery.
	/// </summary>
	/// <param name="writer">The outbox writer.</param>
	/// <param name="message">The message to stage.</param>
	/// <param name="destination">Optional destination/topic for the message.</param>
	/// <param name="scheduledAt">The time at which the message should be delivered.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation.</param>
	/// <returns>A task representing the write operation.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="writer"/> or <paramref name="message"/> is null.</exception>
	[RequiresUnreferencedCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public static ValueTask WriteScheduledAsync(
		this IOutboxWriter writer,
		IDispatchMessage message,
		string? destination,
		DateTimeOffset scheduledAt,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(writer);
		ArgumentNullException.ThrowIfNull(message);

		// The framework's own deferred writer takes the time as a parameter. Anything else gets the plain
		// write -- which is what it got before too, since the ambient this replaced was internal and no
		// other writer read it.
		return writer is IScheduledOutboxWriter scheduled
			? scheduled.WriteScheduledAsync(message, destination, scheduledAt, cancellationToken)
			: writer.WriteAsync(message, destination, cancellationToken);
	}
}
