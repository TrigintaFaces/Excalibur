// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Dispatch.Outbox;

/// <summary>
/// Capability an <see cref="IOutboxWriter"/> implements when it can carry a scheduled delivery time.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="OutboxWriterExtensions.WriteScheduledAsync"/> used to hand the time to the writer through an
/// <see cref="AsyncLocal{T}"/>, which the writer then read straight back. The runtime copies the whole
/// async-local value map on any write and swaps representation by entry count, so an ambient nobody else
/// reads still taxes every publication in the process -- ours and the consumer's. A parameter costs
/// nothing and cannot be read by the wrong frame.
/// </para>
/// <para>
/// Internal, and not derived from <see cref="IOutboxWriter"/>: it is a detail of how the framework's own
/// writer is handed the time, not a contract a consumer implements. A writer that does not implement it
/// receives the plain <see cref="IOutboxWriter.WriteAsync"/> call, exactly as before.
/// </para>
/// </remarks>
internal interface IScheduledOutboxWriter
{
	/// <summary>
	/// Writes a message to the outbox for delivery at <paramref name="scheduledAt"/>.
	/// </summary>
	/// <param name="message">The message to stage.</param>
	/// <param name="destination">Optional destination/topic for the message.</param>
	/// <param name="scheduledAt">The time at which the message should be delivered.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation.</param>
	/// <returns>A task representing the write operation.</returns>
	[RequiresUnreferencedCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	ValueTask WriteScheduledAsync(
		IDispatchMessage message,
		string? destination,
		DateTimeOffset scheduledAt,
		CancellationToken cancellationToken);
}
