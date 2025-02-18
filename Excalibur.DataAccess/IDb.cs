using System.Data;

namespace Excalibur.DataAccess;

/// <summary>
///     Defines a contract for database access, providing basic operations to manage a database connection.
/// </summary>
public interface IDb
{
	/// <summary>
	///     Gets the underlying database connection.
	/// </summary>
	IDbConnection Connection { get; }

	/// <summary>
	///     Opens the database connection.
	/// </summary>
	void Open();

	/// <summary>
	///     Closes the database connection.
	/// </summary>
	void Close();
}
