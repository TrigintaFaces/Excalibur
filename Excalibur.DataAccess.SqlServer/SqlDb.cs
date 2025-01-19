using System.Data;

namespace Excalibur.DataAccess.SqlServer;

/// <summary>
///     Represents a specific implementation of <see cref="Db" /> for SQL databases.
/// </summary>
public class SqlDb : Db
{
	/// <summary>
	///     Initializes a new instance of the <see cref="SqlDb" /> class.
	/// </summary>
	/// <param name="connection"> The SQL database connection to manage. </param>
	public SqlDb(IDbConnection connection) : base(connection)
	{
	}
}
