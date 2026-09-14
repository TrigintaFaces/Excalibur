// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using CloudNative.CloudEvents;

using Excalibur.Dispatch.CloudEvents;

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Typed adapter that closes <see cref="ICloudEventEncoder{TOutbound}"/> over a concrete transport
/// message type and exposes it through the non-generic <see cref="ICloudEventEncoderAdapter"/> boundary the
/// core envelope/CloudEvent bridge dispatches against.
/// </summary>
/// <remarks>
/// The generic is closed at the DI registration site (where <typeparamref name="TOutbound"/> is a
/// compile-time type argument), so the bridge never needs
/// <see cref="System.Type.MakeGenericType(System.Type[])"/> or <c>dynamic</c> dispatch. The only residual
/// trimming/AOT annotation is the encoder's own reflection-based JSON serialization, suppressed (not
/// swallowed) at the single call boundary below — an AOT consumer supplies a source-generated serializer
/// context or an AOT-safe encoder.
/// </remarks>
/// <typeparam name="TOutbound">The transport message type the inner encoder produces.</typeparam>
internal sealed class CloudEventEncoderAdapter<TOutbound>(ICloudEventEncoder<TOutbound> inner)
	: ICloudEventEncoderAdapter
{
	private readonly ICloudEventEncoder<TOutbound> _inner =
		inner ?? throw new ArgumentNullException(nameof(inner));

	/// <inheritdoc />
	public Type TransportMessageType => typeof(TOutbound);

	/// <inheritdoc />
	[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed. See ICloudEventEncoder<TOutbound>.ToTransportMessageAsync.")]
	[RequiresDynamicCode("JSON serialization and deserialization might require runtime code generation. See ICloudEventEncoder<TOutbound>.ToTransportMessageAsync.")]
	[UnconditionalSuppressMessage(
		"Trimming",
		"IL2046:Members annotated with 'RequiresUnreferencedCodeAttribute' should be overridden or implement members that are also annotated",
		Justification = "Mismatch-only, not a swallow: ICloudEventEncoderAdapter is an internal, non-extensible wiring shim over ICloudEventEncoder<TOutbound> (whose interface member already carries this requirement) — the consumer-facing signal already lives at each transport's opt-in registration (e.g. UseCloudEvents/AddCloudEventsForRabbitMq/AddCloudEventsForPubSub), which is annotated. Annotating this internal interface too would be redundant, not a new hole.")]
	[UnconditionalSuppressMessage(
		"AOT",
		"IL3051:Members annotated with 'RequiresDynamicCodeAttribute' should be overridden or implement members that are also annotated",
		Justification = "Same as IL2046: the interface mismatch is against ICloudEventEncoderAdapter, an internal wiring shim with a single implementation, not a consumer-implementable contract. Same as IL2046.")]
	public async Task<object> ToTransportAsync(CloudEvent cloudEvent, CloudEventMode mode, CancellationToken cancellationToken)
	{
		var result = await _inner.ToTransportMessageAsync(cloudEvent, mode, cancellationToken).ConfigureAwait(false);
		return result!;
	}
}
