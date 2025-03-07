using System.Collections.Concurrent;

using Excalibur.DataAccess.SqlServer.Cdc.Exceptions;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.DataAccess.SqlServer.Cdc;

/// <summary>
///     Factory for managing and retrieving <see cref="IDataChangeHandler" /> instances based on table names.
/// </summary>
public class DataChangeHandlerFactory : IDataChangeHandlerFactory
{
	private readonly IServiceProvider _serviceProvider;

	private readonly ConcurrentDictionary<string, Type> _handlerMappings = new();

	/// <summary>
	///     Initializes a new instance of the <see cref="DataChangeHandlerFactory" /> class with the provided handlers.
	/// </summary>
	/// <param name="serviceProvider"> The service provider for dependency resolution. </param>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="serviceProvider" /> is <c> null </c>. </exception>
	/// <exception cref="CdcMultipleTableHandlerException"> Thrown if multiple handlers are registered for the same table name. </exception>
	public DataChangeHandlerFactory(IServiceProvider serviceProvider)
	{
		ArgumentNullException.ThrowIfNull(serviceProvider, nameof(serviceProvider));

		_serviceProvider = serviceProvider;

		using var scope = serviceProvider.CreateScope();
		var handlers = scope.ServiceProvider.GetServices<IDataChangeHandler>();

		var tempMappings = new Dictionary<string, Type>();

		foreach (var handler in handlers)
		{
			foreach (var tableName in handler.TableNames)
			{
				if (tempMappings.TryGetValue(tableName, out var existingHandler))
				{
					throw new CdcMultipleTableHandlerException(tableName, existingHandler.Name, handler.GetType().Name);
				}

				tempMappings[tableName] = handler.GetType();
			}
		}

		foreach (var kvp in tempMappings)
		{
			_ = _handlerMappings.TryAdd(kvp.Key, kvp.Value);
		}
	}

	/// <summary>
	///     Retrieves the <see cref="IDataChangeHandler" /> registered for the specified table name.
	/// </summary>
	/// <param name="tableName"> The name of the table for which the handler is requested. </param>
	/// <returns> The <see cref="IDataChangeHandler" /> associated with the specified table name. </returns>
	/// <exception cref="CdcMissingTableHandlerException"> Thrown if no handler is found for the specified table name. </exception>
	public IDataChangeHandler GetHandler(string tableName)
	{
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw new ArgumentNullException(nameof(tableName));
		}

		if (_handlerMappings.TryGetValue(tableName, out var handlerType))
		{
			using var scope = _serviceProvider.CreateScope();
			var handler = (IDataChangeHandler)scope.ServiceProvider.GetRequiredService(handlerType);
			return handler;
		}

		throw new CdcMissingTableHandlerException(tableName);
	}
}
