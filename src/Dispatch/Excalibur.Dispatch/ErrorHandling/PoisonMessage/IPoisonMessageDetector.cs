// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.ErrorHandling;

/// <summary>
/// Defines the contract for detecting poison messages.
/// </summary>
public interface IPoisonMessageDetector
{
	/// <summary>
	/// Determines whether a message should be considered poison based on processing context.
	/// </summary>
	/// <param name="message"> The message being processed. </param>
	/// <param name="context"> The message context containing metadata. </param>
	/// <param name="processingInfo"> Information about the current processing attempt. </param>
	/// <param name="exception"> The exception that occurred during processing, if any. </param>
	/// <returns> A result indicating whether the message is poison and the reason. </returns>
	Task<PoisonDetectionResult> IsPoisonMessageAsync(
		IDispatchMessage message,
		IMessageContext context,
		MessageProcessingInfo processingInfo,
		Exception? exception = null);
}
