using Microsoft.Data.SqlClient;

namespace Excalibur.DataAccess.SqlServer.Cdc;

/// <summary>
///     Factory interface for creating instances of <see cref="IDataChangeEventProcessor" /> with specified configuration and database connections.
/// </summary>
public interface IDataChangeEventProcessorFactory
{
	/// <summary>
	///     Creates a new instance of <see cref="IDataChangeEventProcessor" /> using the specified database configuration and database abstractions.
	/// </summary>
	/// <param name="dbConfig"> The database configuration containing details for the CDC process. </param>
	/// <param name="cdcDb"> The database connection abstraction for reading CDC changes. </param>
	/// <param name="stateStoreDb"> The database connection abstraction for storing CDC state information. </param>
	/// <returns> An instance of <see cref="IDataChangeEventProcessor" /> configured with the provided inputs. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if any of the parameters are <c> null </c>. </exception>
	IDataChangeEventProcessor Create(IDatabaseConfig dbConfig, IDb cdcDb, IDb stateStoreDb);

	/// <summary>
	///     Creates a new instance of <see cref="IDataChangeEventProcessor" /> using the specified database configuration and raw SQL connections.
	/// </summary>
	/// <param name="dbConfig"> The database configuration containing details for the CDC process. </param>
	/// <param name="cdcConnection"> The raw SQL connection for reading CDC changes. </param>
	/// <param name="stateStoreConnection"> The raw SQL connection for storing CDC state information. </param>
	/// <returns> An instance of <see cref="IDataChangeEventProcessor" /> configured with the provided inputs. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if any of the parameters are <c> null </c>. </exception>
	IDataChangeEventProcessor Create(IDatabaseConfig dbConfig, SqlConnection cdcConnection, SqlConnection stateStoreConnection);
}
