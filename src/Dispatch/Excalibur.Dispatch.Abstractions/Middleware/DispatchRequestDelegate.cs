// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

// R0.8: Identifiers should not have incorrect suffix - Delegate is appropriate for this delegate type
#pragma warning disable CA1711
/// <summary>
/// Represents a delegate that defines the signature for handling dispatch requests in the middleware pipeline.
/// </summary>
/// <param name="message"> The dispatch message to process. </param>
/// <param name="context"> The message context containing metadata and state. </param>
/// <param name="cancellationToken"> A token to monitor for cancellation requests. </param>
/// <returns>
/// A <see cref="ValueTask{TResult}"/> that represents the asynchronous operation, containing the message processing result.
/// Using ValueTask enables zero-allocation dispatch on synchronous completion paths.
/// </returns>
public delegate ValueTask<IMessageResult> DispatchRequestDelegate(
	IDispatchMessage message,
	IMessageContext context,
	CancellationToken cancellationToken);

#pragma warning restore CA1711
