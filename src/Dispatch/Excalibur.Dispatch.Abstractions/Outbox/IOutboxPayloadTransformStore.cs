// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Dispatch;

/// <summary>Optional provider-owned legacy enqueue with payload transformation before persistence.</summary>
/// <remarks>
/// Discover this facet through its owning <see cref="IOutboxStore"/> and <c>GetService</c>.
/// It operates on that same store's rows and does not supply a separate store registration, tenant
/// boundary, worker admission, claim or fencing capability. The provider retains its original serializer,
/// envelope construction, framing, identity, routing and transaction behavior; only payload bytes change.
/// Invariant decorators must not expose this writer without mediation that preserves their invariant.
/// </remarks>
public interface IOutboxPayloadTransformStore
{
	/// <summary>Prepares the provider's envelope, transforms its payload once, then persists it.</summary>
	/// <param name="message">The message the provider serializes using its existing enqueue configuration.</param>
	/// <param name="context">The context the provider uses to construct its complete envelope before awaiting the transform.</param>
	/// <param name="transformPayload">
	/// Receives the exact serialized bytes, the prepared envelope's tenant and the supplied cancellation
	/// token. Input memory is borrowed read-only and remains stable during invocation; do not mutate it or
	/// retain it for later changes. Return non-null callback-owned bytes. The provider copies that result
	/// before persistence, so later changes after acceptance cannot alter stored state. Concurrent mutation
	/// during the synchronous handoff copy is unsupported. This is not recursive cloning of header objects.
	/// </param>
	/// <param name="cancellationToken">Cancellation checked before preparation and after the transform, before accepting a write.</param>
	/// <returns>Completion of the provider's enqueue operation.</returns>
	/// <remarks>
	/// A supplied transform runs exactly once, including empty payloads, before accepting any row mutation.
	/// Null results, callback faults and callback cancellation cause no accepting write or callback retry.
	/// A callback that cancels the supplied token but returns bytes is refused before acceptance. Once
	/// persistence starts, cancellation retains the provider's existing transaction and uncertainty semantics.
	/// </remarks>
	/// <exception cref="ArgumentNullException">Message, context or transform is null.</exception>
	/// <exception cref="InvalidOperationException">The transform returns a null payload.</exception>
	[RequiresUnreferencedCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	ValueTask EnqueueWithPayloadTransformAsync(
		IDispatchMessage message,
		IMessageContext context,
		Func<ReadOnlyMemory<byte>, string?, CancellationToken, ValueTask<byte[]>> transformPayload,
		CancellationToken cancellationToken);
}
