// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Transport;

/// <summary>
/// Provides transport context for incoming messages, enabling pipeline profile selection
/// based on message origin before the pipeline is constructed.
/// </summary>
/// <remarks>
/// <para>
/// This interface is used by the Dispatcher to resolve the transport binding for an
/// incoming message <em>before</em> selecting the pipeline profile. This ensures that
/// transport-specific pipeline profiles are applied correctly.
/// </para>
/// <para>
/// The transport binding is resolved by examining context properties set by the transport
/// adapter when the message was received. If no transport binding is found (e.g., for
/// directly dispatched messages), the method returns <see langword="null"/>.
/// </para>
/// <para>
/// <strong>Important:</strong> This resolution must happen in the Dispatcher, before
/// <see cref="Pipeline.IPipelineProfileResolver.ResolveProfileAsync"/> is called, to ensure
/// the correct pipeline profile is selected before the middleware chain is constructed.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // In Dispatcher.DispatchAsync:
/// var binding = _transportContextProvider.GetTransportBinding(context);
/// var profile = await _profileResolver.ResolveProfileAsync(message, context, binding, ct);
/// var pipeline = _pipelineFactory.CreatePipeline(profile);
/// return await pipeline.ExecuteAsync(message, context, ct);
/// </code>
/// </example>
public interface ITransportContextProvider
{
	/// <summary>
	/// Gets the transport binding for an incoming message, if available.
	/// </summary>
	/// <param name="context">The message context containing transport metadata.</param>
	/// <returns>
	/// The transport binding if the message originated from a transport adapter;
	/// otherwise, <see langword="null"/> for directly dispatched messages.
	/// </returns>
	/// <remarks>
	/// <para>
	/// The implementation should look for transport-specific context properties
	/// such as <c>TransportBindingName</c> or <c>TransportEndpoint</c> that were
	/// set by the transport adapter when the message was received.
	/// </para>
	/// <para>
	/// This method must be thread-safe and should not throw exceptions for missing
	/// transport context - it should simply return <see langword="null"/>.
	/// </para>
	/// </remarks>
	ITransportBinding? GetTransportBinding(IMessageContext context);
}
