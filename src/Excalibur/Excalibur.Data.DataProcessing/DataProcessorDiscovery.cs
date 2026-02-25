// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Excalibur.Data.DataProcessing;

/// <summary>
/// Provides mechanisms to discover and retrieve metadata about data processors in specified assemblies.
/// </summary>
internal static class DataProcessorDiscovery
{
	/// <summary>
	/// Discovers all types implementing the <see cref="IDataProcessor" /> interface within the specified assemblies.
	/// </summary>
	/// <param name="assembliesToScan"> A collection of assemblies to scan for data processor types. </param>
	/// <returns>
	/// An enumerable of <see cref="Type" /> objects representing the discovered data processor types that implement <see cref="IDataProcessor" />.
	/// </returns>
	/// <remarks>
	/// Uses Assembly.GetTypes() which requires reflection. In AOT scenarios, consider using source generators
	/// or explicit type registration instead of assembly scanning.
	/// </remarks>
	[RequiresUnreferencedCode("Assembly scanning uses reflection to discover types implementing IDataProcessor")]
	internal static IEnumerable<Type> DiscoverProcessors(IEnumerable<Assembly> assembliesToScan) => assembliesToScan.SelectMany(static
		assembly =>
		assembly
			.GetTypes()
			.Where(static t =>
				t is { IsClass: true, IsAbstract: false, IsInterface: false } && typeof(IDataProcessor).IsAssignableFrom(t)));

	/// <summary>
	/// Tries to retrieve the record type name associated with a data processor type, either from an attribute or a public property.
	/// </summary>
	/// <param name="processorType"> The <see cref="Type" /> of the processor to inspect. </param>
	/// <param name="recordType"> When this method returns, contains the record type name if found, or <c> null </c> if not found. </param>
	/// <returns> <c> true </c> if a record type name is found; otherwise, <c> false </c>. </returns>
	/// <exception cref="InvalidOperationException"> Thrown if the <paramref name="processorType" /> cannot be instantiated. </exception>
	/// <remarks>
	/// Uses reflection-based construction which requires runtime type metadata for processor types.
	/// </remarks>
	[RequiresUnreferencedCode(
		"Uses reflection to instantiate processor types dynamically. The processor types should be preserved if using AOT.")]
	internal static bool TryGetRecordType(
		Type processorType,
		[NotNullWhen(true)] out string? recordType)
	{
		// Check for the presence of the DataTaskRecordTypeAttribute on the type
		if (processorType
				.GetCustomAttributes(typeof(DataTaskRecordTypeAttribute), inherit: false)
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

		// Attempt to create an instance of the processor type using a public parameterless constructor.
		if (processorType.GetConstructor(Type.EmptyTypes) is not { } constructor)
		{
			throw new InvalidOperationException($"Type {processorType.FullName} must expose a public parameterless constructor.");
		}

		if (constructor.Invoke(parameters: null) is not { } instance)
		{
			throw new InvalidOperationException($"Could not create an instance of {processorType.FullName}.");
		}

		// Retrieve the value of the "RecordType" property
		recordType = property.GetValue(instance) as string;
		return !string.IsNullOrEmpty(recordType);
	}
}
