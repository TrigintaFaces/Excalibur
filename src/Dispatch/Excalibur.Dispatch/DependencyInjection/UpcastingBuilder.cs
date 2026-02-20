// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Excalibur.Dispatch.Abstractions;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Versioning;

/// <summary>
/// Fluent builder for configuring message upcasting services.
/// </summary>
/// <remarks>
/// <para>
/// This builder collects upcaster registration actions during configuration.
/// The actions are executed when the <see cref="IUpcastingPipeline"/> singleton is created,
/// avoiding the anti-pattern of calling <c>BuildServiceProvider()</c> during configuration.
/// </para>
/// <para>
/// <b>Usage:</b>
/// <code>
/// services.AddMessageUpcasting(builder =>
/// {
///     builder.RegisterUpcaster&lt;UserEventV1, UserEventV2&gt;(new UserEventV1ToV2());
///     builder.ScanAssembly(typeof(Program).Assembly);
/// });
/// </code>
/// </para>
/// </remarks>
public sealed class UpcastingBuilder
{
	private readonly UpcastingOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="UpcastingBuilder"/> class.
	/// </summary>
	/// <param name="options">The options to configure.</param>
	internal UpcastingBuilder(UpcastingOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	/// <summary>
	/// Registers an upcaster instance for a specific version transition.
	/// </summary>
	/// <typeparam name="TOld">The old message type.</typeparam>
	/// <typeparam name="TNew">The new message type.</typeparam>
	/// <param name="upcaster">The upcaster instance.</param>
	/// <returns>The builder for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// Use this method when the upcaster has no dependencies and can be created directly.
	/// </para>
	/// <para>
	/// <b>Example:</b>
	/// <code>
	/// builder.RegisterUpcaster&lt;UserEventV1, UserEventV2&gt;(new UserEventV1ToV2());
	/// </code>
	/// </para>
	/// </remarks>
	public UpcastingBuilder RegisterUpcaster<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TOld, TNew>(
		IMessageUpcaster<TOld, TNew> upcaster)
		where TOld : IDispatchMessage, IVersionedMessage
		where TNew : IDispatchMessage, IVersionedMessage
	{
		ArgumentNullException.ThrowIfNull(upcaster);

		_options.AddRegistration(pipeline => pipeline.Register(upcaster));
		return this;
	}

	/// <summary>
	/// Registers an upcaster using a factory function that receives the service provider.
	/// </summary>
	/// <typeparam name="TOld">The old message type.</typeparam>
	/// <typeparam name="TNew">The new message type.</typeparam>
	/// <param name="factory">Factory function that creates the upcaster using DI services.</param>
	/// <returns>The builder for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// Use this method when the upcaster has dependencies that need to be resolved from DI.
	/// </para>
	/// <para>
	/// <b>Example:</b>
	/// <code>
	/// builder.RegisterUpcaster&lt;UserEventV1, UserEventV2&gt;(sp =>
	///     new UserEventV1ToV2(sp.GetRequiredService&lt;ILogger&gt;()));
	/// </code>
	/// </para>
	/// </remarks>
	public UpcastingBuilder RegisterUpcaster<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TOld, TNew>(
		Func<IServiceProvider, IMessageUpcaster<TOld, TNew>> factory)
		where TOld : IDispatchMessage, IVersionedMessage
		where TNew : IDispatchMessage, IVersionedMessage
	{
		ArgumentNullException.ThrowIfNull(factory);

		_options.AddRegistration((pipeline, sp) =>
		{
			var upcaster = factory(sp);
			pipeline.Register(upcaster);
		});
		return this;
	}

	/// <summary>
	/// Scans an assembly for all types implementing <see cref="IMessageUpcaster{TOld,TNew}"/>
	/// and registers them with the pipeline.
	/// </summary>
	/// <param name="assembly">The assembly to scan.</param>
	/// <param name="filter">Optional filter to exclude certain types. Return true to include, false to exclude.</param>
	/// <returns>The builder for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// This method discovers all concrete types implementing <see cref="IMessageUpcaster{TOld,TNew}"/>
	/// and creates instances using either their parameterless constructor or DI resolution.
	/// </para>
	/// <para>
	/// <b>Example:</b>
	/// <code>
	/// // Scan all upcasters in the assembly
	/// builder.ScanAssembly(typeof(Program).Assembly);
	///
	/// // Scan with filter
	/// builder.ScanAssembly(typeof(Program).Assembly, t => !t.Name.Contains("Test"));
	/// </code>
	/// </para>
	/// </remarks>
	[RequiresUnreferencedCode("Assembly scanning uses reflection to discover upcaster types.")]
	[RequiresDynamicCode("Assembly scanning registers generic upcasters at runtime.")]
	public UpcastingBuilder ScanAssembly(Assembly assembly, Func<Type, bool>? filter = null)
	{
		ArgumentNullException.ThrowIfNull(assembly);

		_options.AddRegistration((pipeline, sp) =>
		{
			var upcasterTypes = DiscoverUpcasterTypes(assembly, filter);
			foreach (var upcasterType in upcasterTypes)
			{
				RegisterDiscoveredUpcaster(pipeline, sp, upcasterType);
			}
		});
		return this;
	}

	/// <summary>
	/// Scans multiple assemblies for upcaster types.
	/// </summary>
	/// <param name="assemblies">The assemblies to scan.</param>
	/// <param name="filter">Optional filter to exclude certain types.</param>
	/// <returns>The builder for method chaining.</returns>
	[RequiresUnreferencedCode("Assembly scanning uses reflection to discover upcaster types.")]
	[RequiresDynamicCode("Assembly scanning registers generic upcasters at runtime.")]
	public UpcastingBuilder ScanAssemblies(IEnumerable<Assembly> assemblies, Func<Type, bool>? filter = null)
	{
		ArgumentNullException.ThrowIfNull(assemblies);

		foreach (var assembly in assemblies)
		{
			_ = ScanAssembly(assembly, filter);
		}

		return this;
	}

	/// <summary>
	/// Enables or disables automatic upcasting during event store replay.
	/// </summary>
	/// <param name="enable">True to enable, false to disable.</param>
	/// <returns>The builder for method chaining.</returns>
	/// <remarks>
	/// When enabled, events loaded from the event store will be automatically upcasted
	/// to their latest version before being applied to aggregates.
	/// </remarks>
	public UpcastingBuilder EnableAutoUpcastOnReplay(bool enable = true)
	{
		_options.EnableAutoUpcastOnReplay = enable;
		return this;
	}

	/// <summary>
	/// Discovers all types implementing IMessageUpcaster in an assembly.
	/// </summary>
	[RequiresUnreferencedCode("Uses reflection to discover types.")]
	private static IEnumerable<Type> DiscoverUpcasterTypes(Assembly assembly, Func<Type, bool>? filter)
	{
		var upcasterInterface = typeof(IMessageUpcaster<,>);

		foreach (var type in assembly.GetTypes())
		{
			// Skip abstract, interface, and generic type definitions
			if (type.IsAbstract || type.IsInterface || type.IsGenericTypeDefinition)
			{
				continue;
			}

			// Check if type implements IMessageUpcaster<,>
			var implementsUpcaster = type.GetInterfaces()
				.Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == upcasterInterface);

			if (!implementsUpcaster)
			{
				continue;
			}

			// Apply filter if provided
			if (filter != null && !filter(type))
			{
				continue;
			}

			yield return type;
		}
	}

