using System.Data;

namespace Excalibur.DataAccess;

/// <summary>
///     A specialized base class for database requests using an <see cref="IDbConnection" /> and a specific return model.
/// </summary>
/// <typeparam name="TModel"> The type of the model to be returned by the request. </typeparam>
public abstract class DataRequest<TModel> : DataRequestBase<IDbConnection, TModel>
{
}
