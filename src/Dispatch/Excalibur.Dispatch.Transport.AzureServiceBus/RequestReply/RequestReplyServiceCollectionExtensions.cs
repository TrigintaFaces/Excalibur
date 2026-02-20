// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Azure;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Azure Service Bus request/reply pattern with the service collection.
/// </summary>
/// <remarks>
/// <para>
/// The request/reply pattern enables synchronous-style interactions over Azure Service Bus
/// using sessions for correlation. Requests are sent with a unique session ID, and replies
/// are received on a session-enabled queue filtered by that session ID.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddAzureServiceBusRequestReply&lt;MyRequestReplyClient&gt;(options =>
/// {
///     options.ReplyQueueName = "replies";
///     options.ReplyTimeout = TimeSpan.FromSeconds(30);
/// });
/// </code>
/// </example>
public static class RequestReplyServiceCollectionExtensions
{
	/// <summary>
	/// Adds the Azure Service Bus request/reply client with the specified configuration.
	/// </summary>
	/// <typeparam name="TImplementation">
	/// The concrete type implementing <see cref="IRequestReplyClient"/>.
	/// </typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">The action to configure request/reply options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="services"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Registers <see cref="RequestReplyOptions"/> and <see cref="IRequestReplyClient"/>
	/// in the DI container. The reply queue must be session-enabled in Azure Service Bus.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddAzureServiceBusRequestReply<TImplementation>(
		this IServiceCollection services,
		Action<RequestReplyOptions> configure)
		where TImplementation : class, IRequestReplyClient
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<RequestReplyOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.AddSingleton<IRequestReplyClient, TImplementation>();

		return services;
	}

	/// <summary>
	/// Adds the Azure Service Bus request/reply client using a factory delegate.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">The action to configure request/reply options.</param>
	/// <param name="factory">The factory delegate that creates the request/reply client instance.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="services"/>, <paramref name="configure"/>, or <paramref name="factory"/> is null.
	/// </exception>
	public static IServiceCollection AddAzureServiceBusRequestReply(
		this IServiceCollection services,
		Action<RequestReplyOptions> configure,
		Func<IServiceProvider, IRequestReplyClient> factory)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);
		ArgumentNullException.ThrowIfNull(factory);

		_ = services.AddOptions<RequestReplyOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.AddSingleton(factory);

		return services;
	}
}
