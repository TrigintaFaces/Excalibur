// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Excalibur.Data.DataProcessing;

/// <summary>
/// Provides mechanisms to discover and retrieve metadata about record handlers in specified assemblies.
/// </summary>
internal static class RecordHandlerDiscovery
{
	/// <summary>
	/// Discovers all types implementing the <see cref="IRecordHandler{T}" /> interface within the specified assemblies.
	/// </summary>
	/// <param name="assembliesToScan"> A collection of assemblies to scan for record handler types. </param>
	/// <returns> An enumerable of tuples containing the interface type and implementation type of discovered record handlers. </returns>
	/// <remarks>
	/// Uses Assembly.GetTypes() and GetInterfaces() which require reflection. In AOT scenarios, consider using
	/// source generators or explicit type registration instead of assembly scanning.
	/// </remarks>
	[RequiresUnreferencedCode("Assembly scanning uses reflection to discover types implementing IRecordHandler<T>")]
	internal static IEnumerable<(Type InterfaceType, Type ImplementationType)> DiscoverHandlers(IEnumerable<Assembly> assembliesToScan) =>
		assembliesToScan.SelectMany(assembly =>
			assembly.GetTypes()
				.Where(type => type is { IsClass: true, IsAbstract: false, IsInterface: false }) // Ensure it's a concrete class
				.SelectMany(type => type.GetInterfaces()
					.Where(interfaceType =>
						interfaceType.IsGenericType &&
						interfaceType.GetGenericTypeDefinition() == typeof(IRecordHandler<>)) // Match IRecordHandler<T>
					.Select(interfaceType => (InterfaceType: interfaceType, ImplementationType: type))));

	/// <summary>
	/// Tries to retrieve the record type associated with a record handler implementation.
	/// </summary>
	/// <param name="handlerType"> The <see cref="Type" /> of the record handler to inspect. </param>
	/// <param name="recordType"> When this method returns, contains the record type if found, or <c> null </c> if not found. </param>
	/// <returns> <c> true </c> if a record type is found; otherwise, <c> false </c>. </returns>
	/// <remarks>
	/// Uses GetInterfaces() and GetGenericTypeDefinition() which require reflection for interface inspection.
	/// </remarks>
	[RequiresUnreferencedCode("Uses reflection to inspect interfaces and generic type definitions")]
	internal static bool TryGetRecordType(Type handlerType, [NotNullWhen(true)] out Type? recordType)
	{
		// Check if the type implements IRecordHandler<T>
		var handlerInterface = handlerType
			.GetInterfaces()
			.FirstOrDefault(static interfaceType =>
				interfaceType.IsGenericType &&
				interfaceType.GetGenericTypeDefinition() == typeof(IRecordHandler<>));

		if (handlerInterface == null)
		{
			recordType = null;
			return false;
		}

		// Extract the generic argument (T) from IRecordHandler<T>
		recordType = handlerInterface.GetGenericArguments().FirstOrDefault();
		return recordType != null;
	}
}
