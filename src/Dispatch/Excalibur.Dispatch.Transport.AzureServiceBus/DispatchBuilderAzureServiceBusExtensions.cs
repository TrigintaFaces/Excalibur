// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Transport.Azure;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Azure Service Bus transport via <see cref="IDispatchBuilder"/>.
/// </summary>
public static class DispatchBuilderAzureServiceBusExtensions
{
	/// <summary>
	/// Configures the Azure Service Bus transport with the default name via the dispatch builder.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="configure">The transport configuration action.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddDispatch(dispatch =>
	/// {
	///     dispatch.UseAzureServiceBus(sb =>
	///     {
	///         sb.ConnectionString("Endpoint=sb://...")
	///           .ConfigureProcessor(processor => processor.MaxConcurrentCalls(20));
	///     });
	/// });
	/// </code>
	/// </example>
	public static IDispatchBuilder UseAzureServiceBus(
		this IDispatchBuilder builder,
		Action<IAzureServiceBusTransportBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddAzureServiceBusTransport(configure);
		return builder;
	}

	/// <summary>
	/// Configures a named Azure Service Bus transport via the dispatch builder.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="name">The transport name for multi-transport scenarios.</param>
	/// <param name="configure">The transport configuration action.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="name"/> is null or whitespace.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddDispatch(dispatch =>
	/// {
	///     dispatch.UseAzureServiceBus("payments", sb =>
	///     {
	///         sb.ConnectionString("Endpoint=sb://payments.servicebus.windows.net/;...")
	///           .MapEntity&lt;PaymentReceived&gt;("payments-queue");
	///     });
	/// });
	/// </code>
	/// </example>
	public static IDispatchBuilder UseAzureServiceBus(
		this IDispatchBuilder builder,
		string name,
		Action<IAzureServiceBusTransportBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddAzureServiceBusTransport(name, configure);
		return builder;
	}
}
