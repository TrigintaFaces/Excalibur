using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Excalibur.DataAccess.DataProcessing;

/// <summary>
///     Provides mechanisms to discover and retrieve metadata about data processors in specified assemblies.
/// </summary>
internal static class DataProcessorDiscovery
{
	/// <summary>
	///     Discovers all types implementing the <see cref="IDataProcessor" /> interface within the specified assemblies.
	/// </summary>
	/// <param name="assembliesToScan"> A collection of assemblies to scan for data processor types. </param>
	/// <returns>
	///     An enumerable of <see cref="Type" /> objects representing the discovered data processor types that implement <see cref="IDataProcessor" />.
	/// </returns>
	internal static IEnumerable<Type> DiscoverProcessors(IEnumerable<Assembly> assembliesToScan) => assembliesToScan.SelectMany(assembly =>
		assembly
			.GetTypes()
			.Where(t => t is { IsClass: true, IsAbstract: false, IsInterface: false } && typeof(IDataProcessor).IsAssignableFrom(t)));

	/// <summary>
	///     Tries to retrieve the record type name associated with a data processor type, either from an attribute or a public property.
	/// </summary>
	/// <param name="processorType"> The <see cref="Type" /> of the processor to inspect. </param>
	/// <param name="recordType"> When this method returns, contains the record type name if found, or <c> null </c> if not. </param>
	/// <returns> <c> true </c> if a record type name is found; otherwise, <c> false </c>. </returns>
	/// <exception cref="InvalidOperationException"> Thrown if the <paramref name="processorType" /> cannot be instantiated. </exception>
	internal static bool TryGetRecordType(
		Type processorType,
		[NotNullWhen(true)] out string? recordType)
	{
		// Check for the presence of the DataTaskRecordTypeAttribute on the type
		if (processorType
				.GetCustomAttributes(typeof(DataTaskRecordTypeAttribute), false)
				.FirstOrDefault() is DataTaskRecordTypeAttribute attr)
		{
			recordType = attr.RecordTypeName;
			return true;
		}

		// Check for a public string property named "RecordType"
		var property = processorType.GetProperty("RecordType", BindingFlags.Public | BindingFlags.Instance);

		if (property == null || property.PropertyType != typeof(string))
		{
			recordType = null;
			return false;
		}

		// Attempt to create an instance of the processor type
		if (Activator.CreateInstance(processorType) is not { } instance)
		{
			throw new InvalidOperationException($"Could not create an instance of {processorType.FullName}.");
		}

		// Retrieve the value of the "RecordType" property
		recordType = property.GetValue(instance) as string;
		return !string.IsNullOrEmpty(recordType);
	}
}
