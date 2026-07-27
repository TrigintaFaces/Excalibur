// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Middleware.Inbox;
using Excalibur.Dispatch.Middleware.Outbox;
using Excalibur.Dispatch.Middleware.Versioning;
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
	/// Adds a message handler to the service collection.
	/// </summary>
	/// <typeparam name="TMessage"> The message type. </typeparam>
	/// <typeparam name="THandler"> The handler type. </typeparam>
	/// <param name="services"> The service collection. </param>
	/// <param name="lifetime"> The service lifetime. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddHandler<TMessage,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	THandler>(
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
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TMiddleware>(
		this IServiceCollection services,
		ServiceLifetime lifetime = ServiceLifetime.Scoped)
		where TMiddleware : class, IDispatchMiddleware
	{
		ArgumentNullException.ThrowIfNull(services);
		services.Add(new ServiceDescriptor(typeof(TMiddleware), typeof(TMiddleware), lifetime));
		return services;
	}

	/// <summary>
	/// Adds Dispatch pipelines with a working, non-strict default configuration.
	/// </summary>
	/// <remarks>
	/// The default profile does not declare its security middleware as required, so the pipeline builds and
	/// dispatches out of the box without the consumer having to register authentication, authorization, or
	/// validation. Call <see cref="AddStrictDispatchPipelines"/> instead when those security controls must run
	/// and their absence should fail the build rather than be silently skipped.
	/// </remarks>
	/// <param name="services"> The service collection. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddDefaultDispatchPipelines(this IServiceCollection services) =>
		AddDispatchPipelines(services, strict: false);

	/// <summary>
	/// Adds Dispatch pipelines including the strict security profile.
	/// </summary>
	/// <remarks>
	/// The strict profile declares authentication, authorization, and validation as <b>required</b>, so it is
	/// fail-closed: if those security middleware are not registered, the pipeline build fails loudly rather than
	/// silently omitting a security control. Opt into this configuration when security middleware must run; use
	/// <see cref="AddDefaultDispatchPipelines"/> for the non-strict working default.
	/// </remarks>
	/// <param name="services"> The service collection. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddStrictDispatchPipelines(this IServiceCollection services) =>
		AddDispatchPipelines(services, strict: true);

	private static IServiceCollection AddDispatchPipelines(IServiceCollection services, bool strict)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Register default middleware components
		_ = RegisterDefaultMiddleware(services);

		return services.AddDispatchWithInfrastructure(builder =>
		{
			// Configure default pipeline. UseProfile (the constants, not string literals)
			// so selection and registration share one symbol and can never case-drift:
			// the registered profile keys are lowercase/hyphenated
			// (DefaultPipelineProfiles.Default/"default", .Strict/"strict",
			// .InternalEvent/"internal-event"). UseProfile throws on any key miss.
			_ = builder.ConfigurePipeline(
				"Default",
				static pipeline => pipeline
					.ForMessageKinds(MessageKinds.All)
					.UseProfile(DefaultPipelineProfiles.Default));

			// The strict security profile declares authentication/authorization/validation as Required, so it
			// fails the build when they are unregistered (fail-closed). It is wired only when the caller opts
			// into the strict configuration, so AddDefaultDispatchPipelines builds clean out of the box.
			if (strict)
			{
				_ = builder.ConfigurePipeline(
					"Strict",
					static pipeline => pipeline.UseProfile(DefaultPipelineProfiles.Strict));
			}

			// Configure lightweight pipeline for events
			_ = builder.ConfigurePipeline(
				"Events",
				static pipeline => pipeline.UseProfile(DefaultPipelineProfiles.InternalEvent));
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
		if (services.All(s => s.ServiceType != typeof(IUpcastingPipeline)))
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

		// Register pipeline validation at startup (T.15)
		services.AddHostedService<PipelineValidationHostedService>();

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

		// Register contract-version-check middleware and a default version service so the
		// advertised versioning control on the Default/Strict/InternalEvent profiles is actually
		// wired (UseProfile null-skips unregistered middleware, so an unregistered middleware is
		// silently inert). DefaultContractVersionService is permissive until a consumer configures
		// SupportedVersions or registers a richer IContractVersionService (both TryAdd => overridable).
		services.TryAddScoped<ContractVersionCheckMiddleware>();
		services.TryAddSingleton<IContractVersionService, DefaultContractVersionService>();

		// Register Outbox middleware and its dependencies (R5)
		services.TryAddScoped<OutboxStagingMiddleware>();

		// Cascade middleware stages handler-returned follow-up messages (ICascade) into the outbox.
		services.TryAddScoped<CascadeMiddleware>();

		// Register IOutboxWriter -- default is DeferredOutboxWriter (eventually-consistent mode).
		// TransactionalOutboxWriter is registered by Excalibur.Outbox provider extensions
		// when ConsistencyMode == Transactional.
		services.TryAddScoped<Excalibur.Dispatch.Outbox.IOutboxWriter,
			DeferredOutboxWriter>();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<Excalibur.Dispatch.Options.Middleware.OutboxStagingOptions>,
				Excalibur.Dispatch.Options.Middleware.OutboxStagingOptionsValidator>());

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
		// ybem93: all implementation members are read through the keyed-safe ServiceDescriptorExtensions
		// accessors. Each transparently returns the keyed member for a keyed descriptor and the non-keyed
		// member otherwise, so the keyed/non-keyed distinction is resolved in one sanctioned place and a
		// raw non-keyed read on a keyed descriptor (which throws on .NET 8+) cannot occur here.
		if (descriptor.GetImplementationInstance() is IMessageBus instance)
		{
			return instance;
		}

		var implementationFactory = descriptor.GetImplementationFactory();
		if (implementationFactory is not null)
		{
			return (IMessageBus)implementationFactory(serviceProvider);
		}

		var implementationType = descriptor.GetImplementationType();
		if (implementationType is not null)
		{
			// IL2072: GetImplementationType() returns Type? without DynamicallyAccessedMembers annotations
			// (limitation of Microsoft.Extensions.DependencyInjection.ServiceDescriptorExtensions).
			// This code path is only reached for types explicitly registered via typeof() in DI, which
			// guarantees their public constructors are preserved by the runtime.
#pragma warning disable IL2072
			return (IMessageBus)ActivatorUtilities.CreateInstance(serviceProvider, implementationType);
#pragma warning restore IL2072
		}

		throw new InvalidOperationException(
			Excalibur.Dispatch.Resources.DispatchConfigurationServiceCollectionExtensions_MessageBusRegistrationMissingImplementation);
	}

	private sealed class UpcastingMessageBusDecoratorMarker;
}
