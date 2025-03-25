using System.Data;

using Excalibur.DataAccess;

namespace Excalibur.Domain;

/// <summary>
///     Represents a specialized database context for domain-related operations.
/// </summary>
/// <remarks> This class extends the <see cref="Db" /> class to provide additional abstractions for domain-specific database functionality. </remarks>
public class DomainDb : Db, IDomainDb
{
	/// <summary>
	///     Initializes a new instance of the <see cref="DomainDb" /> class.
	/// </summary>
	/// <param name="connection"> The database connection to be used for operations. </param>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="connection" /> is <c> null </c>. </exception>
	public DomainDb(IDbConnection connection) : base(connection)
	{
		ArgumentNullException.ThrowIfNull(connection);
	}
}
