using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.DataAccess.ElasticSearch;

/// <summary>
///     Provides an extension method for initializing Elasticsearch indexes in IHost.
/// </summary>
public static class HostExtensions
{
	/// <summary>
	///     Initializes all registered Elasticsearch indexes during application startup.
	/// </summary>
	/// <param name="host"> The application host. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="host" /> is null. </exception>
	public static async Task InitializeElasticsearchIndexesAsync(this IHost host)
	{
		ArgumentNullException.ThrowIfNull(host, nameof(host));

		using var scope = host.Services.CreateScope();
		var indexInitializer = scope.ServiceProvider.GetRequiredService<IIndexInitializer>();
		await indexInitializer.InitializeIndexesAsync().ConfigureAwait(false);
	}
}
