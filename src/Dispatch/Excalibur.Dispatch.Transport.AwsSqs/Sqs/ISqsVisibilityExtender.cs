// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Provides the ability to extend the visibility timeout of an SQS message
/// during long-running handler processing.
/// </summary>
/// <remarks>
/// <para>
/// Consumers access this via the <c>GetService(Type)</c> escape hatch on
/// <see cref="ITransportReceiver"/>:
/// </para>
/// <code>
/// var extender = receiver.GetService(typeof(ISqsVisibilityExtender)) as ISqsVisibilityExtender;
/// await extender?.ExtendVisibilityTimeoutAsync(receiptHandle, TimeSpan.FromMinutes(5), ct);
/// </code>
/// <para>
/// This prevents messages from reappearing in the queue when handler
/// processing exceeds the default visibility timeout (30 seconds).
/// </para>
/// </remarks>
internal interface ISqsVisibilityExtender
{
	/// <summary>
	/// Extends the visibility timeout of an SQS message.
	/// </summary>
	/// <param name="receiptHandle">The receipt handle of the message to extend.</param>
	/// <param name="extension">The new visibility timeout duration from now.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	Task ExtendVisibilityTimeoutAsync(
		string receiptHandle,
		TimeSpan extension,
		CancellationToken cancellationToken);
}
