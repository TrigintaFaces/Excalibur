// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0



namespace Excalibur.Dispatch.Abstractions.Transport;

/// <summary>
/// Binding between a transport endpoint and a pipeline profile.
/// </summary>
public interface ITransportBinding
{
	/// <summary>
	/// Gets the unique name of this binding.
	/// </summary>
	/// <value> The identifier assigned to the binding. </value>
	string Name { get; }

	/// <summary>
	/// Gets the transport adapter this binding uses.
	/// </summary>
	/// <value> The adapter handling transport operations. </value>
	ITransportAdapter TransportAdapter { get; }

	/// <summary>
	/// Gets the endpoint pattern this binding matches.
	/// </summary>
	/// <value> The endpoint pattern expression. </value>
	string EndpointPattern { get; }

	/// <summary>
	/// Gets the pipeline profile to use for messages from this binding.
	/// </summary>
	/// <value> The pipeline profile applied to matching messages. </value>
	IPipelineProfile? PipelineProfile { get; }

	/// <summary>
	/// Gets the message kinds this binding accepts.
	/// </summary>
	/// <value> The accepted message kinds. </value>
	MessageKinds AcceptedMessageKinds { get; }

	/// <summary>
	/// Gets the priority of this binding for ordering.
	/// </summary>
	/// <value> The numeric priority used for ordering bindings. </value>
	int Priority { get; }

	/// <summary>
	/// Determines if this binding matches an endpoint.
	/// </summary>
	/// <param name="endpoint"> The endpoint to match. </param>
	/// <returns> <see langword="true" /> if the binding matches; otherwise, <see langword="false" />. </returns>
	bool Matches(string endpoint);
}
