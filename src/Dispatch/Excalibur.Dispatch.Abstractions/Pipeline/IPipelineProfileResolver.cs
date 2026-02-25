// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Transport;

namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Resolves the appropriate pipeline profile for a given message.
/// </summary>
/// <remarks>
/// The profile resolver is responsible for selecting which pipeline profile should be used to process a specific message. It evaluates
/// available profiles based on message characteristics, context, and runtime conditions to determine the most appropriate processing pipeline.
/// </remarks>
public interface IPipelineProfileResolver
{
	/// <summary>
	/// Resolves the pipeline profile to use for the given message.
	/// </summary>
	/// <param name="message"> The message to process. </param>
	/// <param name="context"> The message context containing metadata and state. </param>
	/// <param name="transportBinding">
	/// The transport binding that received this message, or <see langword="null"/> for directly dispatched messages.
	/// When provided, the resolver should consider the binding's <see cref="ITransportBinding.PipelineProfile"/>
	/// as a candidate profile.
	/// </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The selected pipeline profile, or null to use the default pipeline. </returns>
	/// <remarks>
	/// <para>
	/// The resolver should evaluate all registered profiles and select the most appropriate one based on factors such as:
	/// </para>
	/// <list type="bullet">
	/// <item> Transport binding's configured pipeline profile (if any) </item>
	/// <item> Message kind and type </item>
	/// <item> Profile applicability and priority </item>
	/// <item> Context metadata and headers </item>
	/// <item> Runtime conditions and configuration </item>
	/// </list>
	/// <para>
	/// If no profile matches or null is returned, the default pipeline configuration is used.
	/// </para>
	/// <para>
	/// <strong>Important:</strong> This method is called by the Dispatcher <em>before</em> constructing the pipeline.
	/// The transport binding is resolved by <see cref="ITransportContextProvider"/> prior to this call.
	/// </para>
	/// </remarks>
	ValueTask<IPipelineProfile?> ResolveProfileAsync(
		IDispatchMessage message,
		IMessageContext context,
		ITransportBinding? transportBinding,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets all registered pipeline profiles.
	/// </summary>
	/// <returns> A collection of all available pipeline profiles. </returns>
	IReadOnlyCollection<IPipelineProfile> GetRegisteredProfiles();

	/// <summary>
	/// Gets a specific pipeline profile by name.
	/// </summary>
	/// <param name="profileName"> The name of the profile to retrieve. </param>
	/// <returns> The profile with the specified name, or null if not found. </returns>
	IPipelineProfile? GetProfile(string profileName);
}
