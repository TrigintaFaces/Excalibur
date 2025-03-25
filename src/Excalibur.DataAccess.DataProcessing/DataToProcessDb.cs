using System.Data;

namespace Excalibur.DataAccess.DataProcessing;

/// <summary>
///     Implements <see cref="IDataToProcessDb" /> by delegating functionality to an underlying <see cref="IDb" /> instance.
/// </summary>
/// <remarks>
///     This class acts as an adapter, ensuring that the functionality of the provided <see cref="IDb" /> implementation is exposed through
///     the <see cref="IDataToProcessDb" /> interface for use in data processing operations.
/// </remarks>
public class DataToProcessDb : IDataToProcessDb
{
	private readonly IDb _db;

	/// <summary>
	///     Initializes a new instance of the <see cref="DataToProcessDb" /> class with the specified database instance.
	/// </summary>
	/// <param name="db"> The underlying database instance to use for operations. </param>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="db" /> is <c> null </c>. </exception>
	public DataToProcessDb(IDb db)
	{
		ArgumentNullException.ThrowIfNull(db);
		_db = db;
	}

	/// <inheritdoc />
	public IDbConnection Connection => _db.Connection;

	/// <inheritdoc />
	public void Close() => _db.Close();

	/// <inheritdoc />
	public void Open() => _db.Open();
}
