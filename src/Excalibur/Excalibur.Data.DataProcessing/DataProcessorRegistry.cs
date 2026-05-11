// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Data.DataProcessing.Exceptions;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Data.DataProcessing;

/// <summary>
/// A registry for managing and retrieving <see cref="IDataProcessor" /> implementations based on their associated record types.
/// </summary>
internal sealed class DataProcessorRegistry : IDataProcessorRegistry
{
	private readonly Dictionary<string, Func<IServiceProvider, IDataProcessor>> _factories;

	/// <summary>
	/// Initializes a new instance of the <see cref="DataProcessorRegistry" /> class with a collection of data processors.
	/// </summary>
	/// <param name="processors"> The collection of data processors to register. </param>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="processors" /> is <c> null </c>. </exception>
	/// <exception cref="InvalidDataProcessorException">
	/// Thrown if a processor does not have a valid record type or its configuration is invalid.
	/// </exception>
	/// <exception cref="MultipleDataProcessorException"> Thrown if multiple processors are registered for the same record type. </exception>
	/// <remarks>
	/// <para>
	/// This constructor uses the AOT-safe attribute-based record type discovery path.
	/// Processors MUST be decorated with <see cref="DataTaskRecordTypeAttribute"/> for this
	/// constructor to discover their record types.
	/// </para>
	/// <para>
	/// For the assembly-scanning path that also supports runtime property inspection,
	/// use the <see cref="DataProcessorRegistry(IEnumerable{IDataProcessor}, bool)"/> overload
	/// with <c>useReflectionFallback: true</c>.
	/// </para>
	/// </remarks>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "The parameterless constructor passes useReflectionFallback: false, so no reflection occurs.")]
	public DataProcessorRegistry(IEnumerable<IDataProcessor> processors)
		: this(processors, useReflectionFallback: false)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="DataProcessorRegistry" /> class with a collection of data processors,
	/// optionally using reflection-based record type discovery.
	/// </summary>
	/// <param name="processors"> The collection of data processors to register. </param>
	/// <param name="useReflectionFallback">
	/// When <c>true</c>, falls back to reflection-based property inspection and instantiation
	/// if <see cref="DataTaskRecordTypeAttribute"/> is not present. This path is NOT AOT-compatible.
	/// </param>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="processors" /> is <c> null </c>. </exception>
	/// <exception cref="InvalidDataProcessorException">
	/// Thrown if a processor does not have a valid record type or its configuration is invalid.
	/// </exception>
	/// <exception cref="MultipleDataProcessorException"> Thrown if multiple processors are registered for the same record type. </exception>
	[RequiresUnreferencedCode("When useReflectionFallback is true, uses reflection to instantiate processor types. " +
		"Decorate processors with [DataTaskRecordType] for AOT compatibility.")]
	public DataProcessorRegistry(IEnumerable<IDataProcessor> processors, bool useReflectionFallback)
	{
		ArgumentNullException.ThrowIfNull(processors);

		_factories = new Dictionary<string, Func<IServiceProvider, IDataProcessor>>(StringComparer.OrdinalIgnoreCase);

		foreach (var processor in processors)
		{
			var processorType = processor.GetType();

			var found = useReflectionFallback
				? DataProcessorDiscovery.TryGetRecordTypeWithReflection(processorType, out var recordType)
				: DataProcessorDiscovery.TryGetRecordType(processorType, out recordType);

			if (!found)
			{
				throw new InvalidDataProcessorException(processorType);
			}

			if (!_factories.TryAdd(recordType!, sp => (IDataProcessor)sp.GetRequiredService(processorType)))
			{
				throw new MultipleDataProcessorException(recordType!, innerException: null);
			}
		}
	}

	/// <inheritdoc />
	public bool TryGetFactory(string recordType, out Func<IServiceProvider, IDataProcessor> processor)
	{
		ArgumentException.ThrowIfNullOrEmpty(recordType);
		return _factories.TryGetValue(recordType, out processor!);
	}

	/// <inheritdoc />
	public Func<IServiceProvider, IDataProcessor> GetFactory(string recordType)
	{
		if (TryGetFactory(recordType, out var factory))
		{
			return factory;
		}

		throw new MissingDataProcessorException(recordType, innerException: null);
	}
}
