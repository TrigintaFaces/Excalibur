// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using CloudNative.CloudEvents;

namespace Excalibur.Dispatch.CloudEvents;

/// <summary>
/// Non-generic view over a registered <c>ICloudEventMapper&lt;TTransportMessage&gt;</c> that lets
/// <see cref="EnvelopeCloudEventBridge"/> dispatch by transport-message <see cref="Type"/> without
/// closing an open generic at runtime (<see cref="System.Type.MakeGenericType(System.Type[])"/>) or using
/// late-bound <c>dynamic</c> invocation. The generic is closed once, at the registration site where the
/// transport-message type is statically known, so the bridge stays trimming/AOT-safe.
/// </summary>
public interface ICloudEventMapperAdapter
{
	/// <summary>
	/// Gets the transport-message type this adapter maps to and from.
	/// </summary>
	Type TransportMessageType { get; }

	/// <summary>
	/// Serializes a <see cref="CloudEvent"/> into the adapter's transport-message representation, boxed as
	/// <see cref="object"/> for the non-generic bridge boundary.
	/// </summary>
	Task<object> ToTransportAsync(CloudEvent cloudEvent, CloudEventMode mode, CancellationToken cancellationToken);

	/// <summary>
	/// Deserializes a transport message (boxed) back into a <see cref="CloudEvent"/>.
	/// </summary>
	Task<CloudEvent> FromTransportAsync(object transportMessage, CancellationToken cancellationToken);
}
