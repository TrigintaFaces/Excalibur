// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Configuration;

/// <summary>
/// Builder for configuring transport bindings.
/// </summary>
public interface IBindingConfigurationBuilder
{
	/// <summary>
	/// Sets the binding name.
	/// </summary>
	/// <param name="name"> The name for the binding. </param>
	/// <returns> The builder for chaining. </returns>
	IBindingConfigurationBuilder WithName(string name);

	/// <summary>
	/// Sets the transport to bind Excalibur.Dispatch.Transport.Aws.Sqs.LongPolling.Configuration.
	/// </summary>
	/// <param name="transportName"> The name of the transport to bind to. </param>
	/// <returns> The builder for chaining. </returns>
	IBindingConfigurationBuilder ForTransport(string transportName);

	/// <summary>
	/// Sets the endpoint pattern.
	/// </summary>
	/// <param name="endpointPattern"> The endpoint pattern to match. </param>
	/// <returns> The builder for chaining. </returns>
	IBindingConfigurationBuilder ForEndpoint(string endpointPattern);

	/// <summary>
	/// Sets the pipeline profile to use.
	/// </summary>
	/// <param name="pipelineName"> The name of the pipeline profile to use. </param>
	/// <returns> The builder for chaining. </returns>
	IBindingConfigurationBuilder UsePipeline(string pipelineName);

	/// <summary>
	/// Sets the accepted message kinds.
	/// </summary>
	/// <param name="kinds"> The message kinds to accept. </param>
	/// <returns> The builder for chaining. </returns>
	IBindingConfigurationBuilder AcceptMessageKinds(MessageKinds kinds);

	/// <summary>
	/// Sets the binding priority.
	/// </summary>
	/// <param name="priority"> The priority value for the binding. </param>
	/// <returns> The builder for chaining. </returns>
	IBindingConfigurationBuilder WithPriority(int priority);
}
