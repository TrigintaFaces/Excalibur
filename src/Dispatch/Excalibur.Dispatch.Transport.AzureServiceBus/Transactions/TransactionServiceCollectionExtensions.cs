// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Azure;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Azure Service Bus transactional support with the service collection.
/// </summary>
/// <remarks>
/// <para>
/// Registers the <see cref="IAzureServiceBusTransaction"/> implementation and
/// <see cref="TransactionalSendOptions"/> for transactional send operations.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddAzureServiceBusTransactions&lt;MyTransactionImpl&gt;();
/// </code>
/// </example>
public static class TransactionServiceCollectionExtensions
{
	/// <summary>
	/// Adds Azure Service Bus transaction support using the specified implementation.
	/// </summary>
	/// <typeparam name="TImplementation">
	/// The concrete type implementing <see cref="IAzureServiceBusTransaction"/>.
	/// </typeparam>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="services"/> is null.
	/// </exception>
	public static IServiceCollection AddAzureServiceBusTransactions<TImplementation>(
		this IServiceCollection services)
		where TImplementation : class, IAzureServiceBusTransaction
	{
		ArgumentNullException.ThrowIfNull(services);

		services.AddScoped<IAzureServiceBusTransaction, TImplementation>();

		return services;
	}
}
