namespace Excalibur.DataAccess.ElasticSearch;

public interface IIndexInitializer
{
	/// <summary>
	///     Initializes all Elasticsearch indexes by invoking the <see cref="IInitializeElasticIndex.InitializeIndexAsync" /> method on each
	///     registered initializer.
	/// </summary>
	/// <returns> A <see cref="Task" /> representing the asynchronous initialization operation. </returns>
	/// <remarks>
	///     This method iterates through all registered <see cref="IInitializeElasticIndex" /> instances and ensures that each one is
	///     executed sequentially.
	/// </remarks>
	public Task InitializeIndexesAsync();
}

/// <summary>
///     Provides functionality to initialize Elasticsearch indexes by delegating the operation to registered initializers.
/// </summary>
public class IndexInitializer : IIndexInitializer
{
	private readonly IEnumerable<IInitializeElasticIndex> _initializers;

	/// <summary>
	///     Initializes a new instance of the <see cref="IndexInitializer" /> class.
	/// </summary>
	/// <param name="initializers">
	///     A collection of objects that implement the <see cref="IInitializeElasticIndex" /> interface, each responsible for initializing
	///     specific Elasticsearch indexes.
	/// </param>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="initializers" /> is <c> null </c>. </exception>
	public IndexInitializer(IEnumerable<IInitializeElasticIndex> initializers)
	{
		ArgumentNullException.ThrowIfNull(initializers);

		_initializers = initializers;
	}

	/// <summary>
	///     Initializes all Elasticsearch indexes by invoking the <see cref="IInitializeElasticIndex.InitializeIndexAsync" /> method on each
	///     registered initializer.
	/// </summary>
	/// <returns> A <see cref="Task" /> representing the asynchronous initialization operation. </returns>
	/// <remarks>
	///     This method iterates through all registered <see cref="IInitializeElasticIndex" /> instances and ensures that each one is
	///     executed sequentially.
	/// </remarks>
	public async Task InitializeIndexesAsync()
	{
		foreach (var initializer in _initializers)
		{
			await initializer.InitializeIndexAsync().ConfigureAwait(false);
		}
	}
}
