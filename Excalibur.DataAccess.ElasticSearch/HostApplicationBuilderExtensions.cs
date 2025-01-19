using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.DataAccess.ElasticSearch;

/// <summary>
///     Provides extension methods for the <see cref="IHostApplicationBuilder" /> interface to perform Elasticsearch index initialization.
/// </summary>
public static class HostApplicationBuilderExtensions
{
	/// <summary>
	///     Initializes Elasticsearch indexes during application startup.
	/// </summary>
	/// <param name="builder"> The <see cref="IHostApplicationBuilder" /> instance used to configure the host. </param>
	/// <returns> A <see cref="Task" /> representing the asynchronous initialization operation. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="builder" /> is <c> null </c>. </exception>
	/// <remarks>
	///     This method retrieves the <see cref="IndexInitializer" /> service from the DI container and invokes its
	///     <see cref="IndexInitializer.InitializeIndexesAsync" /> method to ensure Elasticsearch indexes are properly initialized.
	/// </remarks>
	public static async Task InitializeElasticsearchIndexesAsync(this IHostApplicationBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		var indexInitializer = builder.Services.BuildServiceProvider().GetRequiredService<IndexInitializer>();
		await indexInitializer.InitializeIndexesAsync().ConfigureAwait(false);
	}
}
