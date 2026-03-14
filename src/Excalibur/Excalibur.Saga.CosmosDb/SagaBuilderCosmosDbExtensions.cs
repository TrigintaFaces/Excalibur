// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.CosmosDb;
using Excalibur.Saga.DependencyInjection;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Cosmos DB saga stores on <see cref="ISagaBuilder"/>.
/// </summary>
public static class SagaBuilderCosmosDbExtensions
{
	/// <summary>
	/// Configures the saga builder to use Azure Cosmos DB for saga state storage.
	/// </summary>
	/// <param name="builder">The saga builder.</param>
	/// <param name="configure">Optional action to configure Cosmos DB saga options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddExcaliburSaga(saga =&gt;
	/// {
	///     saga.UseCosmosDb(options =&gt;
	///     {
	///         options.Client.ConnectionString = "AccountEndpoint=...;AccountKey=...";
	///         options.DatabaseName = "myapp";
	///         options.ContainerName = "sagas";
	///     });
	/// });
	/// </code>
	/// </example>
	public static ISagaBuilder UseCosmosDb(
		this ISagaBuilder builder,
		Action<CosmosDbSagaOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		if (configure is not null)
		{
			_ = builder.Services.AddCosmosDbSagaStore(configure);
		}
		else
		{
			_ = builder.Services.AddCosmosDbSagaStore(_ => { });
		}

		return builder;
	}
}
