// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Excalibur.Dispatch.Delivery;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Excalibur.Dispatch.Extensions;

namespace Excalibur.Dispatch.Configuration;

/// <summary>
/// Extension methods for pipeline configuration, handler registration, and assembly scanning.
/// </summary>
public static class DispatchBuilderExtensions
{
	/// <summary>
	/// Add a pipeline profile with specified middleware order and message kind filtering.
	/// </summary>
	/// <param name="builder"> The dispatch builder. </param>
	/// <param name="profileName"> Name of the pipeline profile. </param>
	/// <param name="configure"> Pipeline configuration action. </param>
	/// <returns> The dispatch builder for chaining. </returns>
	public static IDispatchBuilder AddPipeline(this IDispatchBuilder builder, string profileName,
		Action<IPipelineBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentException.ThrowIfNullOrWhiteSpace(profileName);
		ArgumentNullException.ThrowIfNull(configure);

		return builder.ConfigurePipeline(profileName, configure);
	}

	/// <summary>
	/// Handler interface types that are scanned for automatic registration.
	/// </summary>
	private static readonly Type[] HandlerInterfaceTypes =
	[
		typeof(IDispatchHandler<>),
		typeof(IActionHandler<>),
		typeof(IActionHandler<,>),
		typeof(IEventHandler<>),
		typeof(IDocumentHandler<>),
		typeof(IStreamingDocumentHandler<,>),
		typeof(IStreamConsumerHandler<>),
		typeof(IStreamTransformHandler<,>),
		typeof(IProgressDocumentHandler<>),
	];

