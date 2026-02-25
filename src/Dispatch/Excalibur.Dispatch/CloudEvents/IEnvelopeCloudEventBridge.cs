// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using MessageEnvelope = Excalibur.Dispatch.Abstractions.MessageEnvelope;

namespace Excalibur.Dispatch.CloudEvents;

/// <summary>
/// Bridges Dispatch <see cref="MessageEnvelope" /> instances to transport messages using CloudEvents semantics.
/// </summary>
public interface IEnvelopeCloudEventBridge
{
	/// <summary>
	/// Converts a <see cref="MessageEnvelope" /> into a transport message using the specified CloudEvent mode.
	/// </summary>
	/// <typeparam name="TTransportMessage"> The resulting transport message type. </typeparam>
	/// <param name="envelope"> The Dispatch message envelope to convert. </param>
	/// <param name="mode"> The CloudEvent mode to use during serialization. </param>
	/// <param name="cancellationToken"> Cancellation token for the async operation. </param>
	/// <returns> The provider-specific transport message representation. </returns>
	Task<TTransportMessage> ToTransportAsync<TTransportMessage>(
		MessageEnvelope envelope,
		CloudEventMode mode,
		CancellationToken cancellationToken);

	/// <summary>
	/// Restores a <see cref="MessageEnvelope" /> from a provider-specific transport message.
	/// </summary>
	/// <param name="transportMessage"> The transport message to convert. </param>
	/// <param name="cancellationToken"> Cancellation token for the async operation. </param>
	/// <returns> The reconstructed message envelope. </returns>
	Task<MessageEnvelope> FromTransportAsync(
		object transportMessage,
		CancellationToken cancellationToken);
}
