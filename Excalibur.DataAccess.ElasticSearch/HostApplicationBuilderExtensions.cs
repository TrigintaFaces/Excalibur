// Copyright (c) 2025 The Excalibur Project Authors
//
// Licensed under multiple licenses:
// - Excalibur License 1.0 (see LICENSE-EXCALIBUR.txt)
// - GNU Affero General Public License v3.0 or later (AGPL-3.0) (see LICENSE-AGPL-3.0.txt)
// - Server Side Public License v1.0 (SSPL-1.0) (see LICENSE-SSPL-1.0.txt)
// - Apache License 2.0 (see LICENSE-APACHE-2.0.txt)
//
// You may not use this file except in compliance with the License terms above. You may obtain copies of the licenses in
// the project root or online.
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on
// an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.DataAccess.ElasticSearch;

/// <summary>
///   Provides extension methods for the <see cref="IHostApplicationBuilder" /> interface to perform Elasticsearch index initialization.
/// </summary>
public static class HostApplicationBuilderExtensions
{
	/// <summary>
	///   Initializes Elasticsearch indexes during application startup.
	/// </summary>
	/// <param name="builder"> The <see cref="IHostApplicationBuilder" /> instance used to configure the host. </param>
	/// <returns> A <see cref="Task" /> representing the asynchronous initialization operation. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="builder" /> is <c> null </c>. </exception>
	/// <remarks>
	///   This method retrieves the <see cref="IndexInitializer" /> service from the DI container and invokes its
	///   <see cref="IndexInitializer.InitializeIndexesAsync" /> method to ensure Elasticsearch indexes are properly initialized.
	/// </remarks>
	public static async Task InitializeElasticsearchIndexesAsync(this IHostApplicationBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		var serviceProvider = builder.Services.BuildServiceProvider();

		try
		{
			var indexInitializer = serviceProvider.GetRequiredService<IndexInitializer>();
			await indexInitializer.InitializeIndexesAsync().ConfigureAwait(false);
		}
		finally
		{
			await serviceProvider.DisposeAsync().ConfigureAwait(false);
		}
	}
}
