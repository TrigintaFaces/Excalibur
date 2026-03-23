// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions.Outbox;

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
	public static ValueTask WriteScheduledAsync(
		this IOutboxWriter writer,
		IDispatchMessage message,
		string? destination,
		DateTimeOffset scheduledAt,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(writer);
		ArgumentNullException.ThrowIfNull(message);

		OutboxScheduledDeliveryScope.Current = scheduledAt;
		try
		{
			return writer.WriteAsync(message, destination, cancellationToken);
		}
		finally
		{
			OutboxScheduledDeliveryScope.Current = null;
		}
	}
}
