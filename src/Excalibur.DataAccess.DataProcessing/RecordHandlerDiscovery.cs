using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Excalibur.DataAccess.DataProcessing;

/// <summary>
///     Provides mechanisms to discover and retrieve metadata about record handlers in specified assemblies.
/// </summary>
internal static class RecordHandlerDiscovery
{
	/// <summary>
	///     Discovers all types implementing the <see cref="IRecordHandler{T}" /> interface within the specified assemblies.
	/// </summary>
	/// <param name="assembliesToScan"> A collection of assemblies to scan for record handler types. </param>
	/// <returns> An enumerable of tuples containing the interface type and implementation type of discovered record handlers. </returns>
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
	///     Tries to retrieve the record type associated with a record handler implementation.
	/// </summary>
	/// <param name="handlerType"> The <see cref="Type" /> of the record handler to inspect. </param>
	/// <param name="recordType"> When this method returns, contains the record type if found, or <c> null </c> if not. </param>
	/// <returns> <c> true </c> if a record type is found; otherwise, <c> false </c>. </returns>
	internal static bool TryGetRecordType(Type handlerType, [NotNullWhen(true)] out Type? recordType)
	{
		// Check if the type implements IRecordHandler<T>
		var handlerInterface = handlerType
			.GetInterfaces()
			.FirstOrDefault(interfaceType =>
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
