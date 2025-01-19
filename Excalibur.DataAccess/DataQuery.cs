using System.Data;

namespace Excalibur.DataAccess;

/// <summary>
///     A specialized base class for database queries using an <see cref="IDbConnection" /> and a specific return model.
/// </summary>
/// <typeparam name="TModel"> The type of the model to be returned by the query. </typeparam>
public abstract class DataQuery<TModel> : DataQueryBase<IDbConnection, TModel>
{
}
