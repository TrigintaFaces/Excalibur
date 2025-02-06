using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.DataAccess.ElasticSearch;

public static class ApplicationBuilderExtensions
{
	/// <summary>
	///     Initializes Elasticsearch indexes at application startup.
	/// </summary>
	/// <param name="applicationBuilder"> The application builder. </param>
	public static void UseElasticsearchIndexInitialization(this IApplicationBuilder applicationBuilder)
	{
		ArgumentNullException.ThrowIfNull(applicationBuilder, nameof(applicationBuilder));

		var host = applicationBuilder.ApplicationServices.GetRequiredService<IHost>();
		host.InitializeElasticsearchIndexesAsync().GetAwaiter().GetResult();
	}
}
