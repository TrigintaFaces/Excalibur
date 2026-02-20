// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Middleware;
using Excalibur.Dispatch.Options.Configuration;
using Excalibur.Dispatch.Transport;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Dispatch in the service collection.
/// </summary>
public static class DispatchConfigurationServiceCollectionExtensions
{
	/// <summary>
	/// Adds Excalibur framework to the service collection with advanced configuration.
	/// This method is intended for internal use. Prefer <see cref="DispatchServiceCollectionExtensions.AddDispatch(IServiceCollection, Action{IDispatchBuilder}?)"/>.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configure"> Configuration action for the Dispatch builder. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <remarks>
	/// This method includes additional infrastructure registration (pipeline synthesizer, transport router).
	/// For most use cases, use the simpler <c>AddDispatch</c> extension method.
	/// </remarks>
	internal static IServiceCollection AddDispatchWithInfrastructure(
		this IServiceCollection services,
		Action<IDispatchBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		// Register core services
		services.TryAddSingleton<IMiddlewareApplicabilityStrategy, DefaultMiddlewareApplicabilityStrategy>();
		services.TryAddSingleton<IPipelineProfileRegistry, PipelineProfileRegistry>();
		services.TryAddSingleton<TransportBindingRegistry>();

		// Register pipeline synthesizer for default pipeline creation (R7.5-R7.12)
		services.TryAddSingleton<IDefaultPipelineSynthesizer, DefaultPipelineSynthesizer>();

		// Register transport router middleware (R3.1)
		services.TryAddScoped<TransportRouterMiddleware>();

		// Create and configure the builder
		using var builder = new DispatchBuilder(services);
		configure(builder);

		// Build the runtime configuration
		_ = builder.Build();

		return services;
	}

