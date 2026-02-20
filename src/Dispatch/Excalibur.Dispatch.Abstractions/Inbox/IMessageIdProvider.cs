// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Provides custom message ID extraction for idempotency checking.
/// </summary>
/// <remarks>
/// <para>
/// Implement this interface when <see cref="IdempotentAttribute.Strategy"/> is set to
/// <see cref="MessageIdStrategy.Custom"/> and you need control over how the
/// deduplication key is generated.
/// </para>
/// <para>
/// Common use cases include:
/// <list type="bullet">
/// <item><description>Extracting an ID from the message body</description></item>
/// <item><description>Combining multiple fields into a composite key</description></item>
/// <item><description>Using a hash of message content</description></item>
/// </list>
/// </para>
/// </remarks>
public interface IMessageIdProvider
{
	/// <summary>
	/// Extracts the message ID for deduplication from the given message and context.
	/// </summary>
	/// <param name="message"> The dispatch message being processed. </param>
	/// <param name="context"> The message context containing headers and metadata. </param>
	/// <returns>
	/// A unique identifier for the message, or <see langword="null"/> if the message
	/// should not be tracked for idempotency (i.e., always processed).
	/// </returns>
	string? GetMessageId(IDispatchMessage message, IMessageContext context);
}
