// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Data.DataProcessing;

/// <summary>
/// Extension methods for <see cref="IDataProcessingBuilder"/> that provide assembly-scanning
/// registration for data processors and record handlers.
/// </summary>
/// <remarks>
/// These extension methods use reflection to discover types. For AOT-safe registration,
/// use the explicit generic methods <see cref="IDataProcessingBuilder.AddProcessor{TProcessor}"/>
/// and <see cref="IDataProcessingBuilder.AddRecordHandler{THandler,TRecord}"/> instead.
/// </remarks>
public static class DataProcessingBuilderExtensions
{
	/// <summary>
	/// Scans the specified assembly for types implementing <see cref="IDataProcessor"/>
	/// and registers them with the DI container.
	/// </summary>
	/// <param name="builder">The data processing builder.</param>
	/// <param name="assembly">The assembly to scan for data processor implementations.</param>
	/// <param name="lifetime">
	/// The service lifetime for discovered processors. Defaults to <see cref="ServiceLifetime.Scoped"/>.
	/// </param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// This method discovers all concrete, non-abstract classes that implement
	/// <see cref="IDataProcessor"/> in the given assembly and registers them with
	/// the DI container using both the concrete type and the interface.
	/// </para>
	/// <para>
	/// For AOT-safe registration, use <see cref="IDataProcessingBuilder.AddProcessor{TProcessor}"/>
	/// with explicit type parameters instead.
	/// </para>
	/// </remarks>
	[RequiresUnreferencedCode("Assembly scanning uses reflection to discover data processor types.")]
	public static IDataProcessingBuilder AddProcessorsFromAssembly(
		this IDataProcessingBuilder builder,
		Assembly assembly,
		ServiceLifetime lifetime = ServiceLifetime.Scoped)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(assembly);

		if (builder is not DataProcessingBuilder concreteBuilder)
		{
			throw new InvalidOperationException(
				$"AddProcessorsFromAssembly requires the builder to be of type {nameof(DataProcessingBuilder)}.");
		}

		var processorTypes = assembly.GetTypes()
			.Where(static t => t is { IsClass: true, IsAbstract: false }
				&& typeof(IDataProcessor).IsAssignableFrom(t));

		foreach (var processorType in processorTypes)
		{
			// Register both concrete type and interface (same pattern as AddProcessor<T>)
			concreteBuilder.Services.Add(new ServiceDescriptor(processorType, processorType, lifetime));
			concreteBuilder.Services.Add(new ServiceDescriptor(
				typeof(IDataProcessor), sp => sp.GetRequiredService(processorType), lifetime));
		}

		return builder;
	}

	/// <summary>
	/// Scans the specified assembly for types implementing <see cref="IRecordHandler{TRecord}"/>
	/// and registers them with the DI container.
	/// </summary>
	/// <param name="builder">The data processing builder.</param>
	/// <param name="assembly">The assembly to scan for record handler implementations.</param>
	/// <param name="lifetime">
	/// The service lifetime for discovered handlers. Defaults to <see cref="ServiceLifetime.Scoped"/>.
	/// </param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// This method discovers all concrete, non-abstract classes that implement any closed
	/// generic form of <see cref="IRecordHandler{TRecord}"/> in the given assembly and
	/// registers them with the DI container.
	/// </para>
	/// <para>
	/// For AOT-safe registration, use
	/// <see cref="IDataProcessingBuilder.AddRecordHandler{THandler,TRecord}"/>
	/// with explicit type parameters instead.
	/// </para>
	/// </remarks>
	[RequiresUnreferencedCode("Assembly scanning uses reflection to discover record handler types.")]
	public static IDataProcessingBuilder AddRecordHandlersFromAssembly(
		this IDataProcessingBuilder builder,
		Assembly assembly,
		ServiceLifetime lifetime = ServiceLifetime.Scoped)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(assembly);

		if (builder is not DataProcessingBuilder concreteBuilder)
		{
			throw new InvalidOperationException(
				$"AddRecordHandlersFromAssembly requires the builder to be of type {nameof(DataProcessingBuilder)}.");
		}

		var handlerTypes = assembly.GetTypes()
			.Where(static t => t is { IsClass: true, IsAbstract: false }
				&& t.GetInterfaces().Any(static i =>
					i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRecordHandler<>)));

		foreach (var handlerType in handlerTypes)
		{
			// Find all IRecordHandler<TRecord> interfaces this type implements
			var recordHandlerInterfaces = handlerType.GetInterfaces()
				.Where(static i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRecordHandler<>));

			foreach (var handlerInterface in recordHandlerInterfaces)
			{
				concreteBuilder.Services.Add(new ServiceDescriptor(handlerInterface, handlerType, lifetime));
			}
		}

		return builder;
	}
}
