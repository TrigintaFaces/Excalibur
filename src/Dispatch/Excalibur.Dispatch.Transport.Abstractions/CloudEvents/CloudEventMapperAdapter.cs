// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using CloudNative.CloudEvents;

using Excalibur.Dispatch.CloudEvents;

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Typed adapter that closes <see cref="ICloudEventMapper{TTransportMessage}"/> over a concrete transport
/// message type and exposes it through the non-generic <see cref="ICloudEventMapperAdapter"/> boundary the
/// core envelope/CloudEvent bridge dispatches against.
/// </summary>
/// <remarks>
/// The generic is closed at the DI registration site (where <typeparamref name="TTransportMessage"/> is a
/// compile-time type argument), so the bridge never needs
/// <see cref="System.Type.MakeGenericType(System.Type[])"/> or <c>dynamic</c> dispatch. The only residual
/// trimming/AOT annotation is the mapper's own reflection-based JSON serialization, suppressed (not
/// swallowed) at the single call boundary below — an AOT consumer supplies a source-generated serializer
/// context or an AOT-safe mapper.
/// </remarks>
/// <typeparam name="TTransportMessage">The transport message type the inner mapper handles.</typeparam>
internal sealed class CloudEventMapperAdapter<TTransportMessage>(ICloudEventMapper<TTransportMessage> inner)
	: ICloudEventMapperAdapter
{
	private readonly ICloudEventMapper<TTransportMessage> _inner =
		inner ?? throw new ArgumentNullException(nameof(inner));

	/// <inheritdoc />
	public Type TransportMessageType => typeof(TTransportMessage);

	/// <inheritdoc />
	[UnconditionalSuppressMessage(
		"Trimming",
		"IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
		Justification = "The reflection is confined to the transport mapper's own JSON serialization, an opt-in transport concern; an AOT consumer registers an AOT-safe mapper or a source-generated serializer context. The bridge no longer performs reflective type resolution.")]
	[UnconditionalSuppressMessage(
		"AOT",
		"IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
		Justification = "Same as IL2026: the residual dynamic-code requirement is the transport mapper's JSON path, not the bridge. No MakeGenericType is used.")]
	public async Task<object> ToTransportAsync(CloudEvent cloudEvent, CloudEventMode mode, CancellationToken cancellationToken)
	{
		var result = await _inner.ToTransportMessageAsync(cloudEvent, mode, cancellationToken).ConfigureAwait(false);
		return result!;
	}

	/// <inheritdoc />
	public Task<CloudEvent> FromTransportAsync(object transportMessage, CancellationToken cancellationToken) =>
		_inner.FromTransportMessageAsync((TTransportMessage)transportMessage, cancellationToken);
}
