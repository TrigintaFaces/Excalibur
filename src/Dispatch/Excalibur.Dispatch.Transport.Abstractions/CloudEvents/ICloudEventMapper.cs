// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using CloudNative.CloudEvents;

using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Options.CloudEvents;

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Maps CloudEvents to provider-specific transport message representations and back.
/// </summary>
/// <typeparam name="TTransportMessage"> The transport message type produced or consumed by the mapper. </typeparam>
public interface ICloudEventMapper<TTransportMessage>
{
	/// <summary>
	/// Gets the options used to configure CloudEvent serialization.
	/// </summary>
	/// <value>
	/// The options used to configure CloudEvent serialization.
	/// </value>
	CloudEventOptions Options { get; }

	/// <summary>
	/// Converts a <see cref="CloudEvent" /> instance into a provider-specific transport message.
	/// </summary>
	/// <param name="cloudEvent"> The CloudEvent to convert. </param>
	/// <param name="mode"> The serialization mode to apply. </param>
	/// <param name="cancellationToken"> Cancellation token for the async operation. </param>
	/// <returns> The serialized transport message. </returns>
	Task<TTransportMessage> ToTransportMessageAsync(
		CloudEvent cloudEvent,
		CloudEventMode mode,
		CancellationToken cancellationToken);

	/// <summary>
	/// Converts a provider-specific transport message into a <see cref="CloudEvent" />.
	/// </summary>
	/// <param name="transportMessage"> The transport message to convert. </param>
	/// <param name="cancellationToken"> Cancellation token for the async operation. </param>
	/// <returns> The resulting CloudEvent. </returns>
	Task<CloudEvent> FromTransportMessageAsync(
		TTransportMessage transportMessage,
		CancellationToken cancellationToken);

	/// <summary>
	/// Attempts to detect the CloudEvent mode used by the supplied transport message.
	/// </summary>
	/// <param name="transportMessage"> The transport message to inspect. </param>
	/// <param name="cancellationToken"> Cancellation token for the async operation. </param>
	/// <returns> The detected mode when available; otherwise <c> null </c>. </returns>
	ValueTask<CloudEventMode?> TryDetectModeAsync(
		TTransportMessage transportMessage,
		CancellationToken cancellationToken) =>
		new((CloudEventMode?)null);
}
