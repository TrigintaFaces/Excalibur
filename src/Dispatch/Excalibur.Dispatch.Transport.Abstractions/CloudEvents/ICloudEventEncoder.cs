// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;

using CloudNative.CloudEvents;

using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Options.CloudEvents;

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Maps a <see cref="CloudEvent" /> into a provider-specific outbound transport message.
/// </summary>
/// <typeparam name="TOutbound"> The transport message type produced for send. </typeparam>
/// <remarks>
/// Split from the former <c>ICloudEventMapper&lt;T&gt;</c>: every transport that
/// supports CloudEvents can encode, but not every transport's send type is meaningfully decodable -- four of
/// the original eight mappers are typed on send-only provider SDK types (<c>SendMessageRequest</c>,
/// <c>PublishRequest</c>, <c>PutEventsRequestEntry</c>, <c>ServiceBusMessage</c>) that never appear as an
/// inbound object. Decoding is a separate concern; see <see cref="ICloudEventDecoder{TInbound}"/>.
/// </remarks>
public interface ICloudEventEncoder<TOutbound>
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
	[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
	[RequiresDynamicCode("JSON serialization and deserialization might require runtime code generation.")]
	Task<TOutbound> ToTransportMessageAsync(
		CloudEvent cloudEvent,
		CloudEventMode mode,
		CancellationToken cancellationToken);
}
