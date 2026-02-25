// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Transport.RabbitMQ;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring RabbitMQ transport via <see cref="IDispatchBuilder"/>.
/// </summary>
public static class DispatchBuilderRabbitMQExtensions
{
	/// <summary>
	/// Configures the RabbitMQ transport with the default name via the dispatch builder.
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
	///     dispatch.UseRabbitMQ(rmq =>
	///     {
	///         rmq.HostName("localhost")
	///            .Credentials("guest", "guest");
	///     });
	/// });
	/// </code>
	/// </example>
	public static IDispatchBuilder UseRabbitMQ(
		this IDispatchBuilder builder,
		Action<IRabbitMQTransportBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddRabbitMQTransport(configure);
		return builder;
	}

	/// <summary>
	/// Configures a named RabbitMQ transport via the dispatch builder.
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
	///     dispatch.UseRabbitMQ("payments", rmq =>
	///     {
	///         rmq.HostName("payments.rabbitmq.local")
	///            .ConfigureQueue(q => q.Name("payments"));
	///     });
	/// });
	/// </code>
	/// </example>
	public static IDispatchBuilder UseRabbitMQ(
		this IDispatchBuilder builder,
		string name,
		Action<IRabbitMQTransportBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddRabbitMQTransport(name, configure);
		return builder;
	}
}