	/// <summary>
	/// Adds a message handler to the service collection.
	/// </summary>
	/// <typeparam name="TMessage"> The message type. </typeparam>
	/// <typeparam name="THandler"> The handler type. </typeparam>
	/// <param name="services"> The service collection. </param>
	/// <param name="lifetime"> The service lifetime. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddHandler<TMessage,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>(
		this IServiceCollection services,
		ServiceLifetime lifetime = ServiceLifetime.Scoped)
		where TMessage : IDispatchMessage
		where THandler : class, IDispatchHandler<TMessage>
	{
		ArgumentNullException.ThrowIfNull(services);
		services.Add(new ServiceDescriptor(typeof(IDispatchHandler<TMessage>), typeof(THandler), lifetime));
		services.Add(new ServiceDescriptor(typeof(THandler), typeof(THandler), lifetime));
		return services;
	}

	/// <summary>
	/// Adds middleware to the service collection.
	/// </summary>
	/// <typeparam name="TMiddleware"> The middleware type. </typeparam>
	/// <param name="services"> The service collection. </param>
	/// <param name="lifetime"> The service lifetime. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddMiddleware<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TMiddleware>(
		this IServiceCollection services,
		ServiceLifetime lifetime = ServiceLifetime.Scoped)
		where TMiddleware : class, IDispatchMiddleware
	{
		ArgumentNullException.ThrowIfNull(services);
		services.Add(new ServiceDescriptor(typeof(IDispatchMiddleware), typeof(TMiddleware), lifetime));
		services.Add(new ServiceDescriptor(typeof(TMiddleware), typeof(TMiddleware), lifetime));
		return services;
	}

	/// <summary>
	/// Adds default Dispatch pipelines with common configurations.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddDefaultDispatchPipelines(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Register default middleware components
		_ = RegisterDefaultMiddleware(services);

		return services.AddDispatchWithInfrastructure(static builder =>
		{
			// Configure default pipeline
			_ = builder.ConfigurePipeline("Default", static pipeline => pipeline.ForMessageKinds(MessageKinds.All));

			// Configure strict pipeline for commands
			_ = builder.ConfigurePipeline("Strict", static pipeline => pipeline.UseProfile("Strict"));

			// Configure lightweight pipeline for events
			_ = builder.ConfigurePipeline("Events", static pipeline => pipeline.UseProfile("InternalEvent"));
		});
	}

	/// <summary>
	/// Adds Dispatch with full durability (persistence enabled).
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configure"> Optional additional configuration. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddDispatchWithDurability(
		this IServiceCollection services,
		Action<IDispatchBuilder>? configure = null) =>
		services.AddDispatchWithInfrastructure(builder =>
		{
			// Configure for durability
			_ = builder.ConfigureOptions<DispatchOptions>(options =>
			{
				options.UseLightMode = false;
				options.Features.ValidateMessageSchemas = true;
			});

			// Configure for full durability using new syntax
			_ = builder.WithOptions(options =>
			{
				options.Inbox.Enabled = true; // Use full inbox mode
				options.Consumer.Dedupe.Enabled = false; // Disable deduplication when inbox enabled
				options.Consumer.AckAfterHandle = true;
				options.Outbox.BatchSize = 100;
				options.Outbox.PublishIntervalMs = 1000;
				options.Outbox.UseInMemoryStorage = false;
				options.Outbox.MaxRetries = 10;
				options.Outbox.SentMessageRetention = TimeSpan.FromDays(7);
			});

			// Apply additional configuration if provided
			configure?.Invoke(builder);
		});

	/// <summary>
	/// Decorates the registered <see cref="IMessageBus"/> with automatic version upcasting.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// This method registers the <see cref="UpcastingMessageBusDecorator"/> which intercepts
	/// incoming integration events and upcasts them to the latest registered version before
	/// delivery to handlers.
	/// </para>
	/// <para>
	/// <b>Prerequisites:</b> This method requires that <see cref="IUpcastingPipeline"/> and
	/// <see cref="IMessageBus"/> are already registered in the service collection. If
	/// <see cref="IUpcastingPipeline"/> is not registered, this method does nothing.
	/// </para>
	/// <para>
	/// <b>Usage:</b> Call this after <c>AddMessageUpcasting()</c> and <c>AddDispatch()</c>:
	/// <code>
	/// services.AddMessageUpcasting(builder => { ... });
	/// services.AddDispatch(builder => { ... });
	/// services.AddUpcastingMessageBusDecorator();
	/// </code>
	/// </para>
	/// </remarks>
	/// <seealso cref="UpcastingMessageBusDecorator"/>
	/// <seealso cref="IUpcastingPipeline"/>
	public static IServiceCollection AddUpcastingMessageBusDecorator(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Only decorate if IUpcastingPipeline is registered
		if (!services.Any(s => s.ServiceType == typeof(IUpcastingPipeline)))
		{
			return services;
		}

		if (services.Any(s => s.ServiceType == typeof(UpcastingMessageBusDecoratorMarker)))
		{
			return services;
		}

		var descriptors = services
			.Where(static s => s.ServiceType == typeof(IMessageBus))
			.ToList();

		if (descriptors.Count == 0)
		{
			return services;
		}

		foreach (var descriptor in descriptors)
		{
			_ = services.Remove(descriptor);
			services.Add(CreateUpcastingDescriptor(descriptor));
		}

		_ = services.AddSingleton<UpcastingMessageBusDecoratorMarker>();

		return services;
	}

	/// <summary>
	/// Registers default middleware components required for message processing.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <returns> The service collection for chaining. </returns>
	private static IServiceCollection RegisterDefaultMiddleware(IServiceCollection services)
	{
		// Register transport router middleware (R3.1)
		services.TryAddScoped<TransportRouterMiddleware>();

		// Register Inbox middleware and its dependencies (R4)
		services.TryAddScoped<InboxMiddleware>();
		services.TryAddSingleton<IInMemoryDeduplicator, InMemoryDeduplicator>();

		// Register Outbox middleware and its dependencies (R5)
		services.TryAddScoped<OutboxStagingMiddleware>();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<Excalibur.Dispatch.Options.Middleware.OutboxOptions>,
				Excalibur.Dispatch.Options.Middleware.OutboxOptionsValidator>());

		// Configure default pipeline synthesizer to include middleware in proper order
		services.TryAddSingleton<IDefaultPipelineSynthesizer>(static sp =>
		{
			var profileRegistry = sp.GetRequiredService<IPipelineProfileRegistry>();
			var synthesizer = new DefaultPipelineSynthesizer(profileRegistry);

			// Register middleware in canonical order (R7.10)
			synthesizer.RegisterMiddleware(
				typeof(TransportRouterMiddleware),
				DispatchMiddlewareStage.Routing,
				priority: 100,
				MessageKinds.All);

			synthesizer.RegisterMiddleware(
				typeof(InboxMiddleware),
				DispatchMiddlewareStage.PreProcessing,
				priority: 50,
				MessageKinds.All);

			synthesizer.RegisterMiddleware(
				typeof(OutboxStagingMiddleware),
				DispatchMiddlewareStage.PostProcessing,
				priority: 100,
				MessageKinds.All);

			return synthesizer;
		});

		return services;
	}

	private static ServiceDescriptor CreateUpcastingDescriptor(ServiceDescriptor descriptor)
	{
		if (descriptor.ServiceKey is not null)
		{
			return new ServiceDescriptor(
				descriptor.ServiceType,
				descriptor.ServiceKey,
				(sp, _) => new UpcastingMessageBusDecorator(
					CreateInnerMessageBus(sp, descriptor),
					sp.GetRequiredService<IUpcastingPipeline>()),
				descriptor.Lifetime);
		}

		return ServiceDescriptor.Describe(
			descriptor.ServiceType,
			sp => new UpcastingMessageBusDecorator(
				CreateInnerMessageBus(sp, descriptor),
				sp.GetRequiredService<IUpcastingPipeline>()),
			descriptor.Lifetime);
	}

	private static IMessageBus CreateInnerMessageBus(
		IServiceProvider serviceProvider,
		ServiceDescriptor descriptor)
	{
		if (descriptor.ServiceKey is not null)
		{
			if (descriptor.KeyedImplementationInstance is IMessageBus keyedInstance)
			{
				return keyedInstance;
			}

			if (descriptor.ImplementationInstance is IMessageBus implementationInstance)
			{
				return implementationInstance;
			}

			if (descriptor.KeyedImplementationFactory is not null)
			{
				return (IMessageBus)descriptor.KeyedImplementationFactory(
					serviceProvider,
					descriptor.ServiceKey);
			}

			if (descriptor.ImplementationFactory is not null)
			{
				return (IMessageBus)descriptor.ImplementationFactory(serviceProvider);
			}

			if (descriptor.KeyedImplementationType is not null)
			{
				return (IMessageBus)ActivatorUtilities.CreateInstance(
					serviceProvider,
					descriptor.KeyedImplementationType);
			}
		}
		else if (descriptor.ImplementationFactory is not null)
		{
			return (IMessageBus)descriptor.ImplementationFactory(serviceProvider);
		}

		if (descriptor.ImplementationInstance is IMessageBus instance)
		{
			return instance;
		}

		if (descriptor.ImplementationType is not null)
		{
			return (IMessageBus)ActivatorUtilities.CreateInstance(
				serviceProvider,
				descriptor.ImplementationType);
		}

		throw new InvalidOperationException(
			Excalibur.Dispatch.Resources.DispatchConfigurationServiceCollectionExtensions_MessageBusRegistrationMissingImplementation);
	}

	private sealed class UpcastingMessageBusDecoratorMarker;
}
