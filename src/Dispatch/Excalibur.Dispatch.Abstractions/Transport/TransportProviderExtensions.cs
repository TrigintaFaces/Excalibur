// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Validation;

namespace Excalibur.Dispatch.Abstractions.Transport;

/// <summary>
/// Extension methods for <see cref="ITransportProvider"/>.
/// </summary>
public static class TransportProviderExtensions
{
	/// <summary>Validates transport options.</summary>
	public static Task<ValidationResult> ValidateAsync(this ITransportProvider provider, MessageBusOptions options, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(provider);
		if (provider is ITransportProviderFactory factory)
		{
			return factory.ValidateAsync(options, cancellationToken);
		}
		throw new NotSupportedException($"Transport provider '{provider.Name}' does not support validation. Implement ITransportProviderFactory.");
	}

	/// <summary>Performs a health check on the provider.</summary>
	public static Task<HealthCheckResult> CheckHealthAsync(this ITransportProvider provider, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(provider);
		if (provider is ITransportProviderFactory factory)
		{
			return factory.CheckHealthAsync(cancellationToken);
		}
		throw new NotSupportedException($"Transport provider '{provider.Name}' does not support health checks. Implement ITransportProviderFactory.");
	}

	/// <summary>Creates a transport adapter.</summary>
	public static Task<ITransportAdapter> CreateTransportAdapterAsync(this ITransportProvider provider, string adapterName, MessageBusOptions options, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(provider);
		if (provider is ITransportProviderFactory factory)
		{
			return factory.CreateTransportAdapterAsync(adapterName, options, cancellationToken);
		}
		throw new NotSupportedException($"Transport provider '{provider.Name}' does not support adapter creation. Implement ITransportProviderFactory.");
	}

	/// <summary>Creates a message bus adapter.</summary>
	public static Task<IMessageBusAdapter> CreateMessageBusAdapterAsync(this ITransportProvider provider, string busName, MessageBusOptions options, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(provider);
		if (provider is ITransportProviderFactory factory)
		{
			return factory.CreateMessageBusAdapterAsync(busName, options, cancellationToken);
		}
		throw new NotSupportedException($"Transport provider '{provider.Name}' does not support adapter creation. Implement ITransportProviderFactory.");
	}
}
