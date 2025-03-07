namespace Excalibur.DataAccess.SqlServer.Cdc;

/// <summary>
///     Provides a registry for managing and retrieving data change handlers based on table names.
/// </summary>
public interface IDataChangeHandlerFactory
{
	/// <summary>
	///     Retrieves the registered <see cref="IDataChangeHandler" /> for the specified table name.
	/// </summary>
	/// <param name="tableName"> The name of the table whose data change handler is to be retrieved. </param>
	/// <returns> An instance of <see cref="IDataChangeHandler" /> capable of handling changes for the specified table. </returns>
	/// <exception cref="CdcMissingTableHandlerException"> Thrown if no handler is registered for the specified <paramref name="tableName" />. </exception>
	/// <remarks>
	///     This method ensures that each table has exactly one handler registered. If multiple handlers are mistakenly registered for the
	///     same table, an exception should be thrown during registration.
	/// </remarks>
	IDataChangeHandler GetHandler(string tableName);
}
