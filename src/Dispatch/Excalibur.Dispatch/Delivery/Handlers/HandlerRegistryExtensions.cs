// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Excalibur.Dispatch.Abstractions.Delivery;

namespace Excalibur.Dispatch.Delivery.Handlers;

/// <summary>
/// Provides extension methods for <see cref="IHandlerRegistry" /> to simplify handler registration and discovery operations. These
/// extensions enable bulk registration of handlers from assemblies and other common registration patterns used in message-driven architectures.
/// </summary>
/// <remarks>
/// The extension methods use reflection to discover and register handler implementations, supporting various handler interfaces including
/// action handlers, event handlers, and document handlers. These utilities are particularly useful during application startup when
/// configuring the message processing pipeline with handlers from multiple assemblies or modules.
/// </remarks>
public static class HandlerRegistryExtensions
{
	/// <summary>
	/// Automatically discovers and registers all handler implementations found in the specified assemblies. This method scans for classes
	/// implementing handler interfaces and registers them with appropriate response expectations based on their interface signatures.
	/// </summary>
	/// <param name="registry"> The handler registry to register discovered handlers with. </param>
	/// <param name="assemblies"> The collection of assemblies to scan for handler implementations. </param>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="registry" /> is null. </exception>
	/// <remarks>
	/// <para>
	/// This method discovers and registers handlers for the following interface types:
	/// - <see cref="IActionHandler{T}" /> - Commands without response
	/// - <see cref="IActionHandler{T, TResult}" /> - Commands with response (queries)
	/// - <see cref="IEventHandler{T}" /> - Event handlers
	/// - <see cref="IDocumentHandler{T}" /> - Document processors
	/// </para>
	/// <para>
	/// The registration process automatically determines response expectations based on interface signatures, enabling proper CQRS pattern
	/// support. Abstract classes and interfaces are excluded from registration. This method requires reflection access and may impact AOT
	/// compilation scenarios.
	/// </para>
	/// </remarks>
	[RequiresUnreferencedCode("Uses reflection to scan assemblies for handler implementations")]
	public static void RegisterHandlersFromAssemblies(this IHandlerRegistry registry, IEnumerable<Assembly> assemblies)
	{
		ArgumentNullException.ThrowIfNull(registry);

		foreach (var type in assemblies.SelectMany(static a => a.GetTypes()))
		{
			if (type.IsAbstract || type.IsInterface)
			{
				continue;
			}

			foreach (var iface in type.GetInterfaces())
			{
				if (!iface.IsGenericType)
				{
					continue;
				}

				if (iface.GetGenericTypeDefinition() == typeof(IActionHandler<>))
				{
					var messageType = iface.GetGenericArguments()[0];
					registry.Register(messageType, type, expectsResponse: false);
				}
				else if (iface.GetGenericTypeDefinition() == typeof(IActionHandler<,>))
				{
					var messageType = iface.GetGenericArguments()[0];
					registry.Register(messageType, type, expectsResponse: true);
				}
				else if (iface.GetGenericTypeDefinition() == typeof(IEventHandler<>) ||
						 iface.GetGenericTypeDefinition() == typeof(IDocumentHandler<>))
				{
					var messageType = iface.GetGenericArguments()[0];
					registry.Register(messageType, type, expectsResponse: false);
				}
			}
		}
	}
}
