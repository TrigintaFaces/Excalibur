// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Abstractions.Delivery;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
	/// Add handlers from the specified assembly with automatic discovery and DI registration (R7.2).
	/// </summary>
	/// <param name="builder"> The dispatch builder. </param>
	/// <param name="assembly"> Assembly to scan for handlers. </param>
	/// <param name="lifetime"> Service lifetime for registered handlers. Default is <see cref="ServiceLifetime.Scoped"/>. </param>
	/// <param name="registerWithContainer"> Whether to register handlers with the DI container. Default is <c>true</c>. </param>
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
	/// handler registration separately.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// // Default: registers handlers with Scoped lifetime
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
	/// // Skip DI registration (advanced scenarios)
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
		ServiceLifetime lifetime = ServiceLifetime.Scoped,
		bool registerWithContainer = true)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(assembly);

		// Scan for all handler interface implementations
		var handlerTypes = assembly.GetTypes()
			.Where(static type => type is { IsAbstract: false, IsInterface: false } &&
								  type.GetInterfaces()
									  .Any(static i => i.IsGenericType &&
													   HandlerInterfaceTypes.Contains(i.GetGenericTypeDefinition())));

		foreach (var handlerType in handlerTypes)
		{
			var interfaces = handlerType.GetInterfaces()
				.Where(static i => i.IsGenericType &&
								   HandlerInterfaceTypes.Contains(i.GetGenericTypeDefinition()));

			if (registerWithContainer)
			{
				// Register the handler type itself so DI can resolve it
				builder.Services.TryAdd(new ServiceDescriptor(handlerType, handlerType, lifetime));
			}

			// Register each handler interface
			foreach (var @interface in interfaces)
			{
				builder.Services.Add(new ServiceDescriptor(@interface, handlerType, lifetime));
			}
		}

		return builder;
	}

}
