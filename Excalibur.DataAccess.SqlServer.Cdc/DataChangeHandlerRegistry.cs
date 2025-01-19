using Excalibur.DataAccess.SqlServer.Cdc.Exceptions;

namespace Excalibur.DataAccess.SqlServer.Cdc;

/// <summary>
///     Registry for managing and retrieving <see cref="IDataChangeHandler" /> instances based on table names.
/// </summary>
public class DataChangeHandlerRegistry : IDataChangeHandlerRegistry
{
	private readonly Dictionary<string, IDataChangeHandler> _handlers;

	/// <summary>
	///     Initializes a new instance of the <see cref="DataChangeHandlerRegistry" /> class with the provided handlers.
	/// </summary>
	/// <param name="handlers"> A collection of <see cref="IDataChangeHandler" /> implementations to register. </param>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="handlers" /> is <c> null </c>. </exception>
	/// <exception cref="CdcMultipleTableHandlerException"> Thrown if multiple handlers are registered for the same table name. </exception>
	public DataChangeHandlerRegistry(IEnumerable<IDataChangeHandler> handlers)
	{
		ArgumentNullException.ThrowIfNull(handlers);

		_handlers = new Dictionary<string, IDataChangeHandler>(StringComparer.OrdinalIgnoreCase);

		foreach (var handler in handlers)
		{
			foreach (var tableName in handler.TableNames)
			{
				if (!_handlers.TryAdd(tableName, handler))
				{
					throw new CdcMultipleTableHandlerException(tableName);
				}
			}
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
		if (_handlers.TryGetValue(tableName, out var handler))
		{
			return handler;
		}

		throw new CdcMissingTableHandlerException(tableName);
	}
}
