using System.Data;

namespace Excalibur.DataAccess;

/// <summary>
///     Abstract base class that implements the <see cref="IDb" /> interface for managing a database connection.
/// </summary>
/// <remarks> Ensures the database connection is in a ready state before providing access to it. </remarks>
public abstract class Db : IDb, IDisposable
{
	private readonly IDbConnection _connection;
	private bool _disposed;

	/// <summary>
	///     Initializes a new instance of the <see cref="Db" /> class with a provided <see cref="IDbConnection" />.
	/// </summary>
	/// <param name="connection"> The database connection to manage. </param>
	protected Db(IDbConnection connection)
	{
		ArgumentNullException.ThrowIfNull(connection);
		_connection = connection;
	}

	/// <inheritdoc />
	public IDbConnection Connection => _connection.Ready();

	/// <inheritdoc />
	public void Open() => _connection.Ready();

	/// <inheritdoc />
	public void Close() => _connection.Close();

	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (_disposed)
		{
			return;
		}

		if (disposing)
		{
			_connection.Dispose();
		}

		_disposed = true;
	}
}
