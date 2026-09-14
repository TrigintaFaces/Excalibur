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

		if (writer is IScheduledOutboxWriter scheduled)
		{
			return scheduled.WriteScheduledAsync(message, destination, scheduledAt, cancellationToken);
		}

		// REFUSE rather than fall back to an immediate write. Falling back looks harmless -- the message is
		// still delivered at least once -- but it delivers at the wrong TIME, which is the one thing the
		// caller used this overload to ask for, and it did so with no error and nothing logged. A consumer
		// scheduling a reminder, a retry, or a delayed compensation got it sent immediately and had no way
		// to find out. Refusing turns a silent wrong-time delivery into a startup-visible wiring mistake.
		throw new NotSupportedException(
			$"The registered outbox writer '{writer.GetType().Name}' cannot schedule a message for later "
			+ "delivery, so this call would have written it for immediate delivery instead. Register a writer "
			+ $"implementing {nameof(IScheduledOutboxWriter)}, or call WriteAsync if immediate delivery is "
			+ "what you intended.");
	}
}
