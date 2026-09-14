// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch;

/// <summary>
/// Describes how a message result was produced: whether a handler ran, or the operation was satisfied
/// without one.
/// </summary>
/// <remarks>
/// <para>
/// A successful result does not imply a handler ran. A cached result and a suppressed duplicate both
/// complete successfully with no handler invocation, and a caller that treats success as evidence of
/// handling will draw the wrong conclusion — a redelivery pipeline that records a message as processed
/// on the strength of success alone can mark a message complete that nothing ever handled.
/// </para>
/// <para>
/// This is a classification, not a status. Whether the operation failed is
/// <see cref="IMessageResult.Succeeded" />; this says only what produced the outcome.
/// </para>
/// </remarks>
public enum MessageDisposition
{
	/// <summary>
	/// A handler ran and produced the result. This is the default for any implementation that does not
	/// state otherwise.
	/// </summary>
	Handled = 0,

	/// <summary>
	/// The result was served from cache. No handler ran for this message.
	/// </summary>
	ServedFromCache = 1,

	/// <summary>
	/// The message was recognised as one already accepted for processing and was suppressed. No handler
	/// ran, and that is the correct outcome — but the message was not handled by this invocation, and a
	/// caller responsible for recording completion must not treat it as though it were.
	/// </summary>
	SuppressedAsDuplicate = 2,
}
