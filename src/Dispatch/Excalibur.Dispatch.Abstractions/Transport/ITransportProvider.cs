// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Validation;

namespace Excalibur.Dispatch.Abstractions.Transport;

/// <summary>
/// Interface for transport provider implementations.
/// </summary>
public interface ITransportProvider
{
	/// <summary>
	/// Gets the name of the transport provider.
	/// </summary>
	string Name { get; }

	/// <summary>
	/// Gets the transport type identifier.
	/// </summary>
	string TransportType { get; }

	/// <summary>
	/// Gets the version of the transport provider.
	/// </summary>
	string Version { get; }

	/// <summary>
	/// Gets the capabilities of this transport provider.
	/// </summary>
	TransportCapabilities Capabilities { get; }

	/// <summary>
	/// Gets a value indicating whether this provider is available in the current environment.
	/// </summary>
	bool IsAvailable { get; }

	/// <summary>
	/// Validates transport options.
	/// </summary>
	/// <param name="options">The options to validate.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The validation result.</returns>
	Task<ValidationResult> ValidateAsync(
		IMessageBusOptions options,
		CancellationToken cancellationToken);

	/// <summary>
	/// Performs a health check on the provider.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The health check result.</returns>
	Task<HealthCheckResult> CheckHealthAsync(
		CancellationToken cancellationToken);

	/// <summary>
	/// Creates a transport adapter.
	/// </summary>
	/// <param name="adapterName">The name of the adapter.</param>
	/// <param name="options">The message bus options.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The created transport adapter.</returns>
	/// <exception cref="InvalidOperationException">Thrown when the provider does not support transport adapters.</exception>
	Task<ITransportAdapter> CreateTransportAdapterAsync(
		string adapterName,
		IMessageBusOptions options,
		CancellationToken cancellationToken);

	/// <summary>
	/// Creates a message bus adapter.
	/// </summary>
	/// <param name="busName">The name of the message bus.</param>
	/// <param name="options">The message bus options.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The created message bus adapter.</returns>
	/// <exception cref="InvalidOperationException">Thrown when the provider does not support message bus adapters.</exception>
	Task<IMessageBusAdapter> CreateMessageBusAdapterAsync(
		string busName,
		IMessageBusOptions options,
		CancellationToken cancellationToken);
}
