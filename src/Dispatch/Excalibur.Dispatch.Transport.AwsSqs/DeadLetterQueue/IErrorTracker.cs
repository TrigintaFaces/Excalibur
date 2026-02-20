// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Tracks errors for messages in the DLQ.
/// </summary>
public interface IErrorTracker
{
	/// <summary>
	/// Records an error for a message.
	/// </summary>
	/// <param name="messageId"> The message ID. </param>
	/// <param name="errorDetail"> The error details. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	Task RecordErrorAsync(string messageId, ErrorDetail errorDetail, CancellationToken cancellationToken);

	/// <summary>
	/// Gets the error history for a message.
	/// </summary>
	/// <param name="messageId"> The message ID. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> The error history. </returns>
	Task<IReadOnlyList<ErrorDetail>> GetErrorHistoryAsync(string messageId, CancellationToken cancellationToken);

	/// <summary>
	/// Clears the error history for a message.
	/// </summary>
	/// <param name="messageId"> The message ID. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	Task ClearErrorHistoryAsync(string messageId, CancellationToken cancellationToken);
}
