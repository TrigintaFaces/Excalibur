// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Data.DataProcessing.Exceptions;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Data.DataProcessing;

/// <summary>
/// A registry for managing and retrieving <see cref="IDataProcessor" /> implementations based on their associated record types.
/// </summary>
public sealed class DataProcessorRegistry : IDataProcessorRegistry
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
	[RequiresUnreferencedCode("Type discovery may require unreferenced types for reflection-based processor registration")]
	public DataProcessorRegistry(IEnumerable<IDataProcessor> processors)
	{
		ArgumentNullException.ThrowIfNull(processors);

		_factories = new Dictionary<string, Func<IServiceProvider, IDataProcessor>>(StringComparer.OrdinalIgnoreCase);

		foreach (var processor in processors)
		{
			var processorType = processor.GetType();

			if (!DataProcessorDiscovery.TryGetRecordType(processorType, out var recordType))
			{
				throw new InvalidDataProcessorException(processorType);
			}

			if (!_factories.TryAdd(recordType, sp => (IDataProcessor)sp.GetRequiredService(processorType)))
			{
				throw new MultipleDataProcessorException(recordType);
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

		throw new MissingDataProcessorException(recordType);
	}
}
