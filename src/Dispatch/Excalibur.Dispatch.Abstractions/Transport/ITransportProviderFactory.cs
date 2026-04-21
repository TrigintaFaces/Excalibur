// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Validation;

namespace Excalibur.Dispatch.Abstractions.Transport;

/// <summary>
/// Provides factory and validation operations for a transport provider.
/// Implementations that support these operations should implement this interface
/// alongside <see cref="ITransportProvider"/>.
/// </summary>
public interface ITransportProviderFactory
{
	/// <summary>Validates transport options.</summary>
	Task<ValidationResult> ValidateAsync(MessageBusOptions options, CancellationToken cancellationToken);

	/// <summary>Performs a health check on the provider.</summary>
	Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken);

	/// <summary>Creates a transport adapter.</summary>
	Task<ITransportAdapter> CreateTransportAdapterAsync(string adapterName, MessageBusOptions options, CancellationToken cancellationToken);

	/// <summary>Creates a message bus adapter.</summary>
	Task<IMessageBusAdapter> CreateMessageBusAdapterAsync(string busName, MessageBusOptions options, CancellationToken cancellationToken);
}