	/// <summary>
	/// Add handlers from the specified assembly with automatic discovery and DI registration.
	/// </summary>
	/// <param name="builder"> The dispatch builder. </param>
	/// <param name="assembly"> Assembly to scan for handlers. </param>
	/// <param name="lifetime"> Service lifetime for registered handlers. Default is <see cref="ServiceLifetime.Transient"/>. </param>
	/// <param name="registerWithContainer">
	/// Whether to register the discovered handlers with the DI container. Default is <c>true</c>. When <c>false</c>, nothing from
	/// <paramref name="assembly"/> is registered -- neither the concrete handler types nor their handler interfaces -- and the caller is
	/// responsible for registering every handler it wants dispatched.
	/// </param>
	/// <returns> The dispatch builder for chaining. </returns>
	/// <remarks>
	/// <para>
	/// This method scans the assembly for all handler implementations and registers them:
	/// </para>
	/// <list type="bullet">
	/// <item><description><see cref="IDispatchHandler{TMessage}"/> - General dispatch handlers</description></item>
	/// <item><description><see cref="IActionHandler{TAction}"/> and <see cref="IActionHandler{TAction, TResponse}"/> - Action handlers</description></item>
	/// <item><description><see cref="IEventHandler{TEvent}"/> - Event handlers</description></item>
	/// <item><description><see cref="IDocumentHandler{TDocument}"/> - Document handlers</description></item>
	/// <item><description><see cref="IStreamingDocumentHandler{TDocument, TOutput}"/> - Streaming output handlers</description></item>
	/// <item><description><see cref="IStreamConsumerHandler{TDocument}"/> - Stream consumer handlers</description></item>
	/// <item><description><see cref="IStreamTransformHandler{TInput, TOutput}"/> - Stream transform handlers</description></item>
	/// <item><description><see cref="IProgressDocumentHandler{TDocument}"/> - Progress-reporting handlers</description></item>
	/// </list>
	/// <para>
	/// By default, handlers are registered with the DI container so they can be resolved without explicit registration.
	/// Set <paramref name="registerWithContainer"/> to <c>false</c> for advanced scenarios where you want to control
	/// handler registration separately. The call still marks handler registration as caller-owned, so the
	/// zero-configuration scan of the entry assembly does not run.
	/// </para>
	/// <para>
	/// Scanning never displaces a handler you registered yourself, in either call order. A handler you register for a
	/// message type that expects a single handler wins over the scanned one; a handler you register for an event runs
	/// alongside the scanned handlers for that event, and is not run twice if scanning also discovers it.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// // Default: registers handlers with Transient lifetime
	/// services.AddDispatch(dispatch =>
	/// {
	///     dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
	/// });
	///
	/// // Custom lifetime
	/// services.AddDispatch(dispatch =>
	/// {
	///     dispatch.AddHandlersFromAssembly(typeof(Program).Assembly, ServiceLifetime.Transient);
	/// });
	///
	/// // Register nothing from this assembly -- you register every handler yourself (advanced scenarios)
	/// services.AddDispatch(dispatch =>
	/// {
	///     dispatch.AddHandlersFromAssembly(typeof(Program).Assembly, registerWithContainer: false);
	/// });
	/// </code>
	/// </example>
	[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with RequiresUnreferencedCodeAttribute may break with trimming",
		Justification = "Handler types are preserved through assembly scanning and DI registration")]
	[UnconditionalSuppressMessage("Trimming", "IL2070:'this' argument does not satisfy 'DynamicallyAccessedMemberTypes.Interfaces'",
		Justification = "Handler types are preserved through assembly scanning and DI registration")]
	[UnconditionalSuppressMessage(
		"Trimming",
		"IL2072:'target parameter' argument does not satisfy 'DynamicallyAccessedMemberTypes.PublicConstructors'",
		Justification = "Handler types are preserved through assembly scanning and DI registration")]
	[UnconditionalSuppressMessage("Trimming", "IL2075:'this' argument does not satisfy 'DynamicallyAccessedMemberTypes.Interfaces'",
		Justification = "Handler types are preserved through assembly scanning and DI registration")]
	public static IDispatchBuilder AddHandlersFromAssembly(
		this IDispatchBuilder builder,
		Assembly assembly,
		ServiceLifetime lifetime = ServiceLifetime.Transient,
		bool registerWithContainer = true)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(assembly);

		if (builder is DispatchBuilder concreteBuilder)
		{
			concreteBuilder.HasHandlerRegistrations = true;
		}

		if (!registerWithContainer)
		{
			// The caller owns handler registration entirely. Registering the interface descriptors
			// anyway would put scanned handlers in the container the caller just opted out of, where
			// they would compose with -- and could displace -- the registrations the caller makes
			// instead. Marking HasHandlerRegistrations above is the whole effect of the call: it
			// stops the zero-configuration entry-assembly scan from registering handlers behind the
			// caller's back.
			return builder;
		}

		// Scan for all handler interface implementations
		var handlerTypes = assembly.GetLoadableTypes()
			.Where(static type => type is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false } &&
								  type.GetInterfaces()
									  .Any(static i => i.IsGenericType &&
													   HandlerInterfaceTypes.Contains(i.GetGenericTypeDefinition())));

		foreach (var handlerType in handlerTypes)
		{
			var interfaces = handlerType.GetInterfaces()
				.Where(static i => i.IsGenericType &&
								   HandlerInterfaceTypes.Contains(i.GetGenericTypeDefinition()));

			// Register the handler type itself so DI can resolve it
			builder.Services.TryAdd(new ServiceDescriptor(handlerType, handlerType, lifetime));

			// Register each handler interface. A scanned registration must never displace one the
			// consumer made explicitly, in either call order, so neither branch below uses a plain
			// Add. Which of the two applies is decided by the same predicate HandlerRegistry.Register
			// uses to decide fan-out versus replacement, so the container and the registry cannot
			// disagree about whether a message type has one handler or many.
			foreach (var @interface in interfaces)
			{
				var descriptor = new ServiceDescriptor(@interface, handlerType, lifetime);

				if (typeof(IDispatchEvent).IsAssignableFrom(@interface.GetGenericArguments()[0]))
				{
					// Events fan out to every handler, so all distinct implementations must survive.
					// TryAddEnumerable keeps them all while collapsing a scanned duplicate of a
					// handler the consumer already registered.
					builder.Services.TryAddEnumerable(descriptor);
				}
				else
				{
					// One handler wins for everything else, and it must be the consumer's. TryAdd
					// yields to a registration made before this call; a registration made after this
					// call already wins, because the registry takes the last descriptor for a
					// non-event message type.
					builder.Services.TryAdd(descriptor);
				}
			}
		}

		return builder;
	}

	/// <summary>
	/// Adds every message handler found in the entry assembly, discovered by scanning it.
	/// </summary>
	/// <param name="builder"> The dispatch builder. </param>
	/// <returns> The dispatch builder for chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="builder"/> is null. </exception>
	/// <remarks>
	/// <para>
	/// This is the zero-configuration handler registration: a composition that names no handler of its own
	/// gets the handlers of the application that is running. It is a scan and it says so, so a trimmed or
	/// ahead-of-time compiled application must name its handlers instead.
	/// </para>
	/// <para>
	/// Handlers are registered <see cref="ServiceLifetime.Transient"/> explicitly rather than by inheriting
	/// the default of the opt-in API, because this registration is implicit: the consumer named neither a
	/// handler nor a lifetime, so what is imposed on their behalf is pinned here and does not drift with a
	/// default chosen for a different call. <see cref="ServiceLifetime.Scoped"/> in particular would not
	/// merely be a stronger claim than the consumer made, it would be a harmful one — it forces a
	/// per-dispatch scope for handlers that capture nothing, because the scope resolver short-circuits on a
	/// scoped registration before the constructor walk can prove the handler root-safe, and it hides the
	/// handler from the stateless-handler singleton promotion enabled by default, which considers only
	/// transient descriptors. Transient costs no safety: the resolver still walks the constructor graph,
	/// still demands a scope for a handler that reaches a scoped dependency directly or transitively, and
	/// still biases to a scope on any branch it cannot prove.
	/// </para>
	/// <para>
	/// A host with no entry assembly — one loaded into an unmanaged process — registers nothing here and
	/// does not fail.
	/// </para>
	/// </remarks>
	[RequiresUnreferencedCode("Scans the entry assembly for handler types, which trimming may remove. Name your handlers -- with the source-generated registration for an ahead-of-time compatible composition -- instead.")]
	[RequiresDynamicCode("Scans the entry assembly for handler types and constructs typed invokers at runtime. Name your handlers -- with the source-generated registration for an ahead-of-time compatible composition -- instead.")]
	public static IDispatchBuilder AddHandlersFromEntryAssembly(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		return Assembly.GetEntryAssembly() is { } entry
			? builder.AddHandlersFromAssembly(entry, ServiceLifetime.Transient)
			: builder;
	}

	/// <summary>
	/// Add handlers from multiple assemblies with automatic discovery and DI registration.
	/// </summary>
	/// <param name="builder"> The dispatch builder. </param>
	/// <param name="assemblies"> Assemblies to scan for handlers. </param>
	/// <returns> The dispatch builder for chaining. </returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="assemblies"/> is null,
	/// or when any element in <paramref name="assemblies"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// This overload scans multiple assemblies using the default <see cref="ServiceLifetime.Transient"/>
	/// lifetime and registers handlers with the DI container. For custom lifetime or container
	/// registration control, call the single-assembly overload per assembly.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddDispatch(dispatch =>
	/// {
	///     dispatch.AddHandlersFromAssembly(
	///         typeof(OrderHandler).Assembly,
	///         typeof(CustomerHandler).Assembly);
	/// });
	/// </code>
	/// </example>
	[RequiresUnreferencedCode("Assembly scanning uses reflection to discover handler types.")]
	public static IDispatchBuilder AddHandlersFromAssembly(
		this IDispatchBuilder builder,
		params Assembly[] assemblies)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(assemblies);

		for (var i = 0; i < assemblies.Length; i++)
		{
			if (assemblies[i] is null)
			{
				throw new ArgumentException(
					$"Assembly at index {i} is null.", nameof(assemblies));
			}

			builder.AddHandlersFromAssembly(assemblies[i]);
		}

		return builder;
	}

}
