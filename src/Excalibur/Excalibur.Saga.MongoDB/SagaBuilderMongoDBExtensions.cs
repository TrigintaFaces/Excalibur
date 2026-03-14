// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.MongoDB;
using Excalibur.Saga.DependencyInjection;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring MongoDB saga stores on <see cref="ISagaBuilder"/>.
/// </summary>
public static class SagaBuilderMongoDbExtensions
{
	/// <summary>
	/// Configures the saga builder to use MongoDB for saga state storage.
	/// </summary>
	/// <param name="builder">The saga builder.</param>
	/// <param name="configure">Optional action to configure MongoDB saga options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddExcaliburSaga(saga =&gt;
	/// {
	///     saga.UseMongoDB(options =&gt;
	///     {
	///         options.ConnectionString = "mongodb://localhost:27017";
	///         options.DatabaseName = "myapp";
	///         options.CollectionName = "sagas";
	///     });
	/// });
	/// </code>
	/// </example>
	public static ISagaBuilder UseMongoDB(
		this ISagaBuilder builder,
		Action<MongoDbSagaOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		if (configure is not null)
		{
			_ = builder.Services.AddMongoDbSagaStore(configure);
		}
		else
		{
			_ = builder.Services.AddMongoDbSagaStore(_ => { });
		}

		return builder;
	}
}