	/// <summary>
	/// Registers a discovered upcaster type with the pipeline.
	/// </summary>
	[RequiresUnreferencedCode("Uses reflection to create instances and call generic methods.")]
	[RequiresDynamicCode("Uses reflection to construct generic methods at runtime.")]
	private static void RegisterDiscoveredUpcaster(IUpcastingPipeline pipeline, IServiceProvider sp, Type upcasterType)
	{
		// Find the IMessageUpcaster<TOld, TNew> interface to get type arguments
		var upcasterInterface = upcasterType.GetInterfaces()
			.FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IMessageUpcaster<,>));

		if (upcasterInterface == null)
		{
			return;
		}

		var typeArgs = upcasterInterface.GetGenericArguments();
		var oldType = typeArgs[0];
		var newType = typeArgs[1];

		// Create instance using DI or parameterless constructor
		object upcasterInstance;
		try
		{
			// Try DI first (supports constructor injection)
			upcasterInstance = ActivatorUtilities.CreateInstance(sp, upcasterType);
		}
		catch
		{
			// Fall back to parameterless constructor
			var constructor = upcasterType.GetConstructor(Type.EmptyTypes)
							  ?? throw new InvalidOperationException(
								  $"Upcaster type {upcasterType.Name} has no parameterless constructor and could not be " +
								  "resolved from DI. Either add a parameterless constructor or register the type with DI.");
			upcasterInstance = constructor.Invoke(null);
		}

		// Call pipeline.Register<TOld, TNew>(upcaster) via reflection
		var registerMethod = typeof(IUpcastingPipeline)
			.GetMethod(nameof(IUpcastingPipeline.Register))
			.MakeGenericMethod(oldType, newType);

		_ = registerMethod.Invoke(pipeline, [upcasterInstance]);
	}
}
