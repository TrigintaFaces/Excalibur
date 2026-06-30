// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Compat.MassTransit;
using Excalibur.Dispatch.Delivery;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration extensions for migrating MassTransit-style consumers onto Excalibur.Dispatch.
/// </summary>
public static class MassTransitCompatServiceCollectionExtensions
{
	/// <summary>
	/// Registers a migrated MassTransit-style consumer as an Excalibur.Dispatch event handler, bridging
	/// <see cref="IConsumer{TMessage}.Consume"/> onto <see cref="IEventHandler{TEvent}.HandleAsync"/>.
	/// </summary>
	/// <typeparam name="TConsumer">The migrated consumer type.</typeparam>
	/// <typeparam name="TMessage">
	/// The consumed message type; must be annotated with <see cref="IDispatchEvent"/> (the documented
	/// manual migration step) so it is routable through the dispatch pipeline.
	/// </typeparam>
	/// <param name="services">The service collection.</param>
	/// <returns>The same <paramref name="services"/> instance for chaining.</returns>
	/// <remarks>
	/// Registration is generic and reflection-free (AOT-safe). The consumer is registered as a scoped
	/// service and the adapter as an <see cref="IEventHandler{TEvent}"/> for <typeparamref name="TMessage"/>.
	/// </remarks>
	public static IServiceCollection AddMassTransitConsumer<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TConsumer,
		TMessage>(this IServiceCollection services)
		where TConsumer : class, IConsumer<TMessage>
		where TMessage : class, IDispatchEvent
	{
		services.TryAddScoped<TConsumer>();
		services.AddScoped<IEventHandler<TMessage>, ConsumerEventHandlerAdapter<TConsumer, TMessage>>();
		return services;
	}
}
