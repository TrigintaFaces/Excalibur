namespace Excalibur.DataAccess.ElasticSearch;

/// <summary>
///     Provides functionality to initialize an Elasticsearch index.
/// </summary>
public interface IInitializeElasticIndex
{
	/// <summary>
	///     Initializes the Elasticsearch index asynchronously.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token to cancel the operation if required. </param>
	/// <returns> A <see cref="Task" /> representing the asynchronous operation. </returns>
	public Task InitializeIndexAsync(CancellationToken cancellationToken = default);
}
