namespace Excalibur.DataAccess;

/// <summary>
///     Represents a database connection string with a name and a value.
/// </summary>
/// <remarks>
///     This interface provides a structure for managing database connection strings in a way that supports multiple named connections. It
///     can be implemented to fetch connection details from configuration files, environment variables, or other sources.
/// </remarks>
public interface IConnectionString
{
	/// <summary>
	///     Gets the name of the connection string.
	/// </summary>
	/// <value> A string representing the name or key of the connection string (e.g., "DefaultConnection"). </value>
	string Name { get; }

	/// <summary>
	///     Gets the actual value of the connection string.
	/// </summary>
	/// <value> A string representing the connection string (e.g., "Server=myServer;Database=myDB;User Id=myUsername;Password=myPassword;"). </value>
	string Value { get; }
}
